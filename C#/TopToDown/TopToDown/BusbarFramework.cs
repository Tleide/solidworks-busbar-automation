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
                MainFeedCollectorHoleDiameterMm = 13.0
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

        public CollectorLayoutPlannerV2(ManualBusbarRuleSet rules, BusbarSettings settings)
        {
            _rules = rules;
            _settings = settings;
        }

        public CollectorLayoutV2 CreateLayout(string phase, int phaseIndex, ConnectionPort fuseOut, List<ConnectionPort> loubaoInputs)
        {
            double baseY = loubaoInputs.Max(p => p.HoleCenter.Y) + _settings.CollectorTopClearanceY;
            double collectorY = baseY - phaseIndex * _settings.CollectorPhaseSpacing;
            double collectorZ = loubaoInputs.Average(p => p.HoleCenter.Z) + _settings.CollectorOffsetFromLoubaoInZ;
            double minX = Math.Min(fuseOut.HoleCenter.X, loubaoInputs.Min(p => p.HoleCenter.X));
            double maxX = Math.Max(fuseOut.HoleCenter.X, loubaoInputs.Max(p => p.HoleCenter.X));
            double endExtend = _settings.CollectorProfile.Width / 2.0;

            return new CollectorLayoutV2
            {
                Phase = phase,
                Direction = AxisDirection.X,
                Center = new Point3((minX + maxX) / 2.0, collectorY, collectorZ),
                StartX = minX - endExtend,
                EndX = maxX + endExtend
            };
        }

        public ConnectionPort CreateTap(ManualPortRuleProvider ports, string phase, string name, double x, CollectorLayoutV2 collector, ContactFace face)
        {
            Point3 tapPoint = new Point3(x, collector.Center.Y, collector.Center.Z);
            return ports.CreateCollectorTapPort(phase, name, tapPoint, face);
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
            return sketchLine;
        }

        public bool IsSameSide(ConnectionPort start, ConnectionPort end)
        {
            return start.RequiredFace == end.RequiredFace;
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
    }

    internal static class BusbarPlanningDemoV2
    {
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

                plan.Busbars.Add(CreateBusbar(
                    "Busbar_" + phase + "_MainFeed_V2",
                    BusbarKind.MainFeed,
                    settings.MainFeedProfile,
                    fuseOut,
                    mainTap,
                    rules,
                    routePlanner,
                    topology));

                for (int i = 0; i < loubaoInputs.Count; i++)
                {
                    ConnectionPort branchTap = collectorPlanner.CreateTap(
                        portRules,
                        phase,
                        phase + "_Branch_" + (i + 1) + "_Tap",
                        loubaoInputs[i].HoleCenter.X,
                        collector,
                        rules.BranchCollectorFace);

                    plan.Busbars.Add(CreateBusbar(
                        "Busbar_" + phase + "_Branch_" + (i + 1) + "_V2",
                        BusbarKind.Branch,
                        settings.BranchProfile,
                        loubaoInputs[i],
                        branchTap,
                        rules,
                        routePlanner,
                        topology));
                }
            }

            return plan;
        }

        private static void ApplyMainFeedCollectorTapRules(ConnectionPort tap, BusbarSettings settings, ManualBusbarRuleSet rules)
        {
            tap.EndMarginMm = settings.CollectorWidthMm * rules.MainFeedCollectorEndMarginRatio;
            tap.HoleDiameterMm = rules.MainFeedCollectorHoleDiameterMm;
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
            return busbar;
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
                CollectorPhaseSpacingMm = 60.0,
                CollectorTopClearanceYMm = 180.0,
                CollectorOffsetFromLoubaoInZMm = 120.0,
                MainCollectorFrontClearanceMm = 0.0,
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
            Console.WriteLine("  Face topology: " + (topology.IsSameSide(busbar.StartPort, busbar.EndPort) ? "SameSide" : "DifferentSide"));
            Console.WriteLine("  SheetMetal: width=" + busbar.SheetMetal.WidthMode + ", R=" + busbar.SheetMetal.BendRadiusMm.ToString("0.###") + "mm, K=" + busbar.SheetMetal.KFactor.ToString("0.###"));

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
