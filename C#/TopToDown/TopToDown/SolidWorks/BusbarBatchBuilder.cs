using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SwFeatureDebug
{
    internal partial class Program
    {
        private static List<Busbar> SelectBusbarsForSheetMetalBatch(BusbarPlan plan)
        {
            if (plan == null)
                throw new Exception("Busbar batch plan is null.");

            List<Busbar> selected = new List<Busbar>();

            foreach (string phase in PhaseNames)
                selected.Add(FindRequiredBusbar(plan, phase, BusbarKind.MainFeed));

            foreach (string phase in PhaseNames)
                selected.Add(FindRequiredBusbar(plan, phase, BusbarKind.Collector));

            selected.AddRange(FindOptionalBusbars(plan, NeutralConductorName, BusbarKind.Collector));

            foreach (string phase in PhaseNames)
                selected.AddRange(FindBusbars(plan, phase, BusbarKind.Branch));

            selected.AddRange(FindOptionalBusbars(plan, NeutralConductorName, BusbarKind.Branch));

            Console.WriteLine();
            Console.WriteLine("===== sheet metal batch selection =====");
            Console.WriteLine("Target count: " + selected.Count);
            foreach (Busbar busbar in selected)
                Console.WriteLine("  " + busbar.Name + " [" + busbar.Kind + "] " + busbar.Profile.Label + "mm");

            return selected;
        }

        private static Busbar FindRequiredBusbar(BusbarPlan plan, string phase, BusbarKind kind)
        {
            string phasePrefix = "Busbar_" + phase + "_";

            Busbar busbar = plan.Busbars
                .Where(b => b.Kind == kind && b.Name.StartsWith(phasePrefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (busbar == null)
                throw new Exception("No " + kind + " busbar was planned for phase " + phase + ".");

            return busbar;
        }

        private static List<Busbar> FindBusbars(BusbarPlan plan, string phase, BusbarKind kind)
        {
            string phasePrefix = "Busbar_" + phase + "_";

            List<Busbar> busbars = plan.Busbars
                .Where(b => b.Kind == kind && b.Name.StartsWith(phasePrefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (busbars.Count == 0)
                throw new Exception("No " + kind + " busbars were planned for phase " + phase + ".");

            return busbars;
        }

        private static List<Busbar> FindOptionalBusbars(BusbarPlan plan, string phase, BusbarKind kind)
        {
            string phasePrefix = "Busbar_" + phase + "_";

            return plan.Busbars
                .Where(b => b.Kind == kind && b.Name.StartsWith(phasePrefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void CreateBusbarSheetMetalParts(SldWorks swApp, ModelDoc2 assemblyModel, AssemblyDoc assembly, List<Busbar> busbars)
        {
            if (busbars == null || busbars.Count == 0)
                throw new Exception("No sheet metal busbars were selected for generation.");

            for (int i = 0; i < busbars.Count; i++)
            {
                Console.WriteLine();
                Console.WriteLine("Batch item " + (i + 1) + " / " + busbars.Count);
                CreateBusbarSheetMetalPart(swApp, assemblyModel, assembly, busbars[i]);
            }
        }

        private static void CreateBusbarSheetMetalPart(SldWorks swApp, ModelDoc2 assemblyModel, AssemblyDoc assembly, Busbar busbar)
        {
            if (busbar == null)
                throw new Exception("Busbar sheet metal target is null.");

            if (busbar.SheetMetalSketchLine == null || busbar.SheetMetalSketchLine.Count < 2)
                throw new Exception("Busbar sheet metal sketch line is invalid: " + busbar.Name);

            Console.WriteLine();
            Console.WriteLine("===== Create Busbar sheet metal part =====");
            Console.WriteLine(busbar.Name + " / " + busbar.Profile.Label + "mm");
            Console.WriteLine(
                "Sheet metal: MidPlane, R=" + busbar.SheetMetal.BendRadiusMm.ToString("0.###") +
                "mm, K=" + busbar.SheetMetal.KFactor.ToString("0.###"));

            ModelDoc2 partModel = NewPartDocument(swApp);
            ActivateDocument(swApp, partModel);

            Feature feature = CreateBusbarSheetMetalFeature(swApp, partModel, busbar);
            if (feature != null)
                feature.Name = busbar.Name + "_SheetMetal";

            CreateBusbarMountingHoles(swApp, partModel, busbar);
            partModel.EditRebuild3();

            string savePath = SaveBusbarSheetMetalPart(partModel, assemblyModel, busbar);
            InsertPartIntoAssembly(swApp, assemblyModel, assembly, savePath, Path.GetFileNameWithoutExtension(savePath));
            CloseBusbarPartDocument(swApp, assemblyModel, partModel);

            Console.WriteLine("Busbar sheet metal part inserted: " + savePath);
        }

        private static Feature CreateBusbarSheetMetalFeature(SldWorks swApp, ModelDoc2 partModel, Busbar busbar)
        {
            SheetMetalOpenProfilePlane profilePlane = GetOpenProfilePlane(busbar.Name, busbar.Kind, busbar.SheetMetalSketchLine);
            Feature sketch = CreateSheetMetalOpenProfileSketch(swApp, partModel, busbar, profilePlane);
            Feature feature = CreateSheetMetalBaseFlangeFromSelectedSketch(partModel, sketch, busbar.Profile, busbar.Kind);

            if (feature == null)
                throw new Exception("open-profile base flange creation failed: " + busbar.Name);

            ApplySheetMetalParametersToCreatedFeature(feature, partModel, busbar.Profile);
            return feature;
        }
    }
}