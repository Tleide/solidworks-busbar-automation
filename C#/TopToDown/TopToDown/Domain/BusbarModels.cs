using System;
using System.Collections.Generic;

namespace SwFeatureDebug
{
    internal class SheetMetalOptions
    {
        public double BendRadiusMm;
        public double KFactor;
        public BusbarWidthMode WidthMode;
        public SheetMetalWidthSide WidthSide;
        public bool ThickenDirection;

        // This is the per-busbar manufacturing snapshot consumed by both CAD and reporting.
        public static SheetMetalOptions FromRules(
            ManualBusbarRuleSet rules,
            BusbarSettings settings,
            BusbarKind kind)
        {
            if (rules == null)
                throw new ArgumentNullException("rules");

            if (settings == null)
                throw new ArgumentNullException("settings");

            return new SheetMetalOptions
            {
                BendRadiusMm = rules.BendRadiusMm,
                KFactor = rules.KFactor,
                WidthMode = rules.WidthMode,
                WidthSide = settings.GetSheetMetalWidthSide(kind),
                ThickenDirection = settings.SheetMetalThickenDirection
            };
        }
    }

    internal class BusbarRoutingOptions
    {
        public RouteAxisOrder AxisOrder;
        public ThicknessTransitionPolicy TransitionPolicy;
        public BranchRouteMode BranchRouteMode;
    }

    internal class ConnectionPort
    {
        public string Name;
        public string ComponentName;
        public PortKind Kind;
        public Point3 HoleCenter;
        public ContactFace RequiredFace;
        public AxisDirection PreferredLeadAxis;
        public int PreferredLeadSign;
        public double EndMarginMm;
        public double HoleDiameterMm;

        public override string ToString()
        {
            return
                Name +
                " [" + Kind + "] " +
                "face=" + RequiredFace +
                ", lead=" + PreferredLeadAxis + SignText(PreferredLeadSign) +
                ", margin=" + EndMarginMm.ToString("0.###") + "mm, " +
                "hole=" + HoleDiameterMm.ToString("0.###") + "mm, " +
                HoleCenter.ToMillimeterText();
        }

        private static string SignText(int sign)
        {
            return sign >= 0 ? "+" : "-";
        }
    }

    internal class Busbar
    {
        public string Name;
        public BusbarKind Kind;
        public BranchLegRole BranchLegRole;
        public BusbarProfile Profile;
        public ConnectionPort StartPort;
        public ConnectionPort EndPort;
        public BusbarRoutingOptions Routing;
        public SheetMetalOptions SheetMetal;
        public List<Point3> LogicalCenterline = new List<Point3>();
        public List<Point3> SheetMetalSketchLine = new List<Point3>();
        public List<ConnectionPort> MountingPorts = new List<ConnectionPort>();
    }

    internal class CollectorLayout
    {
        public string Phase;
        public AxisDirection Direction;
        public Point3 Center;
        public double StartX;
        public double EndX;
        public List<ConnectionPort> TapPorts = new List<ConnectionPort>();
    }

    internal class BusbarPlan
    {
        public ManualBusbarRuleSet Rules;
        public string FuseComponentName;
        public List<LoubaoGroup> Loubaos = new List<LoubaoGroup>();
        public List<CollectorLayout> Collectors = new List<CollectorLayout>();
        public List<Busbar> Busbars = new List<Busbar>();
        public List<FastenerJointPlan> FastenerJoints = new List<FastenerJointPlan>();
    }
}
