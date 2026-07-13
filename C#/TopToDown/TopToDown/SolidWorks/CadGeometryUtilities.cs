using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SwFeatureDebug
{
    internal partial class Program
    {
        private static SketchSegment CreateLineOrThrow(SketchManager sketchManager, Point3 a, Point3 b, string name)
        {
            SketchSegment segment = sketchManager.CreateLine(a.X, a.Y, a.Z, b.X, b.Y, b.Z);
            if (segment == null)
                throw new Exception("Failed to create sketch line: " + name);

            return segment;
        }
        private static ModelDoc2 NewPartDocument(SldWorks swApp)
        {
            string templatePath = swApp.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
            bool templateExists = !string.IsNullOrWhiteSpace(templatePath) && File.Exists(templatePath);

            ModelDoc2 partModel;
            if (templateExists)
            {
                partModel = swApp.NewDocument(
                    templatePath,
                    (int)swDwgPaperSizes_e.swDwgPaperA4size,
                    0,
                    0) as ModelDoc2;
            }
            else
            {
                partModel = swApp.NewPart() as ModelDoc2;
            }

            if (partModel == null)
                throw new Exception("Failed to create a new SolidWorks part.");

            return partModel;
        }
        private static SheetMetalOpenProfilePlane GetOpenProfilePlane(string name, BusbarKind kind, List<Point3> centerlinePoints)
        {
            if (centerlinePoints == null || centerlinePoints.Count < 2)
                throw new Exception("Route is invalid for open-profile sheet metal.");

            bool sameX = AllSameCoordinate(centerlinePoints, AxisDirection.X);
            bool sameY = AllSameCoordinate(centerlinePoints, AxisDirection.Y);
            bool sameZ = AllSameCoordinate(centerlinePoints, AxisDirection.Z);

            if (kind == BusbarKind.Collector)
            {
                if (!sameZ)
                    throw new Exception("Collector route must stay on a constant Z plane: " + name);

                return new SheetMetalOpenProfilePlane("Front", centerlinePoints[0].Z, AxisDirection.X, AxisDirection.Y);
            }

            if (sameX)
                return new SheetMetalOpenProfilePlane("Right", centerlinePoints[0].X, AxisDirection.Y, AxisDirection.Z);

            if (sameY)
                return new SheetMetalOpenProfilePlane("Top", centerlinePoints[0].Y, AxisDirection.X, AxisDirection.Z);

            if (sameZ)
                return new SheetMetalOpenProfilePlane("Front", centerlinePoints[0].Z, AxisDirection.X, AxisDirection.Y);

            throw new Exception("Open-profile sheet metal currently supports only routes on a single X/Y/Z plane: " + name);
        }

        private static bool AllSameCoordinate(List<Point3> points, AxisDirection axis)
        {
            double first = GetCoordinate(points[0], axis);
            const double tolerance = 0.000001;

            foreach (Point3 point in points)
            {
                if (Math.Abs(GetCoordinate(point, axis) - first) > tolerance)
                    return false;
            }

            return true;
        }

        private static double GetCoordinate(Point3 point, AxisDirection axis)
        {
            if (axis == AxisDirection.X)
                return point.X;

            if (axis == AxisDirection.Y)
                return point.Y;

            return point.Z;
        }
        private static void LogPartBoundingBox(ModelDoc2 partModel, Busbar busbar)
        {
            PartDoc part = partModel as PartDoc;
            if (part == null)
                return;

            double[] box = part.GetPartBox(false) as double[];
            if (box == null || box.Length < 6)
                return;

            Console.WriteLine(
                "Part bounding box [" + busbar.Name + "]: " +
                "Ymin=" + box[1].ToString("F3") + " mm, " +
                "Ymax=" + box[4].ToString("F3") + " mm");
        }

        private static Feature CreateOffsetPlane(ModelDoc2 partModel, string basePlaneRole, double offset)
        {
            Feature basePlane = FindDefaultPlane(partModel, basePlaneRole);
            if (basePlane == null)
                throw new Exception("Default plane not found: " + basePlaneRole);

            partModel.ClearSelection2(true);
            basePlane.Select2(false, 0);

            double distance = Math.Abs(offset);
            bool flipDirection = offset < 0;

            partModel.ICreatePlaneAtOffset3(distance, flipDirection, true);

            Feature plane = FindLastFeatureByType(partModel, "RefPlane");
            if (plane == null)
                throw new Exception("Failed to create offset plane: " + basePlaneRole);

            Console.WriteLine(
                "Created offset plane: role=" + basePlaneRole +
                ", offset=" + ToMm(offset).ToString("F3") +
                " mm, type=" + plane.GetTypeName2());

            plane.Name = "Busbar_ProfilePlane_" + basePlaneRole;
            return plane;
        }

        private static Feature FindLastFeatureByType(ModelDoc2 model, string typeName)
        {
            Feature feature = model.FirstFeature() as Feature;
            Feature lastMatch = null;

            while (feature != null)
            {
                if (SameText(feature.GetTypeName2(), typeName))
                    lastMatch = feature;

                feature = feature.GetNextFeature() as Feature;
            }

            return lastMatch;
        }

        private static Feature FindDefaultPlane(ModelDoc2 partModel, string role)
        {
            string[] names;
            int fallbackIndex;

            if (SameText(role, "Right"))
            {
                names = new[] { "Right Plane", "Right" };
                fallbackIndex = 2;
            }
            else if (SameText(role, "Front"))
            {
                names = new[] { "Front Plane", "Front" };
                fallbackIndex = 0;
            }
            else
            {
                names = new[] { "Top Plane", "Top" };
                fallbackIndex = 1;
            }

            int refPlaneIndex = 0;
            Feature feature = partModel.FirstFeature() as Feature;

            while (feature != null)
            {
                if (feature.GetTypeName2() == "RefPlane")
                {
                    foreach (string name in names)
                    {
                        if (SameText(feature.Name, name))
                            return feature;
                    }

                    if (refPlaneIndex == fallbackIndex)
                        return feature;

                    refPlaneIndex++;
                }

                feature = feature.GetNextFeature() as Feature;
            }

            return null;
        }
        private static Point3 ModelPointToSketchPoint(SldWorks swApp, Point3 modelPoint, MathTransform modelToSketch)
        {
            MathUtility utility = (MathUtility)swApp.GetMathUtility();
            MathPoint point = (MathPoint)utility.CreatePoint(new[] { modelPoint.X, modelPoint.Y, modelPoint.Z });
            MathPoint sketchPoint = (MathPoint)point.MultiplyTransform(modelToSketch);
            double[] data = sketchPoint.ArrayData as double[];

            if (data == null || data.Length < 3)
                throw new Exception("Failed to convert model point to sketch point.");

            return new Point3(data[0], data[1], data[2]);
        }

        private static Point3 FlattenSketchPoint(Point3 point)
        {
            return new Point3(point.X, point.Y, 0.0);
        }

        private static Feature FindFirstFeatureByType(ModelDoc2 model, string typeName)
        {
            Feature feature = model.FirstFeature() as Feature;

            while (feature != null)
            {
                if (SameText(feature.GetTypeName2(), typeName))
                    return feature;

                feature = feature.GetNextFeature() as Feature;
            }

            return null;
        }
    }
}
