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
        private static Feature CreateSheetMetalOpenProfileSketch(SldWorks swApp, ModelDoc2 partModel, Busbar busbar, SheetMetalOpenProfilePlane profilePlane)
        {
            Console.WriteLine(
                "sheet-metal sketch plane: " +
                profilePlane.BasePlaneRole +
                " offset=" + ToMm(profilePlane.Offset).ToString("F3") + " mm");

            Feature plane = CreateOffsetPlane(partModel, profilePlane.BasePlaneRole, profilePlane.Offset);

            partModel.ClearSelection2(true);
            if (!plane.Select2(false, 0))
                throw new Exception("Failed to select sheet-metal sketch plane: " + busbar.Name);

            SketchManager sketchManager = partModel.SketchManager;
            sketchManager.InsertSketch(true);

            bool sketchStillOpen = true;

            try
            {
                Sketch activeSketch = partModel.GetActiveSketch2() as Sketch;
                if (activeSketch == null)
                    throw new Exception("Failed to get active sheet-metal sketch: " + busbar.Name);

                MathTransform modelToSketch = activeSketch.ModelToSketchTransform;
                if (modelToSketch == null)
                    throw new Exception("Failed to get sheet-metal ModelToSketchTransform: " + busbar.Name);

                for (int i = 0; i < busbar.SheetMetalSketchLine.Count - 1; i++)
                {
                    Point3 p1 = FlattenSketchPoint(ModelPointToSketchPoint(swApp, busbar.SheetMetalSketchLine[i], modelToSketch));
                    Point3 p2 = FlattenSketchPoint(ModelPointToSketchPoint(swApp, busbar.SheetMetalSketchLine[i + 1], modelToSketch));
                    CreateLineOrThrow(sketchManager, p1, p2, "sheet-metal segment " + i);
                }

                sketchManager.InsertSketch(true);
                sketchStillOpen = false;
            }
            finally
            {
                if (sketchStillOpen)
                    sketchManager.InsertSketch(true);
            }

            Feature sketch = partModel.FeatureByPositionReverse(0) as Feature;
            if (sketch == null)
                throw new Exception("sheet-metal sketch was created but could not be located.");

            sketch.Name = busbar.Name + "_OpenProfile";
            return sketch;
        }
    }
}