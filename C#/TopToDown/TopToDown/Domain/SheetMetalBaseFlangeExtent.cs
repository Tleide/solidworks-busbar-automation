using System;

namespace SwFeatureDebug
{
    internal class SheetMetalBaseFlangeExtent
    {
        public double Dist1;
        public double Dist2;
        public bool FlipExtrudeDirection;
        public int EndCondition1;
        public int EndCondition2;
        public int DirToUse;
        public string ModeLabel;

        public SheetMetalBaseFlangeExtent(
            double dist1,
            double dist2,
            bool flipExtrudeDirection,
            int endCondition1,
            int endCondition2,
            int dirToUse,
            string modeLabel)
        {
            Dist1 = dist1;
            Dist2 = dist2;
            FlipExtrudeDirection = flipExtrudeDirection;
            EndCondition1 = endCondition1;
            EndCondition2 = endCondition2;
            DirToUse = dirToUse;
            ModeLabel = modeLabel;
        }
    }
}