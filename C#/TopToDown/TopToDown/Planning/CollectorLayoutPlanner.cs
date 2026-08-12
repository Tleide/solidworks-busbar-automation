using System;
using System.Collections.Generic;
using System.Linq;

using BusbarAutomation.Core.Domain;

using BusbarAutomation.Core.Rules;

namespace BusbarAutomation.Core.Planning
{
    internal class CollectorLayoutPlanner
    {
        private readonly ManualBusbarRuleSet _rules;
        private readonly BusbarSettings _settings;
        private readonly BusbarLengthController _lengthController;

        public CollectorLayoutPlanner(ManualBusbarRuleSet rules, BusbarSettings settings)
        {
            _rules = rules;
            _settings = settings;
            _lengthController = new BusbarLengthController();
        }

        public CollectorLayout CreateLayout(
            string phase,
            int phaseIndex,
            ConnectionPort fuseOut,
            List<ConnectionPort> loubaoInputs,
            List<BusbarProfile> branchProfiles)
        {
            if (loubaoInputs == null || branchProfiles == null || loubaoInputs.Count != branchProfiles.Count)
                throw new Exception("Collector layout requires one branch profile for every loubao input.");

            if (branchProfiles.Any(profile => profile == null))
                throw new Exception("Collector layout does not accept a missing branch profile.");

            double baseY = loubaoInputs.Max(p => p.HoleCenter.Y) + _settings.CollectorTopClearanceYMeters;
            double collectorY = baseY - phaseIndex * _settings.CollectorPhaseSpacingMeters;
            double collectorZ = loubaoInputs.Average(p => p.HoleCenter.Z) + _settings.CollectorOffsetFromLoubaoInZMeters;
            double negativeXExtend = _settings.GetCollectorNegativeXExtend(phase);
            CollectorLengthRange lengthRange = _lengthController.Calculate(
                CreateConnectionExtents(fuseOut, loubaoInputs, branchProfiles),
                negativeXExtend);

            return new CollectorLayout
            {
                Phase = phase,
                Direction = AxisDirection.X,
                Center = new Point3((lengthRange.StartX + lengthRange.EndX) / 2.0, collectorY, collectorZ),
                StartX = lengthRange.StartX,
                EndX = lengthRange.EndX
            };
        }

        public ConnectionPort CreateTap(ManualPortRuleProvider ports, string phase, string name, double x, CollectorLayout collector, ContactFace face)
        {
            return CreateTap(ports, phase, name, x, collector, face, 0.0);
        }

        public ConnectionPort CreateTap(
            ManualPortRuleProvider ports,
            string phase,
            string name,
            double x,
            CollectorLayout collector,
            ContactFace face,
            double yOffset)
        {
            Point3 tapPoint = new Point3(x, collector.Center.Y + yOffset, collector.Center.Z);
            ConnectionPort tap = ports.CreateCollectorTapPort(phase, name, tapPoint, face);
            collector.TapPorts.Add(tap);
            return tap;
        }

        private List<CollectorConnectionExtent> CreateConnectionExtents(
            ConnectionPort fuseOut,
            List<ConnectionPort> loubaoInputs,
            List<BusbarProfile> branchProfiles)
        {
            List<CollectorConnectionExtent> extents = new List<CollectorConnectionExtent>();

            if (fuseOut != null)
            {
                extents.Add(new CollectorConnectionExtent
                {
                    Name = fuseOut.Name,
                    CenterX = fuseOut.HoleCenter.X,
                    HalfSpanX = _settings.MainFeedProfile.WidthMeters / 2.0
                });
            }

            if (loubaoInputs != null)
            {
                for (int i = 0; i < loubaoInputs.Count; i++)
                {
                    ConnectionPort loubaoInput = loubaoInputs[i];
                    BusbarProfile inputProfile = branchProfiles[i];
                    extents.Add(new CollectorConnectionExtent
                    {
                        Name = loubaoInput.Name,
                        CenterX = loubaoInput.HoleCenter.X,
                        HalfSpanX = inputProfile.WidthMeters / 2.0
                    });
                }
            }

            return extents;
        }
    }

    internal class CollectorLengthRange
    {
        public double StartX;
        public double EndX;
    }

    internal class CollectorConnectionExtent
    {
        public string Name;
        public double CenterX;
        public double HalfSpanX;
    }

    internal class BusbarLengthController
    {
        public CollectorLengthRange Calculate(List<CollectorConnectionExtent> connectionExtents, double negativeXExtend)
        {
            if (connectionExtents == null || connectionExtents.Count == 0)
                throw new Exception("Collector length calculation requires at least one connected busbar extent.");

            if (negativeXExtend < 0.0)
                throw new ArgumentOutOfRangeException("negativeXExtend", "Collector negative-X extension cannot be negative.");

            double positiveXLimit = connectionExtents.Max(e => e.CenterX + e.HalfSpanX);
            double negativeXLimit = connectionExtents.Min(e => e.CenterX - e.HalfSpanX);

            return new CollectorLengthRange
            {
                StartX = negativeXLimit - negativeXExtend,
                EndX = positiveXLimit
            };
        }
    }
}
