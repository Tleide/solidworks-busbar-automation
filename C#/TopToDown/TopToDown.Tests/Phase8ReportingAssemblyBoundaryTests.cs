using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;

using BusbarAutomation.Application;
using BusbarAutomation.Reporting;

namespace BusbarAutomation.Tests
{
    [TestClass]
    public class Phase8ReportingAssemblyBoundaryTests
    {
        private static readonly string[] PhaseNames = { "A", "B", "C" };

        [TestMethod]
        public void ReportingAssemblyDependsOnCoreButNotCad()
        {
            string reportingProject = File.ReadAllText(Path.Combine(
                GetSolutionRoot(),
                "BusbarAutomation.Reporting",
                "BusbarAutomation.Reporting.csproj"));

            StringAssert.Contains(
                reportingProject,
                "ProjectReference Include=\"..\\BusbarAutomation.Core\\BusbarAutomation.Core.csproj\"");
            Assert.IsFalse(reportingProject.Contains("SolidWorks.Interop"));
            Assert.IsFalse(reportingProject.Contains("ReferenceDLL"));

            string[] referencedAssemblies = typeof(ProductionReportService).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            CollectionAssert.Contains(referencedAssemblies, "BusbarAutomation.Core");
            CollectionAssert.DoesNotContain(referencedAssemblies, "SolidWorks.Interop.sldworks");
            CollectionAssert.DoesNotContain(referencedAssemblies, "TopToDown");
        }

        [TestMethod]
        public void ExecutableReferencesReportingWithoutCompilingReportingSources()
        {
            string executableProject = File.ReadAllText(Path.Combine(
                GetSolutionRoot(),
                "TopToDown",
                "TopToDown.csproj"));

            StringAssert.Contains(
                executableProject,
                "ProjectReference Include=\"..\\BusbarAutomation.Reporting\\BusbarAutomation.Reporting.csproj\"");
            Assert.IsFalse(executableProject.Contains("Compile Include=\"Reporting\\"));
        }

        [TestMethod]
        public void RepresentativePlanExportsAReadableWorkbookPackage()
        {
            BusbarPlanningWorkflow workflow = new BusbarPlanningWorkflow(
                RepresentativeAssemblyFixture.CreateSettings(),
                PhaseNames);
            BusbarPlanningResult result = workflow.Build(
                RepresentativeAssemblyFixture.CreatePoints(),
                "RepresentativeAssembly");
            string outputDirectory = Path.Combine(
                Path.GetTempPath(),
                "BusbarAutomation.Reporting.Tests",
                Guid.NewGuid().ToString("N"));

            try
            {
                string reportPath = ProductionReportExporter.Export(result.Plan, outputDirectory);

                Assert.IsTrue(File.Exists(reportPath));
                using (ZipArchive archive = ZipFile.OpenRead(reportPath))
                {
                    Assert.IsNotNull(archive.GetEntry("xl/workbook.xml"));
                    Assert.AreEqual(7, archive.Entries.Count(entry =>
                        entry.FullName.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal) &&
                        entry.FullName.EndsWith(".xml", StringComparison.Ordinal)));
                }
            }
            finally
            {
                if (Directory.Exists(outputDirectory))
                    Directory.Delete(outputDirectory, true);
            }
        }

        private static string GetSolutionRoot([CallerFilePath] string testSourcePath = null)
        {
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testSourcePath), ".."));
        }
    }
}
