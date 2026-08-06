using System;
using System.Collections.Generic;

namespace SwFeatureDebug
{
    internal class ContactTopologyResolver
    {
        public List<Point3> CreateSheetMetalSketchLine(Busbar busbar)
        {
            List<Point3> sketchLine = AddEndMargins(busbar);
            ApplyThicknessTransition(busbar, sketchLine);
            return sketchLine;
        }

        public ContactTopologyKind Resolve(Busbar busbar)
        {
            if (busbar == null || busbar.StartPort == null || busbar.EndPort == null)
                return ContactTopologyKind.SameSide;

            if (busbar.Kind == BusbarKind.MainFeed)
                return ResolveMainFeedTopology(busbar);

            if (busbar.Kind == BusbarKind.Branch)
                return busbar.BranchLegRole == BranchLegRole.Lower
                    ? ContactTopologyKind.DifferentSide
                    : ContactTopologyKind.SameSide;

            return ContactTopologyKind.SameSide;
        }

        private static ContactTopologyKind ResolveMainFeedTopology(Busbar busbar)
        {
            bool collectorUpper = busbar.EndPort.RequiredFace == ContactFace.Upper;
            bool collectorLower = busbar.EndPort.RequiredFace == ContactFace.Lower;
            bool fuseOnBackSide = busbar.StartPort.RequiredFace == ContactFace.Back;
            bool fuseOnFrontSide = busbar.StartPort.RequiredFace == ContactFace.Front;

            if (fuseOnBackSide && collectorUpper)
                return ContactTopologyKind.DifferentSide;

            if (fuseOnBackSide && collectorLower)
                return ContactTopologyKind.SameSide;

            if (fuseOnFrontSide && collectorUpper)
                return ContactTopologyKind.SameSide;

            if (fuseOnFrontSide && collectorLower)
                return ContactTopologyKind.DifferentSide;

            return ContactTopologyKind.SameSide;
        }

        private static void ApplyThicknessTransition(Busbar busbar, List<Point3> sketchLine)
        {
            if (busbar == null || sketchLine == null || sketchLine.Count < 2)
                return;

            if (busbar.Routing != null && busbar.Routing.TransitionPolicy == ThicknessTransitionPolicy.None)
                return;

            ContactTopologyKind topology = ResolveForTransition(busbar);
            if (topology == ContactTopologyKind.SameSide)
                return;

            bool compensateStart = ShouldCompensateStart(busbar);
            int anchorIndex = compensateStart
                ? FindPointIndex(sketchLine, busbar.LogicalCenterline[0])
                : FindPointIndex(sketchLine, busbar.LogicalCenterline[busbar.LogicalCenterline.Count - 1]);

            if (anchorIndex < 0)
                return;

            Point3 tangent = GetEndpointTangent(sketchLine, anchorIndex, compensateStart);
            Point3 normal = ChooseThicknessNormal(tangent, compensateStart ? busbar.StartPort.RequiredFace : busbar.EndPort.RequiredFace);
            Point3 offset = Scale(normal, busbar.Profile.ThicknessMeters);

            if (offset.DistanceTo(new Point3(0, 0, 0)) <= Mm(0.001))
                return;

            MoveEndpointRun(sketchLine, anchorIndex, compensateStart, offset);

            Console.WriteLine(
                "Thickness topology transition [" + busbar.Name + "]: " +
                topology + ", compensate=" + (compensateStart ? "Start" : "End") +
                ", offset dX=" + ToMm(offset.X).ToString("F3") +
                " mm, dY=" + ToMm(offset.Y).ToString("F3") +
                " mm, dZ=" + ToMm(offset.Z).ToString("F3") + " mm");
        }

        private static ContactTopologyKind ResolveForTransition(Busbar busbar)
        {
            if (busbar.Kind == BusbarKind.MainFeed)
                return ResolveMainFeedTopology(busbar);

            if (busbar.Kind == BusbarKind.Branch)
                return busbar.BranchLegRole == BranchLegRole.Lower
                    ? ContactTopologyKind.DifferentSide
                    : ContactTopologyKind.SameSide;

            return ContactTopologyKind.SameSide;
        }

        private static bool ShouldCompensateStart(Busbar busbar)
        {
            if (busbar.Routing == null)
                return false;

            if (busbar.Routing.TransitionPolicy == ThicknessTransitionPolicy.PreferStartPort)
                return true;

            return false;
        }

        private static Point3 GetEndpointTangent(List<Point3> points, int anchorIndex, bool atStart)
        {
            if (atStart)
            {
                for (int i = anchorIndex + 1; i < points.Count; i++)
                {
                    Point3 tangent = Normalize(Subtract(points[i], points[anchorIndex]));
                    if (tangent.DistanceTo(new Point3(0, 0, 0)) > Mm(0.001))
                        return tangent;
                }
            }
            else
            {
                for (int i = anchorIndex - 1; i >= 0; i--)
                {
                    Point3 tangent = Normalize(Subtract(points[anchorIndex], points[i]));
                    if (tangent.DistanceTo(new Point3(0, 0, 0)) > Mm(0.001))
                        return tangent;
                }
            }

            return new Point3(0, 0, 0);
        }

        private static Point3 ChooseThicknessNormal(Point3 tangent, ContactFace face)
        {
            if (face == ContactFace.Upper)
                return new Point3(0, 1, 0);

            if (face == ContactFace.Lower)
                return new Point3(0, -1, 0);

            if (face == ContactFace.Back)
                return new Point3(0, 0, 1);

            if (face == ContactFace.Front)
                return new Point3(0, 0, -1);

            if (face == ContactFace.Left)
                return new Point3(1, 0, 0);

            if (face == ContactFace.Right)
                return new Point3(-1, 0, 0);

            return InferPlanarNormal(tangent);
        }

        private static Point3 InferPlanarNormal(Point3 tangent)
        {
            double ax = Math.Abs(tangent.X);
            double ay = Math.Abs(tangent.Y);
            double az = Math.Abs(tangent.Z);

            if (az >= ax && az >= ay)
                return new Point3(0, 1, 0);

            if (ay >= ax && ay >= az)
                return new Point3(0, 0, 1);

            return new Point3(0, 1, 0);
        }

        private static void MoveEndpointRun(List<Point3> points, int anchorIndex, bool atStart, Point3 offset)
        {
            if (atStart)
            {
                Point3 anchor = points[anchorIndex];
                for (int i = anchorIndex; i >= 0; i--)
                    points[i] = Add(points[i], offset);

                for (int i = anchorIndex + 1; i < points.Count; i++)
                {
                    if (!SharesTwoCoordinates(points[i], anchor))
                        break;

                    points[i] = Add(points[i], offset);
                }

                return;
            }

            Point3 anchorEnd = points[anchorIndex];
            for (int i = anchorIndex; i < points.Count; i++)
                points[i] = Add(points[i], offset);

            for (int i = anchorIndex - 1; i >= 0; i--)
            {
                if (!SharesTwoCoordinates(points[i], anchorEnd))
                    break;

                points[i] = Add(points[i], offset);
            }
        }

        private static bool SharesTwoCoordinates(Point3 a, Point3 b)
        {
            int same = 0;
            if (Math.Abs(a.X - b.X) <= Mm(0.01))
                same++;
            if (Math.Abs(a.Y - b.Y) <= Mm(0.01))
                same++;
            if (Math.Abs(a.Z - b.Z) <= Mm(0.01))
                same++;

            return same >= 2;
        }

        private static int FindPointIndex(List<Point3> points, Point3 target)
        {
            if (points == null)
                return -1;

            for (int i = 0; i < points.Count; i++)
            {
                if (points[i].DistanceTo(target) <= Mm(0.01))
                    return i;
            }

            return -1;
        }

        private static List<Point3> AddEndMargins(Busbar busbar)
        {
            List<Point3> route = busbar.LogicalCenterline;
            if (route == null || route.Count < 2)
                return route == null ? new List<Point3>() : new List<Point3>(route);

            Point3 startDirection = Normalize(Subtract(route[1], route[0]));
            Point3 endDirection = Normalize(Subtract(route[route.Count - 1], route[route.Count - 2]));

            double startMargin = Mm(busbar.StartPort.EndMarginMm);
            double endMargin = Mm(busbar.EndPort.EndMarginMm);

            Point3 startEnd = Add(route[0], Scale(startDirection, -startMargin));
            Point3 endEnd = Add(route[route.Count - 1], Scale(endDirection, endMargin));

            List<Point3> sketchLine = new List<Point3>();
            AddIfDifferent(sketchLine, startEnd);
            foreach (Point3 point in route)
                AddIfDifferent(sketchLine, point);
            AddIfDifferent(sketchLine, endEnd);

            return sketchLine;
        }

        private static void AddIfDifferent(List<Point3> points, Point3 point)
        {
            if (points.Count == 0 || points[points.Count - 1].DistanceTo(point) > Mm(0.01))
                points.Add(point);
        }

        private static Point3 Add(Point3 a, Point3 b)
        {
            return new Point3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        private static Point3 Subtract(Point3 a, Point3 b)
        {
            return new Point3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        private static Point3 Scale(Point3 vector, double scale)
        {
            return new Point3(vector.X * scale, vector.Y * scale, vector.Z * scale);
        }

        private static Point3 Normalize(Point3 vector)
        {
            double length = Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z);
            if (length <= Mm(0.001))
                return new Point3(0, 0, 0);

            return new Point3(vector.X / length, vector.Y / length, vector.Z / length);
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
