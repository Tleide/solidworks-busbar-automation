using System;
using System.Collections.Generic;

namespace SwFeatureDebug
{
    internal static class BusbarDirectionResolver
    {
        public static Point3 ResolveLengthDirectionAtPort(Busbar busbar, ConnectionPort port, AxisDirection fallbackAxis)
        {
            Point3 direction = ResolveFromCenterline(busbar, port);
            direction = ProjectToFacePlane(direction, port.RequiredFace);
            direction = Normalize(direction);

            if (!IsZero(direction))
                return direction;

            return ResolveAxisDirection(fallbackAxis, port.RequiredFace);
        }

        public static Point3 ResolveAxisDirection(AxisDirection axis, ContactFace face)
        {
            Point3 direction = ProjectToFacePlane(AxisToVector(axis), face);
            direction = Normalize(direction);
            if (!IsZero(direction))
                return direction;

            return GetPlaneFirstAxis(face);
        }

        public static Point3 GetPlaneFirstAxis(ContactFace face)
        {
            if (face == ContactFace.Left || face == ContactFace.Right)
                return new Point3(0.0, 1.0, 0.0);

            return new Point3(1.0, 0.0, 0.0);
        }

        public static Point3 GetPlaneSecondAxis(ContactFace face)
        {
            if (face == ContactFace.Front || face == ContactFace.Back)
                return new Point3(0.0, 1.0, 0.0);

            return new Point3(0.0, 0.0, 1.0);
        }

        public static Point3 Add(Point3 first, Point3 second)
        {
            return new Point3(first.X + second.X, first.Y + second.Y, first.Z + second.Z);
        }

        public static Point3 Scale(Point3 vector, double scale)
        {
            return new Point3(vector.X * scale, vector.Y * scale, vector.Z * scale);
        }

        private static Point3 ResolveFromCenterline(Busbar busbar, ConnectionPort port)
        {
            if (busbar == null || port == null || busbar.LogicalCenterline == null || busbar.LogicalCenterline.Count < 2)
                return new Point3(0.0, 0.0, 0.0);

            List<Point3> route = busbar.LogicalCenterline;
            Point3 start = route[0];
            Point3 end = route[route.Count - 1];

            if (port.HoleCenter.DistanceTo(start) <= Mm(0.1))
                return Subtract(route[1], route[0]);

            if (port.HoleCenter.DistanceTo(end) <= Mm(0.1))
                return Subtract(route[route.Count - 1], route[route.Count - 2]);

            if (busbar.Kind == BusbarKind.Collector)
                return Subtract(end, start);

            return FindNearestSegmentDirection(route, port.HoleCenter);
        }

        private static Point3 FindNearestSegmentDirection(List<Point3> route, Point3 point)
        {
            double bestDistance = double.MaxValue;
            Point3 bestDirection = new Point3(0.0, 0.0, 0.0);

            for (int i = 0; i < route.Count - 1; i++)
            {
                Point3 start = route[i];
                Point3 end = route[i + 1];
                Point3 direction = Subtract(end, start);
                double distance = DistancePointToSegment(point, start, end);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestDirection = direction;
                }
            }

            return bestDirection;
        }

        private static double DistancePointToSegment(Point3 point, Point3 start, Point3 end)
        {
            Point3 segment = Subtract(end, start);
            double lengthSquared = Dot(segment, segment);
            if (lengthSquared <= Mm(0.001) * Mm(0.001))
                return point.DistanceTo(start);

            double t = Dot(Subtract(point, start), segment) / lengthSquared;
            if (t < 0.0)
                t = 0.0;
            if (t > 1.0)
                t = 1.0;

            Point3 projection = Add(start, Scale(segment, t));
            return point.DistanceTo(projection);
        }

        private static Point3 ProjectToFacePlane(Point3 vector, ContactFace face)
        {
            if (face == ContactFace.Upper || face == ContactFace.Lower)
                return new Point3(vector.X, 0.0, vector.Z);

            if (face == ContactFace.Left || face == ContactFace.Right)
                return new Point3(0.0, vector.Y, vector.Z);

            return new Point3(vector.X, vector.Y, 0.0);
        }

        private static Point3 AxisToVector(AxisDirection axis)
        {
            if (axis == AxisDirection.X)
                return new Point3(1.0, 0.0, 0.0);

            if (axis == AxisDirection.Y)
                return new Point3(0.0, 1.0, 0.0);

            return new Point3(0.0, 0.0, 1.0);
        }

        private static Point3 Subtract(Point3 first, Point3 second)
        {
            return new Point3(first.X - second.X, first.Y - second.Y, first.Z - second.Z);
        }

        private static double Dot(Point3 first, Point3 second)
        {
            return first.X * second.X + first.Y * second.Y + first.Z * second.Z;
        }

        private static Point3 Normalize(Point3 vector)
        {
            double length = Math.Sqrt(Dot(vector, vector));
            if (length <= Mm(0.001))
                return new Point3(0.0, 0.0, 0.0);

            return new Point3(vector.X / length, vector.Y / length, vector.Z / length);
        }

        private static bool IsZero(Point3 vector)
        {
            return Math.Abs(vector.X) <= Mm(0.001) &&
                Math.Abs(vector.Y) <= Mm(0.001) &&
                Math.Abs(vector.Z) <= Mm(0.001);
        }

        private static double Mm(double value)
        {
            return value / 1000.0;
        }
    }
}
