using System;
using System.Collections.Generic;
using System.Linq;

using BusbarAutomation.Core.Rules;

namespace BusbarAutomation.Core.Domain
{
    internal sealed class EngineeringConfigurationSnapshot
    {
        private readonly BusbarSettings _settings;

        public string VersionId { get; private set; }
        public BusbarProfile MainFeedProfile { get { return CloneProfile(_settings.MainFeedProfile); } }
        public BusbarProfile CollectorProfile { get { return CloneProfile(_settings.CollectorProfile); } }
        public BusbarProfile NeutralCollectorProfile { get { return CloneProfile(_settings.NeutralCollectorProfile); } }
        public BusbarOverlapRuleCatalog OverlapRules { get; private set; }

        private EngineeringConfigurationSnapshot(
            string versionId,
            BusbarSettings settings,
            BusbarOverlapRuleCatalog overlapRules)
        {
            VersionId = versionId;
            _settings = settings;
            OverlapRules = overlapRules;
        }

        public static EngineeringConfigurationSnapshot FromSettings(
            BusbarSettings settings,
            string versionId = "hardcoded-v1")
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            return new EngineeringConfigurationSnapshot(
                versionId,
                CloneSettings(settings),
                BusbarOverlapRuleMatrix.CreateCatalog());
        }

        internal BusbarSettings ToPlanningSettings()
        {
            return CloneSettings(_settings);
        }

        public IEnumerable<int> GetSupportedRatedCurrents()
        {
            return _settings.PhaseBranchRules
                .Select(rule => rule.RatedCurrentA)
                .Union(_settings.NeutralBranchRules.Select(rule => rule.RatedCurrentA))
                .Distinct()
                .ToArray();
        }

        private static BusbarSettings CloneSettings(BusbarSettings source)
        {
            return new BusbarSettings
            {
                MainFeedWidthMm = source.MainFeedWidthMm,
                MainFeedThicknessMm = source.MainFeedThicknessMm,
                CollectorWidthMm = source.CollectorWidthMm,
                CollectorThicknessMm = source.CollectorThicknessMm,
                NeutralCollectorWidthMm = source.NeutralCollectorWidthMm,
                NeutralCollectorThicknessMm = source.NeutralCollectorThicknessMm,
                PhaseBranchRules = CloneBranchRules(source.PhaseBranchRules),
                NeutralBranchRules = CloneBranchRules(source.NeutralBranchRules),
                PhaseBranchArrangementOverride = source.PhaseBranchArrangementOverride,
                NeutralBranchArrangementOverride = source.NeutralBranchArrangementOverride,
                DoubleClampUpperStartZSign = source.DoubleClampUpperStartZSign,
                DoubleClampOuterInitialRiseMm = source.DoubleClampOuterInitialRiseMm,
                DoubleClampOuterDiagonalMinimumLengthMm = source.DoubleClampOuterDiagonalMinimumLengthMm,
                CollectorPhaseSpacingMm = source.CollectorPhaseSpacingMm,
                CollectorTopClearanceYMm = source.CollectorTopClearanceYMm,
                CollectorOffsetFromLoubaoInZMm = source.CollectorOffsetFromLoubaoInZMm,
                CollectorANegativeXExtendMm = source.CollectorANegativeXExtendMm,
                CollectorBNegativeXExtendMm = source.CollectorBNegativeXExtendMm,
                CollectorCNegativeXExtendMm = source.CollectorCNegativeXExtendMm,
                NeutralCollectorNegativeXExtendMm = source.NeutralCollectorNegativeXExtendMm,
                MainLeadOutYMm = source.MainLeadOutYMm,
                SheetMetalBendRadiusMm = source.SheetMetalBendRadiusMm,
                SheetMetalKFactor = source.SheetMetalKFactor,
                SheetMetalThickenDirection = source.SheetMetalThickenDirection,
                MainFeedSheetMetalWidthSide = source.MainFeedSheetMetalWidthSide,
                CollectorSheetMetalWidthSide = source.CollectorSheetMetalWidthSide,
                BranchSheetMetalWidthSide = source.BranchSheetMetalWidthSide,
                MainCollectorFrontClearanceMm = source.MainCollectorFrontClearanceMm,
                MinimumThreadProjectionMm = source.MinimumThreadProjectionMm,
                FastenerCatalog = CloneFastenerCatalog(source.FastenerCatalog)
            };
        }

        private static List<BranchBusbarRule> CloneBranchRules(IEnumerable<BranchBusbarRule> rules)
        {
            return (rules ?? Enumerable.Empty<BranchBusbarRule>())
                .Select(rule => new BranchBusbarRule(
                    rule.RatedCurrentA,
                    rule.Profile.WidthMm,
                    rule.Profile.ThicknessMm,
                    rule.Arrangement))
                .ToList();
        }

        private static List<FastenerSpec> CloneFastenerCatalog(IEnumerable<FastenerSpec> catalog)
        {
            return (catalog ?? Enumerable.Empty<FastenerSpec>())
                .Select(spec => new FastenerSpec(
                    spec.NominalSize,
                    spec.ClearanceHoleDiameterMm,
                    spec.SpringWasherCompressedThicknessMm,
                    spec.FlatWasherThicknessMm,
                    spec.FlatWasherDiameterMm,
                    spec.NutHeightMm,
                    spec.BoltHeadHeightMm,
                    spec.BoltHeadEnvelopeDiameterMm,
                    spec.StandardLengthsMm == null ? null : spec.StandardLengthsMm.ToArray()))
                .ToList();
        }

        private static BusbarProfile CloneProfile(BusbarProfile profile)
        {
            return new BusbarProfile(profile.WidthMm, profile.ThicknessMm);
        }
    }
}
