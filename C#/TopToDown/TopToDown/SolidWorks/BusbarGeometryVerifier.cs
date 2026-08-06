using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SwFeatureDebug
{
    internal class GeometryBounds
    {
        public double MinX;
        public double MinY;
        public double MinZ;
        public double MaxX;
        public double MaxY;
        public double MaxZ;

        public double SpanX { get { return MaxX - MinX; } }
        public double SpanY { get { return MaxY - MinY; } }
        public double SpanZ { get { return MaxZ - MinZ; } }
    }

    internal class GeneratedBusbarComponent
    {
        public Busbar PlannedBusbar;
        public ModelDoc2 PartModel;
        public GeometryBounds Bounds;
    }

    // Reads only existing assembly components and their generated part features.
    // It deliberately never creates, deletes, rebuilds, saves, or moves model geometry.
    internal static class BusbarGeometryVerifier
    {
        private const double GeometryToleranceMm = 0.25;

        private sealed class CutFeatureSnapshot
        {
            public string TypeName;
            public bool HasDepth;
            public double Depth;
        }

        private sealed class CylinderFaceSnapshot
        {
            public double[] Parameters;
            public double[] Bounds;
        }

        public static BusbarPreflightReport Verify(
            ModelDoc2 assemblyModel,
            AssemblyDoc assembly,
            IEnumerable<Busbar> expectedBusbars,
            bool reportUnexpectedComponents = true)
        {
            List<Busbar> expected = (expectedBusbars ?? Enumerable.Empty<Busbar>())
                .Where(busbar => busbar != null && !string.IsNullOrWhiteSpace(busbar.Name))
                .ToList();

            Dictionary<string, List<Component2>> generatedComponents = assembly == null
                ? new Dictionary<string, List<Component2>>(StringComparer.OrdinalIgnoreCase)
                : FindGeneratedComponents(assembly);

            try
            {
                return VerifyResolvedComponents(
                    assemblyModel,
                    assembly,
                    expected,
                    generatedComponents,
                    reportUnexpectedComponents,
                    "Busbar geometry verification report");
            }
            finally
            {
                ReleaseComponents(generatedComponents);
            }
        }

        public static BusbarPreflightReport VerifyStaged(
            ModelDoc2 assemblyModel,
            AssemblyDoc assembly,
            IEnumerable<KeyValuePair<Busbar, Component2>> stagedComponents)
        {
            List<KeyValuePair<Busbar, Component2>> staged = (stagedComponents ??
                Enumerable.Empty<KeyValuePair<Busbar, Component2>>())
                .Where(item => item.Key != null && item.Value != null)
                .ToList();
            List<Busbar> expected = staged.Select(item => item.Key).ToList();
            Dictionary<string, List<Component2>> components = new Dictionary<string, List<Component2>>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<Busbar, Component2> item in staged)
            {
                List<Component2> matches;
                if (!components.TryGetValue(item.Key.Name, out matches))
                {
                    matches = new List<Component2>();
                    components.Add(item.Key.Name, matches);
                }

                matches.Add(item.Value);
            }

            return VerifyResolvedComponents(
                assemblyModel,
                assembly,
                expected,
                components,
                false,
                "Staged busbar geometry verification report");
        }

        private static BusbarPreflightReport VerifyResolvedComponents(
            ModelDoc2 assemblyModel,
            AssemblyDoc assembly,
            List<Busbar> expected,
            Dictionary<string, List<Component2>> generatedComponents,
            bool reportUnexpectedComponents,
            string title)
        {
            BusbarPreflightReport report = new BusbarPreflightReport(
                title,
                "SolidWorks entities failed verification.");

            if (assemblyModel == null || assembly == null)
            {
                report.AddError("Geometry", "An active assembly is required for geometry verification.");
                return report;
            }

            if (expected.Count == 0)
            {
                report.AddError("Geometry", "No planned busbars were selected for geometry verification.");
                return report;
            }

            Dictionary<string, GeneratedBusbarComponent> verified = new Dictionary<string, GeneratedBusbarComponent>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (Busbar expectedBusbar in expected)
                {
                    List<Component2> matches;
                    if (!generatedComponents.TryGetValue(expectedBusbar.Name, out matches) || matches.Count == 0)
                    {
                        report.AddError("Component/" + expectedBusbar.Name, "Generated busbar component is missing from the assembly.");
                        continue;
                    }

                    if (matches.Count > 1)
                    {
                        report.AddError(
                            "Component/" + expectedBusbar.Name,
                            "Expected exactly one generated component, found " + matches.Count + ".");
                        continue;
                    }

                    GeneratedBusbarComponent component = ReadComponent(expectedBusbar, matches[0], report);
                    if (component != null)
                        verified[expectedBusbar.Name] = component;
                }

                if (reportUnexpectedComponents)
                {
                    foreach (KeyValuePair<string, List<Component2>> item in generatedComponents)
                    {
                        bool expectedName = expected.Any(busbar => SameText(busbar.Name, item.Key));
                        if (!expectedName)
                        {
                            report.AddWarning(
                                "Component/" + item.Key,
                                "Generated busbar component is not part of the current selected plan.");
                        }
                    }
                }

                foreach (GeneratedBusbarComponent component in verified.Values)
                {
                    ValidateProfileEnvelope(report, component);
                    ValidateHoleFeatures(report, component);
                }

                ValidateDoubleClampSurfaceContacts(report, expected, verified);

                if (!report.HasErrors)
                {
                    report.AddInfo(
                        "Geometry",
                        "SolidWorks components satisfy the checked physical envelopes, through-holes, and double-clamp surface contacts.");
                }

                return report;
            }
            finally
            {
                foreach (GeneratedBusbarComponent component in verified.Values)
                    SolidWorksCom.Release(component.PartModel);
            }
        }

        private static Dictionary<string, List<Component2>> FindGeneratedComponents(AssemblyDoc assembly)
        {
            Dictionary<string, List<Component2>> components = new Dictionary<string, List<Component2>>(StringComparer.OrdinalIgnoreCase);
            object[] items = assembly.GetComponents(false) as object[];
            if (items == null)
                return components;

            try
            {
                foreach (object item in items)
                {
                    Component2 component = item as Component2;
                    if (component == null)
                        continue;

                    bool retained = false;
                    try
                    {
                        string baseName = GeneratedComponentManager.GetGeneratedBusbarBaseName(component.Name2);
                        if (baseName == null)
                            continue;

                        List<Component2> matches;
                        if (!components.TryGetValue(baseName, out matches))
                        {
                            matches = new List<Component2>();
                            components.Add(baseName, matches);
                        }

                        matches.Add(component);
                        retained = true;
                    }
                    finally
                    {
                        if (!retained)
                            SolidWorksCom.Release(component);
                    }
                }
            }
            catch
            {
                ReleaseComponents(components);
                throw;
            }

            return components;
        }

        private static void ReleaseComponents(Dictionary<string, List<Component2>> components)
        {
            if (components == null)
                return;

            foreach (Component2 component in components.Values.SelectMany(items => items))
                SolidWorksCom.Release(component);
        }

        private static GeneratedBusbarComponent ReadComponent(
            Busbar expected,
            Component2 component,
            BusbarPreflightReport report)
        {
            string scope = "Component/" + expected.Name;
            ModelDoc2 partModel = component.GetModelDoc2() as ModelDoc2;
            if (partModel == null)
            {
                report.AddError(scope, "Component part document is unavailable or lightweight.");
                return null;
            }

            GeometryBounds bounds;
            try
            {
                bounds = GetAssemblyBounds(component);
            }
            catch
            {
                SolidWorksCom.Release(partModel);
                throw;
            }

            if (bounds == null)
            {
                report.AddError(scope, "Could not read the component bounding box in assembly coordinates.");
                SolidWorksCom.Release(partModel);
                return null;
            }

            report.AddInfo(
                scope,
                "Bounds: X=" + FormatRange(bounds.MinX, bounds.MaxX) +
                ", Y=" + FormatRange(bounds.MinY, bounds.MaxY) +
                ", Z=" + FormatRange(bounds.MinZ, bounds.MaxZ) + ".");

            return new GeneratedBusbarComponent
            {
                PlannedBusbar = expected,
                PartModel = partModel,
                Bounds = bounds
            };
        }

        private static GeometryBounds GetAssemblyBounds(Component2 component)
        {
            object rawBox = component.GetBox(false, false);
            double[] box = rawBox as double[];
            if (box == null || box.Length < 6)
                return null;

            return new GeometryBounds
            {
                MinX = box[0],
                MinY = box[1],
                MinZ = box[2],
                MaxX = box[3],
                MaxY = box[4],
                MaxZ = box[5]
            };
        }

        private static void ValidateProfileEnvelope(BusbarPreflightReport report, GeneratedBusbarComponent component)
        {
            Busbar busbar = component.PlannedBusbar;
            if (busbar.Profile == null)
            {
                report.AddError("Geometry/" + busbar.Name, "Planned busbar profile is missing.");
                return;
            }

            string scope = "Geometry/" + busbar.Name;
            if (busbar.Kind == BusbarKind.Collector)
            {
                ValidateDimension(report, scope, "collector thickness Y", component.Bounds.SpanY, busbar.Profile.ThicknessMeters);
                ValidateDimension(report, scope, "collector width Z", component.Bounds.SpanZ, busbar.Profile.WidthMeters);
                return;
            }

            ValidateDimension(report, scope, "busbar width X", component.Bounds.SpanX, busbar.Profile.WidthMeters);
        }

        private static void ValidateHoleFeatures(BusbarPreflightReport report, GeneratedBusbarComponent component)
        {
            Busbar busbar = component.PlannedBusbar;
            int expectedCount = busbar.MountingPorts == null ? 0 : busbar.MountingPorts.Count;
            if (expectedCount == 0)
                return;

            string[] featureNames = Enumerable.Range(1, expectedCount)
                .Select(index => busbar.Name + "_HoleCut_P" + index)
                .ToArray();
            Dictionary<string, CutFeatureSnapshot> cutFeatures = ReadCutFeatures(component.PartModel, featureNames);
            PartDoc part = component.PartModel as PartDoc;
            List<CylinderFaceSnapshot> cylinders = part == null
                ? null
                : ReadCylinderFaces(part);

            for (int index = 0; index < expectedCount; index++)
            {
                string featureName = featureNames[index];
                string scope = "Hole/" + busbar.Name + "/P" + (index + 1);
                CutFeatureSnapshot cutFeature;
                if (!cutFeatures.TryGetValue(featureName, out cutFeature))
                {
                    report.AddError(scope, "Expected cut feature is missing: " + featureName + ".");
                    continue;
                }

                if (!IsCutFeatureType(cutFeature.TypeName))
                {
                    report.AddError(scope, "Expected a cut feature but found type " + cutFeature.TypeName + ".");
                    continue;
                }

                if (cutFeature.HasDepth)
                {
                    if (cutFeature.Depth + Mm(GeometryToleranceMm) < busbar.Profile.ThicknessMeters)
                    {
                        report.AddError(
                            scope,
                            "Cut depth=" + FormatMm(cutFeature.Depth) +
                            ", less than planned material thickness " + FormatMm(busbar.Profile.ThicknessMeters) + ".");
                    }
                    else
                    {
                        report.AddInfo(scope, "Cut feature exists; depth=" + FormatMm(cutFeature.Depth) + ".");
                    }
                }
                else
                {
                    report.AddWarning(scope, "Cut feature exists, but its depth could not be read through the current SolidWorks API.");
                }

                ValidatePhysicalThroughHole(report, component, busbar.MountingPorts[index], cylinders, scope);
            }
        }

        private static Dictionary<string, CutFeatureSnapshot> ReadCutFeatures(
            ModelDoc2 model,
            IEnumerable<string> expectedFeatureNames)
        {
            HashSet<string> expected = new HashSet<string>(
                expectedFeatureNames ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            Dictionary<string, CutFeatureSnapshot> found =
                new Dictionary<string, CutFeatureSnapshot>(StringComparer.OrdinalIgnoreCase);
            if (model == null || expected.Count == 0)
                return found;

            Feature feature = model.FirstFeature() as Feature;
            while (feature != null)
            {
                Feature nextFeature = null;
                try
                {
                    string name = feature.Name;
                    if (expected.Contains(name))
                    {
                        double depth;
                        found[name] = new CutFeatureSnapshot
                        {
                            TypeName = feature.GetTypeName2(),
                            HasDepth = TryReadCutDepth(feature, out depth),
                            Depth = depth
                        };
                    }

                    nextFeature = feature.GetNextFeature() as Feature;
                }
                finally
                {
                    SolidWorksCom.Release(feature);
                }

                feature = nextFeature;
            }

            return found;
        }

        private static void ValidatePhysicalThroughHole(
            BusbarPreflightReport report,
            GeneratedBusbarComponent component,
            ConnectionPort port,
            List<CylinderFaceSnapshot> cylinders,
            string scope)
        {
            if (cylinders == null)
            {
                report.AddError(scope, "The generated component is not a readable part document.");
                return;
            }

            double physicalSpan;
            if (!TryFindHoleCylinderSpan(cylinders, port, out physicalSpan))
            {
                report.AddError(
                    scope,
                    "No cylindrical body face matches the planned hole center and diameter.");
                return;
            }

            double requiredSpan = component.PlannedBusbar.Profile.ThicknessMeters;
            if (physicalSpan + Mm(GeometryToleranceMm) < requiredSpan)
            {
                report.AddError(
                    scope,
                    "Physical hole span=" + FormatMm(physicalSpan) +
                    ", less than material thickness " + FormatMm(requiredSpan) + ".");
                return;
            }

            report.AddInfo(scope, "Physical through-hole span=" + FormatMm(physicalSpan) + ".");
        }

        private static List<CylinderFaceSnapshot> ReadCylinderFaces(PartDoc part)
        {
            List<CylinderFaceSnapshot> cylinders = new List<CylinderFaceSnapshot>();
            object[] bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
            if (bodies == null)
                return cylinders;

            foreach (object bodyObject in bodies)
            {
                Body2 body = bodyObject as Body2;
                if (body == null)
                {
                    SolidWorksCom.Release(bodyObject);
                    continue;
                }

                try
                {
                    object[] faces = body.GetFaces() as object[];
                    if (faces == null)
                        continue;

                    foreach (object faceObject in faces)
                    {
                        Face2 face = faceObject as Face2;
                        Surface surface = null;
                        try
                        {
                            if (face == null)
                                continue;

                            surface = face.GetSurface() as Surface;
                            if (surface == null || !surface.IsCylinder())
                                continue;

                            double[] cylinder = surface.CylinderParams as double[];
                            double[] box = face.GetBox() as double[];
                            if (cylinder == null || cylinder.Length < 7 || box == null || box.Length < 6)
                                continue;

                            cylinders.Add(new CylinderFaceSnapshot
                            {
                                Parameters = cylinder,
                                Bounds = box
                            });
                        }
                        finally
                        {
                            SolidWorksCom.Release(surface);
                            SolidWorksCom.Release(face ?? faceObject);
                        }
                    }
                }
                finally
                {
                    SolidWorksCom.Release(body);
                }
            }

            return cylinders;
        }

        private static bool TryFindHoleCylinderSpan(
            IEnumerable<CylinderFaceSnapshot> cylinders,
            ConnectionPort port,
            out double matchingSpan)
        {
            matchingSpan = 0.0;
            bool found = false;
            Point3 expectedAxis = GetHoleAxis(port.RequiredFace);
            double expectedRadius = Mm(port.HoleDiameterMm / 2.0);

            foreach (CylinderFaceSnapshot cylinder in cylinders)
            {
                if (!MatchesHoleCylinder(cylinder.Parameters, port.HoleCenter, expectedAxis, expectedRadius))
                    continue;

                matchingSpan = Math.Max(matchingSpan, GetBoxSpanAlongAxis(cylinder.Bounds, expectedAxis));
                found = true;
            }

            return found;
        }

        private static bool MatchesHoleCylinder(
            double[] cylinder,
            Point3 expectedCenter,
            Point3 expectedAxis,
            double expectedRadius)
        {
            if (cylinder == null || cylinder.Length < 7)
                return false;

            Point3 origin = new Point3(cylinder[0], cylinder[1], cylinder[2]);
            Point3 axis = Normalize(new Point3(cylinder[3], cylinder[4], cylinder[5]));
            if (Math.Abs(Dot(axis, expectedAxis)) < 0.999 ||
                Math.Abs(cylinder[6] - expectedRadius) > Mm(GeometryToleranceMm))
            {
                return false;
            }

            Point3 fromOrigin = Subtract(expectedCenter, origin);
            Point3 perpendicular = Subtract(fromOrigin, Scale(axis, Dot(fromOrigin, axis)));
            return Length(perpendicular) <= Mm(GeometryToleranceMm);
        }

        private static Point3 GetHoleAxis(ContactFace face)
        {
            if (face == ContactFace.Left || face == ContactFace.Right)
                return new Point3(1.0, 0.0, 0.0);
            if (face == ContactFace.Upper || face == ContactFace.Lower)
                return new Point3(0.0, 1.0, 0.0);
            return new Point3(0.0, 0.0, 1.0);
        }

        private static double GetBoxSpanAlongAxis(double[] box, Point3 axis)
        {
            if (Math.Abs(axis.X) > 0.5)
                return box[3] - box[0];
            if (Math.Abs(axis.Y) > 0.5)
                return box[4] - box[1];
            return box[5] - box[2];
        }

        private static Point3 Normalize(Point3 vector)
        {
            double length = Length(vector);
            return length <= 0.0
                ? new Point3(0.0, 0.0, 0.0)
                : Scale(vector, 1.0 / length);
        }

        private static Point3 Subtract(Point3 first, Point3 second)
        {
            return new Point3(first.X - second.X, first.Y - second.Y, first.Z - second.Z);
        }

        private static Point3 Scale(Point3 vector, double factor)
        {
            return new Point3(vector.X * factor, vector.Y * factor, vector.Z * factor);
        }

        private static double Dot(Point3 first, Point3 second)
        {
            return first.X * second.X + first.Y * second.Y + first.Z * second.Z;
        }

        private static double Length(Point3 vector)
        {
            return Math.Sqrt(Dot(vector, vector));
        }

        private static bool TryReadCutDepth(Feature cutFeature, out double depth)
        {
            depth = 0.0;
            ExtrudeFeatureData2 data = null;

            try
            {
                data = cutFeature.GetDefinition() as ExtrudeFeatureData2;
                if (data == null)
                    return false;

                depth = data.GetDepth(true);
                if (depth <= 0.0)
                    depth = data.GetDepth(false);

                return depth > 0.0;
            }
            catch
            {
                return false;
            }
            finally
            {
                SolidWorksCom.Release(data);
            }
        }

        private static bool IsCutFeatureType(string featureType)
        {
            if (string.IsNullOrWhiteSpace(featureType))
                return false;

            // SolidWorks reports FeatureCut4-created extruded cuts as the internal type code "ICE".
            return SameText(featureType, "ICE") ||
                featureType.IndexOf("Cut", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ValidateDoubleClampSurfaceContacts(
            BusbarPreflightReport report,
            List<Busbar> expected,
            Dictionary<string, GeneratedBusbarComponent> verified)
        {
            foreach (Busbar lower in expected.Where(busbar => busbar.BranchLegRole == BranchLegRole.Lower))
            {
                string upperName = lower.Name.Substring(0, lower.Name.Length - "_Lower".Length) + "_Upper";
                string collectorName = "Busbar_" + GetPhaseFromBusbarName(lower.Name) + "_Collector";

                GeneratedBusbarComponent lowerComponent;
                GeneratedBusbarComponent upperComponent;
                GeneratedBusbarComponent collectorComponent;
                if (!verified.TryGetValue(lower.Name, out lowerComponent) ||
                    !verified.TryGetValue(upperName, out upperComponent) ||
                    !verified.TryGetValue(collectorName, out collectorComponent))
                {
                    continue;
                }

                string scope = "DoubleClamp/" + lower.Name;
                ValidateContact(
                    report,
                    scope,
                    "lower branch top / collector bottom",
                    lowerComponent.Bounds.MaxY,
                    collectorComponent.Bounds.MinY);

                double upperBranchBottomAtCollector = upperComponent.Bounds.MaxY - upperComponent.PlannedBusbar.Profile.ThicknessMeters;
                ValidateContact(
                    report,
                    scope,
                    "upper branch bottom / collector top",
                    upperBranchBottomAtCollector,
                    collectorComponent.Bounds.MaxY);
            }
        }

        private static string GetPhaseFromBusbarName(string name)
        {
            const string prefix = "Busbar_";
            if (string.IsNullOrWhiteSpace(name) || !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            int separator = name.IndexOf('_', prefix.Length);
            return separator < 0 ? string.Empty : name.Substring(prefix.Length, separator - prefix.Length);
        }

        private static void ValidateDimension(
            BusbarPreflightReport report,
            string scope,
            string label,
            double actual,
            double expected)
        {
            if (Math.Abs(actual - expected) > Mm(GeometryToleranceMm))
            {
                report.AddError(
                    scope,
                    label + "=" + FormatMm(actual) + ", expected " + FormatMm(expected) + ".");
            }
            else
            {
                report.AddInfo(scope, label + "=" + FormatMm(actual) + ".");
            }
        }

        private static void ValidateContact(
            BusbarPreflightReport report,
            string scope,
            string label,
            double firstSurfaceY,
            double secondSurfaceY)
        {
            double difference = firstSurfaceY - secondSurfaceY;
            if (Math.Abs(difference) > Mm(GeometryToleranceMm))
            {
                report.AddError(
                    scope,
                    label + " has Y difference " + FormatMm(difference) +
                    " (first=" + FormatMm(firstSurfaceY) +
                    ", second=" + FormatMm(secondSurfaceY) + ").");
            }
            else
            {
                report.AddInfo(scope, label + " is in contact at Y=" + FormatMm(secondSurfaceY) + ".");
            }
        }

        private static bool SameText(string first, string second)
        {
            return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatRange(double first, double second)
        {
            return FormatMm(first) + " to " + FormatMm(second);
        }

        private static string FormatMm(double value)
        {
            return (value * 1000.0).ToString("0.###") + "mm";
        }

        private static double Mm(double value)
        {
            return value / 1000.0;
        }
    }
}
