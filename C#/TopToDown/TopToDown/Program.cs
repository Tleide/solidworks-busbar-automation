using System;
using System.Collections.Generic;
using System.Linq;

namespace SwFeatureDebug
{
    internal partial class Program
    {
        private static readonly BusbarSettings Settings = new BusbarSettings
        {
            // 刀熔到 ABC 汇流排的主排规格：宽度、厚度，单位 mm。
            MainFeedWidthMm = 60.0,
            MainFeedThicknessMm = 6.0,

            // ABC 三相汇流排规格：宽度、厚度，单位 mm。
            CollectorWidthMm = 60.0,
            CollectorThicknessMm = 8.0,

            // ABC 分支排统一规格的历史兜底值。
            // 当前正常流程按 PhaseBranchRules 为每台漏保选型，不需要修改这里。
            BranchWidthMm = 30.0,
            BranchThicknessMm = 4.0,

            // N 汇流排规格：宽度、厚度，单位 mm。
            NeutralCollectorWidthMm = 50.0,
            NeutralCollectorThicknessMm = 5.0,
            // N 分支排统一规格的历史兜底值。
            // 当前正常流程按 NeutralBranchRules 为每台漏保选型，不需要修改这里。
            NeutralBranchWidthMm = 30.0,
            NeutralBranchThicknessMm = 4.0,

            // ABC 分支排选型表。
            // BranchBusbarRule(漏保最大电流 A, 铜排宽度 mm, 铜排厚度 mm, 默认单排/双排)。
            // 漏保组件名称必须包含下列其中一个电流值，例如 PGM8LZ-400-1 或 PGM8LZ-400A-1。
            PhaseBranchRules = new List<BranchBusbarRule>
            {
                new BranchBusbarRule(250, 20.0, 4.0, BranchArrangement.Single),
                new BranchBusbarRule(400, 30.0, 4.0, BranchArrangement.DoubleClamp),
                new BranchBusbarRule(630, 40.0, 4.0, BranchArrangement.DoubleClamp)
            },

            // N 分支排选型表，与 ABC 规则表完全独立。
            // 即使当前规格相同，未来修改 N 排时只需改此处，不会影响 ABC。
            NeutralBranchRules = new List<BranchBusbarRule>
            {
                new BranchBusbarRule(250, 20.0, 4.0, BranchArrangement.Single),
                new BranchBusbarRule(400, 30.0, 4.0, BranchArrangement.Single),
                new BranchBusbarRule(630, 40.0, 4.0, BranchArrangement.Single)
            },

            // 銅排搭接螺栓規則：實際螺紋露出量至少 3mm；長度為螺栓頭下表面到末端的公稱長度，不含螺栓頭高度。
            MinimumThreadProjectionMm = 3.0,
            // FastenerSpec(規格, 公司默認通孔, 彈墊壓平厚度, 平墊厚度, 平墊外徑, 螺母高度, 螺栓頭高度, 螺栓頭外接圓直徑, 標準公稱長度...)
            // 這些值同步自數據層.xlsx 的標準件庫；後續接入 Excel Repository 后可移除此處固化表。
            FastenerCatalog = new List<FastenerSpec>
            {
                new FastenerSpec("M8", 9.0, 1.5, 1.5, 16.0, 6.0, 4.5, 14.0, 20.0, 25.0),
                new FastenerSpec("M10", 11.0, 2.5, 2.0, 20.0, 8.0, 6.0, 18.0, 20.0, 30.0, 35.0, 40.0),
                new FastenerSpec("M12", 13.0, 3.5, 2.5, 24.0, 11.0, 7.0, 22.0, 35.0, 40.0, 45.0),
                new FastenerSpec("M14", 15.0, 4.0, 2.5, 28.0, 11.0, 9.5, 26.0, 40.0, 45.0),
                new FastenerSpec("M16", 17.0, 4.5, 3.0, 32.0, 14.0, 11.0, 30.0, 40.0, 45.0)
            },

            // 项目级拓扑强制覆盖：null 表示使用每台漏保在规则表中的默认单双排。
            // 若填 BranchArrangement.Single 或 DoubleClamp，则统一覆盖所有 ABC 或所有 N 分支排的单双排。
            PhaseBranchArrangementOverride = null,
            NeutralBranchArrangementOverride = null,

            // 双排夹接的 Z-方向外侧上排路径参数。
            // -1 表示上排整体从漏保端向 Z-错开一个分支排厚度；改为 1 则向 Z+错开。
            DoubleClampUpperStartZSign = -1,
            // 外侧上排从漏保端先沿 Y+直上引出的长度，单位 mm。
            DoubleClampOuterInitialRiseMm = 50.0,
            // 外侧上排 Y+/Z-斜向避让段的最小实际长度，单位 mm。
            DoubleClampOuterDiagonalMinimumLengthMm = 50.0,

            // 汇流排布局参数：相间 Y 距离、相对最高漏保端子的 Y 净距、相对漏保的 Z 偏移、X-侧外伸，单位 mm。
            CollectorPhaseSpacingMm = 60.0,
            CollectorTopClearanceYMm = 240.0,
            CollectorOffsetFromLoubaoInZMm = 120.0,
            CollectorNegativeXExtendMm = 50.0,

            // 主排从刀熔端起始引出的 Y 向长度，单位 mm。
            MainLeadOutYMm = 40.0,

            // 钣金工艺参数：默认折弯半径、K 因子和开放轮廓宽度生成方向。
            SheetMetalBendRadiusMm = 5.0,
            SheetMetalKFactor = 0.47,
            SheetMetalThickenDirection = false,
            MainFeedSheetMetalWidthSide = SheetMetalWidthSide.Center,
            CollectorSheetMetalWidthSide = SheetMetalWidthSide.Center,
            BranchSheetMetalWidthSide = SheetMetalWidthSide.Center,
            // 主排进入 ABC 汇流排前预留的 Z 向前方净距，单位 mm。
            MainCollectorFrontClearanceMm = 100
        };

        private static readonly string[] PhaseNames = { "A", "B", "C" };
        private const string NeutralConductorName = "N";

        private static bool _replaceExistingBusbar = true;
        private static bool _verboseFeatureScan;
        private static bool _previewOnly;
        private static bool _validateOnly;
        private static bool _verifyGeometryOnly;
        private static bool _exportReportOnly;
        private static string[] _onlyBusbarNames;

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                ConfigureFromArgs(args ?? new string[0]);
                RunSolidWorksGeneration();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                Console.WriteLine();
                Console.WriteLine(ex);
                if (!Console.IsInputRedirected)
                    Console.ReadKey();
            }
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

                if (SameText(arg, "--preview"))
                {
                    _previewOnly = true;
                    continue;
                }

                if (SameText(arg, "--validate"))
                {
                    _validateOnly = true;
                    continue;
                }

                if (SameText(arg, "--verify-geometry"))
                {
                    _verifyGeometryOnly = true;
                    continue;
                }

                if (SameText(arg, "--export-report"))
                {
                    _exportReportOnly = true;
                    continue;
                }

                const string onlyPrefix = "--only=";
                if (arg != null && arg.StartsWith(onlyPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    _onlyBusbarNames = arg.Substring(onlyPrefix.Length)
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(name => name.Trim())
                        .ToArray();
                    continue;
                }
            }
        }
    }
}
