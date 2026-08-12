using System.Collections.Generic;

using BusbarAutomation.Core.Domain;
using BusbarAutomation.Core.Rules;

namespace BusbarAutomation.Core.Planning
{
    internal class BusbarDesignPlan
    {
        public ManualBusbarRuleSet Rules;
        public string FuseComponentName;
        public List<LoubaoGroup> Loubaos = new List<LoubaoGroup>();
        public List<CollectorLayout> Collectors = new List<CollectorLayout>();
        public List<Busbar> Busbars = new List<Busbar>();
    }

    internal class BusbarManufacturingPlan
    {
        public BusbarDesignPlan Design;
        public List<FastenerJointPlan> FastenerJoints = new List<FastenerJointPlan>();

        public ManualBusbarRuleSet Rules { get { return Design.Rules; } }
        public string FuseComponentName { get { return Design.FuseComponentName; } }
        public List<LoubaoGroup> Loubaos { get { return Design.Loubaos; } }
        public List<CollectorLayout> Collectors { get { return Design.Collectors; } }
        public List<Busbar> Busbars { get { return Design.Busbars; } }
    }
}
