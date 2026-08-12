using System;

using BusbarAutomation.Core.Domain;

namespace BusbarAutomation.Core.Rules
{
    internal class ManualPortRuleProvider
    {
        private readonly ManualBusbarRuleSet _rules;

        public ManualPortRuleProvider(ManualBusbarRuleSet rules)
        {
            _rules = rules;
        }

        public ConnectionPort CreateFuseOutPort(string phase, string componentName, Point3 position)
        {
            ConnectionPort port = CreateDevicePort(
                "Fuse " + phase + "_OUT",
                componentName,
                position,
                PortKind.FuseOut,
                _rules.GetFuseOutFace(),
                AxisDirection.Y,
                -1);

            port.EndMarginMm = _rules.MainFeedStartEndMarginMm;
            port.HoleDiameterMm = _rules.MainFeedStartHoleDiameterMm;
            return port;
        }

        public ConnectionPort CreateLoubaoInPort(
            string phase,
            int index,
            string componentName,
            Point3 position)
        {
            return CreateDevicePort(
                "Loubao " + phase + "_IN " + index,
                componentName,
                position,
                PortKind.LoubaoIn,
                _rules.GetLoubaoInFace(),
                AxisDirection.Y,
                1);
        }

        public ConnectionPort CreateCollectorTapPort(string phase, string name, Point3 point, ContactFace face)
        {
            return new ConnectionPort
            {
                Name = name,
                ComponentName = "Collector_" + phase,
                Kind = PortKind.CollectorTap,
                HoleCenter = point,
                RequiredFace = face,
                PreferredLeadAxis = AxisDirection.Z,
                PreferredLeadSign = 0,
                EndMarginMm = _rules.DefaultEndMarginMm,
                HoleDiameterMm = 0.0
            };
        }

        private ConnectionPort CreateDevicePort(
            string name,
            string componentName,
            Point3 position,
            PortKind kind,
            ContactFace face,
            AxisDirection leadAxis,
            int leadSign)
        {
            return new ConnectionPort
            {
                Name = name,
                ComponentName = componentName,
                Kind = kind,
                HoleCenter = position,
                RequiredFace = face,
                PreferredLeadAxis = leadAxis,
                PreferredLeadSign = leadSign,
                EndMarginMm = _rules.DefaultEndMarginMm,
                HoleDiameterMm = 0.0
            };
        }
    }
}
