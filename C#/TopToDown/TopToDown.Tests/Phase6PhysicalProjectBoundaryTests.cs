using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

using BusbarAutomation.Core.Domain;

namespace BusbarAutomation.Tests
{
    [TestClass]
    public class Phase6PhysicalProjectBoundaryTests
    {
        [TestMethod]
        public void CoreProjectHasNoSolidWorksReference()
        {
            string coreProject = File.ReadAllText(Path.Combine(
                GetSolutionRoot(),
                "BusbarAutomation.Core",
                "BusbarAutomation.Core.csproj"));

            Assert.IsFalse(coreProject.Contains("SolidWorks.Interop"));
            Assert.IsFalse(coreProject.Contains("ReferenceDLL"));

            string[] referencedAssemblies = typeof(Point3).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            CollectionAssert.DoesNotContain(referencedAssemblies, "SolidWorks.Interop.sldworks");
            CollectionAssert.DoesNotContain(referencedAssemblies, "SolidWorks.Interop.swconst");
        }

        [TestMethod]
        public void ExecutableReferencesCoreWithoutCompilingCoreSources()
        {
            string executableProject = File.ReadAllText(Path.Combine(
                GetSolutionRoot(),
                "TopToDown",
                "TopToDown.csproj"));

            StringAssert.Contains(
                executableProject,
                "ProjectReference Include=\"..\\BusbarAutomation.Core\\BusbarAutomation.Core.csproj\"");
            Assert.IsFalse(executableProject.Contains("Compile Include=\"Domain\\"));
            Assert.IsFalse(executableProject.Contains("Compile Include=\"Planning\\"));
            Assert.IsFalse(executableProject.Contains("Compile Include=\"Rules\\"));
        }

        private static string GetSolutionRoot([CallerFilePath] string testSourcePath = null)
        {
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testSourcePath), ".."));
        }
    }
}
