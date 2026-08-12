using SolidWorks.Interop.sldworks;
using System;
using System.Linq;

using BusbarAutomation.Core.Domain;

namespace BusbarAutomation.Cad.SolidWorks
{
    internal sealed partial class SolidWorksBusbarPartBuilder
    {
        internal void CreateStagedSheetMetalPart(
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
