using SolidWorks.Interop.sldworks;
using System;
using System.Collections.Generic;

namespace SwFeatureDebug
{
    internal partial class Program
    {
        private static void RunSolidWorksGeneration()
        {
            SldWorks swApp = GetOrStartSolidWorks();
            ModelDoc2 model = GetActiveOrOpenAssembly(swApp);
            AssemblyDoc assembly = (AssemblyDoc)model;

            if (!_previewOnly && _replaceExistingBusbar)
                DeleteExistingBusbarComponents(model, assembly);

            List<FoundPoint> scannedPoints = ScanReferencePoints(swApp, model, assembly);
            BusbarPlan plan = BusbarPlanBuilder.BuildPlanFromScannedAssembly(scannedPoints, PhaseNames, Settings);

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

            Console.WriteLine();
            Console.WriteLine("Busbar sheet metal generation complete. Press any key to exit.");
            if (!Console.IsInputRedirected)
                Console.ReadKey();
        }
    }
}
