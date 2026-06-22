using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace SwFeatureDebug
{
    internal class Program
    {
        // =========================
        // 模块 0：用户配置区
        // =========================
        // 学习阶段建议只改这里：
        // 1. MainFeed / Collector / Branch：三类铜排规格，单位 mm。
        // 2. PhaseName：相别，例如 "A"、"B"、"C"。
        // 3. Start/EndTerminalOffsetZSign：端子参考点到扫掠中心线的 Z 向偏移方向。
        static readonly BusbarSettings Settings = new BusbarSettings
        {
            // 第一类：刀熔出线端到汇流排的转接排/主连接排。
            MainFeedWidthMm = 60.0,
            MainFeedThicknessMm = 6.0,

            // 第二类：沿 X 方向布置的 ABC 汇流排。
            CollectorWidthMm = 80.0,
            CollectorThicknessMm = 6.0,

            // 第三类：汇流排到漏保进线端的分支排。
            BranchWidthMm = 40.0,
            BranchThicknessMm = 4.0,

            // 中心线相对参考点的 Z 向偏移方向。
            // 刀熔端：B_OUT.Z + 厚度/2；漏保端：B_IN.Z - 厚度/2。
            StartTerminalOffsetZSign = 1,
            EndTerminalOffsetZSign = -1,

            // 三相汇流排沿 Y 方向等间距排列，顺序为 A、B、C。
            CollectorPhaseSpacingMm = 60.0,

            // A 相汇流排相对漏保进线点的 Y 向上方净距。
            // B/C 相会在此基础上依次向下偏移 CollectorPhaseSpacingMm。
            // 这里给 180mm：A=漏保上方180，B=120，C=60，便于后续分支排从上往下搭接。
            CollectorTopClearanceYMm = 180.0,

            // 汇流排相对漏保输入点的 Z 向位置。
            // 这里先给一套初始规则：汇流排中心线位于漏保输入点前方 120mm。
            CollectorOffsetFromLoubaoInZMm = 120.0,

            MainLeadOutYMm = 40.0,
            BranchApproachYMm = 40.0,

            // 主连接排与汇流排的搭接参数。
            // 1. 主排先走到汇流排 Z 向前缘外侧：collector.Z + 铜排宽度/2 + FrontClearance。
            // 2. 再沿 Y 到搭接层；Upper 表示搭在汇流排上端，Lower 表示搭在汇流排下端。
            // 3. 最后按铜排宽度比例自动伸入，形成搭接，避免和汇流排中心线重合穿模。
            MainCollectorLapSide = CollectorLapSide.Upper,
            MainCollectorFrontClearanceMm = 0.0,
            MainCollectorLapDepthRatio = 0.5
        };

        static readonly string[] PhaseNames = { "A", "B", "C" };
        static readonly string[] FuseComponentNameHints = { "fuse", "HR6", "熔", "刀熔", "隔离" };
        static readonly string[] LoubaoComponentNameHints = { "loubao", "PGM", "漏保" };
        const string DebugPhaseName = "B";
        static readonly bool ReplaceExistingBusbar = true;
        static readonly bool VerboseFeatureScan = false;
        static readonly bool GenerateMainFeedRoutes = true;
        static readonly bool GenerateCollectorRoutes = true;
        static readonly bool GenerateBranchRoutes = false;

        // =========================
        // 模块 1：程序主流程
        // =========================
        // 主流程只负责串联步骤：
        // 连接 SW -> 获取装配体 -> 扫描参考点 -> 生成中心线路径 -> 新建铜排零件 -> 插回装配体。
        // 函数折叠后看这里：这是程序入口，按自动铜排生成的完整流程顺序执行。
        [STAThread]
        static void Main()
        {
            try
            {
                var swApp = GetOrStartSolidWorks();
                var model = swApp.ActiveDoc as ModelDoc2;

                if (model == null)
                    throw new Exception("请先打开 SolidWorks，并打开目标装配体。");

                if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                    throw new Exception("当前活动文档不是装配体。");

                var asm = (AssemblyDoc)model;
                var foundPoints = new List<FoundPoint>();

                // 重复调试时，先删除上一根程序生成的同相铜排，避免新旧模型叠在一起误判。
                if (ReplaceExistingBusbar)
                    DeleteExistingBusbarComponents(model, asm);

                Console.WriteLine("当前装配体：" + model.GetTitle());
                Console.WriteLine();

                if (VerboseFeatureScan)
                    Console.WriteLine("===== Assembly features =====");
                // 先扫描装配体自身特征，再扫描每个零件组件的特征。
                // 参考点位于零件组件里时，需要用组件 Transform2 转成装配体坐标。
                DumpModelFeatures(swApp, model, null, null, foundPoints);

                Console.WriteLine();
                if (VerboseFeatureScan)
                    Console.WriteLine("===== Component features =====");

                object[] comps = asm.GetComponents(false) as object[];
                if (comps == null || comps.Length == 0)
                {
                    Console.WriteLine("未找到组件。");
                    return;
                }

                foreach (object obj in comps)
                {
                    var comp = obj as Component2;
                    if (comp == null) continue;

                    var compModel = comp.GetModelDoc2() as ModelDoc2;
                    if (compModel == null)
                    {
                        Console.WriteLine("[跳过] 组件未加载或轻化：" + comp.Name2);
                        continue;
                    }

                    if (VerboseFeatureScan)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Component: " + comp.Name2);
                    }
                    DumpModelFeatures(swApp, compModel, comp.Name2, comp.Transform2, foundPoints);
                }

                Console.WriteLine();
                // 根据自动识别到的 A/B/C_OUT 和多个漏保 A/B/C_IN，生成主排、汇流排、分支排三类路径。
                List<BusbarRoute> routes = BuildComplexRoutes(foundPoints);

                foreach (BusbarRoute route in routes)
                    CreateBusbarSolidPart(swApp, model, asm, route);

                Console.WriteLine();
                Console.WriteLine("扫描完成。按任意键退出。");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine("错误：" + ex.Message);
                Console.WriteLine();
                Console.WriteLine(ex);
                Console.ReadKey();
            }
        }

        // 连接已经打开的 SolidWorks；如果没找到运行中的 SW，则尝试启动一个新实例。
        static SldWorks GetOrStartSolidWorks()
        {
            try
            {
                // 优先连接已经打开的 SolidWorks。调试 API 时推荐先手动打开 SW 和目标装配体。
                Console.WriteLine("正在连接已打开的 SolidWorks...");
                return (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
            }
            catch (COMException)
            {
                // 如果没有打开 SW，就启动一个新的实例。启动后仍需要用户打开目标装配体。
                Console.WriteLine("没有从 COM 中找到已运行的 SolidWorks，尝试启动新的 SolidWorks...");

                Type swType = Type.GetTypeFromProgID("SldWorks.Application");
                if (swType == null)
                    throw new Exception("本机没有注册 SolidWorks.Application，请确认 SolidWorks 已正确安装。");

                var swApp = (SldWorks)Activator.CreateInstance(swType);
                swApp.Visible = true;
                return swApp;
            }
        }

        // 删除装配体中上一轮由本程序生成的同相铜排，避免调试时新旧铜排叠在一起。
        static void DeleteExistingBusbarComponents(ModelDoc2 model, AssemblyDoc asm)
        {
            // 只删除本程序生成的同相铜排组件，匹配前缀如 Busbar_B_。
            // 注意：这里只从装配体里移除组件，不删除磁盘上的 .SLDPRT 文件。
            string prefix = "Busbar_";
            object[] comps = asm.GetComponents(false) as object[];

            if (comps == null || comps.Length == 0)
                return;

            model.ClearSelection2(true);

            int selectedCount = 0;

            foreach (object obj in comps)
            {
                Component2 comp = obj as Component2;
                if (comp == null)
                    continue;

                if (comp.Name2 != null && comp.Name2.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    if (comp.Select4(true, null, false))
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

        // 遍历一个文档的 FeatureManager 设计树，把其中的 RefPoint 识别出来。
        // compTransform 不为空时，说明当前扫描的是零件组件，需要把零件坐标转成装配体坐标。
        static void DumpModelFeatures(SldWorks swApp, ModelDoc2 model, string compName, MathTransform compTransform, List<FoundPoint> foundPoints)
        {
            // 遍历 FeatureManager 设计树。
            // 本项目需要的是 RefPoint 特征，例如 A_IN、B_OUT、B_IN。
            Feature feat = model.FirstFeature() as Feature;

            while (feat != null)
            {
                string featName = feat.Name;
                string featType = feat.GetTypeName2();

                if (VerboseFeatureScan)
                    Console.WriteLine($"Feature: {featName}    Type: {featType}");

                TryPrintReferencePoint(swApp, feat, compName, compTransform, foundPoints);

                feat = feat.GetNextFeature() as Feature;
            }
        }

        // 尝试把一个通用 Feature 当成参考点读取；不是 RefPoint 就直接跳过。
        // 成功后会保存为 FoundPoint，坐标统一为装配体全局坐标。
        static void TryPrintReferencePoint(SldWorks swApp, Feature feat, string compName, MathTransform compTransform, List<FoundPoint> foundPoints)
        {
            // Feature.GetSpecificFeature2() 可以把通用 Feature 转成具体 API 对象。
            // 如果它是 RefPoint，就读取点坐标；如果不是，直接跳过。
            object specific = null;

            try
            {
                specific = feat.GetSpecificFeature2();
            }
            catch
            {
                return;
            }

            var refPoint = specific as RefPoint;
            if (refPoint == null)
                return;

            MathPoint localMathPoint = refPoint.GetRefPoint();
            double[] local = localMathPoint.ArrayData as double[];

            if (local == null || local.Length < 3)
                return;

            double x = local[0];
            double y = local[1];
            double z = local[2];

            if (compTransform != null)
            {
                // 零件内部坐标 -> 装配体全局坐标。
                // 这是二次开发最容易错的地方之一：零件点不能直接拿来当装配体点用。
                var mu = (MathUtility)swApp.GetMathUtility();
                var p = (MathPoint)mu.CreatePoint(new double[] { x, y, z });
                var asmPoint = (MathPoint)p.MultiplyTransform(compTransform);
                double[] asm = asmPoint.ArrayData as double[];

                x = asm[0];
                y = asm[1];
                z = asm[2];
            }

            Console.WriteLine("  >>> 找到参考点：" + feat.Name);
            Console.WriteLine("      所属组件：" + (compName ?? "装配体自身"));
            Console.WriteLine($"      装配体坐标：X={x * 1000:F3} mm, Y={y * 1000:F3} mm, Z={z * 1000:F3} mm");

            foundPoints.Add(new FoundPoint
            {
                ComponentName = compName ?? "装配体自身",
                PointName = feat.Name,
                Position = new Point3(x, y, z)
            });
        }

        // 真实配电箱第一版拓扑生成入口：
        // 1. 自动识别刀熔组件：同时拥有 A_OUT/B_OUT/C_OUT 的组件。
        // 2. 自动识别漏保组件：同时拥有 A_IN/B_IN/C_IN，且不是刀熔的组件。
        // 3. 按 X 坐标排序漏保。
        // 4. 对 A/B/C 三相分别生成：主连接排、汇流排、三个分支排。
        static List<BusbarRoute> BuildComplexRoutes(List<FoundPoint> foundPoints)
        {
            string fuseComponent = FindFuseComponent(foundPoints);
            List<LoubaoGroup> loubaos = FindLoubaoGroups(foundPoints, fuseComponent);

            if (loubaos.Count == 0)
                throw new Exception("未识别到漏保组件。请确认漏保上存在 A_IN/B_IN/C_IN 命名参考点。");

            Console.WriteLine("===== complex busbar topology =====");
            Console.WriteLine("Fuse component: " + fuseComponent);
            Console.WriteLine("Loubao count: " + loubaos.Count);
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

                if (GenerateMainFeedRoutes)
                    routes.Add(BuildMainFeedRoute(phase, fuseOut, collector));

                if (GenerateCollectorRoutes)
                    routes.Add(BuildCollectorRoute(phase, collector));

                if (GenerateBranchRoutes)
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

        // 自动识别刀熔组件：它应该同时拥有 A_OUT/B_OUT/C_OUT。
        static string FindFuseComponent(List<FoundPoint> foundPoints)
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
            foreach (var c in candidates)
                Console.WriteLine("  " + c.ComponentName + " out=" + c.OutCount + " in=" + c.InCount + " nameScore=" + c.NameScore);

            var fuse = candidates.FirstOrDefault();
            if (fuse == null)
                throw new Exception("未识别到刀熔组件。请确认刀熔上存在 A_OUT/B_OUT/C_OUT 命名参考点。");

            return fuse.ComponentName;
        }

        // 自动识别漏保组件：不是刀熔，并且同时拥有 A_IN/B_IN/C_IN。
        static List<LoubaoGroup> FindLoubaoGroups(List<FoundPoint> foundPoints, string fuseComponent)
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

        // 根据当前相、刀熔出线点和所有漏保进线点，计算该相汇流排的中心线层级。
        // X 范围来自该相所有连接点；Y 层级按 A/B/C 等间距排列；Z 位置按漏保输入点向前偏移。
        // 注意：汇流排高度不要再复用 BranchApproachY。
        // BranchApproachY 是分支排接近漏保端子的短距离，CollectorTopClearanceY 才是整组三相汇流排的安装高度。
        static CollectorLayout BuildCollectorLayout(string phase, FoundPoint fuseOut, List<FoundPoint> loubaoInputs)
        {
            int phaseIndex = Array.IndexOf(PhaseNames, phase);
            if (phaseIndex < 0)
                throw new Exception("未知相别：" + phase);

            double baseY = loubaoInputs.Max(p => p.Position.Y) + Settings.CollectorTopClearanceY;
            double collectorY = baseY - phaseIndex * Settings.CollectorPhaseSpacing;
            double collectorZ = loubaoInputs.Average(p => p.Position.Z) + Settings.CollectorOffsetFromLoubaoInZ;

            double minX = Math.Min(fuseOut.Position.X, loubaoInputs.Min(p => p.Position.X));
            double maxX = Math.Max(fuseOut.Position.X, loubaoInputs.Max(p => p.Position.X));
            double endExtend = Settings.CollectorProfile.Width / 2.0;

            return new CollectorLayout
            {
                Phase = phase,
                Y = collectorY,
                Z = collectorZ,
                StartX = minX - endExtend,
                EndX = maxX + endExtend,
                MainTapX = fuseOut.Position.X
            };
        }

        // 生成第一类：刀熔出线端到汇流排的主连接排。
        // 当前规则：从刀熔 OUT 点半厚度偏移后出发，Y 负方向让位，再到汇流排附近。
        // 关键修正：汇流排沿 X 方向时，宽度铺在 Z 方向，所以主排不能直接走到 collector.Z。
        // 路径先停在“汇流排前缘外侧”：collector.Z + 宽度/2 + 余量；
        // 再沿 Y 到搭接层，最后沿 Z 负方向伸入一段搭接深度，形成搭接而不是穿模。
        static BusbarRoute BuildMainFeedRoute(string phase, FoundPoint fuseOut, CollectorLayout collector)
        {
            BusbarProfile profile = Settings.GetProfile(BusbarKind.MainFeed);
            Point3 start = OffsetTerminalCenter(fuseOut.Position, Settings.StartTerminalOffsetZSign, profile);
            MainCollectorLapLayout lap = CalculateMainCollectorLap(collector);

            Console.WriteLine(
                "Main feed lap " + phase +
                " [" + Settings.MainCollectorLapSide + "]" +
                ": Y=" + ToMm(lap.Y).ToString("F3") +
                " mm, frontZ=" + ToMm(lap.FrontZ).ToString("F3") +
                " mm, endZ=" + ToMm(lap.EndZ).ToString("F3") +
                " mm, depth=" + ToMm(lap.Depth).ToString("F3") + " mm");

            List<Point3> path = new List<Point3>();
            AddPathPoint(path, start);
            AddPathPoint(path, new Point3(start.X, start.Y - Settings.MainLeadOutY, start.Z));
            AddPathPoint(path, new Point3(start.X, start.Y - Settings.MainLeadOutY, lap.FrontZ));
            AddPathPoint(path, new Point3(start.X, lap.Y, lap.FrontZ));
            AddPathPoint(path, new Point3(start.X, lap.Y, lap.EndZ));

            return new BusbarRoute
            {
                Name = "Busbar_" + phase + "_MainFeed",
                Phase = phase,
                Kind = BusbarKind.MainFeed,
                Profile = profile,
                CenterlinePoints = path
            };
        }

        // 计算主连接排与汇流排的搭接位置。
        // 所有 Z 向搭接值都由汇流排截面自动推导：
        // - 汇流排沿 X 方向时，宽度铺在 Z 方向，所以前缘是 collector.Z + BusbarWidth/2。
        // - 搭接深度 = BusbarWidth * MainCollectorLapDepthRatio。
        // - 搭接终点不会越过汇流排中心线，避免主排扫掠中心线插入汇流排内部过深。
        static MainCollectorLapLayout CalculateMainCollectorLap(CollectorLayout collector)
        {
            BusbarProfile mainProfile = Settings.MainFeedProfile;
            BusbarProfile collectorProfile = Settings.CollectorProfile;

            double frontZ = collector.Z + Settings.GetCollectorFrontZOffset();
            double maxDepthToCenter = collectorProfile.Width / 2.0;
            double requestedDepth = Settings.GetMainCollectorLapDepth();
            double lapDepth = Math.Min(requestedDepth, maxDepthToCenter);
            double yOffset = Settings.GetMainCollectorLapYOffset();

            return new MainCollectorLapLayout
            {
                Y = collector.Y + yOffset,
                FrontZ = frontZ,
                EndZ = frontZ - lapDepth,
                Depth = lapDepth
            };
        }

        // 生成第二类：沿 X 方向布置的汇流排。
        static BusbarRoute BuildCollectorRoute(string phase, CollectorLayout collector)
        {
            BusbarProfile profile = Settings.GetProfile(BusbarKind.Collector);

            List<Point3> path = new List<Point3>();
            AddPathPoint(path, new Point3(collector.StartX, collector.Y, collector.Z));
            AddPathPoint(path, new Point3(collector.EndX, collector.Y, collector.Z));

            return new BusbarRoute
            {
                Name = "Busbar_" + phase + "_Collector",
                Phase = phase,
                Kind = BusbarKind.Collector,
                Profile = profile,
                CenterlinePoints = path
            };
        }

        // 生成第三类：汇流排到某个漏保进线端的分支排。
        // 当前规则：从汇流排对应 X 位置下接，沿 Z 负方向到漏保端子前方，再沿 Y 负方向进入端子中心线终点。
        static BusbarRoute BuildBranchRoute(string phase, int branchIndex, FoundPoint loubaoIn, CollectorLayout collector)
        {
            BusbarProfile profile = Settings.GetProfile(BusbarKind.Branch);
            Point3 end = OffsetTerminalCenter(loubaoIn.Position, Settings.EndTerminalOffsetZSign, profile);

            List<Point3> path = new List<Point3>();
            AddPathPoint(path, new Point3(end.X, collector.Y, collector.Z));
            AddPathPoint(path, new Point3(end.X, collector.Y, end.Z));
            AddPathPoint(path, new Point3(end.X, end.Y + Settings.BranchApproachY, end.Z));
            AddPathPoint(path, end);

            return new BusbarRoute
            {
                Name = "Busbar_" + phase + "_Branch_" + branchIndex,
                Phase = phase,
                Kind = BusbarKind.Branch,
                Profile = profile,
                CenterlinePoints = path
            };
        }

        // 把端子表面参考点转换为铜排扫掠中心线点，偏移距离为该类铜排厚度的一半。
        static Point3 OffsetTerminalCenter(Point3 reference, int zSign, BusbarProfile profile)
        {
            return new Point3(reference.X, reference.Y, reference.Z + zSign * profile.TerminalFaceOffset);
        }

        // 按组件名和点名查找必需参考点；找不到就立即报错，避免生成错误几何。
        static FoundPoint FindRequiredPoint(List<FoundPoint> points, string componentName, string pointName)
        {
            FoundPoint point = points.FirstOrDefault(p => SameText(p.ComponentName, componentName) && SameText(p.PointName, pointName));
            if (point == null)
                throw new Exception("缺少参考点：" + componentName + "." + pointName);

            return point;
        }

        // 根据组件名关键词打分，用于区分刀熔和漏保。
        // 这只是当前阶段的兼容方案；长期更稳的做法是点名或组件属性里显式写 FUSE / LOUBAO。
        static int ScoreNameHint(string componentName, string[] hints)
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

        // 打印一条路径，便于检查每段折弯点。
        static void PrintRoute(BusbarRoute route)
        {
            Console.WriteLine("Route: " + route.Name + " [" + route.Kind + "]");
            for (int i = 0; i < route.CenterlinePoints.Count; i++)
                PrintPathPoint("  P" + i, route.CenterlinePoints[i]);
        }

        // 自动识别当前相的起点/终点，并打印生成的铜排中心线路径点。
        // 这里是调试路径是否正确的第一入口。
        static List<Point3> PrintBusbarBPath(List<FoundPoint> foundPoints)
        {
            // 自动从所有参考点中找当前相的起点和终点。
            // 对 B 相来说，就是找 B_OUT 和 B_IN。
            BusbarConnection connection = AutoFindConnection(foundPoints, DebugPhaseName);

            Console.WriteLine("===== " + DebugPhaseName + " phase busbar path preview =====");
            Console.WriteLine("Auto start point: " + connection.Start);
            Console.WriteLine("Auto end reference point: " + connection.End);
            Console.WriteLine("Busbar size: " + Settings.ProfileLabel + " mm");

            List<Point3> path = BuildBusbarPath(connection.Start.Position, connection.End.Position);

            Console.WriteLine(
                "Auto route values: leadY=" + ToMm(Settings.LeadY).ToString("F3") +
                " mm, firstDropZ=" + ToMm(Settings.FirstDropZ).ToString("F3") +
                " mm, endAboveZ=" + ToMm(Settings.EndAboveZ).ToString("F3") +
                " mm, terminalHalfThickness=" + ToMm(Settings.TerminalFaceOffset).ToString("F3") + " mm");

            for (int i = 0; i < path.Count; i++)
                PrintPathPoint("P" + i, path[i]);

            return path;
        }

        // 从所有 RefPoint 中自动找连接关系：刀熔 phase_OUT -> 漏保 phase_IN。
        // 目前的判定逻辑偏学习用途：带多个 *_OUT 的组件视作刀熔，另一个组件上的 phase_IN 视作漏保输入。
        static BusbarConnection AutoFindConnection(List<FoundPoint> foundPoints, string phase)
        {
            // 识别策略：
            // 1. 起点：优先选择包含多个 *_OUT 点的组件上的 phase_OUT，通常就是刀熔开关。
            // 2. 终点：选择另一个组件上的 phase_IN，且尽量避免选择同一个刀熔组件里的 B_IN。
            // 后续做 A/C 相时，只需要改 PhaseName。
            string startPointName = phase + "_OUT";
            string endPointName = phase + "_IN";

            FoundPoint start = null;
            int bestStartScore = int.MinValue;

            foreach (FoundPoint point in foundPoints)
            {
                if (!PointNameEquals(point, startPointName))
                    continue;

                int score = CountComponentPointsEndingWith(foundPoints, point.ComponentName, "_OUT");
                if (score > bestStartScore)
                {
                    start = point;
                    bestStartScore = score;
                }
            }

            if (start == null)
                throw new Exception("Cannot auto find start point: " + startPointName);

            FoundPoint end = null;
            double bestEndScore = double.MaxValue;

            foreach (FoundPoint point in foundPoints)
            {
                if (!PointNameEquals(point, endPointName))
                    continue;

                if (SameText(point.ComponentName, start.ComponentName))
                    continue;

                double score = Math.Abs(point.Position.X - start.Position.X);

                if (ComponentHasPoint(foundPoints, point.ComponentName, phase + "_OUT"))
                    score += 100000.0;

                score += CountComponentPointsEndingWith(foundPoints, point.ComponentName, "_OUT") * 1000.0;

                if (score < bestEndScore)
                {
                    end = point;
                    bestEndScore = score;
                }
            }

            if (end == null)
                throw new Exception("Cannot auto find end point in another component: " + endPointName);

            return new BusbarConnection { Start = start, End = end };
        }

        // 计算铜排扫掠中心线路径。
        // 后续要调整折弯点、避让隔板、避让其他铜排，主要就改这个函数。
        // 输入是端子表面参考点 B_OUT/B_IN，输出是已经做过半厚度偏移后的中心线点集。
        static List<Point3> BuildBusbarPath(Point3 startReference, Point3 endReference)
        {
            // 重要概念：
            // B_OUT/B_IN 是端子表面的参考点；
            // 扫掠路径应该是铜排“中心线”，不是铜排实体表面。
            // 因此中心线需要相对参考点偏移 half thickness。
            List<Point3> path = new List<Point3>();

            double terminalOffset = Settings.TerminalFaceOffset;

            Point3 startCenter = new Point3(
                startReference.X,
                startReference.Y,
                startReference.Z + Settings.StartTerminalOffsetZSign * terminalOffset);

            Point3 endCenter = new Point3(
                endReference.X,
                endReference.Y,
                endReference.Z + Settings.EndTerminalOffsetZSign * terminalOffset);

            Console.WriteLine("Start reference B_OUT:");
            PrintPathPoint("  B_OUT reference", startReference);
            PrintPathPoint("  centerline start", startCenter);
            Console.WriteLine("End reference B_IN:");
            PrintPathPoint("  B_IN reference", endReference);
            PrintPathPoint("  centerline end", endCenter);
            Console.WriteLine("Centerline terminal offset = " + ToMm(terminalOffset).ToString("F3") + " mm");

            double zDirection = Math.Sign(endCenter.Z - startCenter.Z);
            if (zDirection == 0)
                zDirection = -1.0;

            double zTravel = Math.Abs(endCenter.Z - startCenter.Z);
            double firstDropZ = Math.Min(Settings.FirstDropZ, zTravel * 0.35);
            double endAboveZ = Math.Min(Settings.EndAboveZ, zTravel * 0.35);

            if (firstDropZ < Settings.BusbarThickness)
                firstDropZ = Settings.BusbarThickness;

            if (endAboveZ < Settings.BusbarThickness)
                endAboveZ = Settings.BusbarThickness;

            // 路径顺序对应需求描述：
            // P0 起点中心线
            // P1 沿 Y 负方向让开刀熔
            // P2 沿 Z 方向第一次下降
            // P3/P4 沿 Y 正方向走到漏保前方/上方
            // P5/P6 沿 Z 方向靠近漏保端子
            // P7 沿 Y 负方向进入漏保端子中心线终点
            Point3 p0 = startCenter;
            Point3 p1 = new Point3(p0.X, p0.Y - Settings.LeadY, p0.Z);
            Point3 p2 = new Point3(p1.X, p1.Y, p1.Z + zDirection * firstDropZ);
            Point3 p3 = new Point3(endCenter.X, p2.Y, p2.Z);
            Point3 p4 = new Point3(endCenter.X, endCenter.Y + Settings.LeadY, p2.Z);
            Point3 p5 = new Point3(endCenter.X, endCenter.Y + Settings.LeadY, endCenter.Z - zDirection * endAboveZ);
            Point3 p6 = new Point3(endCenter.X, endCenter.Y + Settings.LeadY, endCenter.Z);
            Point3 p7 = endCenter;

            AddPathPoint(path, p0);
            AddPathPoint(path, p1);
            AddPathPoint(path, p2);
            AddPathPoint(path, p3);
            AddPathPoint(path, p4);
            AddPathPoint(path, p5);
            AddPathPoint(path, p6);
            AddPathPoint(path, p7);

            return path;
        }

        // 向路径里追加点，并自动跳过和上一个点重合的点，避免创建零长度草图线。
        static void AddPathPoint(List<Point3> path, Point3 point)
        {
            if (path.Count == 0 || path[path.Count - 1].DistanceTo(point) > Mm(0.01))
                path.Add(point);
        }

        // 调试用：在装配体里直接画 3D 草图中心线，帮助肉眼检查路径点是否正确。
        // 正式生成实体时默认不开启。
        static void CreateAssembly3DSketch(ModelDoc2 model, List<Point3> path)
        {
            Console.WriteLine();
            Console.WriteLine("===== 创建装配体 3D 草图路径 =====");

            if (path == null || path.Count < 2)
                throw new Exception("路径点数量不足，无法创建 3D 草图。");

            model.ClearSelection2(true);

            SketchManager sketchManager = model.SketchManager;
            bool sketchOpened = false;

            try
            {
                sketchManager.Insert3DSketch(true);
                sketchOpened = true;

                for (int i = 0; i < path.Count - 1; i++)
                {
                    Point3 a = path[i];
                    Point3 b = path[i + 1];

                    SketchSegment segment = sketchManager.CreateLine(a.X, a.Y, a.Z, b.X, b.Y, b.Z);
                    if (segment == null)
                        throw new Exception("创建 3D 草图线段失败，线段编号：" + i);
                }
            }
            finally
            {
                if (sketchOpened)
                    sketchManager.Insert3DSketch(true);
            }

            Feature newSketch = model.FeatureByPositionReverse(0) as Feature;
            if (newSketch != null)
            {
                newSketch.Name = "Debug_Busbar_" + DebugPhaseName + "_Path_" + DateTime.Now.ToString("HHmmss");
                Console.WriteLine("已创建 3D 草图：" + newSketch.Name);
            }
            else
            {
                Console.WriteLine("3D 草图已创建，但未能获取特征对象用于重命名。");
            }

            model.EditRebuild3();
        }

        // 调试用：在装配体里画 6x60 等规格的截面预览，确认宽度/厚度方向是否符合预期。
        static void CreateAssemblyProfilePreview(ModelDoc2 model, Point3 center)
        {
            Console.WriteLine();
            Console.WriteLine($"===== 创建 {Settings.ProfileLabel}mm 截面预览 =====");

            double widthX = Settings.BusbarWidth;
            double thicknessZ = Settings.BusbarThickness;

            double halfWidth = widthX / 2.0;
            double halfThickness = thicknessZ / 2.0;

            // 首段路径是沿 Y 方向，所以截面放在 XZ 平面，Y 坐标固定为起点 Y。
            Point3 p1 = new Point3(center.X - halfWidth, center.Y, center.Z - halfThickness);
            Point3 p2 = new Point3(center.X + halfWidth, center.Y, center.Z - halfThickness);
            Point3 p3 = new Point3(center.X + halfWidth, center.Y, center.Z + halfThickness);
            Point3 p4 = new Point3(center.X - halfWidth, center.Y, center.Z + halfThickness);

            model.ClearSelection2(true);

            SketchManager sketchManager = model.SketchManager;
            bool sketchOpened = false;

            try
            {
                sketchManager.Insert3DSketch(true);
                sketchOpened = true;

                CreateLineOrThrow(sketchManager, p1, p2, "截面线1");
                CreateLineOrThrow(sketchManager, p2, p3, "截面线2");
                CreateLineOrThrow(sketchManager, p3, p4, "截面线3");
                CreateLineOrThrow(sketchManager, p4, p1, "截面线4");
            }
            finally
            {
                if (sketchOpened)
                    sketchManager.Insert3DSketch(true);
            }

            Feature newSketch = model.FeatureByPositionReverse(0) as Feature;
            if (newSketch != null)
            {
                newSketch.Name = "Debug_Busbar_" + DebugPhaseName + "_Profile_" + Settings.ProfileLabel + "_" + DateTime.Now.ToString("HHmmss");
                Console.WriteLine("已创建截面预览 3D 草图：" + newSketch.Name);
            }
            else
            {
                Console.WriteLine("截面预览已创建，但未能获取特征对象用于重命名。");
            }

            model.EditRebuild3();
        }

        // 创建草图直线；如果 SW API 返回 null，就立即抛错，便于定位是哪一段失败。
        static void CreateLineOrThrow(SketchManager sketchManager, Point3 a, Point3 b, string name)
        {
            SketchSegment segment = sketchManager.CreateLine(a.X, a.Y, a.Z, b.X, b.Y, b.Z);
            if (segment == null)
                throw new Exception("创建 " + name + " 失败。");
        }

        // 生成铜排实体的总入口：新建零件 -> 创建路径草图 -> 创建截面草图 -> 扫掠 -> 保存 -> 插入装配体 -> 关闭新零件窗口。
        static void CreateBusbarSolidPart(SldWorks swApp, ModelDoc2 asmModel, AssemblyDoc asm, BusbarRoute route)
        {
            // 建模策略：
            // 在新零件中用装配体坐标直接创建铜排实体，然后以 identity transform 插回装配体。
            // 这样零件坐标 = 装配体坐标，避免插入后再计算复杂配合。
            Console.WriteLine();
            Console.WriteLine("===== 创建铜排实体零件 =====");

            ModelDoc2 partModel = NewPartDocument(swApp);

            ActivateDocument(swApp, partModel);

            Feature pathSketch = CreatePart3DPathSketch(partModel, route);
            Feature profileSketch = CreatePartProfileSketch(swApp, partModel, route);
            Feature sweep = CreateSweepFeature(partModel, profileSketch, pathSketch);

            if (sweep != null)
                sweep.Name = route.Name;

            partModel.EditRebuild3();

            string savePath = SaveBusbarPart(partModel, asmModel, route);
            InsertBusbarPartIntoAssembly(swApp, asmModel, asm, savePath, route);
            CloseBusbarPartDocument(swApp, asmModel, partModel);

            Console.WriteLine("铜排实体已生成并插入装配体。");
        }

        // 新建铜排零件文档。
        // 如果默认模板是 ~BLANK_PART_TEMPLATE.prtdot 这种内部模板名，就改用 NewPart()。
        static ModelDoc2 NewPartDocument(SldWorks swApp)
        {
            string templatePath = swApp.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
            bool templateFileExists = !string.IsNullOrWhiteSpace(templatePath) && System.IO.File.Exists(templatePath);

            Console.WriteLine("默认零件模板：" + templatePath);
            Console.WriteLine("模板文件存在：" + templateFileExists);

            ModelDoc2 partModel = null;

            if (templateFileExists)
            {
                partModel = swApp.NewDocument(
                    templatePath,
                    (int)swDwgPaperSizes_e.swDwgPaperA4size,
                    0,
                    0) as ModelDoc2;
            }
            else
            {
                Console.WriteLine("默认模板不是磁盘文件，改用 SolidWorks 空白零件 NewPart()。");
                partModel = swApp.NewPart() as ModelDoc2;
            }

            if (partModel == null)
                throw new Exception("新建铜排零件失败。请确认 SolidWorks 能手动新建零件，或在 系统选项 > 默认模板 中设置真实的 .prtdot 路径。");

            return partModel;
        }

        // 在铜排零件内创建 3D 路径草图。
        // 这些路径点已经是装配体坐标；后面插回装配体时用 identity transform 对齐。
        static Feature CreatePart3DPathSketch(ModelDoc2 partModel, BusbarRoute route)
        {
            // 用 3D 草图创建扫掠路径。
            // path 内所有点都已经是装配体全局坐标；新零件插回装配体时使用 identity transform。
            Console.WriteLine("创建零件内 3D 路径草图...");

            partModel.ClearSelection2(true);

            SketchManager sketchManager = partModel.SketchManager;
            bool sketchOpened = false;

            try
            {
                sketchManager.Insert3DSketch(true);
                sketchOpened = true;

                for (int i = 0; i < route.CenterlinePoints.Count - 1; i++)
                {
                    CreateLineOrThrow(sketchManager, route.CenterlinePoints[i], route.CenterlinePoints[i + 1], "路径线段" + i);
                }
            }
            finally
            {
                if (sketchOpened)
                    sketchManager.Insert3DSketch(true);
            }

            Feature sketch = partModel.FeatureByPositionReverse(0) as Feature;
            if (sketch == null)
                throw new Exception("路径草图已创建，但无法获取路径草图特征。");

            sketch.Name = route.Name + "_Path";
            return sketch;
        }

        // 在路径起点处创建矩形截面草图。
        // 注意：截面四角先按模型坐标计算，再用 ModelToSketchTransform 转成 2D 草图坐标。
        static Feature CreatePartProfileSketch(SldWorks swApp, ModelDoc2 partModel, BusbarRoute route)
        {
            // 创建矩形截面草图。
            // center 必须是 path[0]，也就是扫掠路径起点中心线。
            Point3 center = route.CenterlinePoints[0];
            AxisDirection firstAxis = GetDominantAxis(route.CenterlinePoints[0], route.CenterlinePoints[1]);
            BusbarProfile profile = route.Profile ?? Settings.GetProfile(route.Kind);
            Console.WriteLine($"创建零件内 {profile.Label}mm 截面草图...");
            Feature profilePlane = CreateProfilePlane(partModel, center, firstAxis);

            partModel.ClearSelection2(true);
            if (!profilePlane.Select2(false, 0))
                throw new Exception("选择截面基准面失败。");

            SketchManager sketchManager = partModel.SketchManager;
            sketchManager.InsertSketch(true);

            double widthX = profile.Width;
            double thicknessZ = profile.Thickness;
            double halfWidth = widthX / 2.0;
            double halfThickness = thicknessZ / 2.0;

            Sketch activeSketch = partModel.GetActiveSketch2() as Sketch;
            if (activeSketch == null)
                throw new Exception("无法获取当前截面草图。");

            MathTransform modelToSketch = activeSketch.ModelToSketchTransform;
            if (modelToSketch == null)
                throw new Exception("无法获取截面草图的模型坐标到草图坐标转换。");

            // 先在“模型坐标”里定义矩形，保证它的中心就是路径起点 center。
            // 矩形所在平面必须垂直于路径首段：
            // - 首段沿 Y：截面在 XZ 平面，宽度沿 X，厚度沿 Z。
            // - 首段沿 X：截面在 YZ 平面，宽度沿 Y，厚度沿 Z。
            // - 首段沿 Z：截面在 XY 平面，宽度沿 X，厚度沿 Y。
            Point3 m1;
            Point3 m2;
            Point3 m3;
            Point3 m4;
            BuildProfileCorners(center, firstAxis, halfWidth, halfThickness, out m1, out m2, out m3, out m4);

            Point3 p1 = ModelPointToSketchPoint(swApp, m1, modelToSketch);
            Point3 p2 = ModelPointToSketchPoint(swApp, m2, modelToSketch);
            Point3 p3 = ModelPointToSketchPoint(swApp, m3, modelToSketch);
            Point3 p4 = ModelPointToSketchPoint(swApp, m4, modelToSketch);

            Console.WriteLine("Profile center check:");
            PrintPathPoint("  model center", center);
            PrintPathPoint("  model p1", m1);
            PrintPathPoint("  model p2", m2);
            PrintPathPoint("  model p3", m3);
            PrintPathPoint("  model p4", m4);

            try
            {
                CreateLineOrThrow(sketchManager, p1, p2, "截面线1");
                CreateLineOrThrow(sketchManager, p2, p3, "截面线2");
                CreateLineOrThrow(sketchManager, p3, p4, "截面线3");
                CreateLineOrThrow(sketchManager, p4, p1, "截面线4");
            }
            finally
            {
                sketchManager.InsertSketch(true);
            }

            Feature sketch = partModel.FeatureByPositionReverse(0) as Feature;
            if (sketch == null)
                throw new Exception("截面草图已创建，但无法获取截面草图特征。");

            sketch.Name = "Busbar_Profile_" + profile.Label;
            return sketch;
        }

        // 创建截面基准面：从 Top Plane 沿 Y 方向偏移到路径起点所在的 Y 坐标。
        // yOffset 为负时必须用 flipDirection，避免截面平面跑到相反方向。
        static Feature CreateOffsetTopPlane(ModelDoc2 partModel, double yOffset)
        {
            // 扫掠首段沿 Y 方向，因此截面平面需要垂直于 Y。
            // SolidWorks 默认 Top Plane 是 XZ 平面，法向正好沿 Y，所以这里创建一个偏移 Top Plane。
            Feature topPlane = FindTopPlane(partModel);
            if (topPlane == null)
                throw new Exception("找不到上视基准面/Top Plane，无法创建截面基准面。");

            partModel.ClearSelection2(true);
            topPlane.Select2(false, 0);

            double distance = Math.Abs(yOffset);
            bool flipDirection = yOffset < 0;

            // 关键点：
            // yOffset 可能是负数，例如 B_OUT.Y = -105mm。
            // 直接传负距离不可靠，所以用绝对距离 + flipDirection 明确指定偏移方向。
            Console.WriteLine(
                "Profile plane offset Y = " + ToMm(yOffset).ToString("F3") +
                " mm, distance = " + ToMm(distance).ToString("F3") +
                " mm, flip = " + flipDirection);

            partModel.ICreatePlaneAtOffset3(distance, flipDirection, true);

            Feature plane = partModel.FeatureByPositionReverse(0) as Feature;
            if (plane == null)
                throw new Exception("创建截面基准面失败。");

            plane.Name = "Busbar_ProfilePlane";
            return plane;
        }

        // 根据路径首段方向创建垂直于路径的截面基准面。
        // 这一步决定 Sweep 能否成功：截面必须垂直于路径首段。
        static Feature CreateProfilePlane(ModelDoc2 partModel, Point3 center, AxisDirection firstAxis)
        {
            if (firstAxis == AxisDirection.Y)
                return CreateOffsetPlane(partModel, "Top", center.Y);

            if (firstAxis == AxisDirection.X)
                return CreateOffsetPlane(partModel, "Right", center.X);

            return CreateOffsetPlane(partModel, "Front", center.Z);
        }

        // 从默认基准面创建偏移平面。
        // Top 平面法向 Y，Right 平面法向 X，Front 平面法向 Z。
        static Feature CreateOffsetPlane(ModelDoc2 partModel, string basePlaneRole, double offset)
        {
            Feature basePlane = FindDefaultPlane(partModel, basePlaneRole);
            if (basePlane == null)
                throw new Exception("找不到默认基准面：" + basePlaneRole);

            partModel.ClearSelection2(true);
            basePlane.Select2(false, 0);

            double distance = Math.Abs(offset);
            bool flipDirection = offset < 0;

            Console.WriteLine(
                "Profile plane " + basePlaneRole +
                " offset = " + ToMm(offset).ToString("F3") +
                " mm, distance = " + ToMm(distance).ToString("F3") +
                " mm, flip = " + flipDirection);

            partModel.ICreatePlaneAtOffset3(distance, flipDirection, true);

            Feature plane = partModel.FeatureByPositionReverse(0) as Feature;
            if (plane == null)
                throw new Exception("创建截面基准面失败：" + basePlaneRole);

            plane.Name = "Busbar_ProfilePlane_" + basePlaneRole;
            return plane;
        }

        // 判断路径首段主要沿 X/Y/Z 哪个轴。
        static AxisDirection GetDominantAxis(Point3 a, Point3 b)
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

        // 根据首段方向在模型坐标中构造矩形截面四个角。
        static void BuildProfileCorners(Point3 center, AxisDirection firstAxis, double halfWidth, double halfThickness, out Point3 p1, out Point3 p2, out Point3 p3, out Point3 p4)
        {
            if (firstAxis == AxisDirection.X)
            {
                // 汇流排沿 X 方向时，先按“横着放”处理：
                // 宽度铺在 Z 方向，厚度放在 Y 方向。
                p1 = new Point3(center.X, center.Y - halfThickness, center.Z - halfWidth);
                p2 = new Point3(center.X, center.Y + halfThickness, center.Z - halfWidth);
                p3 = new Point3(center.X, center.Y + halfThickness, center.Z + halfWidth);
                p4 = new Point3(center.X, center.Y - halfThickness, center.Z + halfWidth);
                return;
            }

            if (firstAxis == AxisDirection.Z)
            {
                p1 = new Point3(center.X - halfWidth, center.Y - halfThickness, center.Z);
                p2 = new Point3(center.X + halfWidth, center.Y - halfThickness, center.Z);
                p3 = new Point3(center.X + halfWidth, center.Y + halfThickness, center.Z);
                p4 = new Point3(center.X - halfWidth, center.Y + halfThickness, center.Z);
                return;
            }

            p1 = new Point3(center.X - halfWidth, center.Y, center.Z - halfThickness);
            p2 = new Point3(center.X + halfWidth, center.Y, center.Z - halfThickness);
            p3 = new Point3(center.X + halfWidth, center.Y, center.Z + halfThickness);
            p4 = new Point3(center.X - halfWidth, center.Y, center.Z + halfThickness);
        }

        // 把模型坐标点转换为当前 2D 草图坐标点。
        // 这是保证“截面中心 = 路径起点”的关键坐标变换。
        static Point3 ModelPointToSketchPoint(SldWorks swApp, Point3 modelPoint, MathTransform modelToSketch)
        {
            // 把模型坐标转换到当前 2D 草图坐标。
            // 这是保证“截面中心 = 路径起点”的关键。
            MathUtility mathUtility = (MathUtility)swApp.GetMathUtility();
            MathPoint point = (MathPoint)mathUtility.CreatePoint(new double[]
            {
                modelPoint.X,
                modelPoint.Y,
                modelPoint.Z
            });

            MathPoint sketchPoint = (MathPoint)point.MultiplyTransform(modelToSketch);
            double[] data = sketchPoint.ArrayData as double[];

            if (data == null || data.Length < 3)
                throw new Exception("模型点转换到草图坐标失败。");

            return new Point3(data[0], data[1], data[2]);
        }

        // 查找零件默认 Top Plane / 上视基准面。
        // 中英文模板名不同，所以先按名称找，找不到再按默认基准面顺序兜底。
        static Feature FindTopPlane(ModelDoc2 partModel)
        {
            string[] names = { "上视基准面", "Top Plane", "上基准面" };

            int refPlaneIndex = 0;
            Feature feat = partModel.FirstFeature() as Feature;

            while (feat != null)
            {
                if (feat.GetTypeName2() == "RefPlane")
                {
                    foreach (string name in names)
                    {
                        if (feat.Name == name)
                            return feat;
                    }

                    if (refPlaneIndex == 1)
                        return feat;

                    refPlaneIndex++;
                }

                feat = feat.GetNextFeature() as Feature;
            }

            return null;
        }

        // 按角色查找默认基准面。
        static Feature FindDefaultPlane(ModelDoc2 partModel, string role)
        {
            string[] names;
            int fallbackIndex;

            if (SameText(role, "Right"))
            {
                names = new[] { "右视基准面", "Right Plane", "右基准面" };
                fallbackIndex = 2;
            }
            else if (SameText(role, "Front"))
            {
                names = new[] { "前视基准面", "Front Plane", "前基准面" };
                fallbackIndex = 0;
            }
            else
            {
                names = new[] { "上视基准面", "Top Plane", "上基准面" };
                fallbackIndex = 1;
            }

            int refPlaneIndex = 0;
            Feature feat = partModel.FirstFeature() as Feature;

            while (feat != null)
            {
                if (feat.GetTypeName2() == "RefPlane")
                {
                    foreach (string name in names)
                    {
                        if (feat.Name == name)
                            return feat;
                    }

                    if (refPlaneIndex == fallbackIndex)
                        return feat;

                    refPlaneIndex++;
                }

                feat = feat.GetNextFeature() as Feature;
            }

            return null;
        }

        // 执行扫掠生成铜排实体。
        // Select2 的 Mark 很重要：1 表示截面，4 表示路径。
        static Feature CreateSweepFeature(ModelDoc2 partModel, Feature profileSketch, Feature pathSketch)
        {
            // 选择标记是 Sweep API 的重点：
            // Mark 1 = 扫掠截面 Profile
            // Mark 4 = 扫掠路径 Path
            Console.WriteLine("执行扫掠生成铜排实体...");

            partModel.ClearSelection2(true);

            bool profileSelected = profileSketch.Select2(false, 1);
            bool pathSelected = pathSketch.Select2(true, 4);

            if (!profileSelected)
                throw new Exception("扫掠前选择截面草图失败。");

            if (!pathSelected)
                throw new Exception("扫掠前选择路径草图失败。");

            Feature sweep = partModel.FeatureManager.InsertProtrusionSwept4(
                false,
                false,
                0,
                true,
                false,
                0,
                0,
                false,
                0,
                0,
                0,
                0,
                true,
                false,
                true,
                0,
                true,
                false,
                0,
                0);

            partModel.ClearSelection2(true);

            if (sweep == null)
                throw new Exception("扫掠失败。常见原因：截面草图未闭合，或截面没有垂直于路径起点。");

            return sweep;
        }

        // 保存新生成的铜排零件；优先保存到装配体所在目录。
        static string SaveBusbarPart(ModelDoc2 partModel, ModelDoc2 asmModel, BusbarRoute route)
        {
            // 铜排零件保存到装配体同目录。
            // 如果装配体尚未保存，就临时保存到桌面。
            string asmPath = asmModel.GetPathName();
            string folder = string.IsNullOrWhiteSpace(asmPath)
                ? System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory)
                : System.IO.Path.GetDirectoryName(asmPath);

            BusbarProfile profile = route.Profile ?? Settings.GetProfile(route.Kind);
            string savePath = System.IO.Path.Combine(folder, route.Name + "_" + profile.Label + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".SLDPRT");

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
                throw new Exception("保存铜排零件失败。Errors=" + errors + ", Warnings=" + warnings);

            Console.WriteLine("铜排零件已保存：" + savePath);
            return savePath;
        }

        // 把铜排零件插入当前装配体。
        // 因为零件几何已经按装配体坐标建好，所以组件变换设为 identity。
        static void InsertBusbarPartIntoAssembly(SldWorks swApp, ModelDoc2 asmModel, AssemblyDoc asm, string partPath, BusbarRoute route)
        {
            // 新零件内部几何已经使用装配体全局坐标建好。
            // 因此插入装配体后，把组件 Transform2 设为 identity 即可对齐。
            Console.WriteLine("插入铜排零件到当前装配体...");

            ActivateDocument(swApp, asmModel);

            Component2 comp = asm.AddComponent5(partPath, 0, "", false, "", 0, 0, 0);
            if (comp == null)
                throw new Exception("插入铜排零件失败。");

            MathUtility mu = (MathUtility)swApp.GetMathUtility();
            MathTransform identity = (MathTransform)mu.CreateTransform(new double[]
            {
                1, 0, 0,
                0, 1, 0,
                0, 0, 1,
                0, 0, 0,
                1, 0, 0, 0
            });

            comp.Transform2 = identity;
            BusbarProfile profile = route.Profile ?? Settings.GetProfile(route.Kind);
            comp.Name2 = route.Name + "_" + profile.Label;
            asmModel.EditRebuild3();
        }

        // 保存并插入完成后关闭新建的铜排零件文档，避免 SolidWorks 后台堆积大量零件页面。
        // 注意：这里只关闭刚刚新建的 .SLDPRT 文档，不删除磁盘文件，也不移除装配体中的铜排组件。
        static void CloseBusbarPartDocument(SldWorks swApp, ModelDoc2 asmModel, ModelDoc2 partModel)
        {
            if (partModel == null)
                return;

            string partTitle = partModel.GetTitle();
            if (string.IsNullOrWhiteSpace(partTitle))
                return;

            Console.WriteLine("关闭铜排零件窗口：" + partTitle);

            // 先切回装配体，再按标题关闭零件，避免当前活动文档被关掉后焦点混乱。
            ActivateDocument(swApp, asmModel);
            swApp.CloseDoc(partTitle);
            ActivateDocument(swApp, asmModel);
        }

        // 激活指定文档，确保后续 SketchManager / FeatureManager 操作作用在正确文档上。
        static void ActivateDocument(SldWorks swApp, ModelDoc2 model)
        {
            int errors = 0;
            swApp.ActivateDoc3(model.GetTitle(), false, 0, ref errors);
        }

        // 判断参考点名是否匹配，例如 B_OUT、B_IN。
        static bool PointNameEquals(FoundPoint point, string pointName)
        {
            return SameText(point.PointName, pointName);
        }

        // 判断某个组件里是否存在指定参考点。
        static bool ComponentHasPoint(List<FoundPoint> points, string componentName, string pointName)
        {
            foreach (FoundPoint point in points)
            {
                if (SameText(point.ComponentName, componentName) && SameText(point.PointName, pointName))
                    return true;
            }

            return false;
        }

        // 统计某个组件里以指定后缀结尾的点数量，例如 *_OUT。
        // 用于辅助判断哪个组件更像刀熔开关。
        static int CountComponentPointsEndingWith(List<FoundPoint> points, string componentName, string suffix)
        {
            int count = 0;

            foreach (FoundPoint point in points)
            {
                if (!SameText(point.ComponentName, componentName))
                    continue;

                if (point.PointName != null && point.PointName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    count++;
            }

            return count;
        }

        // 忽略大小写比较字符串。
        static bool SameText(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        // 按 mm 输出路径点，便于和 SolidWorks 里标注的坐标核对。
        static void PrintPathPoint(string label, Point3 p)
        {
            Console.WriteLine($"{label,-28} X={p.X * 1000:F3} mm, Y={p.Y * 1000:F3} mm, Z={p.Z * 1000:F3} mm");
        }

        // mm -> m。SolidWorks API 长度单位是米。
        static double Mm(double value)
        {
            return value / 1000.0;
        }

        // m -> mm。用于控制台输出。
        static double ToMm(double value)
        {
            return value * 1000.0;
        }
    }

    // 铜排生成参数：用户主要改规格和端子偏移方向，其他路径距离由规格自动推导。
    internal class BusbarSettings
    {
        // 三类铜排规格，单位 mm。
        public double MainFeedWidthMm;
        public double MainFeedThicknessMm;
        public double CollectorWidthMm;
        public double CollectorThicknessMm;
        public double BranchWidthMm;
        public double BranchThicknessMm;

        // 参考点到扫掠中心线的 Z 向偏移符号。
        // 偏移距离由对应铜排规格的 Thickness / 2 决定。
        public int StartTerminalOffsetZSign;
        public int EndTerminalOffsetZSign;

        // 复杂拓扑初版参数：三相汇流排沿 Y 排列，分支从汇流排下接到漏保。
        public double CollectorPhaseSpacingMm;
        public double CollectorTopClearanceYMm;
        public double CollectorOffsetFromLoubaoInZMm;
        public double MainLeadOutYMm;
        public double BranchApproachYMm;
        public CollectorLapSide MainCollectorLapSide;
        public double MainCollectorFrontClearanceMm;
        public double MainCollectorLapDepthRatio;

        public BusbarProfile MainFeedProfile { get { return new BusbarProfile(MainFeedWidthMm, MainFeedThicknessMm); } }
        public BusbarProfile CollectorProfile { get { return new BusbarProfile(CollectorWidthMm, CollectorThicknessMm); } }
        public BusbarProfile BranchProfile { get { return new BusbarProfile(BranchWidthMm, BranchThicknessMm); } }

        // 旧的单根点对点调试函数继续默认使用转接排规格。
        public double BusbarWidth { get { return MainFeedProfile.Width; } }
        public double BusbarThickness { get { return MainFeedProfile.Thickness; } }
        public double TerminalFaceOffset { get { return MainFeedProfile.TerminalFaceOffset; } }
        public double CollectorPhaseSpacing { get { return Mm(CollectorPhaseSpacingMm); } }
        public double CollectorTopClearanceY { get { return Mm(CollectorTopClearanceYMm); } }
        public double CollectorOffsetFromLoubaoInZ { get { return Mm(CollectorOffsetFromLoubaoInZMm); } }
        public double MainLeadOutY { get { return Mm(MainLeadOutYMm); } }
        public double BranchApproachY { get { return Mm(BranchApproachYMm); } }
        public double MainCollectorFrontClearance { get { return Mm(MainCollectorFrontClearanceMm); } }

        public BusbarProfile GetProfile(BusbarKind kind)
        {
            if (kind == BusbarKind.Collector)
                return CollectorProfile;

            if (kind == BusbarKind.Branch)
                return BranchProfile;

            return MainFeedProfile;
        }

        public double GetCollectorFrontZOffset()
        {
            return CollectorProfile.Width / 2.0 + MainCollectorFrontClearance;
        }

        public double GetMainCollectorLapDepth()
        {
            return CollectorProfile.Width * MainCollectorLapDepthRatio;
        }

        public double GetMainCollectorLapYOffset()
        {
            double offset = CollectorProfile.Thickness / 2.0 + MainFeedProfile.Thickness / 2.0;
            return MainCollectorLapSide == CollectorLapSide.Upper ? offset : -offset;
        }

        // 以下路径参数由规格自动推导，不需要单独修改。
        public double LeadY
        {
            get { return Math.Max(BusbarWidth * 0.50, BusbarThickness * 4.0); }
        }

        public double FirstDropZ
        {
            get { return Math.Max(BusbarWidth, BusbarThickness * 8.0); }
        }

        public double EndAboveZ
        {
            get { return Math.Max(BusbarWidth * 0.50, BusbarThickness * 4.0); }
        }

        public string ProfileLabel
        {
            get { return MainFeedProfile.Label; }
        }

        private static double Mm(double value)
        {
            return value / 1000.0;
        }

        private static string FormatMm(double value)
        {
            return value.ToString("0.###");
        }
    }

    // 一个已经识别到的命名参考点，坐标统一保存为装配体全局坐标。
    internal class FoundPoint
    {
        // 保存已经转换到装配体全局坐标系下的参考点。
        public string ComponentName;
        public string PointName;
        public Point3 Position;

        public override string ToString()
        {
            return $"{ComponentName}.{PointName}  X={Position.X * 1000:F3} mm, Y={Position.Y * 1000:F3} mm, Z={Position.Z * 1000:F3} mm";
        }
    }

    // 一组铜排连接关系：Start 是刀熔输出点，End 是漏保输入点。
    internal class BusbarConnection
    {
        // 自动识别出来的一组连接关系：刀熔输出点 -> 漏保输入点。
        public FoundPoint Start;
        public FoundPoint End;
    }

    // 铜排类型：主连接排、汇流排、漏保分支排。
    internal enum BusbarKind
    {
        MainFeed,
        Collector,
        Branch
    }

    // 转接排与汇流排的搭接方式。
    // Upper：转接排搭在汇流排上端；Lower：转接排搭在汇流排下端。
    internal enum CollectorLapSide
    {
        Upper,
        Lower
    }

    // 路径首段主方向，用于决定截面草图所在平面。
    internal enum AxisDirection
    {
        X,
        Y,
        Z
    }

    // 一条待生成的铜排路径。后续所有扫掠建模都只依赖这个对象。
    internal class BusbarRoute
    {
        public string Name;
        public string Phase;
        public BusbarKind Kind;
        public BusbarProfile Profile;
        public List<Point3> CenterlinePoints;
    }

    // 铜排截面规格。
    // Width/Thickness 已转换为 SolidWorks API 使用的米；Label 仍按 mm 显示。
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

    // 某一相汇流排的几何布置参数。
    internal class CollectorLayout
    {
        public string Phase;
        public double Y;
        public double Z;
        public double StartX;
        public double EndX;
        public double MainTapX;
    }

    // 主连接排搭接汇流排时的联动计算结果。
    // 只要汇流排宽度/厚度变化，这里的 FrontZ、EndZ、Y 都会跟着变化。
    internal class MainCollectorLapLayout
    {
        public double Y;
        public double FrontZ;
        public double EndZ;
        public double Depth;
    }

    // 一个漏保组件的摘要信息，用于排序和取点。
    internal class LoubaoGroup
    {
        public string ComponentName;
        public double CenterX;
    }

    // 三维点结构，单位为米，对应 SolidWorks API 的长度单位。
    internal struct Point3
    {
        // SolidWorks API 使用米作为长度单位。
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
    }
}
