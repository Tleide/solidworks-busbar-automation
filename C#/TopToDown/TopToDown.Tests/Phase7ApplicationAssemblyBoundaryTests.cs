using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

using BusbarAutomation.Application;

namespace BusbarAutomation.Tests
{
    [TestClass]
    public class Phase7ApplicationAssemblyBoundaryTests
    {
        [TestMethod]
        public void ApplicationAssemblyDependsOnCoreButNotCad()
        {
            string applicationProject = File.ReadAllText(Path.Combine(
                GetSolutionRoot(),
                "BusbarAutomation.Application",
                "BusbarAutomation.Application.csproj"));

            StringAssert.Contains(
                applicationProject,
                "ProjectReference Include=\"..\\BusbarAutomation.Core\\BusbarAutomation.Core.csproj\"");
            Assert.IsFalse(applicationProject.Contains("SolidWorks.Interop"));
            Assert.IsFalse(applicationProject.Contains("ReferenceDLL"));

            string[] referencedAssemblies = typeof(BusbarPlanningWorkflow).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            CollectionAssert.Contains(referencedAssemblies, "BusbarAutomation.Core");
            CollectionAssert.DoesNotContain(referencedAssemblies, "SolidWorks.Interop.sldworks");
            CollectionAssert.DoesNotContain(referencedAssemblies, "TopToDown");
        }

        [TestMethod]
        public void ExecutableReferencesApplicationWithoutCompilingAppSources()
        {
            string executableProject = File.ReadAllText(Path.Combine(
                GetSolutionRoot(),
                "TopToDown",
                "TopToDown.csproj"));

            StringAssert.Contains(
                executableProject,
                "ProjectReference Include=\"..\\BusbarAutomation.Application\\BusbarAutomation.Application.csproj\"");
            Assert.IsFalse(executableProject.Contains("Compile Include=\"App\\"));
        }

        private static string GetSolutionRoot([CallerFilePath] string testSourcePath = null)
        {
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testSourcePath), ".."));
        }
    }
}
