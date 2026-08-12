using System;

namespace BusbarAutomation.Cad.SolidWorks
{
    internal sealed partial class SolidWorksBusbarPartBuilder
    {
        private static bool SameText(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static double Mm(double value)
        {
            return value / 1000.0;
        }

        private static double ToMm(double value)
        {
            return value * 1000.0;
        }
    }
}
