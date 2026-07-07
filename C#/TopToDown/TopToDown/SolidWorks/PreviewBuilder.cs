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
        private static void CreateBusbarPreviewPart(SldWorks swApp, ModelDoc2 assemblyModel, AssemblyDoc assembly, BusbarPlan plan)
        {
            if (plan == null)
                throw new Exception("Busbar preview plan is null.");

            Console.WriteLine();
            Console.WriteLine("===== Create Busbar preview part =====");
            Console.WriteLine("Collectors: " + plan.Collectors.Count);
            Console.WriteLine("Busbars: " + plan.Busbars.Count);
            Console.WriteLine("This preview creates sketch lines only. It does not create sheet metal solids.");

            ModelDoc2 partModel = NewPartDocument(swApp);
            ActivateDocument(swApp, partModel);

            CreateBusbarPreviewSketches(partModel, plan);
            partModel.EditRebuild3();

            string savePath = SaveGeneratedPart(partModel, assemblyModel, "Busbar_Preview");
            string componentName = Path.GetFileNameWithoutExtension(savePath);
            InsertPartIntoAssembly(swApp, assemblyModel, assembly, savePath, componentName);
            CloseBusbarPartDocument(swApp, assemblyModel, partModel);

            Console.WriteLine("Busbar preview part inserted: " + componentName);
            Console.WriteLine("Preview convention:");
            Console.WriteLine("  Collector_*_Center sketches = planned collector centerlines.");
            Console.WriteLine("  *_Logical sketches = hole-center route references.");
            Console.WriteLine("  *_SheetMetal sketches = current sheet-metal sketch paths with end margins.");
        }

        private static void CreateBusbarPreviewSketches(ModelDoc2 partModel, BusbarPlan plan)
        {
            foreach (CollectorLayout collector in plan.Collectors)
            {
                List<Point3> points = new List<Point3>
                {
                    new Point3(collector.StartX, collector.Center.Y, collector.Center.Z),
                    new Point3(collector.EndX, collector.Center.Y, collector.Center.Z)
                };

                CreatePreview3DPolylineSketch(
                    partModel,
                    "Preview_Collector_" + collector.Phase + "_Center",
                    points,
                    false);
            }

            foreach (Busbar busbar in plan.Busbars)
            {
                string safeName = ToSafeFeatureName(busbar.Name);

                CreatePreview3DPolylineSketch(
                    partModel,
                    "Preview_" + safeName + "_Logical",
                    busbar.LogicalCenterline,
                    true);

                CreatePreview3DPolylineSketch(
                    partModel,
                    "Preview_" + safeName + "_SheetMetal",
                    busbar.SheetMetalSketchLine,
                    false);
            }
        }

        private static Feature CreatePreview3DPolylineSketch(ModelDoc2 partModel, string sketchName, List<Point3> points, bool constructionGeometry)
        {
            if (points == null || points.Count < 2)
                return null;

            partModel.ClearSelection2(true);
            SketchManager sketchManager = partModel.SketchManager;
            bool sketchOpened = false;
            int segmentCount = 0;

            try
            {
                sketchManager.Insert3DSketch(true);
                sketchOpened = true;

                for (int i = 0; i < points.Count - 1; i++)
                {
                    if (points[i].DistanceTo(points[i + 1]) <= Mm(0.01))
                        continue;

                    SketchSegment segment = CreateLineOrThrow(
                        sketchManager,
                        points[i],
                        points[i + 1],
                        sketchName + " segment " + i);

                    segment.ConstructionGeometry = constructionGeometry;
                    segmentCount++;
                }
            }
            finally
            {
                if (sketchOpened)
                    sketchManager.Insert3DSketch(true);
            }

            if (segmentCount == 0)
                return null;

            Feature sketch = partModel.FeatureByPositionReverse(0) as Feature;
            if (sketch == null)
                throw new Exception("Preview sketch was created but could not be located: " + sketchName);

            sketch.Name = sketchName;
            return sketch;
        }

        private static string ToSafeFeatureName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Unnamed";

            char[] chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '_')
                    chars[i] = '_';
            }

            return new string(chars);
        }
    }
}