using SolidWorks.Interop.sldworks;
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
        public Component2 Component;
        public ModelDoc2 PartModel;
        public GeometryBounds Bounds;
    }

    // Reads only existing assembly components and their generated part features.
    // It deliberately never creates, deletes, rebuilds, saves, or moves model geometry.
    internal static class BusbarGeometryVerifier
    {
        private const double GeometryToleranceMm = 0.25;

        public static BusbarPreflightReport Verify(
            ModelDoc2 assemblyModel,
            AssemblyDoc assembly,
            IEnumerable<Busbar> expectedBusbars)
        {
            BusbarPreflightReport report = new BusbarPreflightReport(
                "Busbar geometry verification report",
                "Existing SolidWorks entities failed verification.");
            List<Busbar> expected = (expectedBusbars ?? Enumerable.Empty<Busbar>())
                .Where(busbar => busbar != null && !string.IsNullOrWhiteSpace(busbar.Name))
                .ToList();

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

            Dictionary<string, List<Component2>> generatedComponents = FindGeneratedComponents(assembly);
            Dictionary<string, GeneratedBusbarComponent> verified = new Dictionary<string, GeneratedBusbarComponent>(StringComparer.OrdinalIgnoreCase);

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
                    "Existing SolidWorks components satisfy the checked physical envelopes, hole-cut features, and double-clamp surface contacts.");
            }

            return report;
        }

        private static Dictionary<string, List<Component2>> FindGeneratedComponents(AssemblyDoc assembly)
        {
            Dictionary<string, List<Component2>> components = new Dictionary<string, List<Component2>>(StringComparer.OrdinalIgnoreCase);
            object[] items = assembly.GetComponents(false) as object[];
            if (items == null)
                return components;

            foreach (object item in items)
            {
                Component2 component = item as Component2;
                if (component == null)
                    continue;

                string baseName = GetGeneratedBusbarBaseName(component.Name2);
                if (baseName == null)
                    continue;

                List<Component2> matches;
                if (!components.TryGetValue(baseName, out matches))
                {
                    matches = new List<Component2>();
                    components.Add(baseName, matches);
                }

                matches.Add(component);
            }

            return components;
        }

        private static string GetGeneratedBusbarBaseName(string componentName)
        {
            if (string.IsNullOrWhiteSpace(componentName) ||
                !componentName.StartsWith("Busbar_", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            int sheetMetalMarker = componentName.IndexOf("_SheetMetal_", StringComparison.OrdinalIgnoreCase);
            if (sheetMetalMarker > 0)
                return componentName.Substring(0, sheetMetalMarker);

            int instanceSeparator = componentName.LastIndexOf('-');
            if (instanceSeparator > 0 && instanceSeparator < componentName.Length - 1)
            {
                int instanceNumber;
                if (int.TryParse(componentName.Substring(instanceSeparator + 1), out instanceNumber))
                    return componentName.Substring(0, instanceSeparator);
            }

            return componentName;
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

            GeometryBounds bounds = GetAssemblyBounds(component);
            if (bounds == null)
            {
                report.AddError(scope, "Could not read the component bounding box in assembly coordinates.");
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
                Component = component,
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
                ValidateDimension(report, scope, "collector thickness Y", component.Bounds.SpanY, busbar.Profile.Thickness);
                ValidateDimension(report, scope, "collector width Z", component.Bounds.SpanZ, busbar.Profile.Width);
                return;
            }

            ValidateDimension(report, scope, "busbar width X", component.Bounds.SpanX, busbar.Profile.Width);
        }

        private static void ValidateHoleFeatures(BusbarPreflightReport report, GeneratedBusbarComponent component)
        {
            Busbar busbar = component.PlannedBusbar;
            int expectedCount = busbar.MountingPorts == null ? 0 : busbar.MountingPorts.Count;
            if (expectedCount == 0)
                return;

            for (int index = 0; index < expectedCount; index++)
            {
                string featureName = busbar.Name + "_HoleCut_P" + (index + 1);
                Feature cutFeature = FindFeatureByName(component.PartModel, featureName);
                string scope = "Hole/" + busbar.Name + "/P" + (index + 1);
                if (cutFeature == null)
                {
                    report.AddError(scope, "Expected cut feature is missing: " + featureName + ".");
                    continue;
                }

                string featureType = cutFeature.GetTypeName2();
                if (!IsCutFeatureType(featureType))
                {
                    report.AddError(scope, "Expected a cut feature but found type " + featureType + ".");
                    continue;
                }

                double cutDepth;
                if (TryReadCutDepth(cutFeature, out cutDepth))
                {
                    if (cutDepth + Mm(GeometryToleranceMm) < busbar.Profile.Thickness)
                    {
                        report.AddError(
                            scope,
                            "Cut depth=" + FormatMm(cutDepth) +
                            ", less than planned material thickness " + FormatMm(busbar.Profile.Thickness) + ".");
                    }
                    else
                    {
                        report.AddInfo(scope, "Cut feature exists; depth=" + FormatMm(cutDepth) + ".");
                    }
                }
                else
                {
                    report.AddWarning(scope, "Cut feature exists, but its depth could not be read through the current SolidWorks API.");
                }
            }
        }

        private static bool TryReadCutDepth(Feature cutFeature, out double depth)
        {
            depth = 0.0;

            try
            {
                ExtrudeFeatureData2 data = cutFeature.GetDefinition() as ExtrudeFeatureData2;
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
        }

        private static bool IsCutFeatureType(string featureType)
        {
            if (string.IsNullOrWhiteSpace(featureType))
                return false;

            // SolidWorks reports FeatureCut4-created extruded cuts as the internal type code "ICE".
            return SameText(featureType, "ICE") ||
                featureType.IndexOf("Cut", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Feature FindFeatureByName(ModelDoc2 model, string featureName)
        {
            if (model == null || string.IsNullOrWhiteSpace(featureName))
                return null;

            Feature feature = model.FirstFeature() as Feature;
            while (feature != null)
            {
                if (SameText(feature.Name, featureName))
                    return feature;

                feature = feature.GetNextFeature() as Feature;
            }

            return null;
        }

        private static void ValidateDoubleClampSurfaceContacts(
            BusbarPreflightReport report,
            List<Busbar> expected,
            Dictionary<string, GeneratedBusbarComponent> verified)
        {
            foreach (Busbar lower in expected.Where(busbar => busbar.BranchLegRole == BranchLegRole.Single &&
                busbar.Name.EndsWith("_Lower", StringComparison.OrdinalIgnoreCase)))
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

                double upperBranchBottomAtCollector = upperComponent.Bounds.MaxY - upperComponent.PlannedBusbar.Profile.Thickness;
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
