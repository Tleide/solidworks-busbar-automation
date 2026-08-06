using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;

namespace SwFeatureDebug
{
    internal sealed partial class SolidWorksBusbarPartBuilder
    {
        private static void CreateBusbarMountingHoles(SldWorks swApp, ModelDoc2 partModel, Busbar busbar)
        {
            if (busbar.MountingPorts != null && busbar.MountingPorts.Count > 0)
            {
                for (int i = 0; i < busbar.MountingPorts.Count; i++)
                    CreateBusbarMountingHole(swApp, partModel, busbar, busbar.MountingPorts[i], "P" + (i + 1));

                return;
            }

            CreateBusbarMountingHole(swApp, partModel, busbar, busbar.StartPort, "Start");
            CreateBusbarMountingHole(swApp, partModel, busbar, busbar.EndPort, "End");
        }

        private static void CreateBusbarMountingHole(SldWorks swApp, ModelDoc2 partModel, Busbar busbar, ConnectionPort port, string role)
        {
            if (port == null || port.HoleDiameterMm <= 0.0)
                return;

            Console.WriteLine(
                "Create mounting hole [" + role + "]: diameter=" +
                port.HoleDiameterMm.ToString("0.###") +
                "mm, face=" + port.RequiredFace +
                ", center=" + port.HoleCenter.ToMillimeterText());

            SheetMetalOpenProfilePlane holePlane = busbar.Kind == BusbarKind.Collector
                ? GetCollectorHoleSketchPlane(busbar)
                : GetBranchCollectorHoleSketchPlane(busbar, port) ?? GetHoleSketchPlane(port);
            Feature plane = CreateOffsetPlane(partModel, holePlane.BasePlaneRole, holePlane.Offset);
            try
            {
                SketchManager sketchManager = partModel.SketchManager;
                partModel.EditRebuild3();
                partModel.ClearSelection2(true);
                if (!plane.Select2(false, 0))
                    throw new Exception("Failed to select hole sketch plane: " + busbar.Name + " " + role);

                partModel.InsertSketch2(true);

                bool sketchStillOpen = true;
                MathUtility mathUtility = null;
                Sketch activeSketch = null;
                MathTransform modelToSketch = null;

                try
                {
                    mathUtility = (MathUtility)swApp.GetMathUtility();
                    if (mathUtility == null)
                        throw new Exception("Failed to access the SolidWorks math utility.");

                    activeSketch = sketchManager.ActiveSketch as Sketch;
                    if (activeSketch == null)
                        activeSketch = partModel.GetActiveSketch2() as Sketch;

                    if (activeSketch == null)
                        throw new Exception("Failed to get active hole sketch: " + busbar.Name + " " + role);

                    modelToSketch = activeSketch.ModelToSketchTransform;
                    if (modelToSketch == null)
                        throw new Exception("Failed to get hole sketch transform: " + busbar.Name + " " + role);

                    bool useDerivedTopPlane = busbar.Kind == BusbarKind.Collector ||
                        (busbar.Kind == BusbarKind.Branch && port.Kind == PortKind.CollectorTap);
                    Point3 holeCenter = useDerivedTopPlane
                        ? new Point3(port.HoleCenter.X, holePlane.Offset, port.HoleCenter.Z)
                        : port.HoleCenter;
                    Point3 sketchCenter = FlattenSketchPoint(ModelPointToSketchPoint(mathUtility, holeCenter, modelToSketch));
                    double radius = Mm(port.HoleDiameterMm) / 2.0;
                    SketchSegment circle = sketchManager.CreateCircleByRadius(sketchCenter.X, sketchCenter.Y, 0.0, radius);
                    if (circle == null)
                        throw new Exception("Failed to create mounting hole circle: " + busbar.Name + " " + role);
                    SolidWorksCom.Release(circle);

                    ConnectionPort cutPort = busbar.Kind == BusbarKind.Collector
                        ? CreateCollectorSurfaceCutPort(port, holePlane.Offset)
                        : CreateBranchCollectorSurfaceCutPort(busbar, port, holePlane.Offset) ?? port;
                    Feature activeSketchCut = CreateDirectedBlindCutFromActiveSketch(
                        partModel,
                        busbar.Name + "_HoleCut_" + role,
                        cutPort,
                        busbar.Profile.ThicknessMeters);

                    if (activeSketchCut != null)
                    {
                        SolidWorksCom.Release(activeSketchCut);
                        sketchStillOpen = false;
                        return;
                    }

                    partModel.InsertSketch2(true);
                    sketchStillOpen = false;
                }
                finally
                {
                    if (sketchStillOpen)
                        partModel.InsertSketch2(true);

                    SolidWorksCom.Release(modelToSketch);
                    SolidWorksCom.Release(activeSketch);
                    SolidWorksCom.Release(mathUtility);
                }

                Feature sketch = partModel.FeatureByPositionReverse(0) as Feature;
                if (sketch == null)
                    throw new Exception("hole sketch was created but could not be located: " + busbar.Name + " " + role);

                try
                {
                    sketch.Name = busbar.Name + "_Hole_" + role;
                    partModel.EditRebuild3();

                    ConnectionPort cutPort = busbar.Kind == BusbarKind.Collector
                        ? CreateCollectorSurfaceCutPort(port, holePlane.Offset)
                        : CreateBranchCollectorSurfaceCutPort(busbar, port, holePlane.Offset) ?? port;
                    Feature cut = CreateDirectedBlindCutFromSketch(
                        partModel,
                        sketch,
                        busbar.Name + "_HoleCut_" + role,
                        cutPort,
                        busbar.Profile.ThicknessMeters);
                    if (cut == null)
                        throw new Exception("Failed to create mounting hole cut: " + busbar.Name + " " + role);

                    SolidWorksCom.Release(cut);
                }
                finally
                {
                    SolidWorksCom.Release(sketch);
                }
            }
            finally
            {
                SolidWorksCom.Release(plane);
            }
        }

        // Branch overlap holes are derived from the actual sheet-metal path, not from the collector tap coordinate.
        private static SheetMetalOpenProfilePlane GetBranchCollectorHoleSketchPlane(Busbar busbar, ConnectionPort port)
        {
            if (busbar.Kind != BusbarKind.Branch || port.Kind != PortKind.CollectorTap ||
                busbar.SheetMetalSketchLine == null || busbar.SheetMetalSketchLine.Count == 0)
                return null;

            double routeEndY = busbar.SheetMetalSketchLine[busbar.SheetMetalSketchLine.Count - 1].Y;
            double surfaceY = port.RequiredFace == ContactFace.Lower
                ? routeEndY + busbar.Profile.ThicknessMeters
                : routeEndY;
            return new SheetMetalOpenProfilePlane("Top", surfaceY, AxisDirection.X, AxisDirection.Z);
        }

        private static ConnectionPort CreateBranchCollectorSurfaceCutPort(Busbar busbar, ConnectionPort source, double surfaceY)
        {
            if (busbar.Kind != BusbarKind.Branch || source.Kind != PortKind.CollectorTap)
                return null;

            ConnectionPort port = new ConnectionPort
            {
                Name = source.Name,
                ComponentName = source.ComponentName,
                Kind = source.Kind,
                HoleCenter = new Point3(source.HoleCenter.X, surfaceY, source.HoleCenter.Z),
                RequiredFace = source.RequiredFace == ContactFace.Lower ? ContactFace.Upper : ContactFace.Lower,
                PreferredLeadAxis = source.PreferredLeadAxis,
                PreferredLeadSign = source.PreferredLeadSign,
                EndMarginMm = source.EndMarginMm,
                HoleDiameterMm = source.HoleDiameterMm
            };
            return port;
        }

        // All collector holes share the upper thickness surface; hole patterns only determine X/Z coordinates.
        private static SheetMetalOpenProfilePlane GetCollectorHoleSketchPlane(Busbar collector)
        {
            double upperSurfaceY = collector.LogicalCenterline[0].Y;
            return new SheetMetalOpenProfilePlane("Top", upperSurfaceY, AxisDirection.X, AxisDirection.Z);
        }

        private static ConnectionPort CreateCollectorSurfaceCutPort(ConnectionPort source, double surfaceY)
        {
            ConnectionPort port = new ConnectionPort
            {
                Name = source.Name,
                ComponentName = source.ComponentName,
                Kind = source.Kind,
                HoleCenter = new Point3(source.HoleCenter.X, surfaceY, source.HoleCenter.Z),
                RequiredFace = ContactFace.Upper,
                PreferredLeadAxis = source.PreferredLeadAxis,
                PreferredLeadSign = source.PreferredLeadSign,
                EndMarginMm = source.EndMarginMm,
                HoleDiameterMm = source.HoleDiameterMm
            };
            return port;
        }

        private static SheetMetalOpenProfilePlane GetHoleSketchPlane(ConnectionPort port)
        {
            if (port.RequiredFace == ContactFace.Upper || port.RequiredFace == ContactFace.Lower)
                return new SheetMetalOpenProfilePlane("Top", port.HoleCenter.Y, AxisDirection.X, AxisDirection.Z);

            if (port.RequiredFace == ContactFace.Left || port.RequiredFace == ContactFace.Right)
                return new SheetMetalOpenProfilePlane("Right", port.HoleCenter.X, AxisDirection.Y, AxisDirection.Z);

            return new SheetMetalOpenProfilePlane("Front", port.HoleCenter.Z, AxisDirection.X, AxisDirection.Y);
        }

        private static Feature CreateDirectedBlindCutFromSketch(ModelDoc2 partModel, Feature sketch, string featureName, ConnectionPort port, double cutDepth)
        {
            bool reverseDirection = ShouldReverseHoleCutDirection(port);
            Console.WriteLine(
                "Create directed blind cut: " + featureName +
                ", depth=" + ToMm(cutDepth).ToString("F3") +
                " mm, face=" + port.RequiredFace +
                ", reverseDirection=" + reverseDirection);

            if (!SelectSketchForCut(partModel, sketch))
                throw new Exception("Failed to select cut sketch: " + featureName);

            Feature cut = TryCreateBlindCut(partModel, sketch, featureName, cutDepth, reverseDirection, false, "DirectedBlind");
            if (cut == null)
                cut = TryCreateBlindCut(partModel, sketch, featureName, cutDepth, !reverseDirection, false, "DirectedBlindOpposite");
            if (cut == null)
                cut = TryCreateBlindCut(partModel, sketch, featureName, cutDepth, reverseDirection, true, "DirectedBlindNormalCut");
            if (cut == null)
                cut = TryCreateBlindCutWithScope(partModel, sketch, featureName, cutDepth, reverseDirection, false, false, false, false, false, "DirectedBlindNoScope");

            return cut;
        }

        private static Feature TryCreateBlindCut(
            ModelDoc2 partModel,
            Feature sketch,
            string featureName,
            double cutDepth,
            bool reverseDirection,
            bool normalCut,
            string modeLabel)
        {
            return TryCreateBlindCutWithScope(
                partModel,
                sketch,
                featureName,
                cutDepth,
                reverseDirection,
                normalCut,
                true,
                true,
                true,
                true,
                modeLabel);
        }

        private static Feature TryCreateBlindCutWithScope(
            ModelDoc2 partModel,
            Feature sketch,
            string featureName,
            double cutDepth,
            bool reverseDirection,
            bool normalCut,
            bool useFeatScope,
            bool useAutoSelect,
            bool assemblyFeatureScope,
            bool autoSelectComponents,
            string modeLabel)
        {
            if (!SelectSketchForCut(partModel, sketch))
                throw new Exception("Failed to select cut sketch: " + featureName);

            Feature cut = partModel.FeatureManager.FeatureCut4(
                true,
                false,
                reverseDirection,
                (int)swEndConditions_e.swEndCondBlind,
                (int)swEndConditions_e.swEndCondBlind,
                cutDepth,
                cutDepth,
                false,
                false,
                false,
                false,
                1.0,
                1.0,
                false,
                false,
                false,
                false,
                normalCut,
                useFeatScope,
                useAutoSelect,
                assemblyFeatureScope,
                autoSelectComponents,
                false,
                (int)swStartConditions_e.swStartSketchPlane,
                0.0,
                false,
                false);

            partModel.ClearSelection2(true);

            if (cut == null)
                return null;

            cut.Name = featureName;
            Console.WriteLine("Created cut feature: " + featureName + ", mode=" + modeLabel);
            return cut;
        }

        private static bool ShouldReverseHoleCutDirection(ConnectionPort port)
        {
            if (port.RequiredFace == ContactFace.Upper || port.RequiredFace == ContactFace.Left)
                return true;

            return false;
        }

        private static Feature CreateDirectedBlindCutFromActiveSketch(ModelDoc2 partModel, string featureName, ConnectionPort port, double cutDepth)
        {
            bool reverseDirection = ShouldReverseHoleCutDirection(port);
            Console.WriteLine(
                "Try active-sketch blind cut: " + featureName +
                ", depth=" + ToMm(cutDepth).ToString("F3") +
                " mm, face=" + port.RequiredFace +
                ", reverseDirection=" + reverseDirection);

            Feature cut = TryCreateBlindCutFromCurrentSelection(partModel, featureName, cutDepth, reverseDirection, false, "ActiveSketchDirected");
            if (cut == null)
                cut = TryCreateBlindCutFromCurrentSelection(partModel, featureName, cutDepth, !reverseDirection, false, "ActiveSketchOpposite");
            if (cut == null)
                cut = TryCreateBlindCutFromCurrentSelection(partModel, featureName, cutDepth, reverseDirection, true, "ActiveSketchNormalCut");

            return cut;
        }

        private static Feature TryCreateBlindCutFromCurrentSelection(
            ModelDoc2 partModel,
            string featureName,
            double cutDepth,
            bool reverseDirection,
            bool normalCut,
            string modeLabel)
        {
            Feature cut = partModel.FeatureManager.FeatureCut4(
                true,
                false,
                reverseDirection,
                (int)swEndConditions_e.swEndCondBlind,
                (int)swEndConditions_e.swEndCondBlind,
                cutDepth,
                cutDepth,
                false,
                false,
                false,
                false,
                1.0,
                1.0,
                false,
                false,
                false,
                false,
                normalCut,
                false,
                false,
                false,
                false,
                false,
                (int)swStartConditions_e.swStartSketchPlane,
                0.0,
                false,
                false);

            if (cut == null)
                return null;

            cut.Name = featureName;
            Console.WriteLine("Created cut feature: " + featureName + ", mode=" + modeLabel);
            return cut;
        }

        private static bool SelectSketchForCut(ModelDoc2 partModel, Feature sketch)
        {
            partModel.ClearSelection2(true);

            bool selected = partModel.Extension.SelectByID2(
                sketch.Name,
                "SKETCH",
                0.0,
                0.0,
                0.0,
                false,
                0,
                null,
                (int)swSelectOption_e.swSelectOptionDefault);

            if (!selected)
            {
                partModel.ClearSelection2(true);
                selected = sketch.Select2(false, 0);
            }

            return selected;
        }
    }
}
