using System;

namespace BusbarAutomation.Core.Domain
{
    internal enum BusbarOverlapHolePattern
    {
        Single,
        StraightDouble,
        DiagonalDouble
    }

    internal class BusbarOverlapHoleRule
    {
        public BusbarOverlapHolePattern Pattern;
        public double HoleDiameterMm;
        public double OffsetMm;
        public string SourceCode;

        public BusbarOverlapHoleRule(BusbarOverlapHolePattern pattern, double holeDiameterMm, double offsetMm, string sourceCode)
        {
            Pattern = pattern;
            HoleDiameterMm = holeDiameterMm;
            OffsetMm = offsetMm;
            SourceCode = sourceCode;
        }

        public BusbarOverlapHoleRule Clone()
        {
            return new BusbarOverlapHoleRule(Pattern, HoleDiameterMm, OffsetMm, SourceCode);
        }
    }
}
