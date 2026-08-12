namespace BusbarAutomation.Application
{
    internal sealed class GenerationOptions
    {
        public bool ReplaceExistingBusbar = true;
        public bool VerboseFeatureScan;
        public bool PreviewOnly;
        public bool ValidateOnly;
        public bool VerifyGeometryOnly;
        public bool ExportReportOnly;
        public string[] OnlyBusbarNames;
    }
}
