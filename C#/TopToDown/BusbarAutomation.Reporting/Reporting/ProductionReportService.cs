using System;
using System.IO;

using BusbarAutomation.Core.Planning;

namespace BusbarAutomation.Reporting
{
    internal static class ProductionReportService
    {
        public static string ExportForAssembly(
            BusbarManufacturingPlan plan,
            string assemblyPath)
        {
            string rootFolder = string.IsNullOrWhiteSpace(assemblyPath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
                : Path.GetDirectoryName(assemblyPath);
            string outputFolder = Path.Combine(rootFolder, "Reports");
            return ProductionReportExporter.Export(plan, outputFolder);
        }
    }
}
