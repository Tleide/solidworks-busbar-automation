using SolidWorks.Interop.sldworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;

using BusbarAutomation.Core.Domain;

namespace BusbarAutomation.Cad.SolidWorks
{
    internal sealed class AssemblyReferencePointScanner
    {
        private const string ReferencePointFeatureTypeName = "RefPoint";
        private readonly bool _verbose;

        public AssemblyReferencePointScanner(bool verbose)
        {
            _verbose = verbose;
        }

        public List<FoundPoint> Scan(SldWorks swApp, ModelDoc2 model, AssemblyDoc assembly)
        {
            List<FoundPoint> foundPoints = new List<FoundPoint>();
            Dictionary<string, List<ReferencePointTemplate>> localPointCache =
                new Dictionary<string, List<ReferencePointTemplate>>(StringComparer.OrdinalIgnoreCase);
            Stopwatch stopwatch = Stopwatch.StartNew();
            int scannedModelCount = 0;
            int cacheHitCount = 0;
            int unavailableComponentCount = 0;
            MathUtility mathUtility = null;

            Console.WriteLine("Current assembly: " + model.GetTitle());
            Console.WriteLine();

            try
            {
                mathUtility = (MathUtility)swApp.GetMathUtility();
                if (mathUtility == null)
                    throw new Exception("Failed to access the SolidWorks math utility.");

                if (_verbose)
                    Console.WriteLine("===== Assembly reference points =====");

                List<ReferencePointTemplate> assemblyPoints = ReadReferencePointTemplates(model, "Assembly");
                scannedModelCount++;
                AddFoundPoints(assemblyPoints, "Assembly", null, mathUtility, foundPoints);

                object[] components = assembly.GetComponents(false) as object[];
                if (components == null || components.Length == 0)
                {
                    Console.WriteLine("No assembly components found.");
                }
                else
                {
                    if (_verbose)
                    {
                        Console.WriteLine();
                        Console.WriteLine("===== Component reference points =====");
                    }

                    foreach (object item in components)
                        ScanComponentReferencePoints(
                            item as Component2,
                            mathUtility,
                            localPointCache,
                            foundPoints,
                            ref scannedModelCount,
                            ref cacheHitCount,
                            ref unavailableComponentCount);
                }
            }
            finally
            {
                SolidWorksCom.Release(mathUtility);
            }

            Console.WriteLine();
            Console.WriteLine(
                "Reference point count: " + foundPoints.Count +
                ", scanned models: " + scannedModelCount +
                ", cache hits: " + cacheHitCount +
                ", elapsed: " + stopwatch.ElapsedMilliseconds + " ms.");
            if (unavailableComponentCount > 0)
            {
                Console.WriteLine(
                    "[Warning] Skipped unavailable or lightweight components: " + unavailableComponentCount +
                    ". Resolve required components before generation if their reference points are needed.");
            }
            return foundPoints;
        }

        private void ScanComponentReferencePoints(
            Component2 component,
            MathUtility mathUtility,
            Dictionary<string, List<ReferencePointTemplate>> localPointCache,
            List<FoundPoint> foundPoints,
            ref int scannedModelCount,
            ref int cacheHitCount,
            ref int unavailableComponentCount)
        {
            if (component == null)
                return;

            ModelDoc2 componentModel = null;
            MathTransform componentTransform = null;
            try
            {
                string componentName = component.Name2;
                if (componentName != null &&
                    componentName.StartsWith("Busbar_", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                componentModel = component.GetModelDoc2() as ModelDoc2;
                if (componentModel == null)
                {
                    unavailableComponentCount++;
                    Console.WriteLine("[Skip] Component is unloaded or lightweight: " + componentName);
                    return;
                }

                if (_verbose)
                    Console.WriteLine("Component: " + componentName);

                componentTransform = component.Transform2;
                string cacheKey = GetComponentModelCacheKey(component, componentModel);
                List<ReferencePointTemplate> localPoints;
                if (localPointCache.TryGetValue(cacheKey, out localPoints))
                {
                    cacheHitCount++;
                }
                else
                {
                    localPoints = ReadReferencePointTemplates(componentModel, componentName);
                    localPointCache.Add(cacheKey, localPoints);
                    scannedModelCount++;
                }

                AddFoundPoints(localPoints, componentName, componentTransform, mathUtility, foundPoints);
            }
            finally
            {
                // These references are created for one component scan and are never retained by the plan.
                SolidWorksCom.Release(componentTransform);
                SolidWorksCom.Release(componentModel);
                SolidWorksCom.Release(component);
            }
        }

        private List<ReferencePointTemplate> ReadReferencePointTemplates(ModelDoc2 model, string ownerName)
        {
            List<ReferencePointTemplate> points = new List<ReferencePointTemplate>();
            Feature feature = model.FirstFeature() as Feature;

            while (feature != null)
            {
                Feature nextFeature = null;
                try
                {
                    string featureType = feature.GetTypeName2();
                    if (_verbose)
                        Console.WriteLine("Feature: " + feature.Name + "    Type: " + featureType);

                    if (string.Equals(featureType, ReferencePointFeatureTypeName, StringComparison.OrdinalIgnoreCase))
                    {
                        ReferencePointTemplate point;
                        string failureReason;
                        if (TryReadReferencePointTemplate(feature, out point, out failureReason))
                        {
                            points.Add(point);
                        }
                        else
                        {
                            Console.WriteLine(
                                "[Skip] Failed to read reference point '" + feature.Name +
                                "' in " + ownerName + ": " + failureReason);
                        }
                    }
                }
                catch (Exception exception)
                {
                    Console.WriteLine(
                        "[Skip] Failed to inspect a feature in " + ownerName + ": " + exception.Message);
                }
                finally
                {
                    try
                    {
                        nextFeature = feature.GetNextFeature() as Feature;
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(
                            "[Warning] Feature traversal stopped in " + ownerName + ": " + exception.Message);
                    }

                    SolidWorksCom.Release(feature);
                }

                feature = nextFeature;
            }

            return points;
        }

        private bool TryReadReferencePointTemplate(
            Feature feature,
            out ReferencePointTemplate point,
            out string failureReason)
        {
            point = null;
            failureReason = null;
            RefPoint refPoint = null;
            MathPoint localMathPoint = null;

            try
            {
                refPoint = feature.GetSpecificFeature2() as RefPoint;
                if (refPoint == null)
                {
                    failureReason = "The feature did not expose a RefPoint definition.";
                    return false;
                }

                localMathPoint = refPoint.GetRefPoint();
                double[] local = localMathPoint == null ? null : localMathPoint.ArrayData as double[];
                if (local == null || local.Length < 3)
                {
                    failureReason = "The reference-point coordinates are unavailable.";
                    return false;
                }

                point = new ReferencePointTemplate
                {
                    PointName = feature.Name,
                    LocalPosition = new Point3(local[0], local[1], local[2])
                };
                return true;
            }
            catch (Exception exception)
            {
                failureReason = exception.Message;
                return false;
            }
            finally
            {
                SolidWorksCom.Release(localMathPoint);
                SolidWorksCom.Release(refPoint);
            }
        }

        private void AddFoundPoints(
            List<ReferencePointTemplate> localPoints,
            string owner,
            MathTransform componentTransform,
            MathUtility mathUtility,
            List<FoundPoint> foundPoints)
        {
            foreach (ReferencePointTemplate localPoint in localPoints)
            {
                Point3 assemblyPoint = localPoint.LocalPosition;
                try
                {
                    if (componentTransform != null)
                        assemblyPoint = TransformPoint(mathUtility, localPoint.LocalPosition, componentTransform);
                }
                catch (Exception exception)
                {
                    Console.WriteLine(
                        "[Skip] Failed to transform reference point '" + localPoint.PointName +
                        "' in " + owner + ": " + exception.Message);
                    continue;
                }

                if (_verbose)
                {
                    Console.WriteLine("  >>> Reference point: " + localPoint.PointName);
                    Console.WriteLine("      Owner: " + owner);
                    Console.WriteLine("      Assembly position: " + assemblyPoint.ToMillimeterText());
                }

                foundPoints.Add(new FoundPoint
                {
                    ComponentName = owner,
                    PointName = localPoint.PointName,
                    Position = assemblyPoint
                });
            }
        }

        private Point3 TransformPoint(MathUtility mathUtility, Point3 point, MathTransform transform)
        {
            MathPoint mathPoint = null;
            MathPoint transformed = null;
            try
            {
                mathPoint = (MathPoint)mathUtility.CreatePoint(new[] { point.X, point.Y, point.Z });
                transformed = (MathPoint)mathPoint.MultiplyTransform(transform);
                double[] data = transformed == null ? null : transformed.ArrayData as double[];

                if (data == null || data.Length < 3)
                    throw new Exception("Failed to transform component point into assembly coordinates.");

                return new Point3(data[0], data[1], data[2]);
            }
            finally
            {
                if (!object.ReferenceEquals(transformed, mathPoint))
                    SolidWorksCom.Release(transformed);
                SolidWorksCom.Release(mathPoint);
            }
        }

        private string GetComponentModelCacheKey(Component2 component, ModelDoc2 componentModel)
        {
            string path = componentModel.GetPathName();
            if (string.IsNullOrWhiteSpace(path))
                path = componentModel.GetTitle();
            if (string.IsNullOrWhiteSpace(path))
                path = component.Name2;

            string configuration = component.ReferencedConfiguration;
            return path + "|" + (configuration ?? string.Empty);
        }

        private sealed class ReferencePointTemplate
        {
            public string PointName;
            public Point3 LocalPosition;
        }
    }
}
