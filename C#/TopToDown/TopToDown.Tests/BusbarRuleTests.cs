using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

using BusbarAutomation.Application;

using BusbarAutomation.Cad.SolidWorks;

using BusbarAutomation.Cli;

using BusbarAutomation.Core.Domain;

using BusbarAutomation.Core.Planning;

using BusbarAutomation.Core.Rules;

namespace BusbarAutomation.Tests
{
    [TestClass]
    public class GenerationOptionsParserTests
    {
        [TestMethod]
        public void UnknownOptionIsRejected()
        {
            Assert.ThrowsException<System.ArgumentException>(() =>
                GenerationOptionsParser.Parse(new[] { "--validte" }));
        }

        [TestMethod]
        public void ConflictingModesAreRejected()
        {
            Assert.ThrowsException<System.ArgumentException>(() =>
                GenerationOptionsParser.Parse(new[] { "--validate", "--export-report" }));
        }

        [TestMethod]
        public void OnlyNamesAreTrimmedForGeneration()
        {
            GenerationOptions options = GenerationOptionsParser.Parse(
                new[] { "--only= Busbar_A_MainFeed,Busbar_B_MainFeed " });

            CollectionAssert.AreEqual(
                new[] { "Busbar_A_MainFeed", "Busbar_B_MainFeed" },
                options.OnlyBusbarNames);
        }

        [TestMethod]
        public void KeepExistingCanBeCombinedWithPartialGeneration()
        {
            GenerationOptions options = GenerationOptionsParser.Parse(
                new[] { "--keep-existing", "--only=Busbar_A_Collector" });

            Assert.IsFalse(options.ReplaceExistingBusbar);
            CollectionAssert.AreEqual(new[] { "Busbar_A_Collector" }, options.OnlyBusbarNames);
        }
    }

    [TestClass]
    public class GeneratedComponentManagerTests
    {
        [TestMethod]
        public void PartialReplacementDeletesOnlyTheSelectedBusbar()
        {
            HashSet<string> retained = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "Busbar_A_Collector_SheetMetal_new-1"
            };
            HashSet<string> selected = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "Busbar_A_Collector"
            };

            Assert.IsFalse(GeneratedComponentManager.ShouldDeleteExistingBusbar(
                "Busbar_A_Collector_SheetMetal_new-1", retained, selected));
            Assert.IsTrue(GeneratedComponentManager.ShouldDeleteExistingBusbar(
                "Busbar_A_Collector_SheetMetal_old-1", retained, selected));
            Assert.IsFalse(GeneratedComponentManager.ShouldDeleteExistingBusbar(
                "Busbar_B_Collector_SheetMetal_old-1", retained, selected));
        }

        [TestMethod]
        public void FullReplacementDeletesEveryOldGeneratedBusbar()
        {
            Assert.IsTrue(GeneratedComponentManager.ShouldDeleteExistingBusbar(
                "Busbar_B_Collector-1", null, null));
            Assert.IsFalse(GeneratedComponentManager.ShouldDeleteExistingBusbar(
                "PGM8LZ-400-1", null, null));
        }
    }

    [TestClass]
    public class BusbarOverlapRuleMatrixTests
    {
        [DataTestMethod]
        [DataRow(30.0, 30.0, (int)BusbarOverlapHolePattern.Single, 11.0)]
        [DataRow(30.0, 60.0, (int)BusbarOverlapHolePattern.StraightDouble, 11.0)]
        [DataRow(60.0, 60.0, (int)BusbarOverlapHolePattern.DiagonalDouble, 13.0)]
        public void ApprovedCombinationResolves(
            double firstWidthMm,
            double secondWidthMm,
            int expectedPattern,
            double expectedDiameterMm)
        {
            BusbarOverlapHoleRule rule;

            bool resolved = BusbarOverlapRuleMatrix.TryResolve(firstWidthMm, secondWidthMm, out rule);

            Assert.IsTrue(resolved);
            Assert.AreEqual((BusbarOverlapHolePattern)expectedPattern, rule.Pattern);
            Assert.AreEqual(expectedDiameterMm, rule.HoleDiameterMm, 0.001);
        }

        [DataTestMethod]
        [DataRow(20.0, 60.0)]
        [DataRow(30.4, 60.0)]
        public void UnapprovedCombinationIsRejected(double firstWidthMm, double secondWidthMm)
        {
            BusbarOverlapHoleRule rule;

            bool resolved = BusbarOverlapRuleMatrix.TryResolve(firstWidthMm, secondWidthMm, out rule);

            Assert.IsFalse(resolved);
            Assert.IsNull(rule);
        }
    }

    [TestClass]
    public class DeviceRecognitionTests
    {
        [TestMethod]
        public void FuseRecognitionUsesStandardNameWhenBothDevicesHaveInAndOutPoints()
        {
            List<FoundPoint> points = new List<FoundPoint>();
            AddPhasePoints(points, "HR6-630-1", includeInputs: true);
            AddPhasePoints(points, "PGM8LZ-400-1", includeInputs: true);

            AssemblySnapshot snapshot = AssemblySnapshotFactory.FromFoundPoints(
                points,
                new[] { "A", "B", "C" },
                new[] { 400 });

            Assert.AreEqual("HR6-630-1", snapshot.Fuse.SourceComponentName);
        }

        [TestMethod]
        public void MultipleRecognizedFuseComponentsAreRejected()
        {
            List<FoundPoint> points = new List<FoundPoint>();
            AddPhasePoints(points, "HR6-630-1", includeInputs: true);
            AddPhasePoints(points, "fuse-backup-1", includeInputs: false);

            Assert.ThrowsException<System.Exception>(() =>
                AssemblySnapshotFactory.FromFoundPoints(
                    points,
                    new[] { "A", "B", "C" },
                    new[] { 400 }));
        }

        private static void AddPhasePoints(List<FoundPoint> points, string componentName, bool includeInputs)
        {
            foreach (string phase in new[] { "A", "B", "C" })
            {
                points.Add(new FoundPoint
                {
                    ComponentName = componentName,
                    PointName = phase + "_OUT",
                    Position = new Point3(0.0, 0.0, 0.0)
                });

                if (includeInputs)
                {
                    points.Add(new FoundPoint
                    {
                        ComponentName = componentName,
                        PointName = phase + "_IN",
                        Position = new Point3(0.0, 0.0, 0.0)
                    });
                }
            }
        }
    }

    [TestClass]
    public class ContactTopologyResolverTests
    {
        [TestMethod]
        public void LowerLegWithNonePolicyDoesNotReceiveASecondThicknessOffset()
        {
            ConnectionPort start = Port("Start", new Point3(0.0, 0.0, 0.0));
            ConnectionPort end = Port("End", new Point3(0.0, 0.1, 0.0));
            Busbar busbar = new Busbar
            {
                Name = "Busbar_A_Branch_1_Lower",
                Kind = BusbarKind.Branch,
                BranchLegRole = BranchLegRole.Lower,
                Profile = new BusbarProfile(30.0, 4.0),
                StartPort = start,
                EndPort = end,
                Routing = new BusbarRoutingOptions
                {
                    TransitionPolicy = ThicknessTransitionPolicy.None
                },
                LogicalCenterline = new List<Point3> { start.HoleCenter, end.HoleCenter }
            };

            List<Point3> sketch = new ContactTopologyResolver().CreateSheetMetalSketchLine(busbar);

            Assert.AreEqual(2, sketch.Count);
            Assert.AreEqual(0.0, sketch[0].DistanceTo(start.HoleCenter), 0.0000001);
            Assert.AreEqual(0.0, sketch[1].DistanceTo(end.HoleCenter), 0.0000001);
        }

        private static ConnectionPort Port(string name, Point3 center)
        {
            return new ConnectionPort
            {
                Name = name,
                HoleCenter = center,
                RequiredFace = ContactFace.Upper,
                EndMarginMm = 0.0
            };
        }
    }

    [TestClass]
    public class FastenerPlanBuilderTests
    {
        [TestMethod]
        public void ConnectionsInsideCoordinateToleranceShareOneJoint()
        {
            BusbarDesignPlan design = new BusbarDesignPlan();
            design.Busbars.Add(Branch("Busbar_A_Branch_1_Lower", 0.0));
            design.Busbars.Add(Branch("Busbar_A_Branch_1_Upper", 0.000009));
            BusbarManufacturingPlan plan = new BusbarManufacturingPlan { Design = design };
            BusbarSettings settings = new BusbarSettings
            {
                CollectorThicknessMm = 8.0,
                MinimumThreadProjectionMm = 3.0,
                FastenerCatalog = new List<FastenerSpec>
                {
                    new FastenerSpec("M10", 11.0, 2.5, 2.0, 20.0, 8.0, 6.0, 18.0, 30.0, 35.0, 40.0)
                }
            };

            FastenerPlanBuilder.BuildCollectorJoints(plan, settings);

            Assert.AreEqual(1, plan.FastenerJoints.Count);
            Assert.AreEqual(16.0, plan.FastenerJoints[0].ClampedThicknessMm, 0.001);
            Assert.IsTrue(plan.FastenerJoints[0].IsValid);
        }

        private static Busbar Branch(string name, double xOffsetMeters)
        {
            ConnectionPort port = new ConnectionPort
            {
                Name = name + "_Tap",
                Kind = PortKind.CollectorTap,
                RequiredFace = ContactFace.Upper,
                HoleCenter = new Point3(xOffsetMeters, 0.5, 0.2),
                HoleDiameterMm = 11.0
            };

            return new Busbar
            {
                Name = name,
                Kind = BusbarKind.Branch,
                Profile = new BusbarProfile(30.0, 4.0),
                MountingPorts = new List<ConnectionPort> { port }
            };
        }
    }
}
