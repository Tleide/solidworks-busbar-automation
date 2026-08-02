using System;
using System.Collections.Generic;

namespace SwFeatureDebug
{
    internal class FastenerSpec
    {
        public string NominalSize;
        public double ClearanceHoleDiameterMm;
        public List<double> StandardLengthsMm = new List<double>();
        public double SpringWasherCompressedThicknessMm;
        public double FlatWasherThicknessMm;
        public double FlatWasherDiameterMm;
        public double NutHeightMm;
        public double BoltHeadHeightMm;
        public double BoltHeadEnvelopeDiameterMm;

        public FastenerSpec(
            string nominalSize,
            double clearanceHoleDiameterMm,
            double springWasherCompressedThicknessMm,
            double flatWasherThicknessMm,
            double flatWasherDiameterMm,
            double nutHeightMm,
            double boltHeadHeightMm,
            double boltHeadEnvelopeDiameterMm,
            params double[] standardLengthsMm)
        {
            NominalSize = nominalSize;
            ClearanceHoleDiameterMm = clearanceHoleDiameterMm;
            SpringWasherCompressedThicknessMm = springWasherCompressedThicknessMm;
            FlatWasherThicknessMm = flatWasherThicknessMm;
            FlatWasherDiameterMm = flatWasherDiameterMm;
            NutHeightMm = nutHeightMm;
            BoltHeadHeightMm = boltHeadHeightMm;
            BoltHeadEnvelopeDiameterMm = boltHeadEnvelopeDiameterMm;

            if (standardLengthsMm != null)
                StandardLengthsMm.AddRange(standardLengthsMm);
        }
    }

    internal class FastenerJointPlan
    {
        public string JointId;
        public string CollectorPhase;
        public Point3 HoleCenter;
        public double HoleDiameterMm;
        public FastenerSpec Fastener;
        public List<string> ConnectedBusbars = new List<string>();
        public double ClampedThicknessMm;
        public double RequiredNominalLengthMm;
        public double SelectedNominalLengthMm;
        public double ActualThreadProjectionMm;
        public double MinimumThreadProjectionMm;
        public string SelectionError;

        public bool IsValid
        {
            get { return string.IsNullOrWhiteSpace(SelectionError) && Fastener != null; }
        }
    }
}
