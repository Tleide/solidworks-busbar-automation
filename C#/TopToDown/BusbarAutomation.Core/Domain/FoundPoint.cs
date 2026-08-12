using System;

namespace BusbarAutomation.Core.Domain
{
    internal class FoundPoint
    {
        public string ComponentName;
        public string PointName;
        public Point3 Position;

        public override string ToString()
        {
            return ComponentName + "." + PointName + " " + Position.ToMillimeterText();
        }
    }
}
