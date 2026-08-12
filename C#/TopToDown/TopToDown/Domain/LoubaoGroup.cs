using System;

namespace BusbarAutomation.Core.Domain
{
    internal class LoubaoGroup
    {
        public string ComponentName;
        public double CenterX;
        public int RatedCurrentA;
        public BusbarProfile BranchProfile;
        public BranchArrangement BranchArrangement;
        public BusbarProfile NeutralBranchProfile;
        public BranchArrangement NeutralBranchArrangement;
    }
}
