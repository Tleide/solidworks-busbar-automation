using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

using BusbarAutomation.Application;
using BusbarAutomation.Core.Domain;

using BusbarAutomation.Core.Planning;

namespace BusbarAutomation.Tests
{
    [TestClass]
    public class BusbarPlanBaselineTests
    {
        private static readonly string[] PhaseNames = { "A", "B", "C" };

        [TestMethod]
        public void RepresentativeAssemblyPlanMatchesApprovedBaseline()
        {
            BusbarSettings settings = RepresentativeAssemblyFixture.CreateSettings();
            EngineeringConfigurationSnapshot configuration =
                EngineeringConfigurationSnapshot.FromSettings(settings, "baseline-v1");
            AssemblySnapshot assembly = AssemblySnapshotFactory.FromFoundPoints(
                RepresentativeAssemblyFixture.CreatePoints(),
                PhaseNames,
                configuration.GetSupportedRatedCurrents(),
                "RepresentativeAssembly");
            BusbarPlan plan = BusbarPlanBuilder.BuildPlan(assembly, PhaseNames, configuration);

            Assert.AreEqual(28, plan.Busbars.Count, "The representative 630A + 400A + 400A plan must contain 28 busbars.");
            Assert.AreEqual(4, plan.Collectors.Count, "The representative plan must contain A, B, C and N collectors.");
            Assert.AreEqual(30, plan.FastenerJoints.Count, "The representative plan must contain 30 collector fastener joints.");

            string actual = BusbarPlanSnapshotFormatter.Format(plan);
            string baselinePath = GetBaselinePath();

            if (string.Equals(
                Environment.GetEnvironmentVariable("BUSBAR_APPROVE_BASELINE"),
                "1",
                StringComparison.Ordinal))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(baselinePath));
                File.WriteAllText(baselinePath, actual + Environment.NewLine, new UTF8Encoding(false));
                Assert.Inconclusive("Approved busbar planning baseline: " + baselinePath);
            }

            if (!File.Exists(baselinePath))
            {
                Assert.Fail(
                    "Approved busbar planning baseline is missing: " + baselinePath + Environment.NewLine +
                    "Set BUSBAR_APPROVE_BASELINE=1 for one intentional approval run, inspect the file, then run the tests normally.");
            }

            string expected = Normalize(File.ReadAllText(baselinePath));
            Assert.AreEqual(
                expected,
                Normalize(actual),
                "The busbar plan changed. Review the approved baseline before accepting any planning or geometry change.");
        }

        private static string GetBaselinePath([CallerFilePath] string testSourcePath = null)
        {
            return Path.Combine(
                Path.GetDirectoryName(testSourcePath),
                "Baselines",
                "RepresentativeBusbarPlan.txt");
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r\n", "\n")
                .TrimEnd();
        }
    }

    internal static class RepresentativeAssemblyFixture
    {
        private static readonly string[] PhaseNames = { "A", "B", "C" };

        public static List<FoundPoint> CreatePoints()
        {
            List<FoundPoint> points = new List<FoundPoint>();

            AddPoint(points, "HR6-630-1", "A_OUT", -450.0, -105.0, 24.0);
            AddPoint(points, "HR6-630-1", "B_OUT", -430.0, -105.0, 24.0);
            AddPoint(points, "HR6-630-1", "C_OUT", -410.0, -105.0, 24.0);

            AddLoubao(points, "PGM8LZ-630-1", -200.0);
            AddLoubao(points, "PGM8LZ-400-2", 50.0);
            AddLoubao(points, "PGM8LZ-400-3", 300.0);
            return points;
        }

        public static BusbarSettings CreateSettings()
        {
            return new BusbarSettings
            {
                MainFeedWidthMm = 60.0,
                MainFeedThicknessMm = 6.0,
                CollectorWidthMm = 60.0,
                CollectorThicknessMm = 8.0,
                NeutralCollectorWidthMm = 50.0,
                NeutralCollectorThicknessMm = 5.0,
                PhaseBranchRules = new List<BranchBusbarRule>
                {
                    new BranchBusbarRule(250, 30.0, 4.0, BranchArrangement.Single),
                    new BranchBusbarRule(400, 30.0, 4.0, BranchArrangement.DoubleClamp),
                    new BranchBusbarRule(630, 40.0, 4.0, BranchArrangement.DoubleClamp)
                },
                NeutralBranchRules = new List<BranchBusbarRule>
                {
                    new BranchBusbarRule(250, 20.0, 4.0, BranchArrangement.Single),
                    new BranchBusbarRule(400, 30.0, 4.0, BranchArrangement.Single),
                    new BranchBusbarRule(630, 40.0, 4.0, BranchArrangement.Single)
                },
                PhaseBranchArrangementOverride = null,
                NeutralBranchArrangementOverride = null,
                DoubleClampUpperStartZSign = -1,
                DoubleClampOuterInitialRiseMm = 50.0,
                DoubleClampOuterDiagonalMinimumLengthMm = 50.0,
                CollectorPhaseSpacingMm = 60.0,
                CollectorTopClearanceYMm = 240.0,
                CollectorOffsetFromLoubaoInZMm = 120.0,
                CollectorANegativeXExtendMm = 50.0,
                CollectorBNegativeXExtendMm = 50.0,
                CollectorCNegativeXExtendMm = 50.0,
                NeutralCollectorNegativeXExtendMm = 50.0,
                MainLeadOutYMm = 40.0,
                SheetMetalBendRadiusMm = 5.0,
                SheetMetalKFactor = 0.47,
                SheetMetalThickenDirection = false,
                MainFeedSheetMetalWidthSide = SheetMetalWidthSide.Center,
                CollectorSheetMetalWidthSide = SheetMetalWidthSide.Center,
                BranchSheetMetalWidthSide = SheetMetalWidthSide.Center,
                MainCollectorFrontClearanceMm = 100.0,
                MinimumThreadProjectionMm = 3.0,
                FastenerCatalog = new List<FastenerSpec>
                {
                    new FastenerSpec("M8", 9.0, 1.5, 1.5, 16.0, 6.0, 4.5, 14.0, 20.0, 25.0),
                    new FastenerSpec("M10", 11.0, 2.5, 2.0, 20.0, 8.0, 6.0, 18.0, 20.0, 30.0, 35.0, 40.0),
                    new FastenerSpec("M12", 13.0, 3.5, 2.5, 24.0, 11.0, 7.0, 22.0, 35.0, 40.0, 45.0),
                    new FastenerSpec("M14", 15.0, 4.0, 2.5, 28.0, 11.0, 9.5, 26.0, 40.0, 45.0),
                    new FastenerSpec("M16", 17.0, 4.5, 3.0, 32.0, 14.0, 11.0, 30.0, 40.0, 45.0)
                }
            };
        }

        private static void AddLoubao(List<FoundPoint> points, string componentName, double centerXMm)
        {
            double[] phaseOffsetsMm = { -30.0, -10.0, 10.0 };
            for (int i = 0; i < PhaseNames.Length; i++)
            {
                AddPoint(
                    points,
                    componentName,
                    PhaseNames[i] + "_IN",
                    centerXMm + phaseOffsetsMm[i],
                    0.0,
                    -400.0);
            }

            AddPoint(points, componentName, "N_IN", centerXMm + 30.0, 0.0, -400.0);
        }

        private static void AddPoint(
            List<FoundPoint> points,
            string componentName,
            string pointName,
            double xMm,
            double yMm,
            double zMm)
        {
            points.Add(new FoundPoint
            {
                ComponentName = componentName,
                PointName = pointName,
                Position = new Point3(xMm / 1000.0, yMm / 1000.0, zMm / 1000.0)
            });
        }
    }
}
