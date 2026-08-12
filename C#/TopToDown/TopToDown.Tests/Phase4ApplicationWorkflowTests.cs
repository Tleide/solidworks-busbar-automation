using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

using BusbarAutomation.Application;
using BusbarAutomation.Core.Domain;

namespace BusbarAutomation.Tests
{
    [TestClass]
    public class Phase4ApplicationWorkflowTests
    {
        private static readonly string[] PhaseNames = { "A", "B", "C" };

        [TestMethod]
        public void ValidInputBuildsManufacturingPlanAndCombinedPreflightReport()
        {
            BusbarPlanningWorkflow workflow = new BusbarPlanningWorkflow(
                RepresentativeAssemblyFixture.CreateSettings(),
                PhaseNames);

            BusbarPlanningResult result = workflow.Build(
                RepresentativeAssemblyFixture.CreatePoints(),
                "RepresentativeAssembly");

            Assert.IsTrue(workflow.CanBuild);
            Assert.IsNotNull(result.Plan);
            Assert.AreEqual(28, result.Plan.Busbars.Count);
            Assert.AreEqual(30, result.Plan.FastenerJoints.Count);
            Assert.IsFalse(result.Report.HasErrors);
            Assert.AreEqual(60, result.Report.InfoCount);
        }

        [TestMethod]
        public void InvalidConfigurationStopsBeforePlanning()
        {
            BusbarSettings settings = RepresentativeAssemblyFixture.CreateSettings();
            settings.CollectorThicknessMm = 0.0;
            BusbarPlanningWorkflow workflow = new BusbarPlanningWorkflow(settings, PhaseNames);

            BusbarPlanningResult result = workflow.Build(
                RepresentativeAssemblyFixture.CreatePoints(),
                "RepresentativeAssembly");

            Assert.IsFalse(workflow.CanBuild);
            Assert.IsNull(result.Plan);
            Assert.IsTrue(result.Report.HasErrors);
        }

        [TestMethod]
        public void PlanningFailureIsReturnedAsPreflightError()
        {
            BusbarPlanningWorkflow workflow = new BusbarPlanningWorkflow(
                RepresentativeAssemblyFixture.CreateSettings(),
                PhaseNames);

            BusbarPlanningResult result = workflow.Build(
                new System.Collections.Generic.List<FoundPoint>(),
                "MissingAssemblyInput");

            Assert.IsNull(result.Plan);
            Assert.IsTrue(result.Report.HasErrors);
            Assert.AreEqual("Busbar preflight report", result.Report.Title);
        }

        [TestMethod]
        public void NonCadModulesDoNotReferenceSolidWorksInterop()
        {
            string sourceRoot = GetProductionSourceRoot();
            string[] independentDirectories = { "App", "Domain", "Planning", "Reporting", "Rules" };
            string[] violations = independentDirectories
                .SelectMany(directory => Directory.GetFiles(
                    Path.Combine(sourceRoot, directory),
                    "*.cs",
                    SearchOption.AllDirectories))
                .Where(path => File.ReadAllText(path).Contains("SolidWorks.Interop"))
                .Select(path => path.Substring(sourceRoot.Length).TrimStart(Path.DirectorySeparatorChar))
                .ToArray();

            Assert.AreEqual(
                0,
                violations.Length,
                "Non-CAD modules must not reference SolidWorks interop: " + string.Join(", ", violations));
        }

        private static string GetProductionSourceRoot([CallerFilePath] string testSourcePath = null)
        {
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testSourcePath), "..", "TopToDown"));
        }
    }
}
