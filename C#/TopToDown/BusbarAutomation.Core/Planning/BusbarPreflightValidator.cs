using System;
using System.Collections.Generic;
using System.Linq;

using BusbarAutomation.Core.Domain;
using BusbarAutomation.Core.Rules;

namespace BusbarAutomation.Core.Planning
{
    internal enum PreflightSeverity
    {
        Info,
        Warning,
        Error
    }

    internal class PreflightMessage
    {
        public PreflightSeverity Severity;
        public string Scope;
        public string Message;
    }

    internal class BusbarPreflightReport
    {
        public readonly List<PreflightMessage> Messages = new List<PreflightMessage>();
        public string Title;
        public string FailureResultMessage;

        public BusbarPreflightReport(
            string title = "Busbar preflight report",
            string failureResultMessage = "Sheet-metal generation is blocked.")
        {
            Title = title;
            FailureResultMessage = failureResultMessage;
        }

        public int ErrorCount { get { return Messages.Count(message => message.Severity == PreflightSeverity.Error); } }
        public int WarningCount { get { return Messages.Count(message => message.Severity == PreflightSeverity.Warning); } }
        public int InfoCount { get { return Messages.Count(message => message.Severity == PreflightSeverity.Info); } }
        public bool HasErrors { get { return ErrorCount > 0; } }

        public void Add(PreflightSeverity severity, string scope, string message)
        {
            Messages.Add(new PreflightMessage
            {
                Severity = severity,
                Scope = scope ?? "General",
                Message = message ?? string.Empty
            });
        }

        public void AddInfo(string scope, string message)
        {
            Add(PreflightSeverity.Info, scope, message);
        }

        public void AddWarning(string scope, string message)
        {
            Add(PreflightSeverity.Warning, scope, message);
        }

        public void AddError(string scope, string message)
        {
            Add(PreflightSeverity.Error, scope, message);
        }

    }

    // Validates planning output against independent configuration and geometry contracts.
    // It deliberately does not call a second route planner or make SolidWorks model changes.
    internal static class BusbarPreflightValidator
    {
        private const double CoordinateToleranceMm = 0.01;

        public static BusbarPreflightReport ValidateConfiguration(BusbarSettings settings)
        {
            BusbarPreflightReport report = new BusbarPreflightReport();

            if (settings == null)
            {
                report.AddError("Configuration", "Busbar settings are not configured.");
                return report;
            }

            ValidateProfile(report, "Configuration/MainFeed", settings.MainFeedProfile);
            ValidateProfile(report, "Configuration/ABCCollector", settings.CollectorProfile);
            ValidateProfile(report, "Configuration/NCollector", settings.NeutralCollectorProfile);
            ValidateRuleTable(report, "Configuration/ABCBranchRules", settings.PhaseBranchRules);
            ValidateRuleTable(report, "Configuration/NBranchRules", settings.NeutralBranchRules);
            ValidateFastenerConfiguration(report, settings);
            ValidateSheetMetalConfiguration(report, settings);

            if (!IsDefinedArrangement(settings.PhaseBranchArrangementOverride))
                report.AddError("Configuration/ABCBranchRules", "The ABC arrangement override is invalid.");

            if (!IsDefinedArrangement(settings.NeutralBranchArrangementOverride))
                report.AddError("Configuration/NBranchRules", "The neutral arrangement override is invalid.");

            if (settings.CollectorPhaseSpacingMm <= 0.0)
                report.AddError("Configuration/Collectors", "Collector phase spacing must be greater than zero.");

            if (settings.CollectorTopClearanceYMm <= 0.0)
                report.AddError("Configuration/Collectors", "Collector top clearance must be greater than zero.");

            ValidateCollectorNegativeXExtension(report, "A", settings.CollectorANegativeXExtendMm);
            ValidateCollectorNegativeXExtension(report, "B", settings.CollectorBNegativeXExtendMm);
            ValidateCollectorNegativeXExtension(report, "C", settings.CollectorCNegativeXExtendMm);
            ValidateCollectorNegativeXExtension(report, "N", settings.NeutralCollectorNegativeXExtendMm);

            if (!report.HasErrors)
                report.AddInfo("Configuration", "Profiles, selection tables, and collector layout parameters are valid.");

            return report;
        }

        private static void ValidateCollectorNegativeXExtension(
            BusbarPreflightReport report,
            string phase,
            double negativeXExtendMm)
        {
            if (negativeXExtendMm < 0.0)
                report.AddError(
                    "Configuration/Collectors/" + phase,
                    phase + " collector negative-X extension cannot be negative.");
        }

        public static BusbarPreflightReport ValidatePlan(
            BusbarManufacturingPlan plan,
            BusbarSettings settings,
            string[] phaseNames,
            BusbarOverlapRuleCatalog overlapRules)
        {
            BusbarPreflightReport report = new BusbarPreflightReport();

            if (plan == null)
            {
                report.AddError("Plan", "No busbar plan was produced.");
                return report;
            }

            if (settings == null)
            {
                report.AddError("Plan", "Busbar settings are not available for plan validation.");
                return report;
            }

            if (plan.Loubaos == null || plan.Loubaos.Count == 0)
                report.AddError("Plan", "The plan contains no loubao groups.");

            if (plan.Collectors == null || plan.Collectors.Count == 0)
                report.AddError("Plan", "The plan contains no collector layouts.");

            if (plan.Busbars == null || plan.Busbars.Count == 0)
                report.AddError("Plan", "The plan contains no busbars.");

            if (report.HasErrors)
                return report;

            ValidateLoubaoSelections(report, plan, settings);
            ValidateCollectors(report, plan, settings, phaseNames);
            ValidateBranchTopology(report, plan, settings, phaseNames);
            ValidateOverlapRulesAndPorts(report, plan, phaseNames, overlapRules);
            ValidateDoubleClampRoutes(report, plan, settings, phaseNames);
            ValidateFastenerJoints(report, plan);

            if (!report.HasErrors)
            {
                report.AddInfo(
                    "Plan",
                    "Planning contracts passed. SolidWorks entity and interference verification remains a post-generation check.");
            }

            return report;
        }

        public static BusbarPreflightReport CreatePlanningFailure(Exception exception)
        {
            BusbarPreflightReport report = new BusbarPreflightReport();
            report.AddError(
                "Planning",
                exception == null ? "The planner stopped without an error message." : exception.Message);
            return report;
        }

        private static void ValidateProfile(BusbarPreflightReport report, string scope, BusbarProfile profile)
        {
            if (profile == null)
            {
                report.AddError(scope, "Busbar profile is missing.");
                return;
            }

            if (profile.WidthMm <= 0.0 || profile.ThicknessMm <= 0.0)
            {
                report.AddError(
                    scope,
                    "Width and thickness must both be greater than zero. Current=" + FormatProfile(profile) + ".");
            }
        }

        private static void ValidateRuleTable(
            BusbarPreflightReport report,
            string scope,
            List<BranchBusbarRule> rules)
        {
            if (rules == null || rules.Count == 0)
            {
                report.AddError(scope, "At least one rated-current branch rule is required.");
                return;
            }

            foreach (IGrouping<int, BranchBusbarRule> duplicate in rules.GroupBy(rule => rule.RatedCurrentA).Where(group => group.Count() > 1))
            {
                report.AddError(scope, "Rated current " + duplicate.Key + "A is configured more than once.");
            }

            foreach (BranchBusbarRule rule in rules)
            {
                string ruleScope = scope + "/" + rule.RatedCurrentA + "A";
                if (rule.RatedCurrentA <= 0)
                    report.AddError(ruleScope, "Rated current must be greater than zero.");

                ValidateProfile(report, ruleScope, rule.Profile);

                if (!Enum.IsDefined(typeof(BranchArrangement), rule.Arrangement))
                    report.AddError(ruleScope, "Branch arrangement is invalid.");
            }
        }

        private static void ValidateFastenerConfiguration(
            BusbarPreflightReport report,
            BusbarSettings settings)
        {
            if (settings.MinimumThreadProjectionMm < 0.0)
                report.AddError("Configuration/Fasteners", "Minimum thread projection cannot be negative.");

            if (settings.FastenerCatalog == null || settings.FastenerCatalog.Count == 0)
            {
                report.AddError("Configuration/Fasteners", "Fastener catalog is empty.");
                return;
            }

            foreach (IGrouping<double, FastenerSpec> duplicateHole in settings.FastenerCatalog
                .GroupBy(spec => spec.ClearanceHoleDiameterMm)
                .Where(group => group.Count() > 1))
            {
                report.AddError(
                    "Configuration/Fasteners",
                    "Clearance hole diameter " + duplicateHole.Key.ToString("0.###") + "mm maps to multiple fastener specs.");
            }

            foreach (FastenerSpec spec in settings.FastenerCatalog)
            {
                string scope = "Configuration/Fasteners/" + spec.NominalSize;
                if (string.IsNullOrWhiteSpace(spec.NominalSize))
                    report.AddError("Configuration/Fasteners", "Fastener nominal size is missing.");

                if (spec.ClearanceHoleDiameterMm <= 0.0 ||
                    spec.SpringWasherCompressedThicknessMm < 0.0 ||
                    spec.FlatWasherThicknessMm < 0.0 ||
                    spec.FlatWasherDiameterMm <= 0.0 ||
                    spec.NutHeightMm <= 0.0 ||
                    spec.BoltHeadHeightMm <= 0.0 ||
                    spec.BoltHeadEnvelopeDiameterMm <= 0.0)
                {
                    report.AddError(scope, "Fastener dimensions contain an invalid value.");
                }

                if (spec.StandardLengthsMm == null || spec.StandardLengthsMm.Count == 0 ||
                    spec.StandardLengthsMm.Any(length => length <= 0.0))
                {
                    report.AddError(scope, "At least one positive standard nominal length is required.");
                }
            }
        }

        private static void ValidateSheetMetalConfiguration(
            BusbarPreflightReport report,
            BusbarSettings settings)
        {
            if (settings.SheetMetalBendRadiusMm <= 0.0)
                report.AddError("Configuration/SheetMetal", "Bend radius must be greater than zero.");

            if (settings.SheetMetalKFactor < 0.0 || settings.SheetMetalKFactor > 1.0)
                report.AddError("Configuration/SheetMetal", "K factor must be between 0 and 1.");

            if (!Enum.IsDefined(typeof(SheetMetalWidthSide), settings.MainFeedSheetMetalWidthSide) ||
                !Enum.IsDefined(typeof(SheetMetalWidthSide), settings.CollectorSheetMetalWidthSide) ||
                !Enum.IsDefined(typeof(SheetMetalWidthSide), settings.BranchSheetMetalWidthSide))
            {
                report.AddError("Configuration/SheetMetal", "Sheet-metal width side contains an invalid value.");
            }
        }

        private static bool IsDefinedArrangement(BranchArrangement? arrangement)
        {
            return !arrangement.HasValue || Enum.IsDefined(typeof(BranchArrangement), arrangement.Value);
        }

        private static void ValidateLoubaoSelections(
            BusbarPreflightReport report,
            BusbarManufacturingPlan plan,
            BusbarSettings settings)
        {
            foreach (LoubaoGroup loubao in plan.Loubaos)
            {
                string scope = "Loubao/" + loubao.ComponentName;
                if (loubao.RatedCurrentA <= 0)
                    report.AddError(scope, "The parsed rated current is invalid.");

                BranchBusbarRule phaseRule = FindRule(settings.PhaseBranchRules, loubao.RatedCurrentA);
                BranchBusbarRule neutralRule = FindRule(settings.NeutralBranchRules, loubao.RatedCurrentA);

                if (phaseRule == null)
                    report.AddError(scope, "No ABC rule exists for " + loubao.RatedCurrentA + "A.");
                else
                {
                    ValidateProfileMatch(report, scope + "/ABC", loubao.BranchProfile, phaseRule.Profile);
                    BranchArrangement expectedArrangement = settings.PhaseBranchArrangementOverride ?? phaseRule.Arrangement;
                    if (loubao.BranchArrangement != expectedArrangement)
                    {
                        report.AddError(
                            scope + "/ABC",
                            "Selected arrangement is " + loubao.BranchArrangement +
                            ", expected " + expectedArrangement + ".");
                    }
                }

                if (neutralRule == null)
                    report.AddError(scope, "No neutral rule exists for " + loubao.RatedCurrentA + "A.");
                else
                {
                    ValidateProfileMatch(report, scope + "/N", loubao.NeutralBranchProfile, neutralRule.Profile);
                    BranchArrangement expectedArrangement = settings.NeutralBranchArrangementOverride ?? neutralRule.Arrangement;
                    if (loubao.NeutralBranchArrangement != expectedArrangement)
                    {
                        report.AddError(
                            scope + "/N",
                            "Selected arrangement is " + loubao.NeutralBranchArrangement +
                            ", expected " + expectedArrangement + ".");
                    }
                }

                report.AddInfo(
                    scope,
                    loubao.RatedCurrentA + "A, ABC=" + FormatProfile(loubao.BranchProfile) + " " +
                    loubao.BranchArrangement + ", N=" + FormatProfile(loubao.NeutralBranchProfile) + " " +
                    loubao.NeutralBranchArrangement + ".");
            }
        }

        private static void ValidateFastenerJoints(
            BusbarPreflightReport report,
            BusbarManufacturingPlan plan)
        {
            if (plan.FastenerJoints == null || plan.FastenerJoints.Count == 0)
            {
                report.AddError("Fasteners", "No collector fastener joints were planned.");
                return;
            }

            foreach (FastenerJointPlan joint in plan.FastenerJoints)
            {
                string scope = "Fastener/" + joint.JointId;
                if (!joint.IsValid)
                {
                    report.AddError(scope, joint.SelectionError ?? "Fastener selection failed.");
                    continue;
                }

                report.AddInfo(
                    scope,
                    joint.Fastener.NominalSize +
                    " x " + joint.SelectedNominalLengthMm.ToString("0.###") + "mm" +
                    ", hole=" + joint.HoleDiameterMm.ToString("0.###") + "mm" +
                    ", clamp=" + joint.ClampedThicknessMm.ToString("0.###") + "mm" +
                    ", required=" + joint.RequiredNominalLengthMm.ToString("0.###") + "mm" +
                    ", actual thread projection=" + joint.ActualThreadProjectionMm.ToString("0.###") + "mm" +
                    ", connected=" + string.Join(",", joint.ConnectedBusbars.ToArray()) + ".");
            }
        }

        private static void ValidateCollectors(
            BusbarPreflightReport report,
            BusbarManufacturingPlan plan,
            BusbarSettings settings,
            string[] phaseNames)
        {
            foreach (string phase in GetAllPhases(phaseNames))
            {
                CollectorLayout collector = FindCollector(plan, phase);
                if (collector == null)
                {
                    if (SameText(phase, "N"))
                        report.AddInfo("Collector/N", "No complete N_IN reference-point set was scanned; neutral busbars are omitted.");
                    else
                        report.AddError("Collector/" + phase, "Collector layout is missing.");
                    continue;
                }

                if (collector.StartX > collector.EndX)
                    report.AddError("Collector/" + phase, "Collector start X is greater than end X.");

                Busbar collectorBusbar = FindBusbar(plan, "Busbar_" + phase + "_Collector");
                if (collectorBusbar == null)
                {
                    report.AddError("Collector/" + phase, "Collector busbar is missing.");
                    continue;
                }

                BusbarProfile expectedProfile = SameText(phase, "N")
                    ? settings.NeutralCollectorProfile
                    : settings.CollectorProfile;
                ValidateProfileMatch(report, "Collector/" + phase, collectorBusbar.Profile, expectedProfile);
                ValidateRouteEndpoints(report, "Collector/" + phase, collectorBusbar);

                foreach (Busbar branch in GetPrimaryBranchesForPhase(plan, phase))
                {
                    double halfWidth = branch.Profile == null ? 0.0 : branch.Profile.WidthMeters / 2.0;
                    double leftEdge = branch.EndPort.HoleCenter.X - halfWidth;
                    double rightEdge = branch.EndPort.HoleCenter.X + halfWidth;

                    if (collector.StartX > leftEdge + Mm(CoordinateToleranceMm) ||
                        collector.EndX < rightEdge - Mm(CoordinateToleranceMm))
                    {
                        report.AddError(
                            "Collector/" + phase,
                            "Length does not cover branch overlap width for " + branch.Name + ".");
                    }
                }

                report.AddInfo(
                    "Collector/" + phase,
                    "Profile=" + FormatProfile(collectorBusbar.Profile) +
                    ", X range=" + FormatMm(collector.StartX) + " to " + FormatMm(collector.EndX) + ".");
            }
        }

        private static void ValidateBranchTopology(
            BusbarPreflightReport report,
            BusbarManufacturingPlan plan,
            BusbarSettings settings,
            string[] phaseNames)
        {
            for (int index = 0; index < plan.Loubaos.Count; index++)
            {
                LoubaoGroup loubao = plan.Loubaos[index];
                int branchIndex = index + 1;

                foreach (string phase in phaseNames ?? new string[0])
                {
                    BranchArrangement arrangement = settings.PhaseBranchArrangementOverride ?? loubao.BranchArrangement;
                    ValidateExpectedBranchSet(
                        report,
                        plan,
                        phase,
                        branchIndex,
                        loubao.BranchProfile,
                        arrangement);
                }

                if (FindCollector(plan, "N") != null)
                {
                    BranchArrangement arrangement = settings.NeutralBranchArrangementOverride ?? loubao.NeutralBranchArrangement;
                    ValidateExpectedBranchSet(
                        report,
                        plan,
                        "N",
                        branchIndex,
                        loubao.NeutralBranchProfile,
                        arrangement);
                }
            }
        }

        private static void ValidateExpectedBranchSet(
            BusbarPreflightReport report,
            BusbarManufacturingPlan plan,
            string phase,
            int branchIndex,
            BusbarProfile expectedProfile,
            BranchArrangement arrangement)
        {
            string scope = "Branch/" + phase + "/" + branchIndex;
            string baseName = "Busbar_" + phase + "_Branch_" + branchIndex;
            Busbar single = FindBusbar(plan, baseName);
            Busbar lower = FindBusbar(plan, baseName + "_Lower");
            Busbar upper = FindBusbar(plan, baseName + "_Upper");

            if (arrangement == BranchArrangement.Single)
            {
                if (single == null)
                    report.AddError(scope, "Single branch busbar is missing.");
                else
                {
                    ValidateProfileMatch(report, scope, single.Profile, expectedProfile);
                    ValidateRouteEndpoints(report, scope, single);
                }

                if (lower != null || upper != null)
                    report.AddError(scope, "Single arrangement unexpectedly contains double-clamp legs.");

                return;
            }

            if (single != null)
                report.AddError(scope, "Double-clamp arrangement unexpectedly contains a single branch busbar.");

            if (lower == null)
                report.AddError(scope, "Double-clamp lower branch busbar is missing.");
            else
            {
                ValidateProfileMatch(report, scope + "/Lower", lower.Profile, expectedProfile);
                ValidateRouteEndpoints(report, scope + "/Lower", lower);
            }

            if (upper == null)
                report.AddError(scope, "Double-clamp upper branch busbar is missing.");
            else
            {
                ValidateProfileMatch(report, scope + "/Upper", upper.Profile, expectedProfile);
                ValidateRouteEndpoints(report, scope + "/Upper", upper);
            }
        }

        private static void ValidateOverlapRulesAndPorts(
            BusbarPreflightReport report,
            BusbarManufacturingPlan plan,
            string[] phaseNames,
            BusbarOverlapRuleCatalog overlapRules)
        {
            if (overlapRules == null)
            {
                report.AddError("Overlap", "Overlap-hole rules are not configured.");
                return;
            }

            foreach (string phase in GetAllPhases(phaseNames))
            {
                Busbar collector = FindBusbar(plan, "Busbar_" + phase + "_Collector");
                if (collector == null)
                    continue;

                foreach (Busbar branch in GetBranchesForPhase(plan, phase))
                {
                    if (branch.Profile == null || collector.Profile == null)
                        continue;

                    BusbarOverlapHoleRule rule;
                    string scope = "Overlap/" + branch.Name;
                    if (!overlapRules.TryResolve(branch.Profile.WidthMm, collector.Profile.WidthMm, out rule))
                    {
                        report.AddError(
                            scope,
                            "No formal overlap matrix entry for " + branch.Profile.WidthMm.ToString("0.###") +
                            "x" + collector.Profile.WidthMm.ToString("0.###") +
                            "mm; generation is blocked until an approved rule is added.");
                    }
                    else
                    {
                        report.AddInfo(scope, "Hole rule=" + rule.SourceCode + ".");
                    }

                    List<ConnectionPort> branchOverlapPorts = branch.MountingPorts
                        .Where(port => port.Name != null && branch.EndPort != null &&
                            port.Name.StartsWith(branch.EndPort.Name + "_", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (branchOverlapPorts.Count == 0)
                    {
                        report.AddError(scope, "Branch overlap mounting ports are missing.");
                        continue;
                    }

                    foreach (ConnectionPort branchPort in branchOverlapPorts)
                    {
                        bool collectorContainsHole = collector.MountingPorts.Any(collectorPort =>
                            SameCoordinate(branchPort.HoleCenter.X, collectorPort.HoleCenter.X) &&
                            SameCoordinate(branchPort.HoleCenter.Z, collectorPort.HoleCenter.Z));

                        if (!collectorContainsHole)
                        {
                            report.AddError(
                                scope,
                                "Collector has no matching X/Z mounting port for " + branchPort.Name + ".");
                        }
                    }
                }
            }
        }

        private static void ValidateDoubleClampRoutes(
            BusbarPreflightReport report,
            BusbarManufacturingPlan plan,
            BusbarSettings settings,
            string[] phaseNames)
        {
            foreach (string phase in phaseNames ?? new string[0])
            {
                CollectorLayout collector = FindCollector(plan, phase);
                if (collector == null)
                    continue;

                for (int index = 0; index < plan.Loubaos.Count; index++)
                {
                    LoubaoGroup loubao = plan.Loubaos[index];
                    BranchArrangement arrangement = settings.PhaseBranchArrangementOverride ?? loubao.BranchArrangement;
                    if (arrangement != BranchArrangement.DoubleClamp)
                        continue;

                    string baseName = "Busbar_" + phase + "_Branch_" + (index + 1);
                    Busbar lower = FindBusbar(plan, baseName + "_Lower");
                    Busbar upper = FindBusbar(plan, baseName + "_Upper");
                    if (lower == null || upper == null || lower.Profile == null || upper.Profile == null)
                        continue;

                    string scope = "DoubleClamp/" + phase + "/" + (index + 1);
                    ValidateProfileMatch(report, scope, lower.Profile, upper.Profile);

                    double expectedLowerEndY = collector.Center.Y - settings.CollectorProfile.ThicknessMeters - lower.Profile.ThicknessMeters;
                    if (!SameCoordinate(lower.EndPort.HoleCenter.Y, expectedLowerEndY))
                    {
                        report.AddError(
                            scope,
                            "Lower route-end Y=" + FormatMm(lower.EndPort.HoleCenter.Y) +
                            ", expected " + FormatMm(expectedLowerEndY) +
                            " from collector top - collector thickness - branch thickness.");
                    }

                    if (!SameCoordinate(upper.EndPort.HoleCenter.Y, collector.Center.Y))
                    {
                        report.AddError(
                            scope,
                            "Upper route-end Y=" + FormatMm(upper.EndPort.HoleCenter.Y) +
                            ", expected collector top " + FormatMm(collector.Center.Y) + ".");
                    }

                    if (!SameCoordinate(lower.EndPort.HoleCenter.X, upper.EndPort.HoleCenter.X) ||
                        !SameCoordinate(lower.EndPort.HoleCenter.Z, upper.EndPort.HoleCenter.Z))
                    {
                        report.AddError(scope, "Upper and lower branch ends do not share the same collector tap X/Z position.");
                    }

                    double expectedStartOffsetZ = settings.DoubleClampUpperStartZSign * upper.Profile.ThicknessMeters;
                    double actualStartOffsetZ = upper.StartPort.HoleCenter.Z - lower.StartPort.HoleCenter.Z;
                    if (settings.DoubleClampUpperStartZSign != -1 && settings.DoubleClampUpperStartZSign != 1)
                        report.AddError(scope, "Double-clamp upper start Z sign must be -1 or 1.");

                    if (!SameCoordinate(actualStartOffsetZ, expectedStartOffsetZ) ||
                        !SameCoordinate(upper.StartPort.HoleCenter.X, lower.StartPort.HoleCenter.X) ||
                        !SameCoordinate(upper.StartPort.HoleCenter.Y, lower.StartPort.HoleCenter.Y))
                    {
                        report.AddError(
                            scope,
                            "Upper start must be the lower start shifted only by one branch thickness in configured Z direction.");
                    }

                    ValidateOuterAvoidanceRoute(report, scope, upper, settings);
                }
            }
        }

        private static void ValidateOuterAvoidanceRoute(
            BusbarPreflightReport report,
            string scope,
            Busbar upper,
            BusbarSettings settings)
        {
            if (upper.Routing == null || upper.Routing.BranchRouteMode != BranchRouteMode.DoubleClampOuterAvoidance)
            {
                report.AddError(scope, "Upper branch is not using the DoubleClampOuterAvoidance route mode.");
                return;
            }

            if (settings.DoubleClampOuterInitialRiseMm <= 0.0 || settings.DoubleClampOuterDiagonalMinimumLengthMm <= 0.0)
            {
                report.AddError(scope, "Outer-avoidance rise and diagonal minimum length must both be greater than zero.");
                return;
            }

            if (upper.LogicalCenterline == null || upper.LogicalCenterline.Count < 5)
            {
                report.AddError(scope, "Outer-avoidance route requires at least five logical points.");
                return;
            }

            Point3 p0 = upper.LogicalCenterline[0];
            Point3 p1 = upper.LogicalCenterline[1];
            Point3 p2 = upper.LogicalCenterline[2];
            Point3 p3 = upper.LogicalCenterline[3];
            Point3 p4 = upper.LogicalCenterline[upper.LogicalCenterline.Count - 1];

            if (!SamePoint(p0, upper.StartPort.HoleCenter) || !SamePoint(p4, upper.EndPort.HoleCenter))
                report.AddError(scope, "Outer-avoidance logical route no longer starts and ends at its branch ports.");

            double initialRise = p1.Y - p0.Y;
            if (!SameCoordinate(initialRise, settings.DoubleClampOuterInitialRiseMeters))
                report.AddError(scope, "Initial Y+ rise does not match DoubleClampOuterInitialRiseMm.");

            double expectedDiagonalZ = -upper.Profile.ThicknessMeters * 2.0;
            if (!SameCoordinate(p2.Z - p1.Z, expectedDiagonalZ) || p2.Y <= p1.Y)
                report.AddError(scope, "Diagonal avoidance segment does not satisfy the required Y+ / Z- offset.");

            double diagonalLength = p1.DistanceTo(p2);
            if (diagonalLength + Mm(CoordinateToleranceMm) < settings.DoubleClampOuterDiagonalMinimumLengthMeters)
                report.AddError(scope, "Diagonal avoidance segment is shorter than DoubleClampOuterDiagonalMinimumLengthMm.");

            if (!SameCoordinate(p3.Y, p4.Y) || !SameCoordinate(p3.Z, p2.Z) || p3.Y < p2.Y)
                report.AddError(scope, "Final rise and return segment do not preserve the outer-avoidance route contract.");
        }

        private static void ValidateRouteEndpoints(BusbarPreflightReport report, string scope, Busbar busbar)
        {
            if (busbar == null || busbar.StartPort == null || busbar.EndPort == null)
            {
                report.AddError(scope, "Busbar start or end port is missing.");
                return;
            }

            if (busbar.LogicalCenterline == null || busbar.LogicalCenterline.Count < 2)
            {
                report.AddError(scope, "Logical centerline requires at least two points.");
                return;
            }

            if (!SamePoint(busbar.LogicalCenterline[0], busbar.StartPort.HoleCenter) ||
                !SamePoint(busbar.LogicalCenterline[busbar.LogicalCenterline.Count - 1], busbar.EndPort.HoleCenter))
            {
                report.AddError(scope, "Logical centerline endpoints do not match the connection ports.");
            }
        }

        private static void ValidateProfileMatch(
            BusbarPreflightReport report,
            string scope,
            BusbarProfile actual,
            BusbarProfile expected)
        {
            if (actual == null || expected == null)
            {
                report.AddError(scope, "Busbar profile is missing.");
                return;
            }

            if (!SameMm(actual.WidthMm, expected.WidthMm) || !SameMm(actual.ThicknessMm, expected.ThicknessMm))
            {
                report.AddError(
                    scope,
                    "Profile=" + FormatProfile(actual) + ", expected " + FormatProfile(expected) + ".");
            }
        }

        private static BranchBusbarRule FindRule(List<BranchBusbarRule> rules, int ratedCurrentA)
        {
            return rules == null
                ? null
                : rules.FirstOrDefault(rule => rule.RatedCurrentA == ratedCurrentA);
        }

        private static CollectorLayout FindCollector(BusbarManufacturingPlan plan, string phase)
        {
            return plan.Collectors.FirstOrDefault(collector => SameText(collector.Phase, phase));
        }

        private static Busbar FindBusbar(BusbarManufacturingPlan plan, string name)
        {
            return plan.Busbars.FirstOrDefault(busbar => SameText(busbar.Name, name));
        }

        private static IEnumerable<Busbar> GetPrimaryBranchesForPhase(BusbarManufacturingPlan plan, string phase)
        {
            string prefix = "Busbar_" + phase + "_Branch_";
            return plan.Busbars.Where(busbar =>
                busbar.Kind == BusbarKind.Branch &&
                busbar.Name != null &&
                busbar.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                busbar.BranchLegRole != BranchLegRole.Upper);
        }

        private static IEnumerable<Busbar> GetBranchesForPhase(BusbarManufacturingPlan plan, string phase)
        {
            string prefix = "Busbar_" + phase + "_Branch_";
            return plan.Busbars.Where(busbar =>
                busbar.Kind == BusbarKind.Branch &&
                busbar.Name != null &&
                busbar.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<string> GetAllPhases(string[] phaseNames)
        {
            List<string> phases = (phaseNames ?? new string[0])
                .Where(phase => !string.IsNullOrWhiteSpace(phase))
                .ToList();
            if (!phases.Any(phase => SameText(phase, "N")))
                phases.Add("N");
            return phases;
        }

        private static bool SamePoint(Point3 first, Point3 second)
        {
            return SameCoordinate(first.X, second.X) &&
                SameCoordinate(first.Y, second.Y) &&
                SameCoordinate(first.Z, second.Z);
        }

        private static bool SameCoordinate(double first, double second)
        {
            return Math.Abs(first - second) <= Mm(CoordinateToleranceMm);
        }

        private static bool SameMm(double first, double second)
        {
            return Math.Abs(first - second) <= CoordinateToleranceMm;
        }

        private static bool SameText(string first, string second)
        {
            return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatProfile(BusbarProfile profile)
        {
            return profile == null ? "(missing)" : profile.Label + "mm";
        }

        private static string FormatMm(double valueInMeters)
        {
            return (valueInMeters * 1000.0).ToString("0.###") + "mm";
        }

        private static double Mm(double value)
        {
            return value / 1000.0;
        }
    }
}
