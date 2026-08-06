using System;
using System.Collections.Generic;

namespace SwFeatureDebug
{
    internal class BusbarOverlapHolePlanner
    {
        public List<ConnectionPort> CreateCollectorOverlapPorts(
            ConnectionPort centerPort,
            Busbar connectedBusbar,
            BusbarProfile collectorProfile,
            AxisDirection collectorLengthAxis)
        {
            if (centerPort == null)
                throw new Exception("Overlap hole planning requires a center port.");

            if (connectedBusbar == null || connectedBusbar.Profile == null)
                throw new Exception("Overlap hole planning requires a connected busbar profile.");

            if (collectorProfile == null)
                throw new Exception("Overlap hole planning requires a collector profile.");

            BusbarOverlapHoleRule rule;
            if (!BusbarOverlapRuleMatrix.TryResolve(connectedBusbar.Profile.WidthMm, collectorProfile.WidthMm, out rule))
            {
                throw new InvalidOperationException(
                    "No approved overlap hole rule exists for " +
                    connectedBusbar.Profile.WidthMm.ToString("0.###") + "x" +
                    collectorProfile.WidthMm.ToString("0.###") + "mm.");
            }

            Console.WriteLine(
                "Overlap hole rule [" + connectedBusbar.Name + " -> " + centerPort.Name + "]: " +
                connectedBusbar.Profile.WidthMm.ToString("0.###") + "x" +
                collectorProfile.WidthMm.ToString("0.###") +
                " => " + rule.SourceCode);

            if (rule.Pattern == BusbarOverlapHolePattern.Single)
                return CreateSingle(centerPort, rule);

            if (rule.Pattern == BusbarOverlapHolePattern.StraightDouble)
                return CreateStraightDouble(centerPort, connectedBusbar, collectorProfile, collectorLengthAxis, rule);

            return CreateDiagonalDouble(centerPort, rule);
        }

        private static List<ConnectionPort> CreateSingle(ConnectionPort centerPort, BusbarOverlapHoleRule rule)
        {
            return new List<ConnectionPort>
            {
                ClonePort(centerPort, centerPort.Name + "_Single", centerPort.HoleCenter, rule.HoleDiameterMm)
            };
        }

        private static List<ConnectionPort> CreateStraightDouble(
            ConnectionPort centerPort,
            Busbar connectedBusbar,
            BusbarProfile collectorProfile,
            AxisDirection collectorLengthAxis,
            BusbarOverlapHoleRule rule)
        {
            bool connectedIsNarrow = connectedBusbar.Profile.WidthMm <= collectorProfile.WidthMm;
            Point3 narrowDirection = connectedIsNarrow
                ? BusbarDirectionResolver.ResolveLengthDirectionAtPort(connectedBusbar, centerPort, AxisDirection.Z)
                : BusbarDirectionResolver.ResolveAxisDirection(collectorLengthAxis, centerPort.RequiredFace);

            double wideWidthMm = Math.Max(connectedBusbar.Profile.WidthMm, collectorProfile.WidthMm);
            Point3 offset = BusbarDirectionResolver.Scale(narrowDirection, Mm(wideWidthMm / 4.0));

            return new List<ConnectionPort>
            {
                ClonePort(
                    centerPort,
                    centerPort.Name + "_StraightA",
                    BusbarDirectionResolver.Add(centerPort.HoleCenter, offset),
                    rule.HoleDiameterMm),
                ClonePort(
                    centerPort,
                    centerPort.Name + "_StraightB",
                    BusbarDirectionResolver.Add(centerPort.HoleCenter, BusbarDirectionResolver.Scale(offset, -1.0)),
                    rule.HoleDiameterMm)
            };
        }

        private static List<ConnectionPort> CreateDiagonalDouble(ConnectionPort centerPort, BusbarOverlapHoleRule rule)
        {
            Point3 firstAxis = BusbarDirectionResolver.GetPlaneFirstAxis(centerPort.RequiredFace);
            Point3 secondAxis = BusbarDirectionResolver.GetPlaneSecondAxis(centerPort.RequiredFace);
            Point3 offset = BusbarDirectionResolver.Add(
                BusbarDirectionResolver.Scale(firstAxis, Mm(rule.OffsetMm)),
                BusbarDirectionResolver.Scale(secondAxis, Mm(rule.OffsetMm)));

            return new List<ConnectionPort>
            {
                ClonePort(
                    centerPort,
                    centerPort.Name + "_DiagonalA",
                    BusbarDirectionResolver.Add(centerPort.HoleCenter, offset),
                    rule.HoleDiameterMm),
                ClonePort(
                    centerPort,
                    centerPort.Name + "_DiagonalB",
                    BusbarDirectionResolver.Add(centerPort.HoleCenter, BusbarDirectionResolver.Scale(offset, -1.0)),
                    rule.HoleDiameterMm)
            };
        }

        private static ConnectionPort ClonePort(ConnectionPort source, string name, Point3 holeCenter, double holeDiameterMm)
        {
            return new ConnectionPort
            {
                Name = name,
                ComponentName = source.ComponentName,
                Kind = source.Kind,
                HoleCenter = holeCenter,
                RequiredFace = source.RequiredFace,
                PreferredLeadAxis = source.PreferredLeadAxis,
                PreferredLeadSign = source.PreferredLeadSign,
                EndMarginMm = source.EndMarginMm,
                HoleDiameterMm = holeDiameterMm
            };
        }

        private static double Mm(double value)
        {
            return value / 1000.0;
        }
    }
}
