using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

using BusbarAutomation.Application;
using BusbarAutomation.Core.Domain;
using BusbarAutomation.Core.Planning;

namespace BusbarAutomation.Tests
{
    [TestClass]
    public class Phase3PlanningBoundaryTests
    {
        private static readonly string[] PhaseNames = { "A", "B", "C" };

        [TestMethod]
        public void DesignPlanContainsNoManufacturingDerivedData()
        {
            EngineeringConfigurationSnapshot configuration = CreateConfiguration();
            BusbarDesignPlan design = CreateDesignPlan(configuration);

            Assert.AreEqual(28, design.Busbars.Count);
            Assert.IsTrue(design.Busbars.All(busbar => busbar.SheetMetal == null));
            Assert.IsTrue(design.Busbars.All(busbar => busbar.SheetMetalSketchLine.Count == 0));
        }

        [TestMethod]
        public void ManufacturingPlannerCompletesEveryBusbarAndFastenerJoint()
        {
            EngineeringConfigurationSnapshot configuration = CreateConfiguration();
            BusbarDesignPlan design = CreateDesignPlan(configuration);

            BusbarManufacturingPlan manufacturing = BusbarManufacturingPlanner.Build(design, configuration);

            Assert.AreSame(design, manufacturing.Design);
            Assert.AreEqual(28, manufacturing.Busbars.Count);
            Assert.IsTrue(manufacturing.Busbars.All(busbar => busbar.SheetMetal != null));
            Assert.IsTrue(manufacturing.Busbars.All(busbar => busbar.SheetMetalSketchLine.Count >= 2));
            Assert.AreEqual(30, manufacturing.FastenerJoints.Count);
            Assert.IsTrue(manufacturing.FastenerJoints.All(joint => joint.IsValid));
        }

        [TestMethod]
        public void ManufacturingPlannerCanRebuildWithoutAccumulatingDerivedData()
        {
            EngineeringConfigurationSnapshot configuration = CreateConfiguration();
            BusbarDesignPlan design = CreateDesignPlan(configuration);

            BusbarManufacturingPlan first = BusbarManufacturingPlanner.Build(design, configuration);
            string firstSnapshot = BusbarPlanSnapshotFormatter.Format(first);
            BusbarManufacturingPlan second = BusbarManufacturingPlanner.Build(design, configuration);

            Assert.AreEqual(firstSnapshot, BusbarPlanSnapshotFormatter.Format(second));
            Assert.AreEqual(30, second.FastenerJoints.Count);
        }

        private static EngineeringConfigurationSnapshot CreateConfiguration()
        {
            return EngineeringConfigurationSnapshot.FromSettings(
                RepresentativeAssemblyFixture.CreateSettings(),
                "phase3-test");
        }

        private static BusbarDesignPlan CreateDesignPlan(EngineeringConfigurationSnapshot configuration)
        {
            AssemblySnapshot assembly = AssemblySnapshotFactory.FromFoundPoints(
                RepresentativeAssemblyFixture.CreatePoints(),
                PhaseNames,
                configuration.GetSupportedRatedCurrents(),
                "RepresentativeAssembly");
            return BusbarPlanBuilder.BuildDesignPlan(assembly, PhaseNames, configuration);
        }
    }
}
