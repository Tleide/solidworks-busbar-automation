using System;
using System.Collections.Generic;

using BusbarAutomation.Core.Domain;

namespace BusbarAutomation.Core.Planning
{
    internal class MainFeedRouteDecision
    {
        public double LeadOutY;
        public double ApproachZ;
        public string LeadOutRule;
        public string ApproachRule;
    }

    internal class BusbarRoutePlanner
    {
        private readonly BusbarSettings _settings;

        public BusbarRoutePlanner(BusbarSettings settings)
        {
            _settings = settings;
        }

        public List<Point3> CreateRoute(
            BusbarKind kind,
            BusbarProfile profile,
            ConnectionPort start,
            ConnectionPort end,
            BusbarRoutingOptions routing)
        {
            if (kind == BusbarKind.MainFeed)
                return CreateMainFeedRoute(profile, start, end);

            if (kind == BusbarKind.Branch && routing.BranchRouteMode == BranchRouteMode.DoubleClampOuterAvoidance)
                return CreateDoubleClampOuterAvoidanceRoute(profile, start, end);

            return CreateSimpleRoute(start, end, routing.AxisOrder);
        }

        private List<Point3> CreateDoubleClampOuterAvoidanceRoute(
            BusbarProfile profile,
            ConnectionPort start,
            ConnectionPort end)
        {
            Point3 p0 = start.HoleCenter;
            Point3 p4 = end.HoleCenter;
            double initialRise = _settings.DoubleClampOuterInitialRiseMeters;
            double minimumDiagonalLength = _settings.DoubleClampOuterDiagonalMinimumLengthMeters;
            double additionalOutsideOffsetZ = profile.ThicknessMeters * 2.0;

            if (initialRise <= 0.0)
                throw new Exception("Double-clamp outer initial rise must be greater than zero.");

            if (minimumDiagonalLength <= 0.0)
                throw new Exception("Double-clamp outer diagonal minimum length must be greater than zero.");

            if (minimumDiagonalLength <= additionalOutsideOffsetZ)
            {
                throw new Exception(
                    "Double-clamp outer diagonal minimum length must exceed its Z projection. " +
                    "Diagonal=" + ToMm(minimumDiagonalLength).ToString("0.###") +
                    "mm, Z projection=" + ToMm(additionalOutsideOffsetZ).ToString("0.###") + "mm.");
            }

            double diagonalRise = Math.Sqrt(
                minimumDiagonalLength * minimumDiagonalLength -
                additionalOutsideOffsetZ * additionalOutsideOffsetZ);
            double availableRise = p4.Y - p0.Y;
            double requiredRise = initialRise + diagonalRise;

            if (availableRise + Mm(0.01) < requiredRise)
            {
                throw new Exception(
                    "Double-clamp outer avoidance route has insufficient Y height. " +
                    "Available=" + ToMm(availableRise).ToString("0.###") +
                    "mm, required=" + ToMm(requiredRise).ToString("0.###") +
                    "mm (initial=" + ToMm(initialRise).ToString("0.###") +
                    ", diagonal projection=" + ToMm(diagonalRise).ToString("0.###") + ").");
            }

            Point3 p1 = new Point3(p0.X, p0.Y + initialRise, p0.Z);
            Point3 p2 = new Point3(p0.X, p1.Y + diagonalRise, p1.Z - additionalOutsideOffsetZ);
            Point3 p3 = new Point3(p0.X, p4.Y, p2.Z);

            List<Point3> points = new List<Point3>();
            Add(points, p0);
            Add(points, p1);
            Add(points, p2);
            Add(points, p3);
            Add(points, p4);

            Console.WriteLine(
                "Double-clamp outer avoidance [" + start.Name + " -> " + end.Name + "]: " +
                "initial Y+=" + ToMm(initialRise).ToString("0.###") +
                "mm, diagonal Y+=" + ToMm(diagonalRise).ToString("0.###") +
                "mm/Z-=" + ToMm(additionalOutsideOffsetZ).ToString("0.###") +
                "mm, final Y+=" + ToMm(p3.Y - p2.Y).ToString("0.###") + "mm.");

            return points;
        }

        private List<Point3> CreateMainFeedRoute(BusbarProfile profile, ConnectionPort start, ConnectionPort end)
        {
            List<Point3> points = new List<Point3>();
            Point3 p0 = start.HoleCenter;
            MainFeedRouteDecision decision = CalculateMainFeedRouteDecision(profile, start, end);

            Add(points, p0);
            Add(points, new Point3(p0.X, p0.Y + decision.LeadOutY, p0.Z));
            Add(points, new Point3(p0.X, p0.Y + decision.LeadOutY, decision.ApproachZ));
            Add(points, new Point3(p0.X, end.HoleCenter.Y, decision.ApproachZ));
            Add(points, end.HoleCenter);

            return points;
        }

        private List<Point3> CreateSimpleRoute(ConnectionPort start, ConnectionPort end, RouteAxisOrder axisOrder)
        {
            List<Point3> points = new List<Point3>();
            Add(points, start.HoleCenter);

            if (axisOrder == RouteAxisOrder.ZThenY)
            {
                Add(points, new Point3(start.HoleCenter.X, start.HoleCenter.Y, end.HoleCenter.Z));
                Add(points, new Point3(start.HoleCenter.X, end.HoleCenter.Y, end.HoleCenter.Z));
            }
            else
            {
                Add(points, new Point3(start.HoleCenter.X, end.HoleCenter.Y, start.HoleCenter.Z));
                Add(points, new Point3(start.HoleCenter.X, end.HoleCenter.Y, end.HoleCenter.Z));
            }

            return points;
        }


        private MainFeedRouteDecision CalculateMainFeedRouteDecision(BusbarProfile profile, ConnectionPort start, ConnectionPort end)
        {
            return new MainFeedRouteDecision
            {
                LeadOutY = CalculateMainFeedLeadOutY(start),
                ApproachZ = CalculateMainFeedApproachZ(profile, start.HoleCenter.Z, end.HoleCenter.Z),
                LeadOutRule = "Current simple rule: device preferred Y lead direction * Settings.MainLeadOutYMeters.",
                ApproachRule = "Current simple rule: stay outside collector by half collector width + half main-feed width + front clearance."
            };
        }

        private double CalculateMainFeedLeadOutY(ConnectionPort start)
        {
            int sign = -1;
            if (start.PreferredLeadAxis == AxisDirection.Y && start.PreferredLeadSign != 0)
                sign = start.PreferredLeadSign > 0 ? 1 : -1;

            return sign * _settings.MainLeadOutYMeters;
        }

        private double CalculateMainFeedApproachZ(BusbarProfile mainFeedProfile, double startZ, double collectorTapZ)
        {
            double directionToCollector = Math.Sign(collectorTapZ - startZ);
            if (Math.Abs(directionToCollector) < 0.001)
                return collectorTapZ;

            double offsetFromCollector = CalculateMainFeedApproachOffsetZ(mainFeedProfile);
            double approachZ = collectorTapZ - directionToCollector * offsetFromCollector;

            bool approachBetweenStartAndCollector =
                directionToCollector < 0
                    ? approachZ < startZ && approachZ > collectorTapZ
                    : approachZ > startZ && approachZ < collectorTapZ;

            if (!approachBetweenStartAndCollector)
                approachZ = (startZ + collectorTapZ) / 2.0;

            return approachZ;
        }

        private double CalculateMainFeedApproachOffsetZ(BusbarProfile mainFeedProfile)
        {
            // V0 keeps the long Y move outside the collector width envelope, then enters the collector on the final Z segment.
            return _settings.CollectorProfile.WidthMeters / 2.0 +
                mainFeedProfile.WidthMeters / 2.0 +
                _settings.MainCollectorFrontClearanceMeters;
        }

        private static void Add(List<Point3> points, Point3 point)
        {
            if (points.Count == 0 || points[points.Count - 1].DistanceTo(point) > Mm(0.01))
                points.Add(point);
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
