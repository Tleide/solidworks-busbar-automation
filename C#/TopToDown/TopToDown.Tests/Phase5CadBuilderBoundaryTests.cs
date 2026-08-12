using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

using BusbarAutomation.Application;
using BusbarAutomation.Cad.SolidWorks;
using BusbarAutomation.Core.Domain;
using BusbarAutomation.Core.Planning;

namespace BusbarAutomation.Tests
{
    [TestClass]
    public class Phase5CadBuilderBoundaryTests
    {
        private static readonly string[] PhaseNames = { "A", "B", "C" };

        [TestMethod]
        public void CompleteBatchUsesStableManufacturingOrder()
        {
            SolidWorksBusbarBatchGenerator generator = CreateGenerator(null);

            List<string> actual = generator.SelectBusbars(CreatePlan())
                .Select(busbar => busbar.Name)
                .ToList();

            CollectionAssert.AreEqual(new[]
            {
                "Busbar_A_MainFeed",
                "Busbar_B_MainFeed",
                "Busbar_C_MainFeed",
                "Busbar_A_Collector",
                "Busbar_B_Collector",
                "Busbar_C_Collector",
                "Busbar_N_Collector",
                "Busbar_A_Branch_1_Lower",
                "Busbar_A_Branch_1_Upper",
                "Busbar_A_Branch_2_Lower",
                "Busbar_A_Branch_2_Upper",
                "Busbar_A_Branch_3_Lower",
                "Busbar_A_Branch_3_Upper",
                "Busbar_B_Branch_1_Lower",
                "Busbar_B_Branch_1_Upper",
                "Busbar_B_Branch_2_Lower",
                "Busbar_B_Branch_2_Upper",
                "Busbar_B_Branch_3_Lower",
                "Busbar_B_Branch_3_Upper",
                "Busbar_C_Branch_1_Lower",
                "Busbar_C_Branch_1_Upper",
                "Busbar_C_Branch_2_Lower",
                "Busbar_C_Branch_2_Upper",
                "Busbar_C_Branch_3_Lower",
                "Busbar_C_Branch_3_Upper",
                "Busbar_N_Branch_1",
                "Busbar_N_Branch_2",
                "Busbar_N_Branch_3"
            }, actual);
        }

        [TestMethod]
        public void OnlyFilterKeepsCanonicalBatchOrder()
        {
            SolidWorksBusbarBatchGenerator generator = CreateGenerator(new[]
            {
                "busbar_n_branch_2",
                "Busbar_C_Branch_1_Upper",
                "BUSBAR_A_COLLECTOR"
            });

            List<string> actual = generator.SelectBusbars(CreatePlan())
                .Select(busbar => busbar.Name)
                .ToList();

            CollectionAssert.AreEqual(new[]
            {
                "Busbar_A_Collector",
                "Busbar_C_Branch_1_Upper",
                "Busbar_N_Branch_2"
            }, actual);
        }

        private static SolidWorksBusbarBatchGenerator CreateGenerator(string[] onlyBusbarNames)
        {
            return new SolidWorksBusbarBatchGenerator(
                true,
                onlyBusbarNames,
                PhaseNames,
                "N",
                report => { },
                new SolidWorksBusbarPartBuilder());
        }

        private static BusbarManufacturingPlan CreatePlan()
        {
            BusbarPlanningWorkflow workflow = new BusbarPlanningWorkflow(
                RepresentativeAssemblyFixture.CreateSettings(),
                PhaseNames);
            BusbarPlanningResult result = workflow.Build(
                RepresentativeAssemblyFixture.CreatePoints(),
                "RepresentativeAssembly");

            Assert.IsFalse(result.Report.HasErrors);
            return result.Plan;
        }
    }
}
