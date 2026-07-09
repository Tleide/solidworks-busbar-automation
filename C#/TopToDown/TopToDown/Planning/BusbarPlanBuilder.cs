using System;
using System.Collections.Generic;
using System.Linq;

namespace SwFeatureDebug
{
    internal static class BusbarPlanBuilder
    {
        private const string NeutralConductorName = "N";
        private static readonly string[] FuseComponentNameHints = { "fuse", "HR6", "rong", "knife", "isolator" };
        private static readonly string[] LoubaoComponentNameHints = { "loubao", "PGM", "leakage", "breaker" };
        public static BusbarPlan BuildPlanFromScannedAssembly(List<FoundPoint> foundPoints, string[] phaseNames, BusbarSettings settings)
        {
            if (foundPoints == null || foundPoints.Count == 0)
                throw new Exception("No reference points were scanned from the active assembly.");

            ManualBusbarRuleSet rules = ManualBusbarRuleSet.CreateDefault(CabinetTopologyKind.TypicalDesign);
            ManualPortRuleProvider portRules = new ManualPortRuleProvider(rules);
            CollectorLayoutPlanner collectorPlanner = new CollectorLayoutPlanner(rules, settings);
            BusbarRoutePlanner routePlanner = new BusbarRoutePlanner(settings);
            ContactTopologyResolver topology = new ContactTopologyResolver();
            BusbarOverlapHolePlanner overlapHolePlanner = new BusbarOverlapHolePlanner();

            string fuseComponent = FindFuseComponent(foundPoints, phaseNames);
            List<LoubaoGroup> loubaos = FindLoubaoGroups(foundPoints, phaseNames, fuseComponent);

            if (loubaos.Count == 0)
                throw new Exception("No loubao components were found for planning.");

            BusbarPlan plan = new BusbarPlan
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

                CollectorLayout collector = collectorPlanner.CreateLayout(phase, phaseIndex, fuseOut, loubaoInputs);
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

                Busbar mainFeed = CreateBusbar(
                    "Busbar_" + phase + "_MainFeed",
                    BusbarKind.MainFeed,
                    settings.MainFeedProfile,
                    fuseOut,
                    mainTap,
                    rules,
                    routePlanner,
                    topology);
                ApplyCollectorOverlapHoleRules(mainFeed, mainTap, collector, settings.CollectorProfile, overlapHolePlanner);
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
                    ApplyBranchCollectorTapRules(branchTap, settings.CollectorProfile, rules);
                    ApplyCollectorTapHoleRules(branchTap, rules);

                    Busbar branch = CreateBusbar(
                        "Busbar_" + phase + "_Branch_" + (i + 1),
                        BusbarKind.Branch,
                        settings.BranchProfile,
                        loubaoInputs[i],
                        branchTap,
                        rules,
                        routePlanner,
                        topology);
                    ApplyCollectorOverlapHoleRules(branch, branchTap, collector, settings.CollectorProfile, overlapHolePlanner);
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
            BusbarPlan plan,
            List<FoundPoint> foundPoints,
            List<LoubaoGroup> loubaos,
            int neutralPhaseIndex,
            ManualPortRuleProvider portRules,
            CollectorLayoutPlanner collectorPlanner,
            BusbarRoutePlanner routePlanner,
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

            BusbarOverlapHolePlanner overlapHolePlanner = new BusbarOverlapHolePlanner();

            CollectorLayout neutralCollector = collectorPlanner.CreateLayout(
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
                ApplyNeutralBranchCollectorTapRules(branchTap, settings.NeutralCollectorProfile, rules);

                Busbar branch = CreateBusbar(
                    "Busbar_" + NeutralConductorName + "_Branch_" + (i + 1),
                    BusbarKind.Branch,
                    settings.NeutralBranchProfile,
                    neutralInputs[i],
                    branchTap,
                    rules,
                    routePlanner,
                    topology);
                ApplyCollectorOverlapHoleRules(branch, branchTap, neutralCollector, settings.NeutralCollectorProfile, overlapHolePlanner);
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
            List<LoubaoGroup> loubaos,
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

        private static Busbar CreateCollectorBusbar(
            string phase,
            CollectorLayout collector,
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

            Busbar busbar = new Busbar
            {
                Name = "Busbar_" + phase + "_Collector",
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

        private static void ApplyCollectorOverlapHoleRules(
            Busbar connectedBusbar,
            ConnectionPort centerTap,
            CollectorLayout collector,
            BusbarProfile collectorProfile,
            BusbarOverlapHolePlanner overlapHolePlanner)
        {
            List<ConnectionPort> overlapPorts = overlapHolePlanner.CreateCollectorOverlapPorts(
                centerTap,
                connectedBusbar,
                collectorProfile,
                collector.Direction);

            ReplaceBusbarMountingPort(connectedBusbar, centerTap, overlapPorts);
            ReplaceCollectorTapPort(collector, centerTap, overlapPorts);
        }

        private static void ReplaceBusbarMountingPort(Busbar busbar, ConnectionPort centerPort, List<ConnectionPort> replacementPorts)
        {
            if (busbar.MountingPorts == null)
                busbar.MountingPorts = new List<ConnectionPort>();

            busbar.MountingPorts.RemoveAll(p => SameText(p.Name, centerPort.Name));
            busbar.MountingPorts.AddRange(replacementPorts.Select(CloneConnectionPort));
        }

        private static void ReplaceCollectorTapPort(CollectorLayout collector, ConnectionPort centerPort, List<ConnectionPort> replacementPorts)
        {
            int insertIndex = collector.TapPorts.FindIndex(p => SameText(p.Name, centerPort.Name));
            if (insertIndex < 0)
                insertIndex = collector.TapPorts.Count;

            collector.TapPorts.RemoveAll(p => SameText(p.Name, centerPort.Name));
            collector.TapPorts.InsertRange(insertIndex, replacementPorts.Select(CloneConnectionPort).ToList());
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

        private static void ApplyBranchCollectorTapRules(ConnectionPort tap, BusbarProfile collectorProfile, ManualBusbarRuleSet rules)
        {
            tap.EndMarginMm = collectorProfile.WidthMm / 2.0;
            tap.HoleDiameterMm = rules.BranchCollectorHoleDiameterMm;
        }

        private static void ApplyNeutralBranchCollectorTapRules(ConnectionPort tap, BusbarProfile collectorProfile, ManualBusbarRuleSet rules)
        {
            tap.EndMarginMm = collectorProfile.WidthMm / 2.0;
            tap.HoleDiameterMm = rules.NeutralCollectorTapHoleDiameterMm;
        }

        private static void ApplyCollectorTapHoleRules(ConnectionPort tap, ManualBusbarRuleSet rules)
        {
            tap.HoleDiameterMm = rules.CollectorTapHoleDiameterMm;
        }

        private static Busbar CreateBusbar(
            string name,
            BusbarKind kind,
            BusbarProfile profile,
            ConnectionPort start,
            ConnectionPort end,
            ManualBusbarRuleSet rules,
            BusbarRoutePlanner routePlanner,
            ContactTopologyResolver topology)
        {
            Busbar busbar = new Busbar
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

        private static void AddMountingPortIfNeeded(Busbar busbar, ConnectionPort port)
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
                throw new Exception("No fuse component was found for planning.");

            return fuse.ComponentName;
        }

        private static List<LoubaoGroup> FindLoubaoGroups(List<FoundPoint> foundPoints, string[] phaseNames, string fuseComponent)
        {
            return foundPoints
                .GroupBy(p => p.ComponentName)
                .Where(g => !SameText(g.Key, fuseComponent))
                .Where(g => phaseNames.All(phase => g.Any(p => SameText(p.PointName, phase + "_IN"))))
                .Where(g => ScoreNameHint(g.Key, FuseComponentNameHints) <= ScoreNameHint(g.Key, LoubaoComponentNameHints))
                .Select(g => new LoubaoGroup
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
}
