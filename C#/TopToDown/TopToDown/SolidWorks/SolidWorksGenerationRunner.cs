using SolidWorks.Interop.sldworks;
using System;
using System.Collections.Generic;
using System.IO;

namespace SwFeatureDebug
{
    internal partial class Program
    {
        private static void RunSolidWorksGeneration()
        {
            BusbarPreflightReport configurationReport = BusbarPreflightValidator.ValidateConfiguration(Settings);
            if (configurationReport.HasErrors)
            {
                configurationReport.PrintToConsole();
                StopAfterPreflightFailure();
                return;
            }

            SldWorks swApp;
            ModelDoc2 model;
            AssemblyDoc assembly;
            try
            {
                swApp = GetOrStartSolidWorks();
                model = GetActiveOrOpenAssembly(swApp);
                assembly = (AssemblyDoc)model;
            }
            catch (Exception exception)
            {
                configurationReport.Messages.AddRange(BusbarPreflightValidator.CreatePlanningFailure(exception).Messages);
                configurationReport.PrintToConsole();
                StopAfterPreflightFailure();
                return;
            }

            List<FoundPoint> scannedPoints = ScanReferencePoints(swApp, model, assembly);
            BusbarPlan plan;
            try
            {
                plan = BusbarPlanBuilder.BuildPlanFromScannedAssembly(scannedPoints, PhaseNames, Settings);
            }
            catch (Exception exception)
            {
                configurationReport.Messages.AddRange(BusbarPreflightValidator.CreatePlanningFailure(exception).Messages);
                configurationReport.PrintToConsole();
                StopAfterPreflightFailure();
                return;
            }

            BusbarPreflightReport planReport = BusbarPreflightValidator.ValidatePlan(plan, Settings, PhaseNames);
            planReport.Messages.InsertRange(0, configurationReport.Messages);
            planReport.PrintToConsole();

            if (_exportReportOnly)
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

            if (_validateOnly)
            {
                Console.WriteLine("Validation-only mode complete. The assembly was not modified.");
                return;
            }

            if (planReport.HasErrors)
            {
                StopAfterPreflightFailure();
                return;
            }

            if (_verifyGeometryOnly)
            {
                VerifyExistingGeometry(model, assembly, plan);
                return;
            }

            if (!_previewOnly && _replaceExistingBusbar)
                DeleteExistingBusbarComponents(model, assembly);

            if (_previewOnly)
            {
                CreateBusbarPreviewPart(swApp, model, assembly, plan);
                Console.WriteLine();
                Console.WriteLine("Busbar preview complete. Press any key to exit.");
                if (!Console.IsInputRedirected)
                    Console.ReadKey();
                return;
            }

            List<Busbar> busbars = SelectBusbarsForSheetMetalBatch(plan);
            CreateBusbarSheetMetalParts(swApp, model, assembly, busbars);

            bool geometryPassed = VerifyExistingGeometry(model, assembly, plan, busbars);

            if (geometryPassed && (_onlyBusbarNames == null || _onlyBusbarNames.Length == 0))
                ExportProductionReport(model, plan);
            else if (geometryPassed)
                Console.WriteLine("Production report was skipped because --only generated only part of the complete plan.");

            Console.WriteLine();
            Console.WriteLine("Busbar sheet metal generation complete. Press any key to exit.");
            if (!Console.IsInputRedirected)
                Console.ReadKey();
        }

        private static void StopAfterPreflightFailure()
        {
            if (_validateOnly)
            {
                System.Environment.ExitCode = 1;
                return;
            }

            throw new InvalidOperationException("Busbar preflight failed. Existing generated busbars were not changed.");
        }

        private static bool VerifyExistingGeometry(
            ModelDoc2 model,
            AssemblyDoc assembly,
            BusbarPlan plan,
            List<Busbar> expectedBusbars = null)
        {
            List<Busbar> expected = expectedBusbars ?? SelectBusbarsForSheetMetalBatch(plan);
            BusbarPreflightReport geometryReport = BusbarGeometryVerifier.Verify(model, assembly, expected);
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
