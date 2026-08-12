using SolidWorks.Interop.sldworks;
using System;

using BusbarAutomation.Core.Domain;

namespace BusbarAutomation.Cad.SolidWorks
{
    internal sealed partial class SolidWorksBusbarPartBuilder
    {
        private static Feature CreateSheetMetalOpenProfileSketch(SldWorks swApp, ModelDoc2 partModel, Busbar busbar, SheetMetalOpenProfilePlane profilePlane)
        {
            Console.WriteLine(
                "sheet-metal sketch plane: " +
                profilePlane.BasePlaneRole +
                " offset=" + ToMm(profilePlane.Offset).ToString("F3") + " mm");

            Feature plane = CreateOffsetPlane(partModel, profilePlane.BasePlaneRole, profilePlane.Offset);
            SketchManager sketchManager = partModel.SketchManager;
            try
            {
                partModel.ClearSelection2(true);
                if (!plane.Select2(false, 0))
                    throw new Exception("Failed to select sheet-metal sketch plane: " + busbar.Name);

                sketchManager.InsertSketch(true);
            }
            finally
            {
                SolidWorksCom.Release(plane);
            }

            bool sketchStillOpen = true;
            MathUtility mathUtility = null;
            Sketch activeSketch = null;
            MathTransform modelToSketch = null;

            try
            {
                mathUtility = (MathUtility)swApp.GetMathUtility();
                if (mathUtility == null)
                    throw new Exception("Failed to access the SolidWorks math utility.");

                activeSketch = partModel.GetActiveSketch2() as Sketch;
                if (activeSketch == null)
                    throw new Exception("Failed to get active sheet-metal sketch: " + busbar.Name);

                modelToSketch = activeSketch.ModelToSketchTransform;
                if (modelToSketch == null)
                    throw new Exception("Failed to get sheet-metal ModelToSketchTransform: " + busbar.Name);

                for (int i = 0; i < busbar.SheetMetalSketchLine.Count - 1; i++)
                {
                    Point3 p1 = FlattenSketchPoint(ModelPointToSketchPoint(mathUtility, busbar.SheetMetalSketchLine[i], modelToSketch));
                    Point3 p2 = FlattenSketchPoint(ModelPointToSketchPoint(mathUtility, busbar.SheetMetalSketchLine[i + 1], modelToSketch));
                    SolidWorksCom.Release(CreateLineOrThrow(sketchManager, p1, p2, "sheet-metal segment " + i));
                }

                sketchManager.InsertSketch(true);
                sketchStillOpen = false;
            }
            finally
            {
                if (sketchStillOpen)
                    sketchManager.InsertSketch(true);

                SolidWorksCom.Release(modelToSketch);
                SolidWorksCom.Release(activeSketch);
                SolidWorksCom.Release(mathUtility);
            }

            Feature sketch = partModel.FeatureByPositionReverse(0) as Feature;
            if (sketch == null)
                throw new Exception("sheet-metal sketch was created but could not be located.");

            sketch.Name = busbar.Name + "_OpenProfile";
            return sketch;
        }
    }
}
