using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using BusbarAutomation.Core.Domain;

namespace BusbarAutomation.Application
{
    internal static class AssemblySnapshotFactory
    {
        private static readonly string[] FuseComponentNameHints = { "fuse", "HR6", "rong", "knife", "isolator" };
        private static readonly string[] BreakerComponentNameHints = { "loubao", "PGM", "leakage", "breaker" };

        public static AssemblySnapshot FromFoundPoints(
            IEnumerable<FoundPoint> foundPoints,
            string[] phaseNames,
            IEnumerable<int> supportedRatedCurrents,
            string sourceId = null)
        {
            List<FoundPoint> points = foundPoints == null ? null : foundPoints.ToList();
            if (points == null || points.Count == 0)
                throw new Exception("No reference points were scanned from the active assembly.");

            if (phaseNames == null || phaseNames.Length == 0)
                throw new ArgumentException("At least one phase name is required.", "phaseNames");

            List<IGrouping<string, FoundPoint>> components = points
                .GroupBy(point => point.ComponentName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            IGrouping<string, FoundPoint> fuse = FindFuseComponent(components, phaseNames);
            AssemblyDeviceSnapshot fuseSnapshot = CreateDeviceSnapshot(
                fuse,
                AssemblyDeviceKind.Fuse,
                null);

            List<int> ratedCurrents = (supportedRatedCurrents ?? Enumerable.Empty<int>())
                .Distinct()
                .ToList();
            List<AssemblyDeviceSnapshot> breakers = components
                .Where(component => !SameText(component.Key, fuse.Key))
                .Where(component => phaseNames.All(phase => HasPort(component, phase + "_IN")))
                .Where(component => ScoreNameHint(component.Key, FuseComponentNameHints) <=
                    ScoreNameHint(component.Key, BreakerComponentNameHints))
                .Select(component => CreateDeviceSnapshot(
                    component,
                    AssemblyDeviceKind.Breaker,
                    ParseRatedCurrentA(component.Key, ratedCurrents)))
                .OrderBy(AverageInputX)
                .ToList();

            if (breakers.Count == 0)
                throw new Exception("No loubao components were found for planning.");

            return new AssemblySnapshot(sourceId, fuseSnapshot, breakers);
        }

        private static IGrouping<string, FoundPoint> FindFuseComponent(
            IEnumerable<IGrouping<string, FoundPoint>> components,
            string[] phaseNames)
        {
            List<IGrouping<string, FoundPoint>> candidates = components
                .Where(component => phaseNames.All(phase => HasPort(component, phase + "_OUT")))
                .Where(component => ScoreNameHint(component.Key, FuseComponentNameHints) -
                    ScoreNameHint(component.Key, BreakerComponentNameHints) > 0)
                .ToList();

            if (candidates.Count == 0)
            {
                throw new Exception(
                    "No component has complete phase OUT points and a recognized fuse name. " +
                    "Use a standard fuse component name or update FuseComponentNameHints.");
            }

            if (candidates.Count > 1)
            {
                throw new Exception(
                    "Multiple components have complete phase OUT points and a recognized fuse name: " +
                    string.Join(", ", candidates.Select(candidate => candidate.Key).ToArray()) +
                    ". Keep exactly one fuse component or make the component contract explicit.");
            }

            return candidates[0];
        }

        private static AssemblyDeviceSnapshot CreateDeviceSnapshot(
            IGrouping<string, FoundPoint> component,
            AssemblyDeviceKind kind,
            int? ratedCurrentA)
        {
            List<AssemblyPortSnapshot> ports = new List<AssemblyPortSnapshot>();
            foreach (IGrouping<string, FoundPoint> portGroup in component.GroupBy(
                point => point.PointName,
                StringComparer.OrdinalIgnoreCase))
            {
                List<FoundPoint> matches = portGroup.ToList();
                if (matches.Count > 1)
                    throw new Exception("Duplicate reference point: " + component.Key + "." + portGroup.Key);

                ports.Add(new AssemblyPortSnapshot(portGroup.Key, matches[0].Position));
            }

            return new AssemblyDeviceSnapshot(component.Key, component.Key, kind, ratedCurrentA, ports);
        }

        private static int ParseRatedCurrentA(string componentName, List<int> supportedRatedCurrents)
        {
            if (string.IsNullOrWhiteSpace(componentName))
                throw new Exception("Cannot parse rated current from an empty loubao component name.");

            if (supportedRatedCurrents == null || supportedRatedCurrents.Count == 0)
                throw new Exception("Branch busbar rules are not configured.");

            List<int> matches = supportedRatedCurrents
                .Where(current => Regex.IsMatch(
                    componentName,
                    @"(?<![A-Za-z0-9])" + current + @"(?:A)?(?![A-Za-z0-9])",
                    RegexOptions.IgnoreCase))
                .ToList();

            if (matches.Count != 1)
            {
                throw new Exception(
                    "Loubao component name must contain exactly one configured rated-current token. " +
                    "Component=" + componentName + ", supported=" +
                    string.Join("A, ", supportedRatedCurrents.OrderBy(current => current).ToArray()) + "A.");
            }

            return matches[0];
        }

        private static bool HasPort(IEnumerable<FoundPoint> points, string portName)
        {
            return points.Any(point => SameText(point.PointName, portName));
        }

        private static double AverageInputX(AssemblyDeviceSnapshot device)
        {
            return device.Ports
                .Where(port => port.Name.EndsWith("_IN", StringComparison.OrdinalIgnoreCase))
                .Average(port => port.Position.X);
        }

        private static int ScoreNameHint(string componentName, IEnumerable<string> hints)
        {
            if (string.IsNullOrWhiteSpace(componentName))
                return 0;

            return hints.Count(hint => !string.IsNullOrWhiteSpace(hint) &&
                componentName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool SameText(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
