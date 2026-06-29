using System;
using System.Collections.Generic;
using System.Linq;

namespace SwFeatureDebug
{
    internal enum CabinetTopologyKind
    {
        TypicalDesign,
        SouthernGrid
    }

    internal enum ContactFace
    {
        Front,
        Back,
        Upper,
        Lower,
        Left,
        Right
    }

    internal enum BusbarWidthMode
    {
        MidPlane
    }

    internal enum ThicknessTransitionPolicy
    {
        PreferStartPort,
        PreferEndPort,
        Auto
    }

    internal enum ContactTopologyKind
    {
        SameSide,
        DifferentSide
    }

    internal enum PortKind
    {
        FuseOut,
        LoubaoIn,
        CollectorTap
    }

    internal enum RouteAxisOrder
    {
        YThenZ,
        ZThenY
    }

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

        public static ManualBusbarRuleSet CreateDefault(CabinetTopologyKind topologyKind)
        {
            return new ManualBusbarRuleSet
            {
                TopologyKind = topologyKind,
                DefaultEndMarginMm = 15.0,
                BendRadiusMm = 5.0,
                KFactor = 0.47,
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

    internal class SheetMetalOptions
    {
        public double BendRadiusMm;
        public double KFactor;
        public BusbarWidthMode WidthMode;

        public static SheetMetalOptions FromRules(ManualBusbarRuleSet rules)
        {
            return new SheetMetalOptions
            {
                BendRadiusMm = rules.BendRadiusMm,
                KFactor = rules.KFactor,
                WidthMode = rules.WidthMode
            };
        }
    }

    internal class BusbarRoutingOptions
    {
        public RouteAxisOrder AxisOrder;
        public ThicknessTransitionPolicy TransitionPolicy;
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

    internal class BusbarV2
    {
        public string Name;
        public BusbarKind Kind;
        public BusbarProfile Profile;
        public ConnectionPort StartPort;
        public ConnectionPort EndPort;
        public BusbarRoutingOptions Routing;
        public SheetMetalOptions SheetMetal;
        public List<Point3> LogicalCenterline = new List<Point3>();
        public List<Point3> SheetMetalSketchLine = new List<Point3>();
        public List<ConnectionPort> MountingPorts = new List<ConnectionPort>();
    }

    internal class CollectorLayoutV2
    {
        public string Phase;
        public AxisDirection Direction;
        public Point3 Center;
        public double StartX;
        public double EndX;
        public List<ConnectionPort> TapPorts = new List<ConnectionPort>();
    }

    internal class BusbarPlanV2
    {
        public ManualBusbarRuleSet Rules;
        public string FuseComponentName;
        public List<LoubaoGroupV2> Loubaos = new List<LoubaoGroupV2>();
        public List<CollectorLayoutV2> Collectors = new List<CollectorLayoutV2>();
        public List<BusbarV2> Busbars = new List<BusbarV2>();
    }

    internal class ManualPortRuleProvider
    {
        private readonly ManualBusbarRuleSet _rules;

        public ManualPortRuleProvider(ManualBusbarRuleSet rules)
        {
            _rules = rules;
        }

        public ConnectionPort CreateFuseOutPort(string phase, FoundPoint point)
        {
            ConnectionPort port = CreateDevicePort(
                "Fuse " + phase + "_OUT",
                point,
                PortKind.FuseOut,
                _rules.GetFuseOutFace(),
                AxisDirection.Y,
                -1);

            port.EndMarginMm = _rules.MainFeedStartEndMarginMm;
            port.HoleDiameterMm = _rules.MainFeedStartHoleDiameterMm;
            return port;
        }

        public ConnectionPort CreateLoubaoInPort(string phase, int index, FoundPoint point)
        {
            return CreateDevicePort(
                "Loubao " + phase + "_IN " + index,
                point,
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
            FoundPoint point,
            PortKind kind,
            ContactFace face,
            AxisDirection leadAxis,
            int leadSign)
        {
            return new ConnectionPort
            {
                Name = name,
                ComponentName = point.ComponentName,
                Kind = kind,
                HoleCenter = point.Position,
                RequiredFace = face,
                PreferredLeadAxis = leadAxis,
                PreferredLeadSign = leadSign,
                EndMarginMm = _rules.DefaultEndMarginMm,
                HoleDiameterMm = 0.0
            };
        }
    }

    internal class CollectorLayoutPlannerV2
    {
        private readonly ManualBusbarRuleSet _rules;
        private readonly BusbarSettings _settings;
        private readonly BusbarLengthControllerV2 _lengthController;

        public CollectorLayoutPlannerV2(ManualBusbarRuleSet rules, BusbarSettings settings)
        {
            _rules = rules;
            _settings = settings;
            _lengthController = new BusbarLengthControllerV2(settings);
        }

        public CollectorLayoutV2 CreateLayout(string phase, int phaseIndex, ConnectionPort fuseOut, List<ConnectionPort> loubaoInputs)
        {
            return CreateLayout(phase, phaseIndex, fuseOut, loubaoInputs, _settings.BranchProfile);
        }

        public CollectorLayoutV2 CreateLayout(string phase, int phaseIndex, ConnectionPort fuseOut, List<ConnectionPort> loubaoInputs, BusbarProfile branchProfile)
        {
            double baseY = loubaoInputs.Max(p => p.HoleCenter.Y) + _settings.CollectorTopClearanceY;
            double collectorY = baseY - phaseIndex * _settings.CollectorPhaseSpacing;
            double collectorZ = loubaoInputs.Average(p => p.HoleCenter.Z) + _settings.CollectorOffsetFromLoubaoInZ;
            CollectorLengthRangeV2 lengthRange = _lengthController.Calculate(CreateConnectionExtents(fuseOut, loubaoInputs, branchProfile));

            return new CollectorLayoutV2
            {
                Phase = phase,
                Direction = AxisDirection.X,
                Center = new Point3((lengthRange.StartX + lengthRange.EndX) / 2.0, collectorY, collectorZ),
                StartX = lengthRange.StartX,
                EndX = lengthRange.EndX
            };
        }

        public ConnectionPort CreateTap(ManualPortRuleProvider ports, string phase, string name, double x, CollectorLayoutV2 collector, ContactFace face)
        {
            Point3 tapPoint = new Point3(x, collector.Center.Y, collector.Center.Z);
            ConnectionPort tap = ports.CreateCollectorTapPort(phase, name, tapPoint, face);
            collector.TapPorts.Add(tap);
            return tap;
        }

        private List<CollectorConnectionExtentV2> CreateConnectionExtents(ConnectionPort fuseOut, List<ConnectionPort> loubaoInputs, BusbarProfile branchProfile)
        {
            List<CollectorConnectionExtentV2> extents = new List<CollectorConnectionExtentV2>();
            BusbarProfile inputProfile = branchProfile ?? _settings.BranchProfile;

            if (fuseOut != null)
            {
                extents.Add(new CollectorConnectionExtentV2
                {
                    Name = fuseOut.Name,
                    CenterX = fuseOut.HoleCenter.X,
                    HalfSpanX = _settings.MainFeedProfile.Width / 2.0
                });
            }

            if (loubaoInputs != null)
            {
                foreach (ConnectionPort loubaoInput in loubaoInputs)
                {
                    extents.Add(new CollectorConnectionExtentV2
                    {
                        Name = loubaoInput.Name,
                        CenterX = loubaoInput.HoleCenter.X,
                        HalfSpanX = inputProfile.Width / 2.0
                    });
                }
            }

            return extents;
        }
    }

    internal class CollectorLengthRangeV2
    {
        public double StartX;
        public double EndX;
    }

    internal class CollectorConnectionExtentV2
    {
        public string Name;
        public double CenterX;
        public double HalfSpanX;
    }

    internal class BusbarLengthControllerV2
    {
        private readonly BusbarSettings _settings;

        public BusbarLengthControllerV2(BusbarSettings settings)
        {
            _settings = settings;
        }

        public CollectorLengthRangeV2 Calculate(List<CollectorConnectionExtentV2> connectionExtents)
        {
            if (connectionExtents == null || connectionExtents.Count == 0)
                throw new Exception("Collector length calculation requires at least one connected busbar extent.");

            double positiveXLimit = connectionExtents.Max(e => e.CenterX + e.HalfSpanX);
            double negativeXLimit = connectionExtents.Min(e => e.CenterX - e.HalfSpanX);

            return new CollectorLengthRangeV2
            {
                StartX = negativeXLimit - _settings.CollectorNegativeXExtend,
                EndX = positiveXLimit
            };
        }
    }

    internal class MainFeedRouteDecision
    {
        public double LeadOutY;
        public double ApproachZ;
        public string LeadOutRule;
        public string ApproachRule;
    }

    internal class BusbarRoutePlannerV2
    {
        private readonly BusbarSettings _settings;

        public BusbarRoutePlannerV2(BusbarSettings settings)
        {
            _settings = settings;
        }

        public List<Point3> CreateRoute(BusbarKind kind, BusbarProfile profile, ConnectionPort start, ConnectionPort end, RouteAxisOrder axisOrder)
        {
            if (kind == BusbarKind.MainFeed)
                return CreateMainFeedRoute(profile, start, end);

            return CreateSimpleRoute(start, end, axisOrder);
        }

        private List<Point3> CreateMainFeedRoute(BusbarProfile profile, ConnectionPort start, ConnectionPort end)
        {
            List<Point3> points = new List<Point3>();
            Point3 p0 = start.HoleCenter;
            MainFeedRouteDecision decision = CalculateMainFeedRouteDecision(profile, start, end);

            Add(points, p0);
            Add(points, new Point3(p0.X, p0.Y + decision.LeadOutY, p0.Z));
            Add(points, new Point3(p0.X, p0.Y + decision.LeadOutY, decision.ApproachZ));
            Add(points, new Point3(p0.X, end.HoleCenter.Y, decision.ApproachZ));
            Add(points, end.HoleCenter);

            return points;
        }

        private List<Point3> CreateSimpleRoute(ConnectionPort start, ConnectionPort end, RouteAxisOrder axisOrder)
        {
            List<Point3> points = new List<Point3>();
            Add(points, start.HoleCenter);

            if (axisOrder == RouteAxisOrder.ZThenY)
            {
                Add(points, new Point3(start.HoleCenter.X, start.HoleCenter.Y, end.HoleCenter.Z));
                Add(points, new Point3(start.HoleCenter.X, end.HoleCenter.Y, end.HoleCenter.Z));
            }
            else
            {
                Add(points, new Point3(start.HoleCenter.X, end.HoleCenter.Y, start.HoleCenter.Z));
                Add(points, new Point3(start.HoleCenter.X, end.HoleCenter.Y, end.HoleCenter.Z));
            }

            return points;
        }

        private MainFeedRouteDecision CalculateMainFeedRouteDecision(BusbarProfile profile, ConnectionPort start, ConnectionPort end)
        {
            return new MainFeedRouteDecision
            {
                LeadOutY = CalculateMainFeedLeadOutY(start),
                ApproachZ = CalculateMainFeedApproachZ(profile, start.HoleCenter.Z, end.HoleCenter.Z),
                LeadOutRule = "Current simple rule: device preferred Y lead direction * Settings.MainLeadOutY.",
                ApproachRule = "Current simple rule: stay outside collector by half collector width + half main-feed width + front clearance."
            };
        }

        private double CalculateMainFeedLeadOutY(ConnectionPort start)
        {
            int sign = -1;
            if (start.PreferredLeadAxis == AxisDirection.Y && start.PreferredLeadSign != 0)
                sign = start.PreferredLeadSign > 0 ? 1 : -1;

            return sign * _settings.MainLeadOutY;
        }

        private double CalculateMainFeedApproachZ(BusbarProfile mainFeedProfile, double startZ, double collectorTapZ)
        {
            double directionToCollector = Math.Sign(collectorTapZ - startZ);
            if (Math.Abs(directionToCollector) < 0.001)
                return collectorTapZ;

            double offsetFromCollector = CalculateMainFeedApproachOffsetZ(mainFeedProfile);
            double approachZ = collectorTapZ - directionToCollector * offsetFromCollector;

            bool approachBetweenStartAndCollector =
                directionToCollector < 0
                    ? approachZ < startZ && approachZ > collectorTapZ
                    : approachZ > startZ && approachZ < collectorTapZ;

            if (!approachBetweenStartAndCollector)
                approachZ = (startZ + collectorTapZ) / 2.0;

            return approachZ;
        }

        private double CalculateMainFeedApproachOffsetZ(BusbarProfile mainFeedProfile)
        {
            // V0 keeps the long Y move outside the collector width envelope, then enters the collector on the final Z segment.
            return _settings.CollectorProfile.Width / 2.0 +
                mainFeedProfile.Width / 2.0 +
                _settings.MainCollectorFrontClearance;
        }

        private static void Add(List<Point3> points, Point3 point)
        {
            if (points.Count == 0 || points[points.Count - 1].DistanceTo(point) > Mm(0.01))
                points.Add(point);
        }

        private static double Mm(double value)
        {
            return value / 1000.0;
        }
    }

    internal class ContactTopologyResolver
    {
        public List<Point3> CreateSheetMetalSketchLine(BusbarV2 busbar)
        {
            List<Point3> sketchLine = AddEndMargins(busbar);
            ApplyThicknessTransition(busbar, sketchLine);
            return sketchLine;
        }

        public bool IsSameSide(ConnectionPort start, ConnectionPort end)
        {
            return start.RequiredFace == end.RequiredFace;
        }

        public ContactTopologyKind Resolve(BusbarV2 busbar)
        {
            if (busbar == null || busbar.StartPort == null || busbar.EndPort == null)
                return ContactTopologyKind.SameSide;

            if (busbar.Kind == BusbarKind.MainFeed)
                return ResolveMainFeedTopology(busbar);

            if (busbar.Kind == BusbarKind.Branch)
                return ContactTopologyKind.SameSide;

            return ContactTopologyKind.SameSide;
        }

        private static ContactTopologyKind ResolveMainFeedTopology(BusbarV2 busbar)
        {
            bool collectorUpper = busbar.EndPort.RequiredFace == ContactFace.Upper;
            bool collectorLower = busbar.EndPort.RequiredFace == ContactFace.Lower;
            bool fuseOnBackSide = busbar.StartPort.RequiredFace == ContactFace.Back;
            bool fuseOnFrontSide = busbar.StartPort.RequiredFace == ContactFace.Front;

            if (fuseOnBackSide && collectorUpper)
                return ContactTopologyKind.DifferentSide;

            if (fuseOnBackSide && collectorLower)
                return ContactTopologyKind.SameSide;

            if (fuseOnFrontSide && collectorUpper)
                return ContactTopologyKind.SameSide;

            if (fuseOnFrontSide && collectorLower)
                return ContactTopologyKind.DifferentSide;

            return ContactTopologyKind.SameSide;
        }

        private static void ApplyThicknessTransition(BusbarV2 busbar, List<Point3> sketchLine)
        {
            if (busbar == null || sketchLine == null || sketchLine.Count < 2)
                return;

            ContactTopologyKind topology = ResolveForTransition(busbar);
            if (topology == ContactTopologyKind.SameSide)
                return;

            bool compensateStart = ShouldCompensateStart(busbar);
            int anchorIndex = compensateStart
                ? FindPointIndex(sketchLine, busbar.LogicalCenterline[0])
                : FindPointIndex(sketchLine, busbar.LogicalCenterline[busbar.LogicalCenterline.Count - 1]);

            if (anchorIndex < 0)
                return;

            Point3 tangent = GetEndpointTangent(sketchLine, anchorIndex, compensateStart);
            Point3 normal = ChooseThicknessNormal(tangent, compensateStart ? busbar.StartPort.RequiredFace : busbar.EndPort.RequiredFace);
            Point3 offset = Scale(normal, busbar.Profile.Thickness);

            if (offset.DistanceTo(new Point3(0, 0, 0)) <= Mm(0.001))
                return;

            MoveEndpointRun(sketchLine, anchorIndex, compensateStart, offset);

            Console.WriteLine(
                "Thickness topology transition [" + busbar.Name + "]: " +
                topology + ", compensate=" + (compensateStart ? "Start" : "End") +
                ", offset dX=" + ToMm(offset.X).ToString("F3") +
                " mm, dY=" + ToMm(offset.Y).ToString("F3") +
                " mm, dZ=" + ToMm(offset.Z).ToString("F3") + " mm");
        }

        private static ContactTopologyKind ResolveForTransition(BusbarV2 busbar)
        {
            if (busbar.Kind == BusbarKind.MainFeed)
                return ResolveMainFeedTopology(busbar);

            if (busbar.Kind == BusbarKind.Branch)
                return ContactTopologyKind.SameSide;

            return ContactTopologyKind.SameSide;
        }

        private static bool ShouldCompensateStart(BusbarV2 busbar)
        {
            if (busbar.Routing == null)
                return false;

            if (busbar.Routing.TransitionPolicy == ThicknessTransitionPolicy.PreferStartPort)
                return true;

            return false;
        }

        private static Point3 GetEndpointTangent(List<Point3> points, int anchorIndex, bool atStart)
        {
            if (atStart)
            {
                for (int i = anchorIndex + 1; i < points.Count; i++)
                {
                    Point3 tangent = Normalize(Subtract(points[i], points[anchorIndex]));
                    if (tangent.DistanceTo(new Point3(0, 0, 0)) > Mm(0.001))
                        return tangent;
                }
            }
            else
            {
                for (int i = anchorIndex - 1; i >= 0; i--)
                {
                    Point3 tangent = Normalize(Subtract(points[anchorIndex], points[i]));
                    if (tangent.DistanceTo(new Point3(0, 0, 0)) > Mm(0.001))
                        return tangent;
                }
            }

            return new Point3(0, 0, 0);
        }

        private static Point3 ChooseThicknessNormal(Point3 tangent, ContactFace face)
        {
            if (face == ContactFace.Upper)
                return new Point3(0, 1, 0);

            if (face == ContactFace.Lower)
                return new Point3(0, -1, 0);

            if (face == ContactFace.Back)
                return new Point3(0, 0, 1);

            if (face == ContactFace.Front)
                return new Point3(0, 0, -1);

            if (face == ContactFace.Left)
                return new Point3(1, 0, 0);

            if (face == ContactFace.Right)
                return new Point3(-1, 0, 0);

            return InferPlanarNormal(tangent);
        }

        private static Point3 InferPlanarNormal(Point3 tangent)
        {
            double ax = Math.Abs(tangent.X);
            double ay = Math.Abs(tangent.Y);
            double az = Math.Abs(tangent.Z);

            if (az >= ax && az >= ay)
                return new Point3(0, 1, 0);

            if (ay >= ax && ay >= az)
                return new Point3(0, 0, 1);

            return new Point3(0, 1, 0);
        }

        private static void MoveEndpointRun(List<Point3> points, int anchorIndex, bool atStart, Point3 offset)
        {
            if (atStart)
            {
                Point3 anchor = points[anchorIndex];
                for (int i = anchorIndex; i >= 0; i--)
                    points[i] = Add(points[i], offset);

                for (int i = anchorIndex + 1; i < points.Count; i++)
                {
                    if (!SharesTwoCoordinates(points[i], anchor))
                        break;

                    points[i] = Add(points[i], offset);
                }

                return;
            }

            Point3 anchorEnd = points[anchorIndex];
            for (int i = anchorIndex; i < points.Count; i++)
                points[i] = Add(points[i], offset);

            for (int i = anchorIndex - 1; i >= 0; i--)
            {
                if (!SharesTwoCoordinates(points[i], anchorEnd))
                    break;

                points[i] = Add(points[i], offset);
            }
        }

        private static bool SharesTwoCoordinates(Point3 a, Point3 b)
        {
            int same = 0;
            if (Math.Abs(a.X - b.X) <= Mm(0.01))
                same++;
            if (Math.Abs(a.Y - b.Y) <= Mm(0.01))
                same++;
            if (Math.Abs(a.Z - b.Z) <= Mm(0.01))
                same++;

            return same >= 2;
        }

        private static int FindPointIndex(List<Point3> points, Point3 target)
        {
            if (points == null)
                return -1;

            for (int i = 0; i < points.Count; i++)
            {
                if (points[i].DistanceTo(target) <= Mm(0.01))
                    return i;
            }

            return -1;
        }

        private static List<Point3> AddEndMargins(BusbarV2 busbar)
        {
            List<Point3> route = busbar.LogicalCenterline;
            if (route == null || route.Count < 2)
                return route == null ? new List<Point3>() : new List<Point3>(route);

            Point3 startDirection = Normalize(Subtract(route[1], route[0]));
            Point3 endDirection = Normalize(Subtract(route[route.Count - 1], route[route.Count - 2]));

            double startMargin = Mm(busbar.StartPort.EndMarginMm);
            double endMargin = Mm(busbar.EndPort.EndMarginMm);

            Point3 startEnd = Add(route[0], Scale(startDirection, -startMargin));
            Point3 endEnd = Add(route[route.Count - 1], Scale(endDirection, endMargin));

            List<Point3> sketchLine = new List<Point3>();
            AddIfDifferent(sketchLine, startEnd);
            foreach (Point3 point in route)
                AddIfDifferent(sketchLine, point);
            AddIfDifferent(sketchLine, endEnd);

            return sketchLine;
        }

        private static void AddIfDifferent(List<Point3> points, Point3 point)
        {
            if (points.Count == 0 || points[points.Count - 1].DistanceTo(point) > Mm(0.01))
                points.Add(point);
        }

        private static Point3 Add(Point3 a, Point3 b)
        {
            return new Point3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        private static Point3 Subtract(Point3 a, Point3 b)
        {
            return new Point3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        private static Point3 Scale(Point3 vector, double scale)
        {
            return new Point3(vector.X * scale, vector.Y * scale, vector.Z * scale);
        }

        private static Point3 Normalize(Point3 vector)
        {
            double length = Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z);
            if (length <= Mm(0.001))
                return new Point3(0, 0, 0);

            return new Point3(vector.X / length, vector.Y / length, vector.Z / length);
        }

        private static double Mm(double value)
        {
            return value / 1000.0;
        }

        private static double ToMm(double value)
        {
            return value * 1000.0;
        }
    }

    internal static class BusbarPlanningDemoV2
    {
        private const string NeutralConductorName = "N";
        private static readonly string[] FuseComponentNameHints = { "fuse", "HR6", "rong", "knife", "isolator" };
        private static readonly string[] LoubaoComponentNameHints = { "loubao", "PGM", "leakage", "breaker" };

        public static void Run()
        {
            ManualBusbarRuleSet rules = ManualBusbarRuleSet.CreateDefault(CabinetTopologyKind.TypicalDesign);
            BusbarSettings settings = CreateDemoSettings();
            ManualPortRuleProvider portRules = new ManualPortRuleProvider(rules);
            CollectorLayoutPlannerV2 collectorPlanner = new CollectorLayoutPlannerV2(rules, settings);
            BusbarRoutePlannerV2 routePlanner = new BusbarRoutePlannerV2(settings);
            ContactTopologyResolver topology = new ContactTopologyResolver();

            Console.WriteLine("===== Busbar V2 geometry planning demo =====");
            Console.WriteLine("Topology: " + rules.TopologyKind);
            Console.WriteLine("Fuse OUT face: " + rules.GetFuseOutFace());
            Console.WriteLine("Loubao IN face: " + rules.GetLoubaoInFace());
            Console.WriteLine("Collector default lap: MainFeed=" + rules.MainFeedCollectorFace + ", Branch=" + rules.BranchCollectorFace);
            Console.WriteLine();

            FoundPoint fuseA = DemoFoundPoint("HR6_Demo", "A_OUT", -80, 0, -40);
            List<FoundPoint> loubaoA = new List<FoundPoint>
            {
                DemoFoundPoint("PGM_Demo_1", "A_IN", -160, -200, 40),
                DemoFoundPoint("PGM_Demo_2", "A_IN", -80, -200, 40),
                DemoFoundPoint("PGM_Demo_3", "A_IN", 0, -200, 40)
            };

            ConnectionPort fusePort = portRules.CreateFuseOutPort("A", fuseA);
            List<ConnectionPort> loubaoPorts = loubaoA
                .Select((p, i) => portRules.CreateLoubaoInPort("A", i + 1, p))
                .ToList();

            CollectorLayoutV2 collector = collectorPlanner.CreateLayout("A", 0, fusePort, loubaoPorts);
            PrintCollector(collector);

            List<BusbarV2> busbars = new List<BusbarV2>();

            ConnectionPort mainTap = collectorPlanner.CreateTap(
                portRules,
                "A",
                "A_MainFeed_Tap",
                fusePort.HoleCenter.X,
                collector,
                rules.MainFeedCollectorFace);
            ApplyMainFeedCollectorTapRules(mainTap, settings, rules);

            busbars.Add(CreateBusbar(
                "Busbar_A_MainFeed_V2",
                BusbarKind.MainFeed,
                settings.MainFeedProfile,
                fusePort,
                mainTap,
                rules,
                routePlanner,
                topology));

            for (int i = 0; i < loubaoPorts.Count; i++)
            {
                ConnectionPort branchTap = collectorPlanner.CreateTap(
                    portRules,
                    "A",
                    "A_Branch_" + (i + 1) + "_Tap",
                    loubaoPorts[i].HoleCenter.X,
                    collector,
                    rules.BranchCollectorFace);

                busbars.Add(CreateBusbar(
                    "Busbar_A_Branch_" + (i + 1) + "_V2",
                    BusbarKind.Branch,
                    settings.BranchProfile,
                    loubaoPorts[i],
                    branchTap,
                    rules,
                    routePlanner,
                    topology));
            }

            foreach (BusbarV2 busbar in busbars)
                PrintBusbar(busbar, topology);
        }

        public static void RunFromScannedAssembly(List<FoundPoint> foundPoints, string[] phaseNames, BusbarSettings settings)
        {
            BusbarPlanV2 plan = BuildPlanFromScannedAssembly(foundPoints, phaseNames, settings);

            ContactTopologyResolver topology = new ContactTopologyResolver();

            Console.WriteLine("===== Busbar V2 assembly geometry planning =====");
            Console.WriteLine("Topology: " + plan.Rules.TopologyKind);
            Console.WriteLine("Fuse component: " + plan.FuseComponentName);
            Console.WriteLine("Loubao count: " + plan.Loubaos.Count);
            Console.WriteLine("Fuse OUT face: " + plan.Rules.GetFuseOutFace());
            Console.WriteLine("Loubao IN face: " + plan.Rules.GetLoubaoInFace());
            Console.WriteLine("Collector default lap: MainFeed=" + plan.Rules.MainFeedCollectorFace + ", Branch=" + plan.Rules.BranchCollectorFace);
            Console.WriteLine();

            foreach (CollectorLayoutV2 collector in plan.Collectors)
                PrintCollector(collector);

            foreach (BusbarV2 busbar in plan.Busbars)
                PrintBusbar(busbar, topology);
        }

        public static BusbarPlanV2 BuildPlanFromScannedAssembly(List<FoundPoint> foundPoints, string[] phaseNames, BusbarSettings settings)
        {
            if (foundPoints == null || foundPoints.Count == 0)
                throw new Exception("No reference points were scanned from the active assembly.");

            ManualBusbarRuleSet rules = ManualBusbarRuleSet.CreateDefault(CabinetTopologyKind.TypicalDesign);
            ManualPortRuleProvider portRules = new ManualPortRuleProvider(rules);
            CollectorLayoutPlannerV2 collectorPlanner = new CollectorLayoutPlannerV2(rules, settings);
            BusbarRoutePlannerV2 routePlanner = new BusbarRoutePlannerV2(settings);
            ContactTopologyResolver topology = new ContactTopologyResolver();

            string fuseComponent = FindFuseComponent(foundPoints, phaseNames);
            List<LoubaoGroupV2> loubaos = FindLoubaoGroups(foundPoints, phaseNames, fuseComponent);

            if (loubaos.Count == 0)
                throw new Exception("No loubao components were found for V2 planning.");

            BusbarPlanV2 plan = new BusbarPlanV2
            {
                Rules = rules,
                FuseComponentName = fuseComponent,
                Loubaos = loubaos
            };

            for (int phaseIndex = 0; phaseIndex < phaseNames.Length; phaseIndex++)
            {
                string phase = phaseNames[phaseIndex];
                FoundPoint fuseOutPoint = FindRequiredPoint(foundPoints, fuseComponent, phase + "_OUT");
                ConnectionPort fuseOut = portRules.CreateFuseOutPort(phase, fuseOutPoint);

                List<ConnectionPort> loubaoInputs = loubaos
                    .Select((l, i) => portRules.CreateLoubaoInPort(phase, i + 1, FindRequiredPoint(foundPoints, l.ComponentName, phase + "_IN")))
                    .OrderBy(p => p.HoleCenter.X)
                    .ToList();
                ApplyBranchDevicePortRules(loubaoInputs, rules);

                CollectorLayoutV2 collector = collectorPlanner.CreateLayout(phase, phaseIndex, fuseOut, loubaoInputs);
                plan.Collectors.Add(collector);

                ConnectionPort mainTap = collectorPlanner.CreateTap(
                    portRules,
                    phase,
                    phase + "_MainFeed_Tap",
                    fuseOut.HoleCenter.X,
                    collector,
                    rules.MainFeedCollectorFace);
                ApplyMainFeedCollectorTapRules(mainTap, settings, rules);
                ApplyCollectorTapHoleRules(mainTap, rules);

                BusbarV2 mainFeed = CreateBusbar(
                    "Busbar_" + phase + "_MainFeed_V2",
                    BusbarKind.MainFeed,
                    settings.MainFeedProfile,
                    fuseOut,
                    mainTap,
                    rules,
                    routePlanner,
                    topology);
                plan.Busbars.Add(mainFeed);

                for (int i = 0; i < loubaoInputs.Count; i++)
                {
                    ConnectionPort branchTap = collectorPlanner.CreateTap(
                        portRules,
                        phase,
                        phase + "_Branch_" + (i + 1) + "_Tap",
                        loubaoInputs[i].HoleCenter.X,
                        collector,
                        rules.BranchCollectorFace);
                    ApplyBranchCollectorTapRules(branchTap, rules);
                    ApplyCollectorTapHoleRules(branchTap, rules);

                    BusbarV2 branch = CreateBusbar(
                        "Busbar_" + phase + "_Branch_" + (i + 1) + "_V2",
                        BusbarKind.Branch,
                        settings.BranchProfile,
                        loubaoInputs[i],
                        branchTap,
                        rules,
                        routePlanner,
                        topology);
                    plan.Busbars.Add(branch);
                }

                plan.Busbars.Add(CreateCollectorBusbar(
                    phase,
                    collector,
                    rules,
                    settings,
                    topology,
                    settings.CollectorProfile,
                    rules.MainFeedCollectorFace));
            }

            AddNeutralCollectorAndBranches(
                plan,
                foundPoints,
                loubaos,
                phaseNames.Length,
                portRules,
                collectorPlanner,
                routePlanner,
                topology,
                rules,
                settings);

            return plan;
        }

        private static void AddNeutralCollectorAndBranches(
            BusbarPlanV2 plan,
            List<FoundPoint> foundPoints,
            List<LoubaoGroupV2> loubaos,
            int neutralPhaseIndex,
            ManualPortRuleProvider portRules,
            CollectorLayoutPlannerV2 collectorPlanner,
            BusbarRoutePlannerV2 routePlanner,
            ContactTopologyResolver topology,
            ManualBusbarRuleSet rules,
            BusbarSettings settings)
        {
            List<ConnectionPort> neutralInputs = CreateNeutralLoubaoInputs(foundPoints, loubaos, portRules);
            if (neutralInputs.Count == 0)
            {
                Console.WriteLine("No N_IN reference points were found. Skip neutral collector and neutral branch busbars.");
                return;
            }

            foreach (ConnectionPort neutralInput in neutralInputs)
                neutralInput.HoleDiameterMm = rules.NeutralBranchStartHoleDiameterMm;

            CollectorLayoutV2 neutralCollector = collectorPlanner.CreateLayout(
                NeutralConductorName,
                neutralPhaseIndex,
                null,
                neutralInputs,
                settings.NeutralBranchProfile);
            plan.Collectors.Add(neutralCollector);

            for (int i = 0; i < neutralInputs.Count; i++)
            {
                ConnectionPort branchTap = collectorPlanner.CreateTap(
                    portRules,
                    NeutralConductorName,
                    NeutralConductorName + "_Branch_" + (i + 1) + "_Tap",
                    neutralInputs[i].HoleCenter.X,
                    neutralCollector,
                    rules.BranchCollectorFace);
                branchTap.HoleDiameterMm = rules.NeutralCollectorTapHoleDiameterMm;

                BusbarV2 branch = CreateBusbar(
                    "Busbar_" + NeutralConductorName + "_Branch_" + (i + 1) + "_V2",
                    BusbarKind.Branch,
                    settings.NeutralBranchProfile,
                    neutralInputs[i],
                    branchTap,
                    rules,
                    routePlanner,
                    topology);
                plan.Busbars.Add(branch);
            }

            plan.Busbars.Add(CreateCollectorBusbar(
                NeutralConductorName,
                neutralCollector,
                rules,
                settings,
                topology,
                settings.NeutralCollectorProfile,
                rules.BranchCollectorFace));
        }

        private static List<ConnectionPort> CreateNeutralLoubaoInputs(
            List<FoundPoint> foundPoints,
            List<LoubaoGroupV2> loubaos,
            ManualPortRuleProvider portRules)
        {
            List<ConnectionPort> ports = new List<ConnectionPort>();
            List<string> missingComponents = new List<string>();

            for (int i = 0; i < loubaos.Count; i++)
            {
                FoundPoint neutralPoint = foundPoints.FirstOrDefault(p =>
                    SameText(p.ComponentName, loubaos[i].ComponentName) &&
                    SameText(p.PointName, NeutralConductorName + "_IN"));
                if (neutralPoint == null)
                {
                    missingComponents.Add(loubaos[i].ComponentName);
                    continue;
                }

                ports.Add(portRules.CreateLoubaoInPort(NeutralConductorName, i + 1, neutralPoint));
            }

            if (ports.Count > 0 && missingComponents.Count > 0)
            {
                throw new Exception(
                    "Neutral planning found partial N_IN reference points. Missing N_IN in: " +
                    string.Join(", ", missingComponents.ToArray()));
            }

            return ports
                .OrderBy(p => p.HoleCenter.X)
                .ToList();
        }

        private static BusbarV2 CreateCollectorBusbar(
            string phase,
            CollectorLayoutV2 collector,
            ManualBusbarRuleSet rules,
            BusbarSettings settings,
            ContactTopologyResolver topology,
            BusbarProfile profile,
            ContactFace collectorFace)
        {
            ConnectionPort start = CreateCollectorEndPort(
                phase,
                phase + "_Collector_Start",
                new Point3(collector.StartX, collector.Center.Y, collector.Center.Z),
                collectorFace,
                -1);

            ConnectionPort end = CreateCollectorEndPort(
                phase,
                phase + "_Collector_End",
                new Point3(collector.EndX, collector.Center.Y, collector.Center.Z),
                collectorFace,
                1);

            BusbarV2 busbar = new BusbarV2
            {
                Name = "Busbar_" + phase + "_Collector_V2",
                Kind = BusbarKind.Collector,
                Profile = profile,
                StartPort = start,
                EndPort = end,
                Routing = new BusbarRoutingOptions
                {
                    AxisOrder = rules.RouteAxisOrder,
                    TransitionPolicy = rules.TransitionPolicy
                },
                SheetMetal = SheetMetalOptions.FromRules(rules)
            };

            busbar.LogicalCenterline = new List<Point3>
            {
                start.HoleCenter,
                end.HoleCenter
            };
            busbar.SheetMetalSketchLine = topology.CreateSheetMetalSketchLine(busbar);
            busbar.MountingPorts = collector.TapPorts
                .Where(p => p.HoleDiameterMm > 0.0)
                .Select(CloneConnectionPort)
                .ToList();
            return busbar;
        }

        private static ConnectionPort CreateCollectorEndPort(string phase, string name, Point3 point, ContactFace face, int leadSign)
        {
            return new ConnectionPort
            {
                Name = name,
                ComponentName = "Collector_" + phase,
                Kind = PortKind.CollectorTap,
                HoleCenter = point,
                RequiredFace = face,
                PreferredLeadAxis = AxisDirection.X,
                PreferredLeadSign = leadSign,
                EndMarginMm = 0.0,
                HoleDiameterMm = 0.0
            };
        }

        private static void ApplyMainFeedCollectorTapRules(ConnectionPort tap, BusbarSettings settings, ManualBusbarRuleSet rules)
        {
            tap.EndMarginMm = settings.CollectorWidthMm * rules.MainFeedCollectorEndMarginRatio;
            tap.HoleDiameterMm = rules.MainFeedCollectorHoleDiameterMm;
        }

        private static void ApplyBranchDevicePortRules(List<ConnectionPort> ports, ManualBusbarRuleSet rules)
        {
            if (ports == null)
                return;

            foreach (ConnectionPort port in ports)
                port.HoleDiameterMm = rules.BranchStartHoleDiameterMm;
        }

        private static void ApplyBranchCollectorTapRules(ConnectionPort tap, ManualBusbarRuleSet rules)
        {
            tap.HoleDiameterMm = rules.BranchCollectorHoleDiameterMm;
        }

        private static void ApplyCollectorTapHoleRules(ConnectionPort tap, ManualBusbarRuleSet rules)
        {
            tap.HoleDiameterMm = rules.CollectorTapHoleDiameterMm;
        }

        private static BusbarV2 CreateBusbar(
            string name,
            BusbarKind kind,
            BusbarProfile profile,
            ConnectionPort start,
            ConnectionPort end,
            ManualBusbarRuleSet rules,
            BusbarRoutePlannerV2 routePlanner,
            ContactTopologyResolver topology)
        {
            BusbarV2 busbar = new BusbarV2
            {
                Name = name,
                Kind = kind,
                Profile = profile,
                StartPort = start,
                EndPort = end,
                Routing = new BusbarRoutingOptions
                {
                    AxisOrder = rules.RouteAxisOrder,
                    TransitionPolicy = rules.TransitionPolicy
                },
                SheetMetal = SheetMetalOptions.FromRules(rules)
            };

            busbar.LogicalCenterline = routePlanner.CreateRoute(kind, profile, start, end, rules.RouteAxisOrder);
            busbar.SheetMetalSketchLine = topology.CreateSheetMetalSketchLine(busbar);
            AddMountingPortIfNeeded(busbar, start);
            AddMountingPortIfNeeded(busbar, end);
            return busbar;
        }

        private static void AddMountingPortIfNeeded(BusbarV2 busbar, ConnectionPort port)
        {
            if (busbar == null || port == null || port.HoleDiameterMm <= 0.0)
                return;

            busbar.MountingPorts.Add(CloneConnectionPort(port));
        }

        private static ConnectionPort CloneConnectionPort(ConnectionPort port)
        {
            return new ConnectionPort
            {
                Name = port.Name,
                ComponentName = port.ComponentName,
                Kind = port.Kind,
                HoleCenter = port.HoleCenter,
                RequiredFace = port.RequiredFace,
                PreferredLeadAxis = port.PreferredLeadAxis,
                PreferredLeadSign = port.PreferredLeadSign,
                EndMarginMm = port.EndMarginMm,
                HoleDiameterMm = port.HoleDiameterMm
            };
        }

        private static BusbarSettings CreateDemoSettings()
        {
            return new BusbarSettings
            {
                MainFeedWidthMm = 60.0,
                MainFeedThicknessMm = 6.0,
                CollectorWidthMm = 80.0,
                CollectorThicknessMm = 6.0,
                BranchWidthMm = 40.0,
                BranchThicknessMm = 4.0,
                NeutralCollectorWidthMm = 60.0,
                NeutralCollectorThicknessMm = 6.0,
                NeutralBranchWidthMm = 40.0,
                NeutralBranchThicknessMm = 4.0,
                CollectorPhaseSpacingMm = 60.0,
                CollectorTopClearanceYMm = 240.0,
                CollectorOffsetFromLoubaoInZMm = 120.0,
                CollectorNegativeXExtendMm = 50.0,
                MainCollectorFrontClearanceMm = 200,//转接排第二段的前端间隙，避免碰到母线槽的前端
                MainCollectorLapDepthRatio = 0.5
            };
        }

        private static FoundPoint DemoFoundPoint(string componentName, string pointName, double xMm, double yMm, double zMm)
        {
            return new FoundPoint
            {
                ComponentName = componentName,
                PointName = pointName,
                Position = new Point3(Mm(xMm), Mm(yMm), Mm(zMm))
            };
        }

        private static void PrintCollector(CollectorLayoutV2 collector)
        {
            Console.WriteLine("Collector " + collector.Phase + ":");
            Console.WriteLine("  direction=" + collector.Direction);
            Console.WriteLine("  center=" + collector.Center.ToMillimeterText());
            Console.WriteLine("  startX=" + ToMm(collector.StartX).ToString("F3") + " mm, endX=" + ToMm(collector.EndX).ToString("F3") + " mm");
            Console.WriteLine();
        }

        private static void PrintBusbar(BusbarV2 busbar, ContactTopologyResolver topology)
        {
            Console.WriteLine("Route " + busbar.Name + " [" + busbar.Kind + "] " + busbar.Profile.Label + "mm");
            Console.WriteLine("  Start: " + busbar.StartPort);
            Console.WriteLine("  End:   " + busbar.EndPort);
            Console.WriteLine("  Face topology: " + topology.Resolve(busbar));
            Console.WriteLine("  SheetMetal: width=" + busbar.SheetMetal.WidthMode + ", R=" + busbar.SheetMetal.BendRadiusMm.ToString("0.###") + "mm, K=" + busbar.SheetMetal.KFactor.ToString("0.###"));
            Console.WriteLine("  Mounting holes: " + (busbar.MountingPorts == null ? 0 : busbar.MountingPorts.Count));
            if (busbar.MountingPorts != null)
            {
                for (int i = 0; i < busbar.MountingPorts.Count; i++)
                    Console.WriteLine("    H" + (i + 1) + " " + busbar.MountingPorts[i].Name + ", dia=" + busbar.MountingPorts[i].HoleDiameterMm.ToString("0.###") + "mm, " + busbar.MountingPorts[i].HoleCenter.ToMillimeterText());
            }

            Console.WriteLine("  Hole-center route:");
            for (int i = 0; i < busbar.LogicalCenterline.Count; i++)
                Console.WriteLine("    P" + i + " " + busbar.LogicalCenterline[i].ToMillimeterText());

            Console.WriteLine("  Sheet-metal sketch line:");
            for (int i = 0; i < busbar.SheetMetalSketchLine.Count; i++)
                Console.WriteLine("    S" + i + " " + busbar.SheetMetalSketchLine[i].ToMillimeterText());

            Console.WriteLine();
        }

        private static double Mm(double value)
        {
            return value / 1000.0;
        }

        private static double ToMm(double value)
        {
            return value * 1000.0;
        }

        private static string FindFuseComponent(List<FoundPoint> foundPoints, string[] phaseNames)
        {
            var candidates = foundPoints
                .GroupBy(p => p.ComponentName)
                .Select(g => new
                {
                    ComponentName = g.Key,
                    OutCount = phaseNames.Count(phase => g.Any(p => SameText(p.PointName, phase + "_OUT"))),
                    InCount = phaseNames.Count(phase => g.Any(p => SameText(p.PointName, phase + "_IN"))),
                    NameScore = ScoreNameHint(g.Key, FuseComponentNameHints) - ScoreNameHint(g.Key, LoubaoComponentNameHints)
                })
                .Where(x => x.OutCount == phaseNames.Length)
                .OrderByDescending(x => x.NameScore)
                .ThenByDescending(x => x.OutCount)
                .ThenByDescending(x => x.InCount)
                .ToList();

            var fuse = candidates.FirstOrDefault();
            if (fuse == null)
                throw new Exception("No fuse component was found for V2 planning.");

            return fuse.ComponentName;
        }

        private static List<LoubaoGroupV2> FindLoubaoGroups(List<FoundPoint> foundPoints, string[] phaseNames, string fuseComponent)
        {
            return foundPoints
                .GroupBy(p => p.ComponentName)
                .Where(g => !SameText(g.Key, fuseComponent))
                .Where(g => phaseNames.All(phase => g.Any(p => SameText(p.PointName, phase + "_IN"))))
                .Where(g => ScoreNameHint(g.Key, FuseComponentNameHints) <= ScoreNameHint(g.Key, LoubaoComponentNameHints))
                .Select(g => new LoubaoGroupV2
                {
                    ComponentName = g.Key,
                    CenterX = g.Where(p => p.PointName.EndsWith("_IN", StringComparison.OrdinalIgnoreCase)).Average(p => p.Position.X)
                })
                .OrderBy(g => g.CenterX)
                .ToList();
        }

        private static FoundPoint FindRequiredPoint(List<FoundPoint> points, string componentName, string pointName)
        {
            FoundPoint point = points.FirstOrDefault(p => SameText(p.ComponentName, componentName) && SameText(p.PointName, pointName));
            if (point == null)
                throw new Exception("Missing reference point: " + componentName + "." + pointName);

            return point;
        }

        private static int ScoreNameHint(string componentName, string[] hints)
        {
            if (string.IsNullOrWhiteSpace(componentName))
                return 0;

            int score = 0;
            foreach (string hint in hints)
            {
                if (!string.IsNullOrWhiteSpace(hint) &&
                    componentName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score++;
                }
            }

            return score;
        }

        private static bool SameText(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal class LoubaoGroupV2
    {
        public string ComponentName;
        public double CenterX;
    }
}
