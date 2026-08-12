using SolidWorks.Interop.sldworks;
using System;
using System.Collections.Generic;
using System.IO;

using BusbarAutomation.Core.Domain;

namespace BusbarAutomation.Cad.SolidWorks
{
    internal sealed partial class SolidWorksBusbarPartBuilder
    {
        public void CreateBusbarPreviewPart(SldWorks swApp, ModelDoc2 assemblyModel, AssemblyDoc assembly, BusbarPlan plan)
        {
            if (plan == null)
                throw new Exception("Busbar preview plan is null.");

            Console.WriteLine();
            Console.WriteLine("===== Create Busbar preview part =====");
            Console.WriteLine("Collectors: " + plan.Collectors.Count);
            Console.WriteLine("Busbars: " + plan.Busbars.Count);
            Console.WriteLine("This preview creates sketch lines only. It does not create sheet metal solids.");

            ModelDoc2 partModel = null;
            string componentName;
            try
            {
                partModel = NewPartDocument(swApp);
                SolidWorksSession.ActivateDocument(swApp, partModel);

                CreateBusbarPreviewSketches(partModel, plan);
                partModel.EditRebuild3();

                string savePath = SaveGeneratedPart(partModel, assemblyModel, "Busbar_Preview");
                componentName = Path.GetFileNameWithoutExtension(savePath);
                SolidWorksCom.Release(InsertPartIntoAssembly(swApp, assemblyModel, assembly, savePath, componentName));
                assemblyModel.EditRebuild3();
            }
            finally
            {
                if (partModel != null)
                    CloseBusbarPartDocument(swApp, assemblyModel, partModel);
            }

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

        private static void CreatePreview3DPolylineSketch(ModelDoc2 partModel, string sketchName, List<Point3> points, bool constructionGeometry)
        {
            if (points == null || points.Count < 2)
                return;

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

                    try
                    {
                        segment.ConstructionGeometry = constructionGeometry;
                        segmentCount++;
                    }
                    finally
                    {
                        SolidWorksCom.Release(segment);
                    }
                }
            }
            finally
            {
                if (sketchOpened)
                    sketchManager.Insert3DSketch(true);
            }

            if (segmentCount == 0)
                return;

            Feature sketch = partModel.FeatureByPositionReverse(0) as Feature;
            if (sketch == null)
                throw new Exception("Preview sketch was created but could not be located: " + sketchName);

            try
            {
                sketch.Name = sketchName;
            }
            finally
            {
                SolidWorksCom.Release(sketch);
            }
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
