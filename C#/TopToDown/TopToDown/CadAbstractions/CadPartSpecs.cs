using System.Collections.Generic;

namespace SwFeatureDebug
{
    internal class SheetMetalPartSpec
    {
        public string PartName { get; set; }
        public BusbarKind Kind { get; set; }
        public BusbarProfile Profile { get; set; }
        public double BendRadiusMm { get; set; }
        public double KFactor { get; set; }
        public List<Point3> Centerline { get; } = new List<Point3>();
        public List<HoleSpec> Holes { get; } = new List<HoleSpec>();
    }

    internal class HoleSpec
    {
        public string Name { get; set; }
        public Point3 Center { get; set; }
        public double DiameterMm { get; set; }
        public ContactFace Face { get; set; }
    }
}
