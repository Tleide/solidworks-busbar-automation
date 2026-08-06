using System;
using System.Linq;

namespace SwFeatureDebug
{
    internal static class GenerationOptionsParser
    {
        public static GenerationOptions Parse(string[] args)
        {
            GenerationOptions options = new GenerationOptions();
            int exclusiveModeCount = 0;

            foreach (string arg in args ?? new string[0])
            {
                if (IsOption(arg, "--verbose"))
                {
                    options.VerboseFeatureScan = true;
                    continue;
                }

                if (IsOption(arg, "--keep-existing"))
                {
                    options.ReplaceExistingBusbar = false;
                    continue;
                }

                if (IsOption(arg, "--preview"))
                {
                    options.PreviewOnly = true;
                    exclusiveModeCount++;
                    continue;
                }

                if (IsOption(arg, "--validate"))
                {
                    options.ValidateOnly = true;
                    exclusiveModeCount++;
                    continue;
                }

                if (IsOption(arg, "--verify-geometry"))
                {
                    options.VerifyGeometryOnly = true;
                    exclusiveModeCount++;
                    continue;
                }

                if (IsOption(arg, "--export-report"))
                {
                    options.ExportReportOnly = true;
                    exclusiveModeCount++;
                    continue;
                }

                const string onlyPrefix = "--only=";
                if (arg != null && arg.StartsWith(onlyPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    options.OnlyBusbarNames = arg.Substring(onlyPrefix.Length)
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(name => name.Trim())
                        .ToArray();

                    if (options.OnlyBusbarNames.Length == 0)
                        throw new ArgumentException("--only requires at least one busbar name.", "args");

                    continue;
                }

                throw new ArgumentException("Unknown command-line option: " + (arg ?? "<null>"), "args");
            }

            if (exclusiveModeCount > 1)
                throw new ArgumentException("Only one execution mode may be used.", "args");

            if (!options.ReplaceExistingBusbar &&
                (options.PreviewOnly || options.ValidateOnly || options.VerifyGeometryOnly || options.ExportReportOnly))
                throw new ArgumentException("--keep-existing is valid only for generation mode.", "args");

            if (options.OnlyBusbarNames != null && (options.PreviewOnly || options.ValidateOnly || options.ExportReportOnly))
                throw new ArgumentException("--only is valid only for generation or --verify-geometry mode.", "args");

            return options;
        }

        private static bool IsOption(string actual, string expected)
        {
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
