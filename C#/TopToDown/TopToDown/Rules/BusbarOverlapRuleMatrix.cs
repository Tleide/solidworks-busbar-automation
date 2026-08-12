using System.Collections.Generic;
using BusbarAutomation.Core.Domain;

namespace BusbarAutomation.Core.Rules
{
    internal static class BusbarOverlapRuleMatrix
    {
        // Mirrored from the workbook overlap matrix. Keep the workbook as the human-editable source.
        private static readonly List<BusbarOverlapRuleEntry> Entries = CreateEntries();
        private static readonly BusbarOverlapRuleCatalog Catalog = new BusbarOverlapRuleCatalog(Entries);

        public static bool TryResolve(double firstWidthMm, double secondWidthMm, out BusbarOverlapHoleRule rule)
        {
            return Catalog.TryResolve(firstWidthMm, secondWidthMm, out rule);
        }

        public static BusbarOverlapRuleCatalog CreateCatalog()
        {
            return new BusbarOverlapRuleCatalog(Entries);
        }

        private static List<BusbarOverlapRuleEntry> CreateEntries()
        {
            return new List<BusbarOverlapRuleEntry>
            {
                Entry(30, 30, Single(11.0, "single-11")),
                Entry(30, 40, Single(13.0, "single-13")),
                Entry(30, 50, StraightDouble(11.0, "straight-double-11")),
                Entry(30, 60, StraightDouble(11.0, "straight-double-11")),
                Entry(40, 30, Single(13.0, "single-13")),
                Entry(40, 40, Single(13.0, "single-13")),
                Entry(40, 50, StraightDouble(11.0, "straight-double-11")),
                Entry(40, 60, StraightDouble(11.0, "straight-double-11")),
                Entry(50, 30, StraightDouble(11.0, "straight-double-11")),
                Entry(50, 40, StraightDouble(11.0, "straight-double-11")),
                Entry(50, 50, DiagonalDouble(13.0, 11.0, "diagonal-double-13-offset-11")),
                Entry(50, 60, StraightDouble(11.0, "straight-double-11")),
                Entry(60, 30, StraightDouble(11.0, "straight-double-11")),
                Entry(60, 40, StraightDouble(11.0, "straight-double-11")),
                Entry(60, 50, StraightDouble(11.0, "straight-double-11")),
                Entry(60, 60, DiagonalDouble(13.0, 12.0, "diagonal-double-13-offset-12"))
            };
        }

        private static BusbarOverlapHoleRule Single(double diameterMm, string sourceCode)
        {
            return new BusbarOverlapHoleRule(BusbarOverlapHolePattern.Single, diameterMm, 0.0, sourceCode);
        }

        private static BusbarOverlapHoleRule StraightDouble(double diameterMm, string sourceCode)
        {
            return new BusbarOverlapHoleRule(BusbarOverlapHolePattern.StraightDouble, diameterMm, 0.0, sourceCode);
        }

        private static BusbarOverlapHoleRule DiagonalDouble(double diameterMm, double offsetMm, string sourceCode)
        {
            return new BusbarOverlapHoleRule(BusbarOverlapHolePattern.DiagonalDouble, diameterMm, offsetMm, sourceCode);
        }

        private static BusbarOverlapRuleEntry Entry(
            double firstWidthMm,
            double secondWidthMm,
            BusbarOverlapHoleRule rule)
        {
            return new BusbarOverlapRuleEntry(firstWidthMm, secondWidthMm, rule);
        }

    }
}
