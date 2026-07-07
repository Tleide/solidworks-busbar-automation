using System;

namespace SwFeatureDebug
{
    internal class SheetMetalOpenProfilePlane
    {
        public string BasePlaneRole;
        public double Offset;
        public AxisDirection SketchAxis1;
        public AxisDirection SketchAxis2;

        public SheetMetalOpenProfilePlane(string basePlaneRole, double offset, AxisDirection sketchAxis1, AxisDirection sketchAxis2)
        {
            BasePlaneRole = basePlaneRole;
            Offset = offset;
            SketchAxis1 = sketchAxis1;
            SketchAxis2 = sketchAxis2;
        }
    }
}