using System;

using BusbarAutomation.Core.Planning;

namespace BusbarAutomation.Cli
{
    internal static class PreflightConsolePresenter
    {
        public static void Print(BusbarPreflightReport report)
        {
            if (report == null)
                throw new ArgumentNullException("report");

            Console.WriteLine();
            Console.WriteLine("===== " + report.Title + " =====");

            foreach (PreflightMessage message in report.Messages)
            {
                Console.WriteLine(
                    "[" + message.Severity.ToString().ToUpperInvariant() + "] " +
                    message.Scope + ": " +
                    message.Message);
            }

            Console.WriteLine();
            Console.WriteLine(
                "Summary: errors=" + report.ErrorCount +
                ", warnings=" + report.WarningCount +
                ", info=" + report.InfoCount + ".");
            Console.WriteLine(
                report.HasErrors
                    ? "Result: FAILED. " + report.FailureResultMessage
                    : report.WarningCount > 0
                        ? "Result: PASSED WITH WARNINGS."
                        : "Result: PASSED.");
        }
    }
}
