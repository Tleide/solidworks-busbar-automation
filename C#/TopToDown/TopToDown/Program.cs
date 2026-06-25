using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SwFeatureDebug
{
    internal class Program
    {
        private static readonly BusbarSettings Settings = new BusbarSettings
        {
            ModelingMode = BusbarModelingMode.SheetMetalBaseFlangeOpenProfile,
            MainFeedWidthMm = 60.0,
            MainFeedThicknessMm = 6.0,

            CollectorWidthMm = 80.0,
            CollectorThicknessMm = 6.0,

            BranchWidthMm = 40.0,
            BranchThicknessMm = 4.0,

            StartTerminalOffsetZSign = 1,
            EndTerminalOffsetZSign = -1,

            CollectorPhaseSpacingMm = 60.0,
            CollectorTopClearanceYMm = 180.0,
            CollectorOffsetFromLoubaoInZMm = 120.0,
            CollectorNegativeXExtendMm = 50.0,

            MainLeadOutYMm = 40.0,

            MainCollectorLapSide = CollectorLapSide.Upper,
            BranchCollectorLapSide = CollectorLapSide.Upper,
            SheetMetalBendRadiusMm = 5.0,
            SheetMetalKFactor = 0.47,
            SheetMetalFlangePosition = (int)swSweptFlangePositionTypes_e.swSweptFlangePositionType_BendOutside,
            SheetMetalReverseDirection = false,
            SheetMetalThickenDirection = false,
            MainFeedSheetMetalWidthSide = SheetMetalWidthSide.Center,
            CollectorSheetMetalWidthSide = SheetMetalWidthSide.Center,
            BranchSheetMetalWidthSide = SheetMetalWidthSide.Center,
            MainCollectorFrontClearanceMm = 0.0,
            MainCollectorLapDepthRatio = 0.5,
            ReverseBranchOpenProfileSketchDirection = true
        };

        private static readonly string[] PhaseNames = { "A", "B", "C" };
        private static readonly string[] FuseComponentNameHints = { "fuse", "HR6", "rong", "knife", "isolator" };
        private static readonly string[] LoubaoComponentNameHints = { "loubao", "PGM", "leakage", "breaker" };

        private static bool _replaceExistingBusbar = true;
        private static bool _verboseFeatureScan;
        private static bool _generateMainFeedRoutes = true;
        private static bool _generateCollectorRoutes = true;
        private static bool _generateBranchRoutes = true;
        private static bool _runSheetMetalOpenLineDemoOnly;
        private static bool _runSheetMetalBentLineDemoOnly;
        private static bool _runPlanningDemoV2Only;
        private static bool _runPlanningDemoV2FromAssemblyOnly;
        private static bool _runPreviewDemoV2FromAssemblyOnly;
        private static bool _runSheetMetalV2FirstMainFromAssemblyOnly;
        private static bool _runSheetMetalV2AllFromAssemblyOnly;

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                ConfigureFromArgs(args ?? new string[0]);

                if (_runPlanningDemoV2Only)
                {
                    BusbarPlanningDemoV2.Run();
                    Console.WriteLine();
                    Console.WriteLine("Busbar V2 planning demo complete. Press any key to exit.");
                    if (!Console.IsInputRedirected)
                        Console.ReadKey();
                    return;
                }

                SldWorks swApp = GetOrStartSolidWorks();
                ModelDoc2 model = GetActiveOrOpenAssembly(swApp);
                AssemblyDoc assembly = (AssemblyDoc)model;

                if (_runPlanningDemoV2FromAssemblyOnly)
                {
                    List<FoundPoint> scannedPoints = ScanReferencePoints(swApp, model, assembly);
                    BusbarPlanningDemoV2.RunFromScannedAssembly(scannedPoints, PhaseNames, Settings);
                    Console.WriteLine();
                    Console.WriteLine("Busbar V2 assembly planning demo complete. Press any key to exit.");
                    if (!Console.IsInputRedirected)
                        Console.ReadKey();
                    return;
                }

                if (_runPreviewDemoV2FromAssemblyOnly)
                {
                    List<FoundPoint> scannedPoints = ScanReferencePoints(swApp, model, assembly);
                    BusbarPlanV2 plan = BusbarPlanningDemoV2.BuildPlanFromScannedAssembly(scannedPoints, PhaseNames, Settings);
                    CreateBusbarV2PreviewPart(swApp, model, assembly, plan);
                    Console.WriteLine();
                    Console.WriteLine("Busbar V2 assembly preview complete. Press any key to exit.");
                    if (!Console.IsInputRedirected)
                        Console.ReadKey();
                    return;
                }

                if (_runSheetMetalV2FirstMainFromAssemblyOnly)
                {
                    List<FoundPoint> scannedPoints = ScanReferencePoints(swApp, model, assembly);
                    BusbarPlanV2 plan = BusbarPlanningDemoV2.BuildPlanFromScannedAssembly(scannedPoints, PhaseNames, Settings);
                    BusbarV2 busbar = plan.Busbars.FirstOrDefault(b => b.Kind == BusbarKind.MainFeed);
                    if (busbar == null)
                        throw new Exception("No V2 main-feed busbar was planned from the active assembly.");

                    CreateBusbarV2SheetMetalPart(swApp, model, assembly, busbar);
                    Console.WriteLine();
                    Console.WriteLine("Busbar V2 first main-feed sheet metal test complete. Press any key to exit.");
                    if (!Console.IsInputRedirected)
                        Console.ReadKey();
                    return;
                }

                if (_runSheetMetalV2AllFromAssemblyOnly)
                {
                    if (_replaceExistingBusbar)
                        DeleteExistingBusbarComponents(model, assembly);

                    List<FoundPoint> scannedPoints = ScanReferencePoints(swApp, model, assembly);
                    BusbarPlanV2 plan = BusbarPlanningDemoV2.BuildPlanFromScannedAssembly(scannedPoints, PhaseNames, Settings);
                    List<BusbarV2> busbars = SelectBusbarsForV2SheetMetalBatch(plan);

                    CreateBusbarV2SheetMetalParts(swApp, model, assembly, busbars);
                    Console.WriteLine();
                    Console.WriteLine("Busbar V2 3+3+9 sheet metal generation complete. Press any key to exit.");
                    if (!Console.IsInputRedirected)
                        Console.ReadKey();
                    return;
                }

                if (_runSheetMetalOpenLineDemoOnly)
                {
                    RunSheetMetalOpenLineDemo(swApp, model, assembly);
                    Console.WriteLine();
                    Console.WriteLine("Open-line sheet metal demo complete. Press any key to exit.");
                    Console.ReadKey();
                    return;
                }

                if (_runSheetMetalBentLineDemoOnly)
                {
                    RunSheetMetalBentLineDemo(swApp, model, assembly);
                    Console.WriteLine();
                    Console.WriteLine("Bent-line sheet metal demo complete. Press any key to exit.");
                    Console.ReadKey();
                    return;
                }

                if (_replaceExistingBusbar)
                    DeleteExistingBusbarComponents(model, assembly);

                List<FoundPoint> foundPoints = ScanReferencePoints(swApp, model, assembly);
                List<BusbarRoute> routes = BuildComplexRoutes(foundPoints);

                foreach (BusbarRoute route in routes)
                    CreateBusbarSolidPart(swApp, model, assembly, route);

                Console.WriteLine();
                Console.WriteLine("Generation complete. Press any key to exit.");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                Console.WriteLine();
                Console.WriteLine(ex);
                Console.ReadKey();
            }
        }

        private static ModelDoc2 GetActiveOrOpenAssembly(SldWorks swApp)
        {
            ModelDoc2 model = swApp.ActiveDoc as ModelDoc2;
            if (model != null && model.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
                return model;

            object[] documents = swApp.GetDocuments() as object[];
            if (documents != null)
            {
                foreach (object item in documents)
                {
                    ModelDoc2 openModel = item as ModelDoc2;
                    if (openModel == null || openModel.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                        continue;

                    Console.WriteLine("Active document is not an assembly. Switching to open assembly: " + openModel.GetTitle());
                    ActivateDocument(swApp, openModel);
                    return openModel;
                }
            }

            throw new Exception("Open SolidWorks and activate a target assembly first.");
        }

        private static void ConfigureFromArgs(string[] args)
        {
            foreach (string arg in args)
            {
                if (SameText(arg, "--verbose"))
                {
                    _verboseFeatureScan = true;
                    continue;
                }

                if (SameText(arg, "--keep-existing"))
                {
                    _replaceExistingBusbar = false;
                    continue;
                }

                if (SameText(arg, "--branch"))
                {
                    _generateBranchRoutes = true;
                    continue;
                }

                if (SameText(arg, "--demo-open-line"))
                {
                    _runSheetMetalOpenLineDemoOnly = true;
                    _runSheetMetalBentLineDemoOnly = false;
                    continue;
                }

                if (SameText(arg, "--demo-bent-line"))
                {
                    _runSheetMetalBentLineDemoOnly = true;
                    _runSheetMetalOpenLineDemoOnly = false;
                    continue;
                }

                if (SameText(arg, "--plan-v2-demo"))
                {
                    _runPlanningDemoV2Only = true;
                    continue;
                }

                if (SameText(arg, "--plan-v2-assembly"))
                {
                    _runPlanningDemoV2FromAssemblyOnly = true;
                    continue;
                }

                if (SameText(arg, "--preview-v2-assembly"))
                {
                    _runPreviewDemoV2FromAssemblyOnly = true;
                    continue;
                }

                if (SameText(arg, "--sheetmetal-v2-first-main"))
                {
                    _runSheetMetalV2FirstMainFromAssemblyOnly = true;
                    continue;
                }

                if (SameText(arg, "--sheetmetal-v2-all"))
                {
                    _runSheetMetalV2AllFromAssemblyOnly = true;
                    continue;
                }

                if (SameText(arg, "--open-profile"))
                {
                    Settings.ModelingMode = BusbarModelingMode.SheetMetalBaseFlangeOpenProfile;
                    continue;
                }

                if (SameText(arg, "--swept-flange"))
                {
                    Settings.ModelingMode = BusbarModelingMode.SheetMetalSweptFlange;
                    continue;
                }

                if (SameText(arg, "--no-branch"))
                {
                    _generateBranchRoutes = false;
                    continue;
                }

                if (SameText(arg, "--no-main"))
                {
                    _generateMainFeedRoutes = false;
                    continue;
                }

                if (SameText(arg, "--no-collector"))
                {
                    _generateCollectorRoutes = false;
                    continue;
                }
            }
        }

        private static SldWorks GetOrStartSolidWorks()
        {
            try
            {
                Console.WriteLine("Connecting to running SolidWorks...");
                return (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
            }
            catch (COMException)
            {
                Console.WriteLine("No running SolidWorks instance found. Starting SolidWorks...");

                Type swType = Type.GetTypeFromProgID("SldWorks.Application");
                if (swType == null)
                    throw new Exception("SolidWorks.Application is not registered on this computer.");

                SldWorks swApp = (SldWorks)Activator.CreateInstance(swType);
                swApp.Visible = true;
                return swApp;
            }
        }

        private static List<FoundPoint> ScanReferencePoints(SldWorks swApp, ModelDoc2 model, AssemblyDoc assembly)
        {
            List<FoundPoint> foundPoints = new List<FoundPoint>();

            Console.WriteLine("Current assembly: " + model.GetTitle());
            Console.WriteLine();

            if (_verboseFeatureScan)
                Console.WriteLine("===== Assembly features =====");

            DumpModelFeatures(swApp, model, null, null, foundPoints);

            object[] components = assembly.GetComponents(false) as object[];
            if (components == null || components.Length == 0)
            {
                Console.WriteLine("No assembly components found.");
                return foundPoints;
            }

            if (_verboseFeatureScan)
            {
                Console.WriteLine();
                Console.WriteLine("===== Component features =====");
            }

            foreach (object item in components)
            {
                Component2 component = item as Component2;
                if (component == null)
                    continue;

                ModelDoc2 componentModel = component.GetModelDoc2() as ModelDoc2;
                if (componentModel == null)
                {
                    Console.WriteLine("[Skip] Component is unloaded or lightweight: " + component.Name2);
                    continue;
                }

                if (_verboseFeatureScan)
                {
                    Console.WriteLine();
                    Console.WriteLine("Component: " + component.Name2);
                }

                DumpModelFeatures(swApp, componentModel, component.Name2, component.Transform2, foundPoints);
            }

            Console.WriteLine();
            Console.WriteLine("Reference point count: " + foundPoints.Count);
            return foundPoints;
        }

        private static void DeleteExistingBusbarComponents(ModelDoc2 model, AssemblyDoc assembly)
        {
            object[] components = assembly.GetComponents(false) as object[];
            if (components == null || components.Length == 0)
                return;

            model.ClearSelection2(true);

            int selectedCount = 0;
            foreach (object item in components)
            {
                Component2 component = item as Component2;
                if (component == null || component.Name2 == null)
                    continue;

                if (component.Name2.StartsWith("Busbar_", StringComparison.OrdinalIgnoreCase) &&
                    component.Select4(true, null, false))
                {
                    selectedCount++;
                }
            }

            if (selectedCount > 0)
            {
                Console.WriteLine("Delete old generated busbar components: " + selectedCount);
                model.EditDelete();
                model.EditRebuild3();
            }

            model.ClearSelection2(true);
        }

        private static void DumpModelFeatures(SldWorks swApp, ModelDoc2 model, string componentName, MathTransform componentTransform, List<FoundPoint> foundPoints)
        {
            Feature feature = model.FirstFeature() as Feature;

            while (feature != null)
            {
                string featureName = feature.Name;
                string featureType = feature.GetTypeName2();

                if (_verboseFeatureScan)
                    Console.WriteLine("Feature: " + featureName + "    Type: " + featureType);

                TryReadReferencePoint(swApp, feature, componentName, componentTransform, foundPoints);
                feature = feature.GetNextFeature() as Feature;
            }
        }

        private static void TryReadReferencePoint(SldWorks swApp, Feature feature, string componentName, MathTransform componentTransform, List<FoundPoint> foundPoints)
        {
            object specific;
            try
            {
                specific = feature.GetSpecificFeature2();
            }
            catch
            {
                return;
            }

            RefPoint refPoint = specific as RefPoint;
            if (refPoint == null)
                return;

            MathPoint localMathPoint = refPoint.GetRefPoint();
            double[] local = localMathPoint.ArrayData as double[];
            if (local == null || local.Length < 3)
                return;

            Point3 point = new Point3(local[0], local[1], local[2]);

            if (componentTransform != null)
                point = TransformPoint(swApp, point, componentTransform);

            string owner = string.IsNullOrWhiteSpace(componentName) ? "Assembly" : componentName;

            Console.WriteLine("  >>> Reference point: " + feature.Name);
            Console.WriteLine("      Owner: " + owner);
            Console.WriteLine("      Assembly position: " + point.ToMillimeterText());

            foundPoints.Add(new FoundPoint
            {
                ComponentName = owner,
                PointName = feature.Name,
                Position = point
            });
        }

        private static Point3 TransformPoint(SldWorks swApp, Point3 point, MathTransform transform)
        {
            MathUtility utility = (MathUtility)swApp.GetMathUtility();
            MathPoint mathPoint = (MathPoint)utility.CreatePoint(new[] { point.X, point.Y, point.Z });
            MathPoint transformed = (MathPoint)mathPoint.MultiplyTransform(transform);
            double[] data = transformed.ArrayData as double[];

            if (data == null || data.Length < 3)
                throw new Exception("Failed to transform component point into assembly coordinates.");

            return new Point3(data[0], data[1], data[2]);
        }

        private static List<BusbarRoute> BuildComplexRoutes(List<FoundPoint> foundPoints)
        {
            string fuseComponent = FindFuseComponent(foundPoints);
            List<LoubaoGroup> loubaos = FindLoubaoGroups(foundPoints, fuseComponent);

            if (loubaos.Count == 0)
                throw new Exception("No leakage breaker component was found. Expected A_IN/B_IN/C_IN reference points.");

            Console.WriteLine("===== Busbar topology =====");
            Console.WriteLine("Fuse component: " + fuseComponent);
            Console.WriteLine("Leakage breaker count: " + loubaos.Count);

            foreach (LoubaoGroup loubao in loubaos)
                Console.WriteLine("  Loubao: " + loubao.ComponentName + " centerX=" + ToMm(loubao.CenterX).ToString("F3") + " mm");

            List<BusbarRoute> routes = new List<BusbarRoute>();

            foreach (string phase in PhaseNames)
            {
                FoundPoint fuseOut = FindRequiredPoint(foundPoints, fuseComponent, phase + "_OUT");
                List<FoundPoint> loubaoInputs = loubaos
                    .Select(l => FindRequiredPoint(foundPoints, l.ComponentName, phase + "_IN"))
                    .OrderBy(p => p.Position.X)
                    .ToList();

                CollectorLayout collector = BuildCollectorLayout(phase, fuseOut, loubaoInputs);

                if (_generateMainFeedRoutes)
                    routes.Add(BuildMainFeedRoute(phase, fuseOut, collector));

                if (_generateCollectorRoutes)
                    routes.Add(BuildCollectorRoute(phase, collector));

                if (_generateBranchRoutes)
                {
                    for (int i = 0; i < loubaoInputs.Count; i++)
                        routes.Add(BuildBranchRoute(phase, i + 1, loubaoInputs[i], collector));
                }
            }

            Console.WriteLine("Generated route count: " + routes.Count);
            foreach (BusbarRoute route in routes)
                PrintRoute(route);

            return routes;
        }

        private static string FindFuseComponent(List<FoundPoint> foundPoints)
        {
            var candidates = foundPoints
                .GroupBy(p => p.ComponentName)
                .Select(g => new
                {
                    ComponentName = g.Key,
                    OutCount = PhaseNames.Count(phase => g.Any(p => SameText(p.PointName, phase + "_OUT"))),
                    InCount = PhaseNames.Count(phase => g.Any(p => SameText(p.PointName, phase + "_IN"))),
                    NameScore = ScoreNameHint(g.Key, FuseComponentNameHints) - ScoreNameHint(g.Key, LoubaoComponentNameHints)
                })
                .Where(x => x.OutCount == PhaseNames.Length)
                .OrderByDescending(x => x.NameScore)
                .ThenByDescending(x => x.OutCount)
                .ThenByDescending(x => x.InCount)
                .ToList();

            Console.WriteLine("Fuse candidates:");
            foreach (var candidate in candidates)
                Console.WriteLine("  " + candidate.ComponentName + " out=" + candidate.OutCount + " in=" + candidate.InCount + " nameScore=" + candidate.NameScore);

            var fuse = candidates.FirstOrDefault();
            if (fuse == null)
                throw new Exception("No fuse component was found. Expected A_OUT/B_OUT/C_OUT reference points.");

            return fuse.ComponentName;
        }

        private static List<LoubaoGroup> FindLoubaoGroups(List<FoundPoint> foundPoints, string fuseComponent)
        {
            return foundPoints
                .GroupBy(p => p.ComponentName)
                .Where(g => !SameText(g.Key, fuseComponent))
                .Where(g => PhaseNames.All(phase => g.Any(p => SameText(p.PointName, phase + "_IN"))))
                .Where(g => ScoreNameHint(g.Key, FuseComponentNameHints) <= ScoreNameHint(g.Key, LoubaoComponentNameHints))
                .Select(g => new LoubaoGroup
                {
                    ComponentName = g.Key,
                    CenterX = g.Where(p => p.PointName.EndsWith("_IN", StringComparison.OrdinalIgnoreCase)).Average(p => p.Position.X)
                })
                .OrderBy(g => g.CenterX)
                .ToList();
        }

        private static CollectorLayout BuildCollectorLayout(string phase, FoundPoint fuseOut, List<FoundPoint> loubaoInputs)
        {
            int phaseIndex = Array.IndexOf(PhaseNames, phase);
            if (phaseIndex < 0)
                throw new Exception("Unknown phase: " + phase);

            double baseY = loubaoInputs.Max(p => p.Position.Y) + Settings.CollectorTopClearanceY;
            double collectorY = baseY - phaseIndex * Settings.CollectorPhaseSpacing;
            double collectorZ = loubaoInputs.Average(p => p.Position.Z) + Settings.CollectorOffsetFromLoubaoInZ;

            double minX = Math.Min(fuseOut.Position.X, loubaoInputs.Min(p => p.Position.X));
            double maxX = Math.Max(fuseOut.Position.X, loubaoInputs.Max(p => p.Position.X));
            double endExtend = Settings.CollectorProfile.Width / 2.0;

            return new CollectorLayout
            {
                Y = collectorY,
                Z = collectorZ,
                StartX = minX - endExtend,
                EndX = maxX + endExtend
            };
        }

        private static BusbarRoute BuildMainFeedRoute(string phase, FoundPoint fuseOut, CollectorLayout collector)
        {
            BusbarProfile profile = Settings.GetProfile(BusbarKind.MainFeed);
            Point3 start = GetMainFeedTerminalRoutePoint(fuseOut.Position, profile, "Fuse " + phase + "_OUT");
            MainCollectorLapLayout lap = CalculateMainCollectorLap(collector);

            List<Point3> path = new List<Point3>();
            AddPathPoint(path, start);
            AddPathPoint(path, new Point3(start.X, start.Y - Settings.MainLeadOutY, start.Z));
            AddPathPoint(path, new Point3(start.X, start.Y - Settings.MainLeadOutY, lap.FrontZ));
            AddPathPoint(path, new Point3(start.X, lap.Y, lap.FrontZ));
            AddPathPoint(path, new Point3(start.X, lap.Y, lap.EndZ));

            return new BusbarRoute
            {
                Name = "Busbar_" + phase + "_MainFeed",
                Kind = BusbarKind.MainFeed,
                Profile = profile,
                CenterlinePoints = path
            };
        }

        private static MainCollectorLapLayout CalculateMainCollectorLap(CollectorLayout collector)
        {
            if (Settings.ModelingMode == BusbarModelingMode.SheetMetalBaseFlangeOpenProfile)
                return CalculateSheetMetalMainCollectorLap(collector);

            BusbarProfile collectorProfile = Settings.CollectorProfile;
            double frontZ = collector.Z + Settings.GetCollectorFrontZOffset();
            double requestedDepth = Settings.GetMainCollectorLapDepth();
            double maxDepthToCenter = collectorProfile.Width / 2.0;
            double lapDepth = Math.Min(requestedDepth, maxDepthToCenter);

            return new MainCollectorLapLayout
            {
                Y = collector.Y + Settings.GetMainCollectorLapYOffset(),
                FrontZ = frontZ,
                EndZ = frontZ - lapDepth
            };
        }

        private static MainCollectorLapLayout CalculateSheetMetalMainCollectorLap(CollectorLayout collector)
        {
            BusbarProfile mainProfile = Settings.MainFeedProfile;
            BusbarProfile collectorProfile = Settings.CollectorProfile;

            double collectorFrontZ = collector.Z + collectorProfile.Width / 2.0;
            double frontZ = collectorFrontZ + mainProfile.Width / 2.0 + Settings.MainCollectorFrontClearance;
            double requestedDepth = Settings.GetMainCollectorLapDepth();
            double lapDepth = Math.Min(requestedDepth, collectorProfile.Width / 2.0);
            double yOffset = Settings.MainCollectorLapSide == CollectorLapSide.Upper
                ? mainProfile.Thickness
                : -collectorProfile.Thickness;

            return new MainCollectorLapLayout
            {
                Y = collector.Y + yOffset,
                FrontZ = frontZ,
                EndZ = collectorFrontZ - lapDepth
            };
        }

        private static BusbarRoute BuildCollectorRoute(string phase, CollectorLayout collector)
        {
            BusbarProfile profile = Settings.GetProfile(BusbarKind.Collector);

            List<Point3> path = new List<Point3>();
            AddPathPoint(path, new Point3(collector.StartX, collector.Y, collector.Z));
            AddPathPoint(path, new Point3(collector.EndX, collector.Y, collector.Z));

            return new BusbarRoute
            {
                Name = "Busbar_" + phase + "_Collector",
                Kind = BusbarKind.Collector,
                Profile = profile,
                CenterlinePoints = path
            };
        }

        private static BusbarRoute BuildBranchRoute(string phase, int branchIndex, FoundPoint loubaoIn, CollectorLayout collector)
        {
            BusbarProfile profile = Settings.GetProfile(BusbarKind.Branch);
            Point3 start = GetTerminalRoutePoint(loubaoIn.Position, Settings.EndTerminalOffsetZSign, profile, "Loubao " + phase + "_IN");
            double lapY = CalculateBranchCollectorLapY(collector, profile);

            List<Point3> path = new List<Point3>();
            AddPathPoint(path, start);
            AddPathPoint(path, new Point3(start.X, lapY, start.Z));
            AddPathPoint(path, new Point3(start.X, lapY, collector.Z));

            return new BusbarRoute
            {
                Name = "Busbar_" + phase + "_Branch_" + branchIndex,
                Kind = BusbarKind.Branch,
                Profile = profile,
                CenterlinePoints = path
            };
        }

        private static double CalculateBranchCollectorLapY(CollectorLayout collector, BusbarProfile branchProfile)
        {
            if (Settings.ModelingMode != BusbarModelingMode.SheetMetalBaseFlangeOpenProfile)
                return collector.Y + Settings.GetBranchCollectorLapYOffset();

            double yOffset = Settings.BranchCollectorLapSide == CollectorLapSide.Upper
                ? branchProfile.Thickness
                : -Settings.CollectorProfile.Thickness;

            Console.WriteLine(
                "Branch sheet metal collector lap: collectorY=" + ToMm(collector.Y).ToString("F3") +
                " mm, yOffset=" + ToMm(yOffset).ToString("F3") +
                " mm -> sketch lapY=" + ToMm(collector.Y + yOffset).ToString("F3") + " mm");

            return collector.Y + yOffset;
        }

        private static Point3 GetMainFeedTerminalRoutePoint(Point3 reference, BusbarProfile profile, string label)
        {
            if (Settings.ModelingMode != BusbarModelingMode.SheetMetalBaseFlangeOpenProfile)
                return GetTerminalRoutePoint(reference, Settings.StartTerminalOffsetZSign, profile, label);

            Console.WriteLine(
                label +
                " sheet metal route point: keep terminal reference as busbar width center, " +
                "X=" + ToMm(reference.X).ToString("F3") +
                " mm, Z=" + ToMm(reference.Z).ToString("F3") + " mm");

            return reference;
        }

        private static Point3 GetTerminalRoutePoint(Point3 reference, int zSign, BusbarProfile profile, string label)
        {
            Point3 routePoint;

            if (Settings.ModelingMode == BusbarModelingMode.SheetMetalBaseFlangeOpenProfile)
                routePoint = reference;
            else
                routePoint = OffsetTerminalCenter(reference, zSign, profile);

            Console.WriteLine(
                label +
                " route point: refZ=" + ToMm(reference.Z).ToString("F3") +
                " mm -> pathZ=" + ToMm(routePoint.Z).ToString("F3") +
                " mm");

            return routePoint;
        }

        private static Point3 OffsetTerminalCenter(Point3 reference, int zSign, BusbarProfile profile)
        {
            return new Point3(reference.X, reference.Y, reference.Z + zSign * profile.TerminalFaceOffset);
        }

        private static FoundPoint FindRequiredPoint(List<FoundPoint> points, string componentName, string pointName)
        {
            FoundPoint point = points.FirstOrDefault(p => SameText(p.ComponentName, componentName) && SameText(p.PointName, pointName));
            if (point == null)
                throw new Exception("Missing reference point: " + componentName + "." + pointName);

            return point;
        }

        private static int ScoreNameHint(string componentName, string[] hints)
        {
            if (string.IsNullOrWhiteSpace(componentName))
                return 0;

            int score = 0;
            foreach (string hint in hints)
            {
                if (!string.IsNullOrWhiteSpace(hint) &&
                    componentName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score++;
                }
            }

            return score;
        }

        private static void PrintRoute(BusbarRoute route)
        {
            Console.WriteLine("Route: " + route.Name + " [" + route.Kind + "]");
            for (int i = 0; i < route.CenterlinePoints.Count; i++)
                PrintPathPoint("  P" + i, route.CenterlinePoints[i]);
        }

        private static void AddPathPoint(List<Point3> path, Point3 point)
        {
            if (path.Count == 0 || path[path.Count - 1].DistanceTo(point) > Mm(0.01))
                path.Add(point);
        }

        private static SketchSegment CreateLineOrThrow(SketchManager sketchManager, Point3 a, Point3 b, string name)
        {
            SketchSegment segment = sketchManager.CreateLine(a.X, a.Y, a.Z, b.X, b.Y, b.Z);
            if (segment == null)
                throw new Exception("Failed to create sketch line: " + name);

            return segment;
        }

        private static void CreateBusbarSolidPart(SldWorks swApp, ModelDoc2 assemblyModel, AssemblyDoc assembly, BusbarRoute route)
        {
            Console.WriteLine();
            Console.WriteLine("===== Create busbar part =====");
            Console.WriteLine(route.Name + " / " + (route.Profile ?? Settings.GetProfile(route.Kind)).Label + "mm");

            ModelDoc2 partModel = NewPartDocument(swApp);
            ActivateDocument(swApp, partModel);

            Feature sheetMetalFeature = CreateBusbarFeature(swApp, partModel, route);

            if (sheetMetalFeature != null)
                sheetMetalFeature.Name = route.Name;

            partModel.EditRebuild3();

            string savePath = SaveBusbarPart(partModel, assemblyModel, route);
            InsertBusbarPartIntoAssembly(swApp, assemblyModel, assembly, savePath, route);
            CloseBusbarPartDocument(swApp, assemblyModel, partModel);

            Console.WriteLine("Busbar part created and inserted.");
        }

        private static Feature CreateBusbarFeature(SldWorks swApp, ModelDoc2 partModel, BusbarRoute route)
        {
            if (Settings.ModelingMode == BusbarModelingMode.SheetMetalBaseFlangeOpenProfile)
                return CreateSheetMetalOpenProfileBaseFlangeFeature(swApp, partModel, route);

            BusbarPathSketchResult pathSketch = CreatePart3DPathSketch(partModel, route);
            Feature profileSketch = CreatePartProfileSketch(swApp, partModel, route);
            return CreateSweptFlangeFeature(partModel, profileSketch, pathSketch, route);
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

        private static BusbarPathSketchResult CreatePart3DPathSketch(ModelDoc2 partModel, BusbarRoute route)
        {
            if (route.CenterlinePoints == null || route.CenterlinePoints.Count < 2)
                throw new Exception("Route has fewer than two points: " + route.Name);

            partModel.ClearSelection2(true);

            SketchManager sketchManager = partModel.SketchManager;
            List<SketchSegment> pathSegments = new List<SketchSegment>();
            bool sketchOpened = false;

            try
            {
                sketchManager.Insert3DSketch(true);
                sketchOpened = true;

                for (int i = 0; i < route.CenterlinePoints.Count - 1; i++)
                {
                    pathSegments.Add(
                        CreateLineOrThrow(
                            sketchManager,
                            route.CenterlinePoints[i],
                            route.CenterlinePoints[i + 1],
                            "path segment " + i));
                }
            }
            finally
            {
                if (sketchOpened)
                    sketchManager.Insert3DSketch(true);
            }

            Feature sketch = partModel.FeatureByPositionReverse(0) as Feature;
            if (sketch == null)
                throw new Exception("Path sketch was created but could not be located.");

            sketch.Name = route.Name + "_Path";
            return new BusbarPathSketchResult
            {
                Feature = sketch,
                Sketch = sketch.GetSpecificFeature2() as Sketch,
                Segments = pathSegments
            };
        }

        private static Feature CreateSheetMetalOpenProfileBaseFlangeFeature(SldWorks swApp, ModelDoc2 partModel, BusbarRoute route)
        {
            BusbarProfile profile = route.Profile ?? Settings.GetProfile(route.Kind);
            Console.WriteLine(
                "Create sheet metal base flange from open planar route: " +
                profile.Label +
                "mm, R=" + Settings.SheetMetalBendRadiusMm.ToString("0.###") +
                "mm, K=" + Settings.SheetMetalKFactor.ToString("0.###"));

            SheetMetalOpenProfilePlane profilePlane = GetOpenProfilePlane(route);
            Feature sketch = CreateRouteOpenProfileSketch(swApp, partModel, route, profilePlane);
            Feature feature = CreateSheetMetalBaseFlangeFromSelectedSketch(partModel, sketch, profile, route.Kind);

            if (feature == null)
                throw new Exception("Open-profile base flange creation failed: " + route.Name);

            ApplySheetMetalParametersToCreatedFeature(feature, partModel, profile);
            return feature;
        }

        private static SheetMetalOpenProfilePlane GetOpenProfilePlane(BusbarRoute route)
        {
            if (route == null || route.CenterlinePoints == null || route.CenterlinePoints.Count < 2)
                throw new Exception("Route is invalid for open-profile sheet metal.");

            bool sameX = AllSameCoordinate(route.CenterlinePoints, AxisDirection.X);
            bool sameY = AllSameCoordinate(route.CenterlinePoints, AxisDirection.Y);
            bool sameZ = AllSameCoordinate(route.CenterlinePoints, AxisDirection.Z);

            if (route.Kind == BusbarKind.Collector)
            {
                if (!sameZ)
                    throw new Exception("Collector route must stay on a constant Z plane: " + route.Name);

                return new SheetMetalOpenProfilePlane("Front", route.CenterlinePoints[0].Z, AxisDirection.X, AxisDirection.Y);
            }

            if (sameX)
                return new SheetMetalOpenProfilePlane("Right", route.CenterlinePoints[0].X, AxisDirection.Y, AxisDirection.Z);

            if (sameY)
                return new SheetMetalOpenProfilePlane("Top", route.CenterlinePoints[0].Y, AxisDirection.X, AxisDirection.Z);

            if (sameZ)
                return new SheetMetalOpenProfilePlane("Front", route.CenterlinePoints[0].Z, AxisDirection.X, AxisDirection.Y);

            throw new Exception("Open-profile sheet metal currently supports only routes on a single X/Y/Z plane: " + route.Name);
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

        private static Feature CreateRouteOpenProfileSketch(SldWorks swApp, ModelDoc2 partModel, BusbarRoute route, SheetMetalOpenProfilePlane profilePlane)
        {
            Console.WriteLine(
                "Open-profile sketch plane: " +
                profilePlane.BasePlaneRole +
                " offset=" + ToMm(profilePlane.Offset).ToString("F3") + " mm");

            Feature plane = CreateOffsetPlane(partModel, profilePlane.BasePlaneRole, profilePlane.Offset);

            partModel.ClearSelection2(true);
            if (!plane.Select2(false, 0))
                throw new Exception("Failed to select open-profile sketch plane: " + route.Name);

            SketchManager sketchManager = partModel.SketchManager;
            sketchManager.InsertSketch(true);

            bool sketchStillOpen = true;

            try
            {
                Sketch activeSketch = partModel.GetActiveSketch2() as Sketch;
                if (activeSketch == null)
                    throw new Exception("Failed to get active open-profile sketch: " + route.Name);

                MathTransform modelToSketch = activeSketch.ModelToSketchTransform;
                if (modelToSketch == null)
                    throw new Exception("Failed to get open-profile ModelToSketchTransform: " + route.Name);

                List<Point3> sketchPoints = GetOpenProfileSketchPointOrder(route);

                for (int i = 0; i < sketchPoints.Count - 1; i++)
                {
                    Point3 p1 = FlattenSketchPoint(ModelPointToSketchPoint(swApp, sketchPoints[i], modelToSketch));
                    Point3 p2 = FlattenSketchPoint(ModelPointToSketchPoint(swApp, sketchPoints[i + 1], modelToSketch));
                    CreateLineOrThrow(sketchManager, p1, p2, "open profile segment " + i);
                }

                sketchManager.InsertSketch(true);
                sketchStillOpen = false;
            }
            finally
            {
                if (sketchStillOpen)
                    sketchManager.InsertSketch(true);
            }

            Feature sketch = partModel.FeatureByPositionReverse(0) as Feature;
            if (sketch == null)
                throw new Exception("Open-profile sketch was created but could not be located.");

            sketch.Name = route.Name + "_OpenProfile";
            return sketch;
        }

        private static List<Point3> GetOpenProfileSketchPointOrder(BusbarRoute route)
        {
            List<Point3> points = route.CenterlinePoints;

            if (route.Kind == BusbarKind.Branch && Settings.ReverseBranchOpenProfileSketchDirection)
            {
                Console.WriteLine("Open-profile sketch draw order: reversed for branch material side alignment.");
                List<Point3> reversed = new List<Point3>(route.CenterlinePoints);
                reversed.Reverse();
                points = reversed;
            }

            return ApplyOpenProfileSketchCompensation(route, points);
        }

        private static List<Point3> ApplyOpenProfileSketchCompensation(BusbarRoute route, List<Point3> points)
        {
            BusbarProfile profile = route.Profile ?? Settings.GetProfile(route.Kind);
            List<Point3> compensated = new List<Point3>(points);

            if (route.Kind == BusbarKind.Branch)
                ApplyBranchTerminalClearance(compensated, route.CenterlinePoints, profile);

            for (int i = 0; i < compensated.Count; i++)
            {
                Point3 original = i < points.Count ? points[i] : compensated[i];
                if (original.DistanceTo(compensated[i]) <= Mm(0.001))
                    continue;

                Console.WriteLine(
                    "Open-profile sketch compensation [" + route.Kind + "] P" + i +
                    ": dX=" + ToMm(compensated[i].X - original.X).ToString("F3") +
                    " mm, dY=" + ToMm(compensated[i].Y - original.Y).ToString("F3") +
                    " mm, dZ=" + ToMm(compensated[i].Z - original.Z).ToString("F3") + " mm");
            }

            return compensated;
        }

        private static void ApplyBranchTerminalClearance(List<Point3> sketchPoints, List<Point3> routePoints, BusbarProfile profile)
        {
            if (sketchPoints == null || sketchPoints.Count < 2 || routePoints == null || routePoints.Count < 2)
                return;

            Point3 routeStart = routePoints[0];
            int terminalIndex = FindPointIndex(sketchPoints, routeStart);
            if (terminalIndex < 0)
                return;

            double zOffset = Settings.EndTerminalOffsetZSign * profile.Thickness;
            Point3 terminal = sketchPoints[terminalIndex];
            sketchPoints[terminalIndex] = new Point3(terminal.X, terminal.Y, terminal.Z + zOffset);

            int nextIndex = terminalIndex == 0 ? 1 : terminalIndex - 1;
            Point3 next = sketchPoints[nextIndex];

            if (Math.Abs(next.Z - routeStart.Z) <= Mm(0.01))
                sketchPoints[nextIndex] = new Point3(next.X, next.Y, next.Z + zOffset);

            Console.WriteLine(
                "Branch terminal sketch clearance: zOffset=" + ToMm(zOffset).ToString("F3") +
                " mm from branch thickness " + ToMm(profile.Thickness).ToString("F3") +
                " mm, terminalIndex=" + terminalIndex);
        }

        private static int FindPointIndex(List<Point3> points, Point3 target)
        {
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i].DistanceTo(target) <= Mm(0.01))
                    return i;
            }

            return -1;
        }

        private static Feature CreatePartProfileSketch(SldWorks swApp, ModelDoc2 partModel, BusbarRoute route)
        {
            Point3 center = route.CenterlinePoints[0];
            AxisDirection firstAxis = GetDominantAxis(route.CenterlinePoints[0], route.CenterlinePoints[1]);
            BusbarProfile profile = route.Profile ?? Settings.GetProfile(route.Kind);
            Feature profilePlane = CreateProfilePlane(partModel, center, firstAxis);

            partModel.ClearSelection2(true);
            if (!profilePlane.Select2(false, 0))
                throw new Exception("Failed to select profile plane.");

            SketchManager sketchManager = partModel.SketchManager;
            sketchManager.InsertSketch(true);

            Sketch activeSketch = partModel.GetActiveSketch2() as Sketch;
            if (activeSketch == null)
                throw new Exception("Failed to get active profile sketch.");

            MathTransform modelToSketch = activeSketch.ModelToSketchTransform;
            if (modelToSketch == null)
                throw new Exception("Failed to get ModelToSketchTransform for profile sketch.");

            Point3 modelStart;
            Point3 modelEnd;
            BuildProfileLineEndpoints(center, firstAxis, profile.Width / 2.0, out modelStart, out modelEnd);

            Point3 sketchStart = FlattenSketchPoint(ModelPointToSketchPoint(swApp, modelStart, modelToSketch));
            Point3 sketchEnd = FlattenSketchPoint(ModelPointToSketchPoint(swApp, modelEnd, modelToSketch));

            try
            {
                CreateLineOrThrow(sketchManager, sketchStart, sketchEnd, "sheet metal profile");
            }
            finally
            {
                sketchManager.InsertSketch(true);
            }

            Feature sketch = partModel.FeatureByPositionReverse(0) as Feature;
            if (sketch == null)
                throw new Exception("Profile sketch was created but could not be located.");

            sketch.Name = "Busbar_Profile_" + profile.Label;
            return sketch;
        }

        private static Feature CreateProfilePlane(ModelDoc2 partModel, Point3 center, AxisDirection firstAxis)
        {
            if (firstAxis == AxisDirection.Y)
                return CreateOffsetPlane(partModel, "Top", center.Y);

            if (firstAxis == AxisDirection.X)
                return CreateOffsetPlane(partModel, "Right", center.X);

            return CreateOffsetPlane(partModel, "Front", center.Z);
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

        private static AxisDirection GetDominantAxis(Point3 a, Point3 b)
        {
            double dx = Math.Abs(b.X - a.X);
            double dy = Math.Abs(b.Y - a.Y);
            double dz = Math.Abs(b.Z - a.Z);

            if (dx >= dy && dx >= dz)
                return AxisDirection.X;

            if (dy >= dx && dy >= dz)
                return AxisDirection.Y;

            return AxisDirection.Z;
        }

        private static void BuildProfileLineEndpoints(Point3 center, AxisDirection firstAxis, double halfWidth, out Point3 start, out Point3 end)
        {
            if (firstAxis == AxisDirection.X)
            {
                start = new Point3(center.X, center.Y, center.Z - halfWidth);
                end = new Point3(center.X, center.Y, center.Z + halfWidth);
                return;
            }

            if (firstAxis == AxisDirection.Z)
            {
                start = new Point3(center.X - halfWidth, center.Y, center.Z);
                end = new Point3(center.X + halfWidth, center.Y, center.Z);
                return;
            }

            start = new Point3(center.X - halfWidth, center.Y, center.Z);
            end = new Point3(center.X + halfWidth, center.Y, center.Z);
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

        private static Feature CreateSweptFlangeFeature(ModelDoc2 partModel, Feature profileSketchFeature, BusbarPathSketchResult pathSketch, BusbarRoute route)
        {
            Console.WriteLine("Create sheet metal swept flange...");

            Sketch profileSketch = GetSketchFromFeature(profileSketchFeature, "profile");
            object pathObjects = GetSheetMetalPathObjects(pathSketch);

            FeatureManager featureManager = partModel.FeatureManager;
            ISweptFlangeFeatureData featureData = featureManager.CreateDefinition((int)swFeatureNameID_e.swFmSweptFlange) as ISweptFlangeFeatureData;
            if (featureData == null)
                throw new Exception("Failed to create swept flange feature definition.");

            CustomBendAllowance bendAllowance = CreateKFactorBendAllowance(featureManager);

            featureData.Path = pathObjects;
            featureData.Profile = profileSketch;
            featureData.StartOffset = 0.0;
            featureData.EndOffset = 0.0;
            featureData.TrimSideBends = true;
            featureData.ReverseDirection = Settings.SheetMetalReverseDirection;
            featureData.FlattenAlongPath = false;
            featureData.UseGaugeTable = false;
            featureData.UseMaterialSheetMetalParameters = false;
            featureData.OverrideDefaultSheetMetalParameters = true;
            featureData.UseDefaultRadius = false;
            featureData.BendRadius = Settings.SheetMetalBendRadius;
            featureData.Thickness = (route.Profile ?? Settings.GetProfile(route.Kind)).Thickness;
            featureData.FlangePosition = Settings.SheetMetalFlangePosition;
            featureData.UseDefaultBendAllowance = false;
            featureData.SetCustomBendAllowance(bendAllowance);
            featureData.UseDefaultBendRelief = false;
            featureData.UseReliefRatio = false;
            featureData.ReliefType = (int)swSheetMetalReliefTypes_e.swSheetMetalReliefRectangular;
            featureData.ReliefWidth = Math.Max((route.Profile ?? Settings.GetProfile(route.Kind)).Thickness, Settings.SheetMetalBendRadius);
            featureData.ReliefDepth = Math.Max((route.Profile ?? Settings.GetProfile(route.Kind)).Thickness, Settings.SheetMetalBendRadius);

            Feature sweptFlange = featureManager.CreateFeature(featureData);
            if (sweptFlange == null)
                throw new Exception("Swept flange creation failed. ErrorCode=" + featureData.GetErrorCodes());

            partModel.ClearSelection2(true);
            ApplySheetMetalParametersToCreatedFeature(sweptFlange, partModel, route.Profile ?? Settings.GetProfile(route.Kind));
            return sweptFlange;
        }

        private static Feature CreateSheetMetalBaseFlangeFromActiveOpenProfile(ModelDoc2 partModel, BusbarProfile profile)
        {
            CustomBendAllowance bendAllowance = CreateKFactorBendAllowance(partModel.FeatureManager);
            SheetMetalBaseFlangeExtent widthExtent = GetSheetMetalBaseFlangeExtent(profile, SheetMetalWidthSide.Center);

            return partModel.FeatureManager.InsertSheetMetalBaseFlange2(
                profile.Thickness,
                Settings.SheetMetalThickenDirection,
                Settings.SheetMetalBendRadius,
                widthExtent.Dist1,
                widthExtent.Dist2,
                widthExtent.FlipExtrudeDirection,
                widthExtent.EndCondition1,
                widthExtent.EndCondition2,
                widthExtent.DirToUse,
                bendAllowance,
                false,
                (int)swSheetMetalReliefTypes_e.swSheetMetalReliefObround,
                Mm(0.1),
                Mm(0.1),
                0.5,
                true,
                false,
                true,
                true);
        }

        private static Feature CreateSheetMetalBaseFlangeFromSelectedSketch(ModelDoc2 partModel, Feature sketch, BusbarProfile profile, BusbarKind kind)
        {
            partModel.ClearSelection2(true);
            if (!sketch.Select2(false, 0))
                throw new Exception("Failed to select open-profile sketch.");

            CustomBendAllowance bendAllowance = CreateKFactorBendAllowance(partModel.FeatureManager);
            SheetMetalBaseFlangeExtent widthExtent = GetSheetMetalBaseFlangeExtent(profile, Settings.GetSheetMetalWidthSide(kind));

            Console.WriteLine(
                "Sheet metal width side [" + kind + "]: " +
                Settings.GetSheetMetalWidthSide(kind) +
                ", mode=" + widthExtent.ModeLabel +
                ", dist1=" + ToMm(widthExtent.Dist1).ToString("F3") +
                " mm, dist2=" + ToMm(widthExtent.Dist2).ToString("F3") +
                " mm, end1=" + widthExtent.EndCondition1 +
                ", end2=" + widthExtent.EndCondition2 +
                ", dirToUse=" + widthExtent.DirToUse);

            Feature feature = partModel.FeatureManager.InsertSheetMetalBaseFlange2(
                profile.Thickness,
                Settings.SheetMetalThickenDirection,
                Settings.SheetMetalBendRadius,
                widthExtent.Dist1,
                widthExtent.Dist2,
                widthExtent.FlipExtrudeDirection,
                widthExtent.EndCondition1,
                widthExtent.EndCondition2,
                widthExtent.DirToUse,
                bendAllowance,
                false,
                (int)swSheetMetalReliefTypes_e.swSheetMetalReliefObround,
                Mm(0.1),
                Mm(0.1),
                0.5,
                true,
                false,
                true,
                true);

            partModel.ClearSelection2(true);
            return feature;
        }

        private static SheetMetalBaseFlangeExtent GetSheetMetalBaseFlangeExtent(BusbarProfile profile, SheetMetalWidthSide side)
        {
            const int direction1 = 1;
            double halfWidth = profile.Width / 2.0;

            if (side == SheetMetalWidthSide.Positive)
            {
                return new SheetMetalBaseFlangeExtent(
                    profile.Width,
                    0.0,
                    false,
                    (int)swEndConditions_e.swEndCondBlind,
                    (int)swEndConditions_e.swEndCondBlind,
                    direction1,
                    "Direction1");
            }

            if (side == SheetMetalWidthSide.Negative)
            {
                return new SheetMetalBaseFlangeExtent(
                    profile.Width,
                    0.0,
                    true,
                    (int)swEndConditions_e.swEndCondBlind,
                    (int)swEndConditions_e.swEndCondBlind,
                    direction1,
                    "Direction1Flipped");
            }

            // Use SOLIDWORKS' real Mid Plane end condition.
            // Setting Dist1/Dist2 to half width with Blind conditions is not the same as the UI "Mid Plane" option.
            return new SheetMetalBaseFlangeExtent(
                profile.Width,
                0.0,
                false,
                (int)swEndConditions_e.swEndCondMidPlane,
                (int)swEndConditions_e.swEndCondBlind,
                direction1,
                "MidPlane");
        }

        private static void RunSheetMetalOpenLineDemo(SldWorks swApp, ModelDoc2 assemblyModel, AssemblyDoc assembly)
        {
            Console.WriteLine();
            Console.WriteLine("===== Open-line sheet metal demo =====");

            ModelDoc2 partModel = NewPartDocument(swApp);
            ActivateDocument(swApp, partModel);

            BusbarProfile profile = Settings.MainFeedProfile;
            Feature baseFlange = CreateSheetMetalOpenLineBaseFlangeDemo(partModel, Mm(200.0), profile);

            if (baseFlange != null)
                baseFlange.Name = "Busbar_SheetMetal_OpenLine_Demo";

            partModel.EditRebuild3();

            string savePath = SaveDemoBusbarPart(partModel, assemblyModel, "Busbar_SheetMetal_OpenLine_Demo_" + profile.Label);
            InsertDemoPartIntoAssembly(swApp, assemblyModel, assembly, savePath, "Busbar_SheetMetal_OpenLine_Demo_" + profile.Label);
            CloseBusbarPartDocument(swApp, assemblyModel, partModel);
        }

        private static Feature CreateSheetMetalOpenLineBaseFlangeDemo(ModelDoc2 partModel, double lineLength, BusbarProfile profile)
        {
            Feature frontPlane = FindDefaultPlane(partModel, "Front");
            if (frontPlane == null)
                throw new Exception("Front plane was not found for open-line demo.");

            partModel.ClearSelection2(true);
            if (!frontPlane.Select2(false, 0))
                throw new Exception("Failed to select front plane for open-line demo.");

            SketchManager sketchManager = partModel.SketchManager;
            sketchManager.InsertSketch(true);

            bool sketchStillOpen = true;

            try
            {
                double halfLength = lineLength / 2.0;
                CreateLineOrThrow(
                    sketchManager,
                    new Point3(-halfLength, 0, 0),
                    new Point3(halfLength, 0, 0),
                    "open line demo");

                Feature feature = CreateSheetMetalBaseFlangeFromActiveOpenProfile(partModel, profile);
                sketchStillOpen = false;
                partModel.ClearSelection2(true);

                if (feature == null)
                    throw new Exception("Open-line demo base flange creation failed.");

                ApplySheetMetalParametersToCreatedFeature(feature, partModel, profile);
                return feature;
            }
            finally
            {
                if (sketchStillOpen)
                    sketchManager.InsertSketch(true);
            }
        }

        private static void RunSheetMetalBentLineDemo(SldWorks swApp, ModelDoc2 assemblyModel, AssemblyDoc assembly)
        {
            Console.WriteLine();
            Console.WriteLine("===== Bent-line sheet metal demo =====");

            ModelDoc2 partModel = NewPartDocument(swApp);
            ActivateDocument(swApp, partModel);

            BusbarProfile profile = Settings.MainFeedProfile;
            Feature baseFlange = CreateSheetMetalBentLineBaseFlangeDemo(partModel, Mm(120.0), Mm(80.0), profile);

            if (baseFlange != null)
                baseFlange.Name = "Busbar_SheetMetal_BentLine_Demo";

            partModel.EditRebuild3();

            string savePath = SaveDemoBusbarPart(partModel, assemblyModel, "Busbar_SheetMetal_BentLine_Demo_" + profile.Label);
            InsertDemoPartIntoAssembly(swApp, assemblyModel, assembly, savePath, "Busbar_SheetMetal_BentLine_Demo_" + profile.Label);
            CloseBusbarPartDocument(swApp, assemblyModel, partModel);
        }

        private static Feature CreateSheetMetalBentLineBaseFlangeDemo(ModelDoc2 partModel, double firstLength, double secondLength, BusbarProfile profile)
        {
            Feature frontPlane = FindDefaultPlane(partModel, "Front");
            if (frontPlane == null)
                throw new Exception("Front plane was not found for bent-line demo.");

            partModel.ClearSelection2(true);
            if (!frontPlane.Select2(false, 0))
                throw new Exception("Failed to select front plane for bent-line demo.");

            SketchManager sketchManager = partModel.SketchManager;
            sketchManager.InsertSketch(true);

            bool sketchStillOpen = true;

            try
            {
                Point3 p0 = new Point3(0, 0, 0);
                Point3 p1 = new Point3(firstLength, 0, 0);
                Point3 p2 = new Point3(firstLength, secondLength, 0);

                CreateLineOrThrow(sketchManager, p0, p1, "bent line demo segment 1");
                CreateLineOrThrow(sketchManager, p1, p2, "bent line demo segment 2");

                Feature feature = CreateSheetMetalBaseFlangeFromActiveOpenProfile(partModel, profile);
                sketchStillOpen = false;
                partModel.ClearSelection2(true);

                if (feature == null)
                    throw new Exception("Bent-line demo base flange creation failed.");

                ApplySheetMetalParametersToCreatedFeature(feature, partModel, profile);
                return feature;
            }
            finally
            {
                if (sketchStillOpen)
                    sketchManager.InsertSketch(true);
            }
        }

        private static Sketch GetSketchFromFeature(Feature feature, string role)
        {
            if (feature == null)
                throw new Exception("Missing " + role + " sketch feature.");

            Sketch sketch = feature.GetSpecificFeature2() as Sketch;
            if (sketch == null)
                throw new Exception("Failed to get " + role + " sketch object.");

            return sketch;
        }

        private static object GetSheetMetalPathObjects(BusbarPathSketchResult pathSketch)
        {
            if (pathSketch == null || pathSketch.Segments == null || pathSketch.Segments.Count == 0)
                throw new Exception("Sheet metal swept flange path is empty.");

            object[] sketchPaths = GetSketchPaths(pathSketch);
            if (sketchPaths != null && sketchPaths.Length > 0)
            {
                Console.WriteLine("Sheet metal path mode: SketchPath x " + sketchPaths.Length);
                return sketchPaths;
            }

            Console.WriteLine("Sheet metal path mode: SketchSegment x " + pathSketch.Segments.Count);
            object[] pathObjects = new object[pathSketch.Segments.Count];
            for (int i = 0; i < pathSketch.Segments.Count; i++)
                pathObjects[i] = pathSketch.Segments[i];

            return pathObjects;
        }

        private static object[] GetSketchPaths(BusbarPathSketchResult pathSketch)
        {
            if (pathSketch == null || pathSketch.Sketch == null)
                return null;

            object rawPaths = pathSketch.Sketch.GetSketchPaths();
            return rawPaths as object[];
        }

        private static CustomBendAllowance CreateKFactorBendAllowance(FeatureManager featureManager)
        {
            CustomBendAllowance bendAllowance = featureManager.CreateCustomBendAllowance();
            if (bendAllowance == null)
                throw new Exception("Failed to create custom bend allowance.");

            bendAllowance.Type = (int)swBendAllowanceTypes_e.swBendAllowanceKFactor;
            bendAllowance.KFactor = Settings.SheetMetalKFactor;
            return bendAllowance;
        }

        private static void ApplySheetMetalParametersToCreatedFeature(Feature createdFeature, ModelDoc2 partModel, BusbarProfile profile)
        {
            Feature sheetMetalFeature = FindFirstFeatureByType(partModel, "SheetMetal");
            if (sheetMetalFeature == null)
                return;

            SheetMetalFeatureData sheetMetalData = sheetMetalFeature.GetDefinition() as SheetMetalFeatureData;
            if (sheetMetalData == null)
                return;

            sheetMetalData.Thickness = profile.Thickness;
            sheetMetalData.BendRadius = Settings.SheetMetalBendRadius;
            sheetMetalData.BendAllowanceType = (int)swBendAllowanceTypes_e.swBendAllowanceKFactor;
            sheetMetalData.KFactor = Settings.SheetMetalKFactor;
            sheetMetalData.UseMaterialSheetMetalParameters = false;
            sheetMetalData.UseAutoRelief = true;
            sheetMetalData.AutoReliefType = (int)swSheetMetalReliefTypes_e.swSheetMetalReliefRectangular;
            sheetMetalData.SetCustomBendAllowance(CreateKFactorBendAllowance(partModel.FeatureManager));
            sheetMetalFeature.ModifyDefinition(sheetMetalData, partModel, null);
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

        private static void CreateBusbarV2PreviewPart(SldWorks swApp, ModelDoc2 assemblyModel, AssemblyDoc assembly, BusbarPlanV2 plan)
        {
            if (plan == null)
                throw new Exception("Busbar V2 preview plan is null.");

            Console.WriteLine();
            Console.WriteLine("===== Create Busbar V2 preview part =====");
            Console.WriteLine("Collectors: " + plan.Collectors.Count);
            Console.WriteLine("Busbars: " + plan.Busbars.Count);
            Console.WriteLine("This preview creates sketch lines only. It does not create sheet metal solids.");

            ModelDoc2 partModel = NewPartDocument(swApp);
            ActivateDocument(swApp, partModel);

            CreateBusbarV2PreviewSketches(partModel, plan);
            partModel.EditRebuild3();

            string savePath = SaveDemoBusbarPart(partModel, assemblyModel, "Busbar_V2_Preview");
            string componentName = Path.GetFileNameWithoutExtension(savePath);
            InsertDemoPartIntoAssembly(swApp, assemblyModel, assembly, savePath, componentName);
            CloseBusbarPartDocument(swApp, assemblyModel, partModel);

            Console.WriteLine("Busbar V2 preview part inserted: " + componentName);
            Console.WriteLine("Preview convention:");
            Console.WriteLine("  Collector_*_Center sketches = planned collector centerlines.");
            Console.WriteLine("  *_Logical sketches = hole-center route references.");
            Console.WriteLine("  *_SheetMetal sketches = current sheet-metal sketch paths with end margins.");
        }

        private static void CreateBusbarV2PreviewSketches(ModelDoc2 partModel, BusbarPlanV2 plan)
        {
            foreach (CollectorLayoutV2 collector in plan.Collectors)
            {
                List<Point3> points = new List<Point3>
                {
                    new Point3(collector.StartX, collector.Center.Y, collector.Center.Z),
                    new Point3(collector.EndX, collector.Center.Y, collector.Center.Z)
                };

                CreatePreview3DPolylineSketch(
                    partModel,
                    "Preview_Collector_" + collector.Phase + "_Center",
                    points,
                    false);
            }

            foreach (BusbarV2 busbar in plan.Busbars)
            {
                string safeName = ToSafeFeatureName(busbar.Name);

                CreatePreview3DPolylineSketch(
                    partModel,
                    "Preview_" + safeName + "_Logical",
                    busbar.LogicalCenterline,
                    true);

                CreatePreview3DPolylineSketch(
                    partModel,
                    "Preview_" + safeName + "_SheetMetal",
                    busbar.SheetMetalSketchLine,
                    false);
            }
        }

        private static Feature CreatePreview3DPolylineSketch(ModelDoc2 partModel, string sketchName, List<Point3> points, bool constructionGeometry)
        {
            if (points == null || points.Count < 2)
                return null;

            partModel.ClearSelection2(true);
            SketchManager sketchManager = partModel.SketchManager;
            bool sketchOpened = false;
            int segmentCount = 0;

            try
            {
                sketchManager.Insert3DSketch(true);
                sketchOpened = true;

                for (int i = 0; i < points.Count - 1; i++)
                {
                    if (points[i].DistanceTo(points[i + 1]) <= Mm(0.01))
                        continue;

                    SketchSegment segment = CreateLineOrThrow(
                        sketchManager,
                        points[i],
                        points[i + 1],
                        sketchName + " segment " + i);

                    segment.ConstructionGeometry = constructionGeometry;
                    segmentCount++;
                }
            }
            finally
            {
                if (sketchOpened)
                    sketchManager.Insert3DSketch(true);
            }

            if (segmentCount == 0)
                return null;

            Feature sketch = partModel.FeatureByPositionReverse(0) as Feature;
            if (sketch == null)
                throw new Exception("Preview sketch was created but could not be located: " + sketchName);

            sketch.Name = sketchName;
            return sketch;
        }

        private static string ToSafeFeatureName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Unnamed";

            char[] chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '_')
                    chars[i] = '_';
            }

            return new string(chars);
        }

        private static List<BusbarV2> SelectBusbarsForV2SheetMetalBatch(BusbarPlanV2 plan)
        {
            if (plan == null)
                throw new Exception("Busbar V2 batch plan is null.");

            List<BusbarV2> selected = new List<BusbarV2>();

            foreach (string phase in PhaseNames)
                selected.Add(FindRequiredV2Busbar(plan, phase, BusbarKind.MainFeed));

            foreach (string phase in PhaseNames)
                selected.Add(FindRequiredV2Busbar(plan, phase, BusbarKind.Collector));

            foreach (string phase in PhaseNames)
                selected.AddRange(FindV2Busbars(plan, phase, BusbarKind.Branch));

            Console.WriteLine();
            Console.WriteLine("===== V2 sheet metal batch selection =====");
            Console.WriteLine("Target count: 3 MainFeed + 3 Collector + 9 Branch = " + selected.Count);
            foreach (BusbarV2 busbar in selected)
                Console.WriteLine("  " + busbar.Name + " [" + busbar.Kind + "] " + busbar.Profile.Label + "mm");

            return selected;
        }

        private static BusbarV2 FindRequiredV2Busbar(BusbarPlanV2 plan, string phase, BusbarKind kind)
        {
            string phasePrefix = "Busbar_" + phase + "_";

            BusbarV2 busbar = plan.Busbars
                .Where(b => b.Kind == kind && b.Name.StartsWith(phasePrefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (busbar == null)
                throw new Exception("No V2 " + kind + " busbar was planned for phase " + phase + ".");

            return busbar;
        }

        private static List<BusbarV2> FindV2Busbars(BusbarPlanV2 plan, string phase, BusbarKind kind)
        {
            string phasePrefix = "Busbar_" + phase + "_";

            List<BusbarV2> busbars = plan.Busbars
                .Where(b => b.Kind == kind && b.Name.StartsWith(phasePrefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (busbars.Count == 0)
                throw new Exception("No V2 " + kind + " busbars were planned for phase " + phase + ".");

            return busbars;
        }

        private static void CreateBusbarV2SheetMetalParts(SldWorks swApp, ModelDoc2 assemblyModel, AssemblyDoc assembly, List<BusbarV2> busbars)
        {
            if (busbars == null || busbars.Count == 0)
                throw new Exception("No V2 sheet metal busbars were selected for generation.");

            for (int i = 0; i < busbars.Count; i++)
            {
                Console.WriteLine();
                Console.WriteLine("V2 batch item " + (i + 1) + " / " + busbars.Count);
                CreateBusbarV2SheetMetalPart(swApp, assemblyModel, assembly, busbars[i]);
            }
        }

        private static void CreateBusbarV2SheetMetalPart(SldWorks swApp, ModelDoc2 assemblyModel, AssemblyDoc assembly, BusbarV2 busbar)
        {
            if (busbar == null)
                throw new Exception("Busbar V2 sheet metal target is null.");

            if (busbar.SheetMetalSketchLine == null || busbar.SheetMetalSketchLine.Count < 2)
                throw new Exception("Busbar V2 sheet metal sketch line is invalid: " + busbar.Name);

            Console.WriteLine();
            Console.WriteLine("===== Create Busbar V2 sheet metal part =====");
            Console.WriteLine(busbar.Name + " / " + busbar.Profile.Label + "mm");
            Console.WriteLine(
                "Sheet metal: MidPlane, R=" + busbar.SheetMetal.BendRadiusMm.ToString("0.###") +
                "mm, K=" + busbar.SheetMetal.KFactor.ToString("0.###"));

            ModelDoc2 partModel = NewPartDocument(swApp);
            ActivateDocument(swApp, partModel);

            Feature feature = CreateBusbarV2SheetMetalFeature(swApp, partModel, busbar);
            if (feature != null)
                feature.Name = busbar.Name + "_SheetMetal";

            CreateBusbarV2MountingHoles(swApp, partModel, busbar);
            partModel.EditRebuild3();

            string savePath = SaveBusbarV2SheetMetalPart(partModel, assemblyModel, busbar);
            InsertDemoPartIntoAssembly(swApp, assemblyModel, assembly, savePath, Path.GetFileNameWithoutExtension(savePath));
            CloseBusbarPartDocument(swApp, assemblyModel, partModel);

            Console.WriteLine("Busbar V2 sheet metal part inserted: " + savePath);
        }

        private static Feature CreateBusbarV2SheetMetalFeature(SldWorks swApp, ModelDoc2 partModel, BusbarV2 busbar)
        {
            BusbarRoute route = new BusbarRoute
            {
                Name = busbar.Name,
                Kind = busbar.Kind,
                Profile = busbar.Profile,
                CenterlinePoints = new List<Point3>(busbar.SheetMetalSketchLine)
            };

            SheetMetalOpenProfilePlane profilePlane = GetOpenProfilePlane(route);
            Feature sketch = CreateV2SheetMetalOpenProfileSketch(swApp, partModel, busbar, profilePlane);
            Feature feature = CreateSheetMetalBaseFlangeFromSelectedSketch(partModel, sketch, busbar.Profile, busbar.Kind);

            if (feature == null)
                throw new Exception("V2 open-profile base flange creation failed: " + busbar.Name);

            ApplySheetMetalParametersToCreatedFeature(feature, partModel, busbar.Profile);
            return feature;
        }

        private static void CreateBusbarV2MountingHoles(SldWorks swApp, ModelDoc2 partModel, BusbarV2 busbar)
        {
            if (busbar.MountingPorts != null && busbar.MountingPorts.Count > 0)
            {
                for (int i = 0; i < busbar.MountingPorts.Count; i++)
                    CreateBusbarV2MountingHole(swApp, partModel, busbar, busbar.MountingPorts[i], "P" + (i + 1));

                return;
            }

            CreateBusbarV2MountingHole(swApp, partModel, busbar, busbar.StartPort, "Start");
            CreateBusbarV2MountingHole(swApp, partModel, busbar, busbar.EndPort, "End");
        }

        private static void CreateBusbarV2MountingHole(SldWorks swApp, ModelDoc2 partModel, BusbarV2 busbar, ConnectionPort port, string role)
        {
            if (port == null || port.HoleDiameterMm <= 0.0)
                return;

            Console.WriteLine(
                "Create V2 mounting hole [" + role + "]: diameter=" +
                port.HoleDiameterMm.ToString("0.###") +
                "mm, face=" + port.RequiredFace +
                ", center=" + port.HoleCenter.ToMillimeterText());

            SheetMetalOpenProfilePlane holePlane = GetHoleSketchPlane(port);
            Feature plane = CreateOffsetPlane(partModel, holePlane.BasePlaneRole, holePlane.Offset);
            partModel.EditRebuild3();

            partModel.ClearSelection2(true);
            if (!plane.Select2(false, 0))
                throw new Exception("Failed to select V2 hole sketch plane: " + busbar.Name + " " + role);

            SketchManager sketchManager = partModel.SketchManager;
            partModel.InsertSketch2(true);

            bool sketchStillOpen = true;

            try
            {
                Sketch activeSketch = sketchManager.ActiveSketch as Sketch;
                if (activeSketch == null)
                    activeSketch = partModel.GetActiveSketch2() as Sketch;

                if (activeSketch == null)
                    throw new Exception("Failed to get active V2 hole sketch: " + busbar.Name + " " + role);

                MathTransform modelToSketch = activeSketch.ModelToSketchTransform;
                if (modelToSketch == null)
                    throw new Exception("Failed to get V2 hole sketch transform: " + busbar.Name + " " + role);

                Point3 sketchCenter = FlattenSketchPoint(ModelPointToSketchPoint(swApp, port.HoleCenter, modelToSketch));
                double radius = Mm(port.HoleDiameterMm) / 2.0;
                SketchSegment circle = sketchManager.CreateCircleByRadius(sketchCenter.X, sketchCenter.Y, 0.0, radius);
                if (circle == null)
                    throw new Exception("Failed to create V2 mounting hole circle: " + busbar.Name + " " + role);

                Feature activeSketchCut = CreateDirectedBlindCutFromActiveSketch(
                    partModel,
                    busbar.Name + "_HoleCut_" + role,
                    port,
                    busbar.Profile.Thickness);

                if (activeSketchCut != null)
                {
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
            }

            Feature sketch = partModel.FeatureByPositionReverse(0) as Feature;
            if (sketch == null)
                throw new Exception("V2 hole sketch was created but could not be located: " + busbar.Name + " " + role);

            sketch.Name = busbar.Name + "_Hole_" + role;
            partModel.EditRebuild3();

            Feature cut = CreateDirectedBlindCutFromSketch(
                partModel,
                sketch,
                busbar.Name + "_HoleCut_" + role,
                port,
                busbar.Profile.Thickness);
            if (cut == null)
                throw new Exception("Failed to create V2 mounting hole cut: " + busbar.Name + " " + role);
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

            Feature cut = TryCreateBlindCut(partModel, sketch, featureName, cutDepth, reverseDirection, false, "DirectedBlind");
            if (cut == null)
                cut = TryCreateBlindCut(partModel, sketch, featureName, cutDepth, !reverseDirection, false, "DirectedBlindOpposite");
            if (cut == null)
                cut = TryCreateBlindCut(partModel, sketch, featureName, cutDepth, reverseDirection, true, "DirectedBlindNormalCut");
            if (cut == null)
                cut = TryCreateBlindCutWithScope(partModel, sketch, featureName, cutDepth, reverseDirection, false, false, false, false, false, "DirectedBlindNoScope");

            return cut;
        }

        private static bool ShouldReverseHoleCutDirection(ConnectionPort port)
        {
            if (port.RequiredFace == ContactFace.Upper || port.RequiredFace == ContactFace.Left)
                return true;

            return false;
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

        private static Feature CreateV2SheetMetalOpenProfileSketch(SldWorks swApp, ModelDoc2 partModel, BusbarV2 busbar, SheetMetalOpenProfilePlane profilePlane)
        {
            Console.WriteLine(
                "V2 sheet-metal sketch plane: " +
                profilePlane.BasePlaneRole +
                " offset=" + ToMm(profilePlane.Offset).ToString("F3") + " mm");

            Feature plane = CreateOffsetPlane(partModel, profilePlane.BasePlaneRole, profilePlane.Offset);

            partModel.ClearSelection2(true);
            if (!plane.Select2(false, 0))
                throw new Exception("Failed to select V2 sheet-metal sketch plane: " + busbar.Name);

            SketchManager sketchManager = partModel.SketchManager;
            sketchManager.InsertSketch(true);

            bool sketchStillOpen = true;

            try
            {
                Sketch activeSketch = partModel.GetActiveSketch2() as Sketch;
                if (activeSketch == null)
                    throw new Exception("Failed to get active V2 sheet-metal sketch: " + busbar.Name);

                MathTransform modelToSketch = activeSketch.ModelToSketchTransform;
                if (modelToSketch == null)
                    throw new Exception("Failed to get V2 sheet-metal ModelToSketchTransform: " + busbar.Name);

                for (int i = 0; i < busbar.SheetMetalSketchLine.Count - 1; i++)
                {
                    Point3 p1 = FlattenSketchPoint(ModelPointToSketchPoint(swApp, busbar.SheetMetalSketchLine[i], modelToSketch));
                    Point3 p2 = FlattenSketchPoint(ModelPointToSketchPoint(swApp, busbar.SheetMetalSketchLine[i + 1], modelToSketch));
                    CreateLineOrThrow(sketchManager, p1, p2, "v2 sheet-metal segment " + i);
                }

                sketchManager.InsertSketch(true);
                sketchStillOpen = false;
            }
            finally
            {
                if (sketchStillOpen)
                    sketchManager.InsertSketch(true);
            }

            Feature sketch = partModel.FeatureByPositionReverse(0) as Feature;
            if (sketch == null)
                throw new Exception("V2 sheet-metal sketch was created but could not be located.");

            sketch.Name = busbar.Name + "_V2_OpenProfile";
            return sketch;
        }

        private static string SaveBusbarV2SheetMetalPart(ModelDoc2 partModel, ModelDoc2 assemblyModel, BusbarV2 busbar)
        {
            string assemblyPath = assemblyModel.GetPathName();
            string folder = string.IsNullOrWhiteSpace(assemblyPath)
                ? System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory)
                : Path.GetDirectoryName(assemblyPath);

            string savePath = Path.Combine(folder, busbar.Name + "_V2SheetMetal_" + busbar.Profile.Label + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".SLDPRT");

            int errors = 0;
            int warnings = 0;

            bool ok = partModel.Extension.SaveAs(
                savePath,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null,
                ref errors,
                ref warnings);

            if (!ok || errors != 0)
                throw new Exception("Failed to save V2 sheet metal part. Errors=" + errors + ", Warnings=" + warnings);

            return savePath;
        }

        private static string SaveDemoBusbarPart(ModelDoc2 partModel, ModelDoc2 assemblyModel, string baseName)
        {
            string assemblyPath = assemblyModel.GetPathName();
            string folder = string.IsNullOrWhiteSpace(assemblyPath)
                ? System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory)
                : Path.GetDirectoryName(assemblyPath);

            string savePath = Path.Combine(folder, baseName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".SLDPRT");

            int errors = 0;
            int warnings = 0;

            bool ok = partModel.Extension.SaveAs(
                savePath,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null,
                ref errors,
                ref warnings);

            if (!ok || errors != 0)
                throw new Exception("Failed to save demo sheet metal part. Errors=" + errors + ", Warnings=" + warnings);

            return savePath;
        }

        private static void InsertDemoPartIntoAssembly(SldWorks swApp, ModelDoc2 assemblyModel, AssemblyDoc assembly, string partPath, string componentName)
        {
            ActivateDocument(swApp, assemblyModel);

            Component2 component = assembly.AddComponent5(partPath, 0, "", false, "", 0, 0, 0);
            if (component == null)
                throw new Exception("Failed to insert demo part into assembly.");

            MathUtility utility = (MathUtility)swApp.GetMathUtility();
            MathTransform identity = (MathTransform)utility.CreateTransform(new double[]
            {
                1, 0, 0,
                0, 1, 0,
                0, 0, 1,
                0, 0, 0,
                1, 0, 0, 0
            });

            component.Transform2 = identity;
            component.Name2 = componentName;
            assemblyModel.EditRebuild3();
        }

        private static string SaveBusbarPart(ModelDoc2 partModel, ModelDoc2 assemblyModel, BusbarRoute route)
        {
            string assemblyPath = assemblyModel.GetPathName();
            string folder = string.IsNullOrWhiteSpace(assemblyPath)
                ? System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory)
                : Path.GetDirectoryName(assemblyPath);

            BusbarProfile profile = route.Profile ?? Settings.GetProfile(route.Kind);
            string savePath = Path.Combine(folder, route.Name + "_" + profile.Label + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".SLDPRT");

            int errors = 0;
            int warnings = 0;

            bool ok = partModel.Extension.SaveAs(
                savePath,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null,
                ref errors,
                ref warnings);

            if (!ok || errors != 0)
                throw new Exception("Failed to save busbar part. Errors=" + errors + ", Warnings=" + warnings);

            Console.WriteLine("Busbar part saved: " + savePath);
            return savePath;
        }

        private static void InsertBusbarPartIntoAssembly(SldWorks swApp, ModelDoc2 assemblyModel, AssemblyDoc assembly, string partPath, BusbarRoute route)
        {
            ActivateDocument(swApp, assemblyModel);

            Component2 component = assembly.AddComponent5(partPath, 0, "", false, "", 0, 0, 0);
            if (component == null)
                throw new Exception("Failed to insert busbar part into assembly.");

            MathUtility utility = (MathUtility)swApp.GetMathUtility();
            MathTransform identity = (MathTransform)utility.CreateTransform(new double[]
            {
                1, 0, 0,
                0, 1, 0,
                0, 0, 1,
                0, 0, 0,
                1, 0, 0, 0
            });

            component.Transform2 = identity;
            component.Name2 = route.Name + "_" + (route.Profile ?? Settings.GetProfile(route.Kind)).Label;
            assemblyModel.EditRebuild3();
        }

        private static void CloseBusbarPartDocument(SldWorks swApp, ModelDoc2 assemblyModel, ModelDoc2 partModel)
        {
            if (partModel == null)
                return;

            string partTitle = partModel.GetTitle();
            if (string.IsNullOrWhiteSpace(partTitle))
                return;

            ActivateDocument(swApp, assemblyModel);
            swApp.CloseDoc(partTitle);
            ActivateDocument(swApp, assemblyModel);
        }

        private static void ActivateDocument(SldWorks swApp, ModelDoc2 model)
        {
            int errors = 0;
            swApp.ActivateDoc3(model.GetTitle(), false, 0, ref errors);
        }

        private static bool SameText(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static double Mm(double value)
        {
            return value / 1000.0;
        }

        private static double ToMm(double value)
        {
            return value * 1000.0;
        }

        private static void PrintPathPoint(string label, Point3 point)
        {
            Console.WriteLine(label + " X=" + ToMm(point.X).ToString("F3") + " mm, Y=" + ToMm(point.Y).ToString("F3") + " mm, Z=" + ToMm(point.Z).ToString("F3") + " mm");
        }
    }

    internal class BusbarSettings
    {
        public BusbarModelingMode ModelingMode;
        public double MainFeedWidthMm;
        public double MainFeedThicknessMm;
        public double CollectorWidthMm;
        public double CollectorThicknessMm;
        public double BranchWidthMm;
        public double BranchThicknessMm;

        public int StartTerminalOffsetZSign;
        public int EndTerminalOffsetZSign;

        public double CollectorPhaseSpacingMm;
        public double CollectorTopClearanceYMm;
        public double CollectorOffsetFromLoubaoInZMm;
        public double CollectorNegativeXExtendMm;
        public double MainLeadOutYMm;
        public CollectorLapSide MainCollectorLapSide;
        public CollectorLapSide BranchCollectorLapSide;
        public double SheetMetalBendRadiusMm;
        public double SheetMetalKFactor;
        public int SheetMetalFlangePosition;
        public bool SheetMetalReverseDirection;
        public bool SheetMetalThickenDirection;
        public SheetMetalWidthSide MainFeedSheetMetalWidthSide;
        public SheetMetalWidthSide CollectorSheetMetalWidthSide;
        public SheetMetalWidthSide BranchSheetMetalWidthSide;
        public double MainCollectorFrontClearanceMm;
        public double MainCollectorLapDepthRatio;
        public bool ReverseBranchOpenProfileSketchDirection;

        public BusbarProfile MainFeedProfile { get { return new BusbarProfile(MainFeedWidthMm, MainFeedThicknessMm); } }
        public BusbarProfile CollectorProfile { get { return new BusbarProfile(CollectorWidthMm, CollectorThicknessMm); } }
        public BusbarProfile BranchProfile { get { return new BusbarProfile(BranchWidthMm, BranchThicknessMm); } }

        public double CollectorPhaseSpacing { get { return Mm(CollectorPhaseSpacingMm); } }
        public double CollectorTopClearanceY { get { return Mm(CollectorTopClearanceYMm); } }
        public double CollectorOffsetFromLoubaoInZ { get { return Mm(CollectorOffsetFromLoubaoInZMm); } }
        public double CollectorNegativeXExtend { get { return Mm(CollectorNegativeXExtendMm); } }
        public double MainLeadOutY { get { return Mm(MainLeadOutYMm); } }
        public double MainCollectorFrontClearance { get { return Mm(MainCollectorFrontClearanceMm); } }
        public double SheetMetalBendRadius { get { return Mm(SheetMetalBendRadiusMm); } }

        public BusbarProfile GetProfile(BusbarKind kind)
        {
            if (kind == BusbarKind.Collector)
                return CollectorProfile;

            if (kind == BusbarKind.Branch)
                return BranchProfile;

            return MainFeedProfile;
        }

        public SheetMetalWidthSide GetSheetMetalWidthSide(BusbarKind kind)
        {
            if (kind == BusbarKind.Collector)
                return CollectorSheetMetalWidthSide;

            if (kind == BusbarKind.Branch)
                return BranchSheetMetalWidthSide;

            return MainFeedSheetMetalWidthSide;
        }

        public double GetCollectorFrontZOffset()
        {
            return CollectorProfile.Width / 2.0 + MainCollectorFrontClearance;
        }

        public double GetMainCollectorLapDepth()
        {
            double overlapWindow = Math.Min(CollectorProfile.Width, MainFeedProfile.Width);
            return overlapWindow * MainCollectorLapDepthRatio;
        }

        public double GetMainCollectorLapYOffset()
        {
            double offset = CollectorProfile.Thickness / 2.0 + MainFeedProfile.Thickness / 2.0;
            return MainCollectorLapSide == CollectorLapSide.Upper ? offset : -offset;
        }

        public double GetBranchCollectorLapYOffset()
        {
            double offset = CollectorProfile.Thickness / 2.0 + BranchProfile.Thickness / 2.0;
            return BranchCollectorLapSide == CollectorLapSide.Upper ? offset : -offset;
        }

        private static double Mm(double value)
        {
            return value / 1000.0;
        }
    }

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

    internal enum BusbarKind
    {
        MainFeed,
        Collector,
        Branch
    }

    internal enum BusbarModelingMode
    {
        SheetMetalBaseFlangeOpenProfile,
        SheetMetalSweptFlange
    }

    internal enum SheetMetalWidthSide
    {
        Center,
        Positive,
        Negative
    }

    internal enum CollectorLapSide
    {
        Upper,
        Lower
    }

    internal enum AxisDirection
    {
        X,
        Y,
        Z
    }

    internal class BusbarRoute
    {
        public string Name;
        public BusbarKind Kind;
        public BusbarProfile Profile;
        public List<Point3> CenterlinePoints;
    }

    internal class BusbarPathSketchResult
    {
        public Feature Feature;
        public Sketch Sketch;
        public List<SketchSegment> Segments;
    }

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

    internal class SheetMetalBaseFlangeExtent
    {
        public double Dist1;
        public double Dist2;
        public bool FlipExtrudeDirection;
        public int EndCondition1;
        public int EndCondition2;
        public int DirToUse;
        public string ModeLabel;

        public SheetMetalBaseFlangeExtent(
            double dist1,
            double dist2,
            bool flipExtrudeDirection,
            int endCondition1,
            int endCondition2,
            int dirToUse,
            string modeLabel)
        {
            Dist1 = dist1;
            Dist2 = dist2;
            FlipExtrudeDirection = flipExtrudeDirection;
            EndCondition1 = endCondition1;
            EndCondition2 = endCondition2;
            DirToUse = dirToUse;
            ModeLabel = modeLabel;
        }
    }

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

    internal class CollectorLayout
    {
        public double Y;
        public double Z;
        public double StartX;
        public double EndX;
    }

    internal class MainCollectorLapLayout
    {
        public double Y;
        public double FrontZ;
        public double EndZ;
    }

    internal class LoubaoGroup
    {
        public string ComponentName;
        public double CenterX;
    }

    internal struct Point3
    {
        public double X;
        public double Y;
        public double Z;

        public Point3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double DistanceTo(Point3 other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            double dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public string ToMillimeterText()
        {
            return "X=" + (X * 1000.0).ToString("F3") + " mm, Y=" + (Y * 1000.0).ToString("F3") + " mm, Z=" + (Z * 1000.0).ToString("F3") + " mm";
        }
    }
}
