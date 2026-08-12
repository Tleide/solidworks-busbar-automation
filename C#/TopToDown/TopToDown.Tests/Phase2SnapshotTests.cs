using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

using BusbarAutomation.Application;
using BusbarAutomation.Core.Domain;
using BusbarAutomation.Core.Planning;

namespace BusbarAutomation.Tests
{
    [TestClass]
    public class Phase2SnapshotTests
    {
        private static readonly string[] PhaseNames = { "A", "B", "C" };

        [TestMethod]
        public void AssemblySnapshotNormalizesDeviceIdentityCurrentAndPorts()
        {
            BusbarSettings settings = RepresentativeAssemblyFixture.CreateSettings();
            EngineeringConfigurationSnapshot configuration =
                EngineeringConfigurationSnapshot.FromSettings(settings);

            AssemblySnapshot snapshot = AssemblySnapshotFactory.FromFoundPoints(
                RepresentativeAssemblyFixture.CreatePoints(),
                PhaseNames,
                configuration.GetSupportedRatedCurrents(),
                "fixture://representative");

            Assert.AreEqual("fixture://representative", snapshot.SourceId);
            Assert.AreEqual("HR6-630-1", snapshot.Fuse.SourceComponentName);
            CollectionAssert.AreEqual(
                new[] { "PGM8LZ-630-1", "PGM8LZ-400-2", "PGM8LZ-400-3" },
                snapshot.Breakers.Select(device => device.SourceComponentName).ToArray());
            CollectionAssert.AreEqual(
                new[] { 630, 400, 400 },
                snapshot.Breakers.Select(device => device.RatedCurrentA.Value).ToArray());
            Assert.AreEqual(-0.230, snapshot.Breakers[0].GetRequiredPort("A_IN").Position.X, 0.000001);
            Assert.AreEqual(-0.170, snapshot.Breakers[0].GetRequiredPort("N_IN").Position.X, 0.000001);
        }

        [TestMethod]
        public void ConfigurationSnapshotIsIsolatedFromSourceSettingsMutation()
        {
            BusbarSettings settings = RepresentativeAssemblyFixture.CreateSettings();
            EngineeringConfigurationSnapshot snapshot =
                EngineeringConfigurationSnapshot.FromSettings(settings, "test-v1");

            settings.MainFeedWidthMm = 999.0;
            settings.PhaseBranchRules[1].Profile.WidthMm = 999.0;
            settings.FastenerCatalog[1].StandardLengthsMm.Clear();

            Assert.AreEqual("test-v1", snapshot.VersionId);
            Assert.AreEqual(60.0, snapshot.MainFeedProfile.WidthMm, 0.001);

            AssemblySnapshot assembly = AssemblySnapshotFactory.FromFoundPoints(
                RepresentativeAssemblyFixture.CreatePoints(),
                PhaseNames,
                snapshot.GetSupportedRatedCurrents());
            BusbarPlan plan = BusbarPlanBuilder.BuildPlan(assembly, PhaseNames, snapshot);

            Assert.AreEqual(28, plan.Busbars.Count);
            Assert.IsTrue(plan.Loubaos
                .Where(group => group.RatedCurrentA == 400)
                .All(group => group.BranchProfile.WidthMm == 30.0));
            Assert.IsTrue(plan.FastenerJoints.All(joint => joint.IsValid));
        }

        [TestMethod]
        public void PlannerUsesSnapshotCurrentInsteadOfParsingComponentName()
        {
            BusbarSettings settings = RepresentativeAssemblyFixture.CreateSettings();
            EngineeringConfigurationSnapshot configuration =
                EngineeringConfigurationSnapshot.FromSettings(settings);
            AssemblySnapshot parsed = AssemblySnapshotFactory.FromFoundPoints(
                RepresentativeAssemblyFixture.CreatePoints(),
                PhaseNames,
                configuration.GetSupportedRatedCurrents());

            List<AssemblyDeviceSnapshot> renamedBreakers = parsed.Breakers
                .Select((device, index) => new AssemblyDeviceSnapshot(
                    "breaker-" + (index + 1),
                    "Breaker-" + (index + 1),
                    AssemblyDeviceKind.Breaker,
                    device.RatedCurrentA,
                    device.Ports.Select(port => new AssemblyPortSnapshot(port.Name, port.Position))))
                .ToList();
            AssemblySnapshot normalized = new AssemblySnapshot(
                "normalized-input",
                parsed.Fuse,
                renamedBreakers);

            BusbarPlan plan = BusbarPlanBuilder.BuildPlan(normalized, PhaseNames, configuration);

            CollectionAssert.AreEqual(
                new[] { 630, 400, 400 },
                plan.Loubaos.Select(group => group.RatedCurrentA).ToArray());
            Assert.AreEqual(28, plan.Busbars.Count);
        }

        [TestMethod]
        public void OverlapRuleCatalogReturnsIndependentRuleCopies()
        {
            EngineeringConfigurationSnapshot configuration =
                EngineeringConfigurationSnapshot.FromSettings(RepresentativeAssemblyFixture.CreateSettings());

            BusbarOverlapHoleRule first;
            BusbarOverlapHoleRule second;
            Assert.IsTrue(configuration.OverlapRules.TryResolve(60.0, 60.0, out first));
            first.HoleDiameterMm = 999.0;
            Assert.IsTrue(configuration.OverlapRules.TryResolve(60.0, 60.0, out second));

            Assert.AreEqual(BusbarOverlapHolePattern.DiagonalDouble, second.Pattern);
            Assert.AreEqual(13.0, second.HoleDiameterMm, 0.001);
            Assert.AreEqual(12.0, second.OffsetMm, 0.001);
        }
    }
}
