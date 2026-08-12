using SolidWorks.Interop.sldworks;
using System;
using System.Collections.Generic;
using System.IO;

using BusbarAutomation.Application;

using BusbarAutomation.Core.Domain;

using BusbarAutomation.Core.Planning;

using BusbarAutomation.Reporting;

namespace BusbarAutomation.Cad.SolidWorks
{
    internal sealed class SolidWorksGenerationRunner
    {
        private static readonly string[] PhaseNames = { "A", "B", "C" };
        private const string NeutralConductorName = "N";

        private readonly BusbarSettings _settings;
        private readonly GenerationOptions _options;
        private readonly SolidWorksBusbarPartBuilder _partBuilder;

        public SolidWorksGenerationRunner(BusbarSettings settings, GenerationOptions options)
        {
            _settings = settings ?? throw new ArgumentNullException("settings");
            _options = options ?? throw new ArgumentNullException("options");
            _partBuilder = new SolidWorksBusbarPartBuilder(options, PhaseNames, NeutralConductorName);
        }

        public void Run()
        {
            BusbarPreflightReport configurationReport = BusbarPreflightValidator.ValidateConfiguration(_settings);
            if (configurationReport.HasErrors)
            {
                configurationReport.PrintToConsole();
                StopAfterPreflightFailure();
                return;
            }

            EngineeringConfigurationSnapshot configuration =
                EngineeringConfigurationSnapshot.FromSettings(_settings);
            BusbarSettings planningSettings = configuration.ToPlanningSettings();

            SldWorks swApp;
            ModelDoc2 model;
            AssemblyDoc assembly;
            try
            {
                swApp = SolidWorksSession.GetOrStartSolidWorks();
                model = SolidWorksSession.GetActiveOrOpenAssembly(swApp);
                assembly = (AssemblyDoc)model;
            }
            catch (Exception exception)
            {
                configurationReport.Messages.AddRange(BusbarPreflightValidator.CreatePlanningFailure(exception).Messages);
                configurationReport.PrintToConsole();
                StopAfterPreflightFailure();
                return;
            }

            List<FoundPoint> scannedPoints = new AssemblyReferencePointScanner(_options.VerboseFeatureScan)
                .Scan(swApp, model, assembly);
            BusbarPlan plan;
            try
            {
                AssemblySnapshot assemblySnapshot = AssemblySnapshotFactory.FromFoundPoints(
                    scannedPoints,
                    PhaseNames,
                    configuration.GetSupportedRatedCurrents(),
                    model.GetPathName());
                plan = BusbarPlanBuilder.BuildPlan(assemblySnapshot, PhaseNames, configuration);
            }
            catch (Exception exception)
            {
                configurationReport.Messages.AddRange(BusbarPreflightValidator.CreatePlanningFailure(exception).Messages);
                configurationReport.PrintToConsole();
                StopAfterPreflightFailure();
                return;
            }

            BusbarPreflightReport planReport = BusbarPreflightValidator.ValidatePlan(
                plan,
                planningSettings,
                PhaseNames,
                configuration.OverlapRules);
            planReport.Messages.InsertRange(0, configurationReport.Messages);
            planReport.PrintToConsole();

            if (_options.ExportReportOnly)
            {
                if (planReport.HasErrors)
                {
                    StopAfterPreflightFailure();
                    return;
                }

                ExportProductionReport(model, plan);
                Console.WriteLine("Production-report-only mode complete. The assembly was not modified.");
                return;
            }

            if (_options.ValidateOnly)
            {
                Console.WriteLine("Validation-only mode complete. The assembly was not modified.");
                return;
            }

            if (planReport.HasErrors)
            {
                StopAfterPreflightFailure();
                return;
            }

            if (_options.VerifyGeometryOnly)
            {
                VerifyExistingGeometry(model, assembly, plan);
                return;
            }

            if (_options.PreviewOnly)
            {
                _partBuilder.CreateBusbarPreviewPart(swApp, model, assembly, plan);
                Console.WriteLine();
                Console.WriteLine("Busbar preview complete. Press any key to exit.");
                if (!Console.IsInputRedirected)
                    Console.ReadKey();
                return;
            }

            List<Busbar> busbars = _partBuilder.SelectBusbarsForSheetMetalBatch(plan);
            _partBuilder.CreateBusbarSheetMetalParts(swApp, model, assembly, busbars);

            bool geometryPassed = true;
            if (_options.ReplaceExistingBusbar)
            {
                geometryPassed = VerifyExistingGeometry(model, assembly, plan, busbars);
            }
            else
            {
                Console.WriteLine(
                    "Final assembly-wide verification was skipped because --keep-existing intentionally leaves duplicate busbar sets. " +
                    "The newly staged components passed verification before insertion completed.");
            }

            if (geometryPassed && _options.ReplaceExistingBusbar &&
                (_options.OnlyBusbarNames == null || _options.OnlyBusbarNames.Length == 0))
                ExportProductionReport(model, plan);
            else if (geometryPassed && _options.OnlyBusbarNames != null)
                Console.WriteLine("Production report was skipped because --only generated only part of the complete plan.");
            else if (geometryPassed && !_options.ReplaceExistingBusbar)
                Console.WriteLine("Production report was skipped because --keep-existing does not leave a canonical assembly state.");

            if (!geometryPassed)
            {
                Console.WriteLine("Busbar generation finished with geometry verification errors.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Busbar sheet metal generation complete. Press any key to exit.");
            if (!Console.IsInputRedirected)
                Console.ReadKey();
        }

        private void StopAfterPreflightFailure()
        {
            if (_options.ValidateOnly)
            {
                System.Environment.ExitCode = 1;
                return;
            }

            throw new InvalidOperationException("Busbar preflight failed. Existing generated busbars were not changed.");
        }

        private bool VerifyExistingGeometry(
            ModelDoc2 model,
            AssemblyDoc assembly,
            BusbarPlan plan,
            List<Busbar> expectedBusbars = null)
        {
            List<Busbar> expected = expectedBusbars ?? _partBuilder.SelectBusbarsForSheetMetalBatch(plan);
            bool completePlan = _options.OnlyBusbarNames == null || _options.OnlyBusbarNames.Length == 0;
            BusbarPreflightReport geometryReport = BusbarGeometryVerifier.Verify(
                model,
                assembly,
                expected,
                completePlan);
            geometryReport.PrintToConsole();

            if (geometryReport.HasErrors)
            {
                System.Environment.ExitCode = 1;
                Console.WriteLine("Geometry verification found errors. Existing assembly components were not changed by verification.");
                return false;
            }

            Console.WriteLine("Geometry verification passed. The assembly was only read during verification.");
            return true;
        }

        private static string ExportProductionReport(ModelDoc2 assemblyModel, BusbarPlan plan)
        {
            string assemblyPath = assemblyModel == null ? null : assemblyModel.GetPathName();
            string rootFolder = string.IsNullOrWhiteSpace(assemblyPath)
                ? System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory)
                : Path.GetDirectoryName(assemblyPath);
            string outputFolder = Path.Combine(rootFolder, "Reports");
            string reportPath = ProductionReportExporter.Export(plan, outputFolder);

            Console.WriteLine("Production report exported: " + reportPath);
            return reportPath;
        }
    }
}
