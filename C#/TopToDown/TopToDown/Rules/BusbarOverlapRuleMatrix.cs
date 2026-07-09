using System;
using System.Collections.Generic;

namespace SwFeatureDebug
{
    internal static class BusbarOverlapRuleMatrix
    {
        // Mirrored from the workbook overlap matrix. Keep the workbook as the human-editable source.
        private static readonly Dictionary<string, BusbarOverlapHoleRule> Rules = CreateRules();

        public static bool TryResolve(double firstWidthMm, double secondWidthMm, out BusbarOverlapHoleRule rule)
        {
            int first = NormalizeWidth(firstWidthMm);
            int second = NormalizeWidth(secondWidthMm);
            string key = MakeKey(first, second);

            BusbarOverlapHoleRule found;
            if (Rules.TryGetValue(key, out found))
            {
                rule = found.Clone();
                return true;
            }

            rule = null;
            return false;
        }

        private static Dictionary<string, BusbarOverlapHoleRule> CreateRules()
        {
            Dictionary<string, BusbarOverlapHoleRule> rules = new Dictionary<string, BusbarOverlapHoleRule>();

            Add(rules, 30, 30, Single(11.0, "single-11"));
            Add(rules, 30, 40, Single(13.0, "single-13"));
            Add(rules, 30, 50, StraightDouble(11.0, "straight-double-11"));
            Add(rules, 30, 60, StraightDouble(11.0, "straight-double-11"));

            Add(rules, 40, 30, Single(13.0, "single-13"));
            Add(rules, 40, 40, Single(13.0, "single-13"));
            Add(rules, 40, 50, StraightDouble(11.0, "straight-double-11"));
            Add(rules, 40, 60, StraightDouble(11.0, "straight-double-11"));

            Add(rules, 50, 30, StraightDouble(11.0, "straight-double-11"));
            Add(rules, 50, 40, StraightDouble(11.0, "straight-double-11"));
            Add(rules, 50, 50, DiagonalDouble(13.0, 11.0, "diagonal-double-13-offset-11"));
            Add(rules, 50, 60, StraightDouble(11.0, "straight-double-11"));

            Add(rules, 60, 30, StraightDouble(11.0, "straight-double-11"));
            Add(rules, 60, 40, StraightDouble(11.0, "straight-double-11"));
            Add(rules, 60, 50, StraightDouble(11.0, "straight-double-11"));
            Add(rules, 60, 60, DiagonalDouble(13.0, 12.0, "diagonal-double-13-offset-12"));

            return rules;
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

        private static void Add(Dictionary<string, BusbarOverlapHoleRule> rules, int firstWidthMm, int secondWidthMm, BusbarOverlapHoleRule rule)
        {
            rules[MakeKey(firstWidthMm, secondWidthMm)] = rule;
        }

        private static string MakeKey(int firstWidthMm, int secondWidthMm)
        {
            return firstWidthMm.ToString() + "x" + secondWidthMm.ToString();
        }

        private static int NormalizeWidth(double widthMm)
        {
            return (int)Math.Round(widthMm, 0, MidpointRounding.AwayFromZero);
        }
    }
}
