using System;

namespace SwFeatureDebug
{
    internal class ManualBusbarRuleSet
    {
        public CabinetTopologyKind TopologyKind;
        public double DefaultEndMarginMm;
        public double BendRadiusMm;
        public double KFactor;
        public BusbarWidthMode WidthMode;
        public ContactFace MainFeedCollectorFace;
        public ContactFace BranchCollectorFace;
        public ThicknessTransitionPolicy TransitionPolicy;
        public RouteAxisOrder RouteAxisOrder;
        public double MainFeedStartEndMarginMm;
        public double MainFeedCollectorEndMarginRatio;
        public double MainFeedStartHoleDiameterMm;
        public double MainFeedCollectorHoleDiameterMm;
        public double BranchStartHoleDiameterMm;
        public double BranchCollectorHoleDiameterMm;
        public double CollectorTapHoleDiameterMm;
        public double NeutralBranchStartHoleDiameterMm;
        public double NeutralCollectorTapHoleDiameterMm;

        public static ManualBusbarRuleSet CreateDefault(
            CabinetTopologyKind topologyKind,
            BusbarSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            return new ManualBusbarRuleSet
            {
                TopologyKind = topologyKind,
                DefaultEndMarginMm = 15.0,
                BendRadiusMm = settings.SheetMetalBendRadiusMm,
                KFactor = settings.SheetMetalKFactor,
                WidthMode = BusbarWidthMode.MidPlane,
                MainFeedCollectorFace = ContactFace.Upper,
                BranchCollectorFace = ContactFace.Upper,
                TransitionPolicy = ThicknessTransitionPolicy.Auto,
                RouteAxisOrder = RouteAxisOrder.YThenZ,
                MainFeedStartEndMarginMm = 30.0,
                MainFeedCollectorEndMarginRatio = 0.5,
                MainFeedStartHoleDiameterMm = 13.0,
                MainFeedCollectorHoleDiameterMm = 13.0,
                BranchStartHoleDiameterMm = 13.0,
                BranchCollectorHoleDiameterMm = 13.0,
                CollectorTapHoleDiameterMm = 13.0,
                NeutralBranchStartHoleDiameterMm = 13.0,
                NeutralCollectorTapHoleDiameterMm = 13.0
            };
        }

        public ContactFace GetFuseOutFace()
        {
            return TopologyKind == CabinetTopologyKind.TypicalDesign
                ? ContactFace.Back
                : ContactFace.Front;
        }

        public ContactFace GetLoubaoInFace()
        {
            return ContactFace.Front;
        }
    }
}
