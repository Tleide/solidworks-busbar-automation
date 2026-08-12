using System;

namespace BusbarAutomation.Core.Domain
{
    internal class BranchBusbarRule
    {
        public int RatedCurrentA;
        public BusbarProfile Profile;
        public BranchArrangement Arrangement;

        public BranchBusbarRule(int ratedCurrentA, double widthMm, double thicknessMm, BranchArrangement arrangement)
        {
            RatedCurrentA = ratedCurrentA;
            Profile = new BusbarProfile(widthMm, thicknessMm);
            Arrangement = arrangement;
        }
    }
}
