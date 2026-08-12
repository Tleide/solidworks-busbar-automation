using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using BusbarAutomation.Core.Domain;
using BusbarAutomation.Core.Planning;

using BusbarAutomation.Core.Rules;

namespace BusbarAutomation.Tests
{
    internal static class BusbarPlanSnapshotFormatter
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static string Format(BusbarManufacturingPlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException("plan");

            StringBuilder text = new StringBuilder();
            Line(text, "BUSBAR_PLAN_SNAPSHOT_V1");
            Line(text, "Fuse=" + Value(plan.FuseComponentName));
            AppendRules(text, plan.Rules);

            Line(text, "Loubaos=" + plan.Loubaos.Count);
            for (int i = 0; i < plan.Loubaos.Count; i++)
            {
                LoubaoGroup loubao = plan.Loubaos[i];
                Line(
                    text,
                    "L[" + Index(i) + "]|Component=" + Value(loubao.ComponentName) +
                    "|CenterXmm=" + Mm(loubao.CenterX) +
                    "|CurrentA=" + loubao.RatedCurrentA +
                    "|PhaseProfile=" + Profile(loubao.BranchProfile) +
                    "|PhaseArrangement=" + loubao.BranchArrangement +
                    "|NeutralProfile=" + Profile(loubao.NeutralBranchProfile) +
                    "|NeutralArrangement=" + loubao.NeutralBranchArrangement);
            }

            Line(text, "Collectors=" + plan.Collectors.Count);
            for (int i = 0; i < plan.Collectors.Count; i++)
                AppendCollector(text, i, plan.Collectors[i]);

            Line(text, "Busbars=" + plan.Busbars.Count);
            for (int i = 0; i < plan.Busbars.Count; i++)
                AppendBusbar(text, i, plan.Busbars[i]);

            Line(text, "FastenerJoints=" + plan.FastenerJoints.Count);
            for (int i = 0; i < plan.FastenerJoints.Count; i++)
                AppendFastenerJoint(text, i, plan.FastenerJoints[i]);

            return text.ToString().TrimEnd();
        }

        private static void AppendRules(StringBuilder text, ManualBusbarRuleSet rules)
        {
            if (rules == null)
            {
                Line(text, "Rules=<null>");
                return;
            }

            Line(
                text,
                "Rules|Topology=" + rules.TopologyKind +
                "|DefaultEndMarginMm=" + Number(rules.DefaultEndMarginMm) +
                "|BendRadiusMm=" + Number(rules.BendRadiusMm) +
                "|KFactor=" + Number(rules.KFactor) +
                "|WidthMode=" + rules.WidthMode +
                "|MainFace=" + rules.MainFeedCollectorFace +
                "|BranchFace=" + rules.BranchCollectorFace +
                "|Transition=" + rules.TransitionPolicy +
                "|AxisOrder=" + rules.RouteAxisOrder);
            Line(
                text,
                "RuleHoles|MainStart=" + Number(rules.MainFeedStartHoleDiameterMm) +
                "|MainCollector=" + Number(rules.MainFeedCollectorHoleDiameterMm) +
                "|BranchStart=" + Number(rules.BranchStartHoleDiameterMm) +
                "|BranchCollector=" + Number(rules.BranchCollectorHoleDiameterMm) +
                "|CollectorTap=" + Number(rules.CollectorTapHoleDiameterMm) +
                "|NeutralStart=" + Number(rules.NeutralBranchStartHoleDiameterMm) +
                "|NeutralTap=" + Number(rules.NeutralCollectorTapHoleDiameterMm));
        }

        private static void AppendCollector(StringBuilder text, int index, CollectorLayout collector)
        {
            Line(
                text,
                "C[" + Index(index) + "]|Phase=" + Value(collector.Phase) +
                "|Direction=" + collector.Direction +
                "|CenterMm=" + Point(collector.Center) +
                "|StartXmm=" + Mm(collector.StartX) +
                "|EndXmm=" + Mm(collector.EndX) +
                "|Taps=" + collector.TapPorts.Count);

            for (int i = 0; i < collector.TapPorts.Count; i++)
                Line(text, "C[" + Index(index) + "].Tap[" + Index(i) + "]|" + Port(collector.TapPorts[i]));
        }

        private static void AppendBusbar(StringBuilder text, int index, Busbar busbar)
        {
            Line(
                text,
                "B[" + Index(index) + "]|Name=" + Value(busbar.Name) +
                "|Kind=" + busbar.Kind +
                "|Leg=" + busbar.BranchLegRole +
                "|Profile=" + Profile(busbar.Profile) +
                "|Routing=" + Routing(busbar.Routing) +
                "|SheetMetal=" + SheetMetal(busbar.SheetMetal));
            Line(text, "B[" + Index(index) + "].Start|" + Port(busbar.StartPort));
            Line(text, "B[" + Index(index) + "].End|" + Port(busbar.EndPort));
            AppendPoints(text, "B[" + Index(index) + "].Logical", busbar.LogicalCenterline);
            AppendPoints(text, "B[" + Index(index) + "].Sketch", busbar.SheetMetalSketchLine);

            Line(text, "B[" + Index(index) + "].MountingPorts=" + busbar.MountingPorts.Count);
            for (int i = 0; i < busbar.MountingPorts.Count; i++)
                Line(text, "B[" + Index(index) + "].Mount[" + Index(i) + "]|" + Port(busbar.MountingPorts[i]));
        }

        private static void AppendPoints(StringBuilder text, string prefix, List<Point3> points)
        {
            int count = points == null ? 0 : points.Count;
            Line(text, prefix + "=" + count);
            for (int i = 0; i < count; i++)
                Line(text, prefix + "[" + Index(i) + "]=" + Point(points[i]));
        }

        private static void AppendFastenerJoint(StringBuilder text, int index, FastenerJointPlan joint)
        {
            string connected = joint.ConnectedBusbars == null
                ? string.Empty
                : string.Join(",", joint.ConnectedBusbars.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray());

            Line(
                text,
                "F[" + Index(index) + "]|Id=" + Value(joint.JointId) +
                "|Phase=" + Value(joint.CollectorPhase) +
                "|CenterMm=" + Point(joint.HoleCenter) +
                "|HoleMm=" + Number(joint.HoleDiameterMm) +
                "|Fastener=" + Value(joint.Fastener == null ? null : joint.Fastener.NominalSize) +
                "|Connected=" + Value(connected) +
                "|ClampedMm=" + Number(joint.ClampedThicknessMm) +
                "|RequiredMm=" + Number(joint.RequiredNominalLengthMm) +
                "|SelectedMm=" + Number(joint.SelectedNominalLengthMm) +
                "|ProjectionMm=" + Number(joint.ActualThreadProjectionMm) +
                "|MinimumProjectionMm=" + Number(joint.MinimumThreadProjectionMm) +
                "|Valid=" + joint.IsValid +
                "|Error=" + Value(joint.SelectionError));
        }

        private static string Routing(BusbarRoutingOptions routing)
        {
            return routing == null
                ? "<null>"
                : routing.AxisOrder + "," + routing.TransitionPolicy + "," + routing.BranchRouteMode;
        }

        private static string SheetMetal(SheetMetalOptions sheetMetal)
        {
            return sheetMetal == null
                ? "<null>"
                : "R" + Number(sheetMetal.BendRadiusMm) +
                  ",K" + Number(sheetMetal.KFactor) +
                  "," + sheetMetal.WidthMode +
                  "," + sheetMetal.WidthSide +
                  ",Thicken=" + sheetMetal.ThickenDirection;
        }

        private static string Port(ConnectionPort port)
        {
            if (port == null)
                return "<null>";

            return "Name=" + Value(port.Name) +
                   "|Component=" + Value(port.ComponentName) +
                   "|Kind=" + port.Kind +
                   "|CenterMm=" + Point(port.HoleCenter) +
                   "|Face=" + port.RequiredFace +
                   "|Lead=" + port.PreferredLeadAxis + Number(port.PreferredLeadSign) +
                   "|EndMarginMm=" + Number(port.EndMarginMm) +
                   "|HoleMm=" + Number(port.HoleDiameterMm);
        }

        private static string Profile(BusbarProfile profile)
        {
            return profile == null
                ? "<null>"
                : Number(profile.ThicknessMm) + "x" + Number(profile.WidthMm);
        }

        private static string Point(Point3 point)
        {
            return "(" + Mm(point.X) + "," + Mm(point.Y) + "," + Mm(point.Z) + ")";
        }

        private static string Mm(double meters)
        {
            return Number(meters * 1000.0);
        }

        private static string Number(double value)
        {
            if (Math.Abs(value) < 0.0000005)
                value = 0.0;

            return value.ToString("0.######", Invariant);
        }

        private static string Value(string value)
        {
            return string.IsNullOrEmpty(value)
                ? "<empty>"
                : value.Replace("\\", "\\\\").Replace("|", "\\|").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string Index(int index)
        {
            return index.ToString("D2", Invariant);
        }

        private static void Line(StringBuilder text, string value)
        {
            text.Append(value).Append('\n');
        }
    }
}
