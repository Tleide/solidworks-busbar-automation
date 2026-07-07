using System;

namespace SwFeatureDebug
{
    internal class BusbarProfile
    {
        public double WidthMm;
        public double ThicknessMm;

        public BusbarProfile(double widthMm, double thicknessMm)
        {
            WidthMm = widthMm;
            ThicknessMm = thicknessMm;
        }

        public double Width { get { return WidthMm / 1000.0; } }
        public double Thickness { get { return ThicknessMm / 1000.0; } }
        public double TerminalFaceOffset { get { return Thickness / 2.0; } }
        public string Label { get { return FormatMm(ThicknessMm) + "x" + FormatMm(WidthMm); } }

        private static string FormatMm(double value)
        {
            return value.ToString("0.###");
        }
    }
}
