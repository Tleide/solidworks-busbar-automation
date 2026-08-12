using System;
using System.Collections.Generic;

using BusbarAutomation.Core.Domain;
using BusbarAutomation.Core.Planning;

namespace BusbarAutomation.Application
{
    internal sealed class BusbarPlanningResult
    {
        public BusbarManufacturingPlan Plan;
        public BusbarPreflightReport Report;
    }

    internal sealed class BusbarPlanningWorkflow
    {
        private readonly string[] _phaseNames;
        private readonly EngineeringConfigurationSnapshot _configuration;

        public BusbarPreflightReport ConfigurationReport { get; private set; }
        public bool CanBuild { get { return _configuration != null && !ConfigurationReport.HasErrors; } }

        public BusbarPlanningWorkflow(BusbarSettings settings, string[] phaseNames)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            if (phaseNames == null)
                throw new ArgumentNullException("phaseNames");

            _phaseNames = (string[])phaseNames.Clone();
            ConfigurationReport = BusbarPreflightValidator.ValidateConfiguration(settings);
            if (ConfigurationReport.HasErrors)
                return;

            try
            {
                _configuration = EngineeringConfigurationSnapshot.FromSettings(settings);
            }
            catch (Exception exception)
            {
                ConfigurationReport.Messages.AddRange(
                    BusbarPreflightValidator.CreatePlanningFailure(exception).Messages);
            }
        }

        public BusbarPlanningResult Build(IList<FoundPoint> scannedPoints, string sourceId)
        {
            if (!CanBuild)
            {
                return new BusbarPlanningResult
                {
                    Report = CopyReport(ConfigurationReport)
                };
            }

            try
            {
                AssemblySnapshot assembly = AssemblySnapshotFactory.FromFoundPoints(
                    scannedPoints,
                    _phaseNames,
                    _configuration.GetSupportedRatedCurrents(),
                    sourceId);
                BusbarDesignPlan design = BusbarPlanBuilder.BuildDesignPlan(
                    assembly,
                    _phaseNames,
                    _configuration);
                BusbarManufacturingPlan plan = BusbarManufacturingPlanner.Build(design, _configuration);
                BusbarPreflightReport report = BusbarPreflightValidator.ValidatePlan(
                    plan,
                    _configuration.ToPlanningSettings(),
                    _phaseNames,
                    _configuration.OverlapRules);
                report.Messages.InsertRange(0, ConfigurationReport.Messages);

                return new BusbarPlanningResult
                {
                    Plan = plan,
                    Report = report
                };
            }
            catch (Exception exception)
            {
                return new BusbarPlanningResult
                {
                    Report = CreateFailureReport(exception)
                };
            }
        }

        public BusbarPreflightReport CreateFailureReport(Exception exception)
        {
            BusbarPreflightReport report = CopyReport(ConfigurationReport);
            report.Messages.AddRange(BusbarPreflightValidator.CreatePlanningFailure(exception).Messages);
            return report;
        }

        private static BusbarPreflightReport CopyReport(BusbarPreflightReport source)
        {
            BusbarPreflightReport copy = new BusbarPreflightReport(
                source.Title,
                source.FailureResultMessage);
            copy.Messages.AddRange(source.Messages);
            return copy;
        }
    }
}
