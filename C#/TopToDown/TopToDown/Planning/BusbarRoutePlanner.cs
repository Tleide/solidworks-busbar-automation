using System;
using System.Collections.Generic;

namespace SwFeatureDebug
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

        public List<Point3> CreateRoute(BusbarKind kind, BusbarProfile profile, ConnectionPort start, ConnectionPort end, RouteAxisOrder axisOrder)
        {
            if (kind == BusbarKind.MainFeed)
                return CreateMainFeedRoute(profile, start, end);

            return CreateSimpleRoute(start, end, axisOrder);
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
                LeadOutRule = "Current simple rule: device preferred Y lead direction * Settings.MainLeadOutY.",
                ApproachRule = "Current simple rule: stay outside collector by half collector width + half main-feed width + front clearance."
            };
        }

        private double CalculateMainFeedLeadOutY(ConnectionPort start)
        {
            int sign = -1;
            if (start.PreferredLeadAxis == AxisDirection.Y && start.PreferredLeadSign != 0)
                sign = start.PreferredLeadSign > 0 ? 1 : -1;

            return sign * _settings.MainLeadOutY;
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
            return _settings.CollectorProfile.Width / 2.0 +
                mainFeedProfile.Width / 2.0 +
                _settings.MainCollectorFrontClearance;
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
    }
}