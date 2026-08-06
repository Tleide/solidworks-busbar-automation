using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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

            ManualBusbarRuleSet rules = ManualBusbarRuleSet.CreateDefault(
                CabinetTopologyKind.TypicalDesign,
                settings);
            ManualPortRuleProvider portRules = new ManualPortRuleProvider(rules);
            CollectorLayoutPlanner collectorPlanner = new CollectorLayoutPlanner(rules, settings);
            BusbarRoutePlanner routePlanner = new BusbarRoutePlanner(settings);
            ContactTopologyResolver topology = new ContactTopologyResolver();
            BusbarOverlapHolePlanner overlapHolePlanner = new BusbarOverlapHolePlanner();

            string fuseComponent = FindFuseComponent(foundPoints, phaseNames);
            List<LoubaoGroup> loubaos = FindLoubaoGroups(foundPoints, phaseNames, fuseComponent, settings);

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

                List<BusbarProfile> branchProfiles = loubaoInputs
                    .Select(input => FindLoubaoGroup(loubaos, input.ComponentName).BranchProfile)
                    .ToList();
                CollectorLayout collector = collectorPlanner.CreateLayout(phase, phaseIndex, fuseOut, loubaoInputs, branchProfiles);
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
                    settings,
                    routePlanner,
                    topology,
                    BranchLegRole.Single);
                ApplyCollectorOverlapHoleRules(mainFeed, mainTap, collector, settings.CollectorProfile, overlapHolePlanner, true);
                plan.Busbars.Add(mainFeed);

                for (int i = 0; i < loubaoInputs.Count; i++)
                {
                    AddPlannedBranchBusbars(
                        plan,
                        portRules,
                        collectorPlanner,
                        routePlanner,
                        topology,
                        overlapHolePlanner,
                        rules,
                        settings,
                        phase,
                        i + 1,
                        loubaoInputs[i],
                        collector,
                        branchProfiles[i],
                        settings.CollectorProfile,
                        settings.PhaseBranchArrangementOverride ??
                            FindLoubaoGroup(loubaos, loubaoInputs[i].ComponentName).BranchArrangement,
                        false);
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

            FastenerPlanBuilder.BuildCollectorJoints(plan, settings);

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
                neutralInputs
                    .Select(input => FindLoubaoGroup(loubaos, input.ComponentName).NeutralBranchProfile)
                    .ToList());
            plan.Collectors.Add(neutralCollector);

            for (int i = 0; i < neutralInputs.Count; i++)
            {
                AddPlannedBranchBusbars(
                    plan,
                    portRules,
                    collectorPlanner,
                    routePlanner,
                    topology,
                    overlapHolePlanner,
                    rules,
                    settings,
                    NeutralConductorName,
                    i + 1,
                    neutralInputs[i],
                    neutralCollector,
                    FindLoubaoGroup(loubaos, neutralInputs[i].ComponentName).NeutralBranchProfile,
                    settings.NeutralCollectorProfile,
                    settings.NeutralBranchArrangementOverride ??
                        FindLoubaoGroup(loubaos, neutralInputs[i].ComponentName).NeutralBranchArrangement,
                    true);
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
                SheetMetal = SheetMetalOptions.FromRules(rules, settings, BusbarKind.Collector)
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
            BusbarOverlapHolePlanner overlapHolePlanner,
            bool updateCollector)
        {
            List<ConnectionPort> overlapPorts = overlapHolePlanner.CreateCollectorOverlapPorts(
                centerTap,
                connectedBusbar,
                collectorProfile,
                collector.Direction);

            ReplaceBusbarMountingPort(connectedBusbar, centerTap, overlapPorts);
            if (updateCollector)
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

        private static void AddPlannedBranchBusbars(
            BusbarPlan plan,
            ManualPortRuleProvider portRules,
            CollectorLayoutPlanner collectorPlanner,
            BusbarRoutePlanner routePlanner,
            ContactTopologyResolver topology,
            BusbarOverlapHolePlanner overlapHolePlanner,
            ManualBusbarRuleSet rules,
            BusbarSettings settings,
            string phase,
            int branchIndex,
            ConnectionPort devicePort,
            CollectorLayout collector,
            BusbarProfile branchProfile,
            BusbarProfile collectorProfile,
            BranchArrangement arrangement,
            bool useNeutralTapRules)
        {
            if (arrangement != BranchArrangement.DoubleClamp)
            {
                ConnectionPort tap = collectorPlanner.CreateTap(
                    portRules,
                    phase,
                    phase + "_Branch_" + branchIndex + "_Tap",
                    devicePort.HoleCenter.X,
                    collector,
                    rules.BranchCollectorFace);
                ApplyBranchCollectorTapRules(tap, collectorProfile, rules, useNeutralTapRules);

                Busbar branch = CreateBusbar(
                    "Busbar_" + phase + "_Branch_" + branchIndex,
                    BusbarKind.Branch,
                    branchProfile,
                    devicePort,
                    tap,
                    rules,
                    settings,
                    routePlanner,
                    topology,
                    BranchLegRole.Single);
                ApplyCollectorOverlapHoleRules(branch, tap, collector, collectorProfile, overlapHolePlanner, true);
                plan.Busbars.Add(branch);
                return;
            }

            ConnectionPort lowerTap = collectorPlanner.CreateTap(
                portRules,
                phase,
                phase + "_Branch_" + branchIndex + "_Lower_Tap",
                devicePort.HoleCenter.X,
                collector,
                ContactFace.Lower,
                -collectorProfile.ThicknessMeters / 2.0);
            ApplyBranchCollectorTapRules(lowerTap, collectorProfile, rules, useNeutralTapRules);
            // In the current sheet-metal backend, the collector path is its upper surface and the branch path grows toward Y+.
            double collectorTopY = collector.Center.Y;
            ConnectionPort lowerRouteEnd = CreateRouteCenterlinePort(
                lowerTap,
                collectorTopY - collectorProfile.ThicknessMeters - branchProfile.ThicknessMeters);

            Busbar lowerBranch = CreateBusbar(
                "Busbar_" + phase + "_Branch_" + branchIndex + "_Lower",
                BusbarKind.Branch,
                branchProfile,
                devicePort,
                lowerRouteEnd,
                rules,
                settings,
                routePlanner,
                topology,
                BranchLegRole.Lower,
                BranchRouteMode.Standard,
                ThicknessTransitionPolicy.None);
            ApplyCollectorOverlapHoleRules(lowerBranch, lowerTap, collector, collectorProfile, overlapHolePlanner, true);
            plan.Busbars.Add(lowerBranch);

            ConnectionPort upperTap = collectorPlanner.CreateTap(
                portRules,
                phase,
                phase + "_Branch_" + branchIndex + "_Upper_Tap",
                devicePort.HoleCenter.X,
                collector,
                ContactFace.Upper,
                collectorProfile.ThicknessMeters / 2.0);
            ApplyBranchCollectorTapRules(upperTap, collectorProfile, rules, useNeutralTapRules);
            ConnectionPort upperRouteEnd = CreateRouteCenterlinePort(
                upperTap,
                collectorTopY);

            double upperStartZOffsetMm = settings.DoubleClampUpperStartZSign * branchProfile.ThicknessMm;
            ConnectionPort upperStart = CloneConnectionPort(devicePort);
            upperStart.Name = devicePort.Name + "_Upper_Start";
            upperStart.HoleCenter = new Point3(
                devicePort.HoleCenter.X,
                devicePort.HoleCenter.Y,
                devicePort.HoleCenter.Z + Mm(upperStartZOffsetMm));

            // The upper branch is physically offset as a whole. Its route starts at the offset point, not at the device face.
            Busbar upperBranch = CreateBusbar(
                "Busbar_" + phase + "_Branch_" + branchIndex + "_Upper",
                BusbarKind.Branch,
                branchProfile,
                upperStart,
                upperRouteEnd,
                rules,
                settings,
                routePlanner,
                topology,
                BranchLegRole.Upper,
                BranchRouteMode.DoubleClampOuterAvoidance);
            ApplyCollectorOverlapHoleRules(upperBranch, upperTap, collector, collectorProfile, overlapHolePlanner, false);
            // The lower branch owns the collector through-hole pair; remove the upper temporary center tap.
            collector.TapPorts.RemoveAll(p => SameText(p.Name, upperTap.Name));
            plan.Busbars.Add(upperBranch);
        }
        // The route is modeled from the backend-defined branch path surface; mounting holes remain on the physical collector contact face.
        private static ConnectionPort CreateRouteCenterlinePort(ConnectionPort contactPort, double routeCenterY)
        {
            ConnectionPort routePort = CloneConnectionPort(contactPort);
            routePort.HoleCenter = new Point3(contactPort.HoleCenter.X, routeCenterY, contactPort.HoleCenter.Z);
            return routePort;
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

        private static void ApplyBranchCollectorTapRules(
            ConnectionPort tap,
            BusbarProfile collectorProfile,
            ManualBusbarRuleSet rules,
            bool useNeutralTapRules)
        {
            if (useNeutralTapRules)
            {
                ApplyNeutralBranchCollectorTapRules(tap, collectorProfile, rules);
                return;
            }

            tap.EndMarginMm = collectorProfile.WidthMm / 2.0;
            tap.HoleDiameterMm = rules.BranchCollectorHoleDiameterMm;
            ApplyCollectorTapHoleRules(tap, rules);
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
            BusbarSettings settings,
            BusbarRoutePlanner routePlanner,
            ContactTopologyResolver topology,
            BranchLegRole branchLegRole,
            BranchRouteMode branchRouteMode = BranchRouteMode.Standard,
            ThicknessTransitionPolicy? transitionPolicy = null)
        {
            Busbar busbar = new Busbar
            {
                Name = name,
                Kind = kind,
                BranchLegRole = branchLegRole,
                Profile = profile,
                StartPort = start,
                EndPort = end,
                Routing = new BusbarRoutingOptions
                {
                    AxisOrder = rules.RouteAxisOrder,
                    TransitionPolicy = transitionPolicy ?? rules.TransitionPolicy,
                    BranchRouteMode = branchRouteMode
                },
                SheetMetal = SheetMetalOptions.FromRules(rules, settings, kind)
            };

            busbar.LogicalCenterline = routePlanner.CreateRoute(kind, profile, start, end, busbar.Routing);
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

        internal static string FindFuseComponent(List<FoundPoint> foundPoints, string[] phaseNames)
        {
            var candidates = foundPoints
                .GroupBy(p => p.ComponentName)
                .Select(g => new
                {
                    ComponentName = g.Key,
                    OutCount = phaseNames.Count(phase => g.Any(p => SameText(p.PointName, phase + "_OUT"))),
                    NameScore = ScoreNameHint(g.Key, FuseComponentNameHints) -
                        ScoreNameHint(g.Key, LoubaoComponentNameHints)
                })
                .Where(x => x.OutCount == phaseNames.Length && x.NameScore > 0)
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
                    string.Join(", ", candidates.Select(candidate => candidate.ComponentName).ToArray()) +
                    ". Keep exactly one fuse component or make the component contract explicit.");
            }

            return candidates[0].ComponentName;
        }

        private static List<LoubaoGroup> FindLoubaoGroups(
            List<FoundPoint> foundPoints,
            string[] phaseNames,
            string fuseComponent,
            BusbarSettings settings)
        {
            return foundPoints
                .GroupBy(p => p.ComponentName)
                .Where(g => !SameText(g.Key, fuseComponent))
                .Where(g => phaseNames.All(phase => g.Any(p => SameText(p.PointName, phase + "_IN"))))
                .Where(g => ScoreNameHint(g.Key, FuseComponentNameHints) <= ScoreNameHint(g.Key, LoubaoComponentNameHints))
                .Select(group => CreateLoubaoGroup(
                    group,
                    settings.PhaseBranchRules,
                    settings.NeutralBranchRules))
                .OrderBy(g => g.CenterX)
                .ToList();
        }

        private static LoubaoGroup CreateLoubaoGroup(
            IGrouping<string, FoundPoint> componentPoints,
            List<BranchBusbarRule> phaseRules,
            List<BranchBusbarRule> neutralRules)
        {
            BranchBusbarRule phaseRule = ResolveBranchRule(
                componentPoints.Key,
                phaseRules,
                "phase branch");
            BranchBusbarRule neutralRule = ResolveBranchRule(
                componentPoints.Key,
                neutralRules,
                "neutral branch");
            return new LoubaoGroup
            {
                ComponentName = componentPoints.Key,
                CenterX = componentPoints
                    .Where(point => point.PointName.EndsWith("_IN", StringComparison.OrdinalIgnoreCase))
                    .Average(point => point.Position.X),
                RatedCurrentA = phaseRule.RatedCurrentA,
                BranchProfile = phaseRule.Profile,
                BranchArrangement = phaseRule.Arrangement,
                NeutralBranchProfile = neutralRule.Profile,
                NeutralBranchArrangement = neutralRule.Arrangement
            };
        }

        private static LoubaoGroup FindLoubaoGroup(List<LoubaoGroup> loubaos, string componentName)
        {
            LoubaoGroup loubao = loubaos.FirstOrDefault(group => SameText(group.ComponentName, componentName));
            if (loubao == null)
                throw new Exception("No loubao selection rule was found for component: " + componentName);

            return loubao;
        }

        private static BranchBusbarRule ResolveBranchRule(
            string componentName,
            List<BranchBusbarRule> rules,
            string ruleSetName)
        {
            int ratedCurrentA = ParseRatedCurrentA(componentName, rules);
            BranchBusbarRule rule = rules.FirstOrDefault(candidate => candidate.RatedCurrentA == ratedCurrentA);
            if (rule == null || rule.Profile == null)
                throw new Exception("No " + ruleSetName + " busbar rule was found for " + ratedCurrentA + "A: " + componentName);

            Console.WriteLine(
                "Loubao " + ruleSetName + " rule [" + componentName + "]: " +
                ratedCurrentA + "A => " + rule.Profile.Label + "mm, " + rule.Arrangement);
            return rule;
        }

        private static int ParseRatedCurrentA(string componentName, List<BranchBusbarRule> rules)
        {
            if (string.IsNullOrWhiteSpace(componentName))
                throw new Exception("Cannot parse rated current from an empty loubao component name.");

            if (rules == null || rules.Count == 0)
                throw new Exception("Branch busbar rules are not configured.");

            List<int> matchedCurrents = rules
                .Select(rule => rule.RatedCurrentA)
                .Distinct()
                .Where(current => Regex.IsMatch(
                    componentName,
                    @"(?<![A-Za-z0-9])" + current + @"(?:A)?(?![A-Za-z0-9])",
                    RegexOptions.IgnoreCase))
                .ToList();

            if (matchedCurrents.Count != 1)
            {
                throw new Exception(
                    "Loubao component name must contain exactly one configured rated-current token. " +
                    "Component=" + componentName + ", supported=" +
                    string.Join("A, ", rules.Select(rule => rule.RatedCurrentA).Distinct().OrderBy(current => current).ToArray()) +
                    "A.");
            }

            return matchedCurrents[0];
        }

        private static FoundPoint FindRequiredPoint(List<FoundPoint> points, string componentName, string pointName)
        {
            List<FoundPoint> matches = points
                .Where(point => SameText(point.ComponentName, componentName) && SameText(point.PointName, pointName))
                .ToList();
            if (matches.Count == 0)
                throw new Exception("Missing reference point: " + componentName + "." + pointName);

            if (matches.Count > 1)
                throw new Exception("Duplicate reference point: " + componentName + "." + pointName);

            return matches[0];
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
