using System;
using System.Collections.Generic;
using System.Linq;

namespace SwFeatureDebug
{
    internal static class FastenerPlanBuilder
    {
        private const double CoordinateToleranceMm = 0.01;

        public static void BuildCollectorJoints(BusbarPlan plan, BusbarSettings settings)
        {
            if (plan == null || settings == null)
                return;

            plan.FastenerJoints.Clear();

            List<CollectorHoleConnection> connections = plan.Busbars
                .Where(busbar => busbar.Kind == BusbarKind.MainFeed || busbar.Kind == BusbarKind.Branch)
                .SelectMany(busbar => CreateCollectorHoleConnections(busbar, settings))
                .ToList();

            foreach (IGrouping<CollectorHoleConnection, CollectorHoleConnection> group in
                connections.GroupBy(connection => connection, new CollectorHoleConnectionComparer()))
            {
                CollectorHoleConnection first = group.First();
                FastenerJointPlan joint = new FastenerJointPlan
                {
                    JointId = "Collector_" + first.Phase + "_X" + ToMm(first.Port.HoleCenter.X).ToString("0.###") +
                        "_Z" + ToMm(first.Port.HoleCenter.Z).ToString("0.###"),
                    CollectorPhase = first.Phase,
                    HoleCenter = first.Port.HoleCenter,
                    HoleDiameterMm = first.Port.HoleDiameterMm,
                    ClampedThicknessMm = first.CollectorThicknessMm + group.Sum(connection => connection.Busbar.Profile.ThicknessMm),
                    MinimumThreadProjectionMm = settings.MinimumThreadProjectionMm,
                    ConnectedBusbars = group.Select(connection => connection.Busbar.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                };

                BuildSelection(joint, settings.FastenerCatalog);
                plan.FastenerJoints.Add(joint);
            }
        }

        private static IEnumerable<CollectorHoleConnection> CreateCollectorHoleConnections(
            Busbar busbar,
            BusbarSettings settings)
        {
            if (busbar.MountingPorts == null || busbar.Profile == null)
                return Enumerable.Empty<CollectorHoleConnection>();

            string phase = GetPhase(busbar.Name);
            double collectorThicknessMm = SameText(phase, "N")
                ? settings.NeutralCollectorThicknessMm
                : settings.CollectorThicknessMm;

            return busbar.MountingPorts
                .Where(port => port != null && port.Kind == PortKind.CollectorTap && port.HoleDiameterMm > 0.0)
                .Select(port => new CollectorHoleConnection
                {
                    Busbar = busbar,
                    Port = port,
                    Phase = phase,
                    CollectorThicknessMm = collectorThicknessMm
                });
        }

        private static void BuildSelection(FastenerJointPlan joint, List<FastenerSpec> catalog)
        {
            if (catalog == null || catalog.Count == 0)
            {
                joint.SelectionError = "Fastener catalog is empty.";
                return;
            }

            FastenerSpec fastener = catalog.FirstOrDefault(spec =>
                Math.Abs(spec.ClearanceHoleDiameterMm - joint.HoleDiameterMm) <= CoordinateToleranceMm);
            if (fastener == null)
            {
                joint.SelectionError =
                    "No fastener matches hole diameter " + joint.HoleDiameterMm.ToString("0.###") + "mm.";
                return;
            }

            joint.Fastener = fastener;
            joint.RequiredNominalLengthMm =
                joint.ClampedThicknessMm +
                2.0 * fastener.FlatWasherThicknessMm +
                fastener.SpringWasherCompressedThicknessMm +
                fastener.NutHeightMm +
                joint.MinimumThreadProjectionMm;

            joint.SelectedNominalLengthMm = fastener.StandardLengthsMm
                .Where(length => length + CoordinateToleranceMm >= joint.RequiredNominalLengthMm)
                .OrderBy(length => length)
                .FirstOrDefault();

            if (joint.SelectedNominalLengthMm <= 0.0)
            {
                joint.SelectionError =
                    "No standard length reaches the required " +
                    joint.RequiredNominalLengthMm.ToString("0.###") + "mm.";
                return;
            }

            joint.ActualThreadProjectionMm = joint.SelectedNominalLengthMm -
                joint.ClampedThicknessMm -
                2.0 * fastener.FlatWasherThicknessMm -
                fastener.SpringWasherCompressedThicknessMm -
                fastener.NutHeightMm;

            if (joint.ActualThreadProjectionMm + CoordinateToleranceMm < joint.MinimumThreadProjectionMm)
            {
                joint.SelectionError =
                    "Selected length leaves only " + joint.ActualThreadProjectionMm.ToString("0.###") +
                    "mm thread projection; minimum is " + joint.MinimumThreadProjectionMm.ToString("0.###") + "mm.";
            }
        }

        private static string GetPhase(string busbarName)
        {
            const string prefix = "Busbar_";
            if (string.IsNullOrWhiteSpace(busbarName) || !busbarName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            int separator = busbarName.IndexOf('_', prefix.Length);
            return separator < 0 ? string.Empty : busbarName.Substring(prefix.Length, separator - prefix.Length);
        }

        private static bool SameText(string first, string second)
        {
            return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }

        private static double ToMm(double valueInMeters)
        {
            return valueInMeters * 1000.0;
        }

        private class CollectorHoleConnection
        {
            public Busbar Busbar;
            public ConnectionPort Port;
            public string Phase;
            public double CollectorThicknessMm;
        }

        private sealed class CollectorHoleConnectionComparer : IEqualityComparer<CollectorHoleConnection>
        {
            public bool Equals(CollectorHoleConnection first, CollectorHoleConnection second)
            {
                if (object.ReferenceEquals(first, second))
                    return true;
                if (first == null || second == null || !SameText(first.Phase, second.Phase))
                    return false;

                return Math.Abs(ToMm(first.Port.HoleCenter.X - second.Port.HoleCenter.X)) <= CoordinateToleranceMm &&
                    Math.Abs(ToMm(first.Port.HoleCenter.Z - second.Port.HoleCenter.Z)) <= CoordinateToleranceMm;
            }

            public int GetHashCode(CollectorHoleConnection connection)
            {
                return StringComparer.OrdinalIgnoreCase.GetHashCode(connection == null ? string.Empty : connection.Phase ?? string.Empty);
            }
        }
    }
}
