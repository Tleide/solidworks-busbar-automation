using System;
using System.Collections.Generic;

namespace SwFeatureDebug
{
    internal class BusbarSettings
    {
        public double MainFeedWidthMm;
        public double MainFeedThicknessMm;
        public double CollectorWidthMm;
        public double CollectorThicknessMm;
        public double NeutralCollectorWidthMm;
        public double NeutralCollectorThicknessMm;
        public List<BranchBusbarRule> PhaseBranchRules = new List<BranchBusbarRule>();
        public List<BranchBusbarRule> NeutralBranchRules = new List<BranchBusbarRule>();
        public BranchArrangement? PhaseBranchArrangementOverride;
        public BranchArrangement? NeutralBranchArrangementOverride;
        public int DoubleClampUpperStartZSign;
        public double DoubleClampOuterInitialRiseMm;
        public double DoubleClampOuterDiagonalMinimumLengthMm;

        public double CollectorPhaseSpacingMm;
        public double CollectorTopClearanceYMm;
        public double CollectorOffsetFromLoubaoInZMm;
        public double CollectorANegativeXExtendMm;
        public double CollectorBNegativeXExtendMm;
        public double CollectorCNegativeXExtendMm;
        public double NeutralCollectorNegativeXExtendMm;
        public double MainLeadOutYMm;
        public double SheetMetalBendRadiusMm;
        public double SheetMetalKFactor;
        public bool SheetMetalThickenDirection;
        public SheetMetalWidthSide MainFeedSheetMetalWidthSide;
        public SheetMetalWidthSide CollectorSheetMetalWidthSide;
        public SheetMetalWidthSide BranchSheetMetalWidthSide;
        public double MainCollectorFrontClearanceMm;
        public double MinimumThreadProjectionMm;
        public List<FastenerSpec> FastenerCatalog = new List<FastenerSpec>();

        public BusbarProfile MainFeedProfile { get { return new BusbarProfile(MainFeedWidthMm, MainFeedThicknessMm); } }
        public BusbarProfile CollectorProfile { get { return new BusbarProfile(CollectorWidthMm, CollectorThicknessMm); } }
        public BusbarProfile NeutralCollectorProfile { get { return new BusbarProfile(NeutralCollectorWidthMm, NeutralCollectorThicknessMm); } }

        public double CollectorPhaseSpacingMeters { get { return Mm(CollectorPhaseSpacingMm); } }
        public double CollectorTopClearanceYMeters { get { return Mm(CollectorTopClearanceYMm); } }
        public double CollectorOffsetFromLoubaoInZMeters { get { return Mm(CollectorOffsetFromLoubaoInZMm); } }
        public double MainLeadOutYMeters { get { return Mm(MainLeadOutYMm); } }
        public double MainCollectorFrontClearanceMeters { get { return Mm(MainCollectorFrontClearanceMm); } }
        public double DoubleClampOuterInitialRiseMeters { get { return Mm(DoubleClampOuterInitialRiseMm); } }
        public double DoubleClampOuterDiagonalMinimumLengthMeters { get { return Mm(DoubleClampOuterDiagonalMinimumLengthMm); } }

        public SheetMetalWidthSide GetSheetMetalWidthSide(BusbarKind kind)
        {
            if (kind == BusbarKind.Collector)
                return CollectorSheetMetalWidthSide;

            if (kind == BusbarKind.Branch)
                return BranchSheetMetalWidthSide;

            return MainFeedSheetMetalWidthSide;
        }

        public double GetCollectorNegativeXExtend(string phase)
        {
            if (string.Equals(phase, "A", StringComparison.OrdinalIgnoreCase))
                return Mm(CollectorANegativeXExtendMm);

            if (string.Equals(phase, "B", StringComparison.OrdinalIgnoreCase))
                return Mm(CollectorBNegativeXExtendMm);

            if (string.Equals(phase, "C", StringComparison.OrdinalIgnoreCase))
                return Mm(CollectorCNegativeXExtendMm);

            if (string.Equals(phase, "N", StringComparison.OrdinalIgnoreCase))
                return Mm(NeutralCollectorNegativeXExtendMm);

            throw new ArgumentException("No negative-X collector extension is configured for phase '" + phase + "'.", "phase");
        }

        private static double Mm(double value)
        {
            return value / 1000.0;
        }
    }
}
