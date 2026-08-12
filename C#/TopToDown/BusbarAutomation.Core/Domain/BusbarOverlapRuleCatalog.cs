using System;
using System.Collections.Generic;
using System.Linq;

namespace BusbarAutomation.Core.Domain
{
    internal sealed class BusbarOverlapRuleCatalog
    {
        private const double WidthToleranceMm = 0.01;
        private readonly Dictionary<string, BusbarOverlapHoleRule> _rules;

        public BusbarOverlapRuleCatalog(IEnumerable<BusbarOverlapRuleEntry> entries)
        {
            _rules = (entries ?? Enumerable.Empty<BusbarOverlapRuleEntry>())
                .ToDictionary(
                    entry => MakeKey(entry.FirstWidthMm, entry.SecondWidthMm),
                    entry => entry.Rule.Clone(),
                    StringComparer.Ordinal);
        }

        public bool TryResolve(double firstWidthMm, double secondWidthMm, out BusbarOverlapHoleRule rule)
        {
            int first;
            int second;
            if (!TryNormalizeWidth(firstWidthMm, out first) || !TryNormalizeWidth(secondWidthMm, out second))
            {
                rule = null;
                return false;
            }

            BusbarOverlapHoleRule found;
            if (_rules.TryGetValue(MakeKey(first, second), out found))
            {
                rule = found.Clone();
                return true;
            }

            rule = null;
            return false;
        }

        private static string MakeKey(double firstWidthMm, double secondWidthMm)
        {
            return ((int)Math.Round(firstWidthMm)).ToString() + "x" +
                ((int)Math.Round(secondWidthMm)).ToString();
        }

        private static bool TryNormalizeWidth(double widthMm, out int normalizedWidthMm)
        {
            normalizedWidthMm = (int)Math.Round(widthMm, 0, MidpointRounding.AwayFromZero);
            return Math.Abs(widthMm - normalizedWidthMm) <= WidthToleranceMm;
        }
    }

    internal sealed class BusbarOverlapRuleEntry
    {
        public double FirstWidthMm { get; private set; }
        public double SecondWidthMm { get; private set; }
        public BusbarOverlapHoleRule Rule { get; private set; }

        public BusbarOverlapRuleEntry(
            double firstWidthMm,
            double secondWidthMm,
            BusbarOverlapHoleRule rule)
        {
            FirstWidthMm = firstWidthMm;
            SecondWidthMm = secondWidthMm;
            Rule = rule ?? throw new ArgumentNullException("rule");
        }
    }
}
