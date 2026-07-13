using System;

namespace SwFeatureDebug
{
    internal class BusbarSettings
    {
        public double MainFeedWidthMm;
        public double MainFeedThicknessMm;
        public double CollectorWidthMm;
        public double CollectorThicknessMm;
        public double BranchWidthMm;
        public double BranchThicknessMm;
        public double NeutralCollectorWidthMm;
        public double NeutralCollectorThicknessMm;
        public double NeutralBranchWidthMm;
        public double NeutralBranchThicknessMm;
        public BranchArrangement PhaseBranchArrangement;
        public BranchArrangement NeutralBranchArrangement;
        public int DoubleClampUpperStartZSign;
        public double DoubleClampOuterInitialRiseMm;
        public double DoubleClampOuterDiagonalMinimumLengthMm;

        public double CollectorPhaseSpacingMm;
        public double CollectorTopClearanceYMm;
        public double CollectorOffsetFromLoubaoInZMm;
        public double CollectorNegativeXExtendMm;
        public double MainLeadOutYMm;
        public double SheetMetalBendRadiusMm;
        public double SheetMetalKFactor;
        public bool SheetMetalThickenDirection;
        public SheetMetalWidthSide MainFeedSheetMetalWidthSide;
        public SheetMetalWidthSide CollectorSheetMetalWidthSide;
        public SheetMetalWidthSide BranchSheetMetalWidthSide;
        public double MainCollectorFrontClearanceMm;

        public BusbarProfile MainFeedProfile { get { return new BusbarProfile(MainFeedWidthMm, MainFeedThicknessMm); } }
        public BusbarProfile CollectorProfile { get { return new BusbarProfile(CollectorWidthMm, CollectorThicknessMm); } }
        public BusbarProfile BranchProfile { get { return new BusbarProfile(BranchWidthMm, BranchThicknessMm); } }
        public BusbarProfile NeutralCollectorProfile { get { return new BusbarProfile(NeutralCollectorWidthMm, NeutralCollectorThicknessMm); } }
        public BusbarProfile NeutralBranchProfile { get { return new BusbarProfile(NeutralBranchWidthMm, NeutralBranchThicknessMm); } }

        public double CollectorPhaseSpacing { get { return Mm(CollectorPhaseSpacingMm); } }
        public double CollectorTopClearanceY { get { return Mm(CollectorTopClearanceYMm); } }
        public double CollectorOffsetFromLoubaoInZ { get { return Mm(CollectorOffsetFromLoubaoInZMm); } }
        public double CollectorNegativeXExtend { get { return Mm(CollectorNegativeXExtendMm); } }
        public double MainLeadOutY { get { return Mm(MainLeadOutYMm); } }
        public double MainCollectorFrontClearance { get { return Mm(MainCollectorFrontClearanceMm); } }
        public double SheetMetalBendRadius { get { return Mm(SheetMetalBendRadiusMm); } }
        public double DoubleClampOuterInitialRise { get { return Mm(DoubleClampOuterInitialRiseMm); } }
        public double DoubleClampOuterDiagonalMinimumLength { get { return Mm(DoubleClampOuterDiagonalMinimumLengthMm); } }

        public SheetMetalWidthSide GetSheetMetalWidthSide(BusbarKind kind)
        {
            if (kind == BusbarKind.Collector)
                return CollectorSheetMetalWidthSide;

            if (kind == BusbarKind.Branch)
                return BranchSheetMetalWidthSide;

            return MainFeedSheetMetalWidthSide;
        }

        private static double Mm(double value)
        {
            return value / 1000.0;
        }
    }
}
