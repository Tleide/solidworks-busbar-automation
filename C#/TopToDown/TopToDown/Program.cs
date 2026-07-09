using System;

namespace SwFeatureDebug
{
    internal partial class Program
    {
        private static readonly BusbarSettings Settings = new BusbarSettings
        {
            MainFeedWidthMm = 60.0,
            MainFeedThicknessMm = 6.0,

            CollectorWidthMm = 60.0,
            CollectorThicknessMm = 8.0,

            BranchWidthMm = 30.0,
            BranchThicknessMm = 4.0,

            NeutralCollectorWidthMm = 50.0,
            NeutralCollectorThicknessMm = 5.0,
            NeutralBranchWidthMm = 30.0,
            NeutralBranchThicknessMm = 4.0,

            CollectorPhaseSpacingMm = 60.0,
            CollectorTopClearanceYMm = 240.0,
            CollectorOffsetFromLoubaoInZMm = 120.0,
            CollectorNegativeXExtendMm = 50.0,

            MainLeadOutYMm = 40.0,

            SheetMetalBendRadiusMm = 5.0,
            SheetMetalKFactor = 0.47,
            SheetMetalThickenDirection = false,
            MainFeedSheetMetalWidthSide = SheetMetalWidthSide.Center,
            CollectorSheetMetalWidthSide = SheetMetalWidthSide.Center,
            BranchSheetMetalWidthSide = SheetMetalWidthSide.Center,
            MainCollectorFrontClearanceMm = 100
        };

        private static readonly string[] PhaseNames = { "A", "B", "C" };
        private const string NeutralConductorName = "N";

        private static bool _replaceExistingBusbar = true;
        private static bool _verboseFeatureScan;
        private static bool _previewOnly;

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
            }
        }
    }
}
