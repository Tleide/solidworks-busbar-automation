using SolidWorks.Interop.sldworks;
using System;
using System.Collections.Generic;

using BusbarAutomation.Application;
using BusbarAutomation.Cad.SolidWorks;
using BusbarAutomation.Core.Domain;
using BusbarAutomation.Core.Planning;
using BusbarAutomation.Reporting;

namespace BusbarAutomation.Cli
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
            _partBuilder = new SolidWorksBusbarPartBuilder(
                options.ReplaceExistingBusbar,
                options.OnlyBusbarNames,
                PhaseNames,
                NeutralConductorName,
                PreflightConsolePresenter.Print);
        }

        public void Run()
        {
            BusbarPlanningWorkflow planningWorkflow = new BusbarPlanningWorkflow(_settings, PhaseNames);
            if (!planningWorkflow.CanBuild)
            {
                PreflightConsolePresenter.Print(planningWorkflow.ConfigurationReport);
                StopAfterPreflightFailure();
                return;
            }

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
                PreflightConsolePresenter.Print(planningWorkflow.CreateFailureReport(exception));
                StopAfterPreflightFailure();
                return;
            }

            List<FoundPoint> scannedPoints;
            try
            {
                scannedPoints = new AssemblyReferencePointScanner(_options.VerboseFeatureScan)
                    .Scan(swApp, model, assembly);
            }
            catch (Exception exception)
            {
                PreflightConsolePresenter.Print(planningWorkflow.CreateFailureReport(exception));
                StopAfterPreflightFailure();
                return;
            }

            BusbarPlanningResult planningResult = planningWorkflow.Build(scannedPoints, model.GetPathName());
            PreflightConsolePresenter.Print(planningResult.Report);
            BusbarManufacturingPlan plan = planningResult.Plan;

            if (_options.ExportReportOnly)
            {
                if (planningResult.Report.HasErrors || plan == null)
                {
                    StopAfterPreflightFailure();
                    return;
                }

                ExportProductionReport(plan, model.GetPathName());
                Console.WriteLine("Production-report-only mode complete. The assembly was not modified.");
                return;
            }

            if (_options.ValidateOnly)
            {
                Console.WriteLine("Validation-only mode complete. The assembly was not modified.");
                return;
            }

            if (planningResult.Report.HasErrors || plan == null)
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
                ExportProductionReport(plan, model.GetPathName());
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
            BusbarManufacturingPlan plan,
            List<Busbar> expectedBusbars = null)
        {
            List<Busbar> expected = expectedBusbars ?? _partBuilder.SelectBusbarsForSheetMetalBatch(plan);
            bool completePlan = _options.OnlyBusbarNames == null || _options.OnlyBusbarNames.Length == 0;
            BusbarPreflightReport geometryReport = BusbarGeometryVerifier.Verify(
                model,
                assembly,
                expected,
                completePlan);
            PreflightConsolePresenter.Print(geometryReport);

            if (geometryReport.HasErrors)
            {
                System.Environment.ExitCode = 1;
                Console.WriteLine("Geometry verification found errors. Existing assembly components were not changed by verification.");
                return false;
            }

            Console.WriteLine("Geometry verification passed. The assembly was only read during verification.");
            return true;
        }

        private static string ExportProductionReport(BusbarManufacturingPlan plan, string assemblyPath)
        {
            string reportPath = ProductionReportService.ExportForAssembly(plan, assemblyPath);

            Console.WriteLine("Production report exported: " + reportPath);
            return reportPath;
        }
    }
}
