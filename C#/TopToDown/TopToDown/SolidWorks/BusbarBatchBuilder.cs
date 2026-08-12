using SolidWorks.Interop.sldworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using BusbarAutomation.Core.Domain;

using BusbarAutomation.Core.Planning;

namespace BusbarAutomation.Cad.SolidWorks
{
    internal sealed partial class SolidWorksBusbarPartBuilder
    {
        private readonly bool _replaceExistingBusbar;
        private readonly string[] _onlyBusbarNames;
        private readonly string[] _phaseNames;
        private readonly string _neutralConductorName;
        private readonly Action<BusbarPreflightReport> _reportSink;

        public SolidWorksBusbarPartBuilder(
            bool replaceExistingBusbar,
            string[] onlyBusbarNames,
            string[] phaseNames,
            string neutralConductorName,
            Action<BusbarPreflightReport> reportSink)
        {
            _replaceExistingBusbar = replaceExistingBusbar;
            _onlyBusbarNames = onlyBusbarNames == null ? null : (string[])onlyBusbarNames.Clone();
            _phaseNames = phaseNames == null
                ? throw new ArgumentNullException("phaseNames")
                : (string[])phaseNames.Clone();
            _neutralConductorName = neutralConductorName ?? throw new ArgumentNullException("neutralConductorName");
            _reportSink = reportSink ?? throw new ArgumentNullException("reportSink");
        }

        private sealed class StagedBusbarPart
        {
            public Busbar Busbar;
            public string SavePath;
            public Component2 Component;
        }

        public List<Busbar> SelectBusbarsForSheetMetalBatch(BusbarManufacturingPlan plan)
        {
            if (plan == null)
                throw new Exception("Busbar batch plan is null.");

            List<Busbar> selected = new List<Busbar>();

            foreach (string phase in _phaseNames)
                selected.Add(FindRequiredBusbar(plan, phase, BusbarKind.MainFeed));

            foreach (string phase in _phaseNames)
                selected.Add(FindRequiredBusbar(plan, phase, BusbarKind.Collector));

            selected.AddRange(FindOptionalBusbars(plan, _neutralConductorName, BusbarKind.Collector));

            foreach (string phase in _phaseNames)
                selected.AddRange(FindBusbars(plan, phase, BusbarKind.Branch));

            selected.AddRange(FindOptionalBusbars(plan, _neutralConductorName, BusbarKind.Branch));

            if (_onlyBusbarNames != null && _onlyBusbarNames.Length > 0)
            {
                selected = selected
                    .Where(b => _onlyBusbarNames.Any(name => SameText(b.Name, name)))
                    .ToList();

                if (selected.Count == 0)
                    throw new Exception("No planned busbar matches --only=" + string.Join(",", _onlyBusbarNames));
            }

            Console.WriteLine();
            Console.WriteLine("===== sheet metal batch selection =====");
            Console.WriteLine("Target count: " + selected.Count);
            foreach (Busbar busbar in selected)
                Console.WriteLine("  " + busbar.Name + " [" + busbar.Kind + "] " + busbar.Profile.Label + "mm");

            return selected;
        }

        private Busbar FindRequiredBusbar(BusbarManufacturingPlan plan, string phase, BusbarKind kind)
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

        private List<Busbar> FindBusbars(BusbarManufacturingPlan plan, string phase, BusbarKind kind)
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

        private List<Busbar> FindOptionalBusbars(BusbarManufacturingPlan plan, string phase, BusbarKind kind)
        {
            string phasePrefix = "Busbar_" + phase + "_";

            return plan.Busbars
                .Where(b => b.Kind == kind && b.Name.StartsWith(phasePrefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public void CreateBusbarSheetMetalParts(SldWorks swApp, ModelDoc2 assemblyModel, AssemblyDoc assembly, List<Busbar> busbars)
        {
            if (busbars == null || busbars.Count == 0)
                throw new Exception("No sheet metal busbars were selected for generation.");

            List<StagedBusbarPart> staged = new List<StagedBusbarPart>();
            try
            {
                try
                {
                    for (int i = 0; i < busbars.Count; i++)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Batch item " + (i + 1) + " / " + busbars.Count);
                        StagedBusbarPart item = new StagedBusbarPart
                        {
                            Busbar = busbars[i]
                        };
                        staged.Add(item);

                        // AddComponent5 is most reliable while the just-saved part
                        // document is still open. The part is closed in the method's
                        // finally block after insertion completes.
                        CreateBusbarSheetMetalPart(swApp, assemblyModel, assembly, item, i + 1);
                    }

                    assemblyModel.EditRebuild3();

                    BusbarPreflightReport stagedReport = BusbarGeometryVerifier.VerifyStaged(
                        assemblyModel,
                        assembly,
                        staged.Select(item => new KeyValuePair<Busbar, Component2>(item.Busbar, item.Component)));
                    _reportSink(stagedReport);
                    if (stagedReport.HasErrors)
                        throw new InvalidOperationException("Staged busbar geometry verification failed. Existing busbars were not changed.");

                    foreach (StagedBusbarPart item in staged)
                    {
                        string componentName = Path.GetFileNameWithoutExtension(item.SavePath);
                        item.Component.Name2 = componentName;
                        if (string.IsNullOrWhiteSpace(item.Component.Name2) ||
                            !item.Component.Name2.StartsWith(componentName, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException("Failed to assign generated component name: " + componentName);
                        }
                    }
                }
                catch
                {
                    GeneratedComponentManager.DeleteSelected(
                        assemblyModel,
                        assembly,
                        staged.Where(item => item.Component != null).Select(item => item.Component),
                        "Rollback staged busbar components");
                    foreach (StagedBusbarPart item in staged)
                    {
                        SolidWorksCom.Release(item.Component);
                        item.Component = null;
                    }
                    DeleteStagedPartFiles(staged);
                    throw;
                }

                if (_replaceExistingBusbar)
                {
                    HashSet<string> stagedNames = new HashSet<string>(
                        staged.Select(item => item.Component.Name2),
                        StringComparer.OrdinalIgnoreCase);
                    HashSet<string> replacementNames = _onlyBusbarNames == null
                        ? null
                        : new HashSet<string>(staged.Select(item => item.Busbar.Name), StringComparer.OrdinalIgnoreCase);
                    GeneratedComponentManager.DeleteExistingBusbars(
                        assemblyModel,
                        assembly,
                        stagedNames,
                        replacementNames);
                }

                assemblyModel.EditRebuild3();
                foreach (StagedBusbarPart item in staged)
                    Console.WriteLine("Busbar sheet metal part inserted: " + item.SavePath);
            }
            finally
            {
                foreach (StagedBusbarPart item in staged)
                    SolidWorksCom.Release(item.Component);
            }
        }

        private void CreateBusbarSheetMetalPart(
            SldWorks swApp,
            ModelDoc2 assemblyModel,
            AssemblyDoc assembly,
            StagedBusbarPart stagedPart,
            int batchIndex)
        {
            Busbar busbar = stagedPart == null ? null : stagedPart.Busbar;
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
            Console.WriteLine("Sheet-metal path: " + string.Join(" -> ", busbar.SheetMetalSketchLine.Select(p => p.ToMillimeterText()).ToArray()));

            ModelDoc2 partModel = null;
            try
            {
                partModel = NewPartDocument(swApp);
                SolidWorksSession.ActivateDocument(swApp, partModel);

                Feature feature = null;
                try
                {
                    feature = CreateBusbarSheetMetalFeature(swApp, partModel, busbar);
                    feature.Name = busbar.Name + "_SheetMetal";
                }
                finally
                {
                    SolidWorksCom.Release(feature);
                }

                CreateBusbarMountingHoles(swApp, partModel, busbar);
                partModel.EditRebuild3();
                LogPartBoundingBox(partModel, busbar);

                stagedPart.SavePath = SaveBusbarSheetMetalPart(partModel, assemblyModel, busbar);
                stagedPart.Component = InsertPartIntoAssembly(
                    swApp,
                    assemblyModel,
                    assembly,
                    stagedPart.SavePath,
                    "StagedBusbar_" + batchIndex + "_" + Guid.NewGuid().ToString("N"));
            }
            finally
            {
                if (partModel != null)
                    CloseBusbarPartDocument(swApp, assemblyModel, partModel);
            }
        }

        private void DeleteStagedPartFiles(IEnumerable<StagedBusbarPart> staged)
        {
            foreach (string path in staged
                .Where(item => !string.IsNullOrWhiteSpace(item.SavePath))
                .Select(item => item.SavePath))
            {
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch (Exception exception)
                {
                    Console.WriteLine("[Warning] Failed to delete staged part file '" + path + "': " + exception.Message);
                }
            }
        }

        private Feature CreateBusbarSheetMetalFeature(SldWorks swApp, ModelDoc2 partModel, Busbar busbar)
        {
            SheetMetalOpenProfilePlane profilePlane = GetOpenProfilePlane(busbar.Name, busbar.Kind, busbar.SheetMetalSketchLine);
            Feature sketch = null;
            Feature feature;
            try
            {
                sketch = CreateSheetMetalOpenProfileSketch(swApp, partModel, busbar, profilePlane);
                feature = CreateSheetMetalBaseFlangeFromSelectedSketch(partModel, sketch, busbar);
            }
            finally
            {
                SolidWorksCom.Release(sketch);
            }

            if (feature == null)
                throw new Exception("open-profile base flange creation failed: " + busbar.Name);

            ApplySheetMetalParametersToCreatedFeature(partModel, busbar);
            return feature;
        }
    }
}
