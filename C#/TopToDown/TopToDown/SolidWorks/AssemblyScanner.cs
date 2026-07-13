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
        private static List<FoundPoint> ScanReferencePoints(SldWorks swApp, ModelDoc2 model, AssemblyDoc assembly)
        {
            List<FoundPoint> foundPoints = new List<FoundPoint>();

            Console.WriteLine("Current assembly: " + model.GetTitle());
            Console.WriteLine();

            if (_verboseFeatureScan)
                Console.WriteLine("===== Assembly features =====");

            DumpModelFeatures(swApp, model, null, null, foundPoints);

            object[] components = assembly.GetComponents(false) as object[];
            if (components == null || components.Length == 0)
            {
                Console.WriteLine("No assembly components found.");
                return foundPoints;
            }

            if (_verboseFeatureScan)
            {
                Console.WriteLine();
                Console.WriteLine("===== Component features =====");
            }

            foreach (object item in components)
            {
                Component2 component = item as Component2;
                if (component == null)
                    continue;

                ModelDoc2 componentModel = component.GetModelDoc2() as ModelDoc2;
                if (componentModel == null)
                {
                    Console.WriteLine("[Skip] Component is unloaded or lightweight: " + component.Name2);
                    continue;
                }

                if (_verboseFeatureScan)
                {
                    Console.WriteLine();
                    Console.WriteLine("Component: " + component.Name2);
                }

                DumpModelFeatures(swApp, componentModel, component.Name2, component.Transform2, foundPoints);
            }

            Console.WriteLine();
            Console.WriteLine("Reference point count: " + foundPoints.Count);
            return foundPoints;
        }

        private static void DeleteExistingBusbarComponents(ModelDoc2 model, AssemblyDoc assembly)
        {
            object[] components = assembly.GetComponents(false) as object[];
            if (components == null || components.Length == 0)
                return;

            model.ClearSelection2(true);

            int selectedCount = 0;
            foreach (object item in components)
            {
                Component2 component = item as Component2;
                if (component == null || component.Name2 == null)
                    continue;

                if (component.Name2.StartsWith("Busbar_", StringComparison.OrdinalIgnoreCase) &&
                    component.Select4(true, null, false))
                {
                    selectedCount++;
                }
            }

            if (selectedCount > 0)
            {
                Console.WriteLine("Delete old generated busbar components: " + selectedCount);
                model.EditDelete();
                model.EditRebuild3();
            }

            model.ClearSelection2(true);
        }

        private static void DumpModelFeatures(SldWorks swApp, ModelDoc2 model, string componentName, MathTransform componentTransform, List<FoundPoint> foundPoints)
        {
            Feature feature = model.FirstFeature() as Feature;

            while (feature != null)
            {
                if (_verboseFeatureScan)
                    Console.WriteLine("Feature: " + feature.Name + "    Type: " + feature.GetTypeName2());

                TryReadReferencePoint(swApp, feature, componentName, componentTransform, foundPoints);
                feature = feature.GetNextFeature() as Feature;
            }
        }

        private static void TryReadReferencePoint(SldWorks swApp, Feature feature, string componentName, MathTransform componentTransform, List<FoundPoint> foundPoints)
        {
            object specific;
            try
            {
                specific = feature.GetSpecificFeature2();
            }
            catch
            {
                return;
            }

            RefPoint refPoint = specific as RefPoint;
            if (refPoint == null)
                return;

            MathPoint localMathPoint = refPoint.GetRefPoint();
            double[] local = localMathPoint.ArrayData as double[];
            if (local == null || local.Length < 3)
                return;

            Point3 point = new Point3(local[0], local[1], local[2]);

            if (componentTransform != null)
                point = TransformPoint(swApp, point, componentTransform);

            string owner = string.IsNullOrWhiteSpace(componentName) ? "Assembly" : componentName;

            Console.WriteLine("  >>> Reference point: " + feature.Name);
            Console.WriteLine("      Owner: " + owner);
            Console.WriteLine("      Assembly position: " + point.ToMillimeterText());

            foundPoints.Add(new FoundPoint
            {
                ComponentName = owner,
                PointName = feature.Name,
                Position = point
            });
        }

        private static Point3 TransformPoint(SldWorks swApp, Point3 point, MathTransform transform)
        {
            MathUtility utility = (MathUtility)swApp.GetMathUtility();
            MathPoint mathPoint = (MathPoint)utility.CreatePoint(new[] { point.X, point.Y, point.Z });
            MathPoint transformed = (MathPoint)mathPoint.MultiplyTransform(transform);
            double[] data = transformed.ArrayData as double[];

            if (data == null || data.Length < 3)
                throw new Exception("Failed to transform component point into assembly coordinates.");

            return new Point3(data[0], data[1], data[2]);
        }
    }
}
