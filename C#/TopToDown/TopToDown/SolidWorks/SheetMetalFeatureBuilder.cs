using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;

namespace SwFeatureDebug
{
    internal sealed partial class SolidWorksBusbarPartBuilder
    {
        private static Feature CreateSheetMetalBaseFlangeFromSelectedSketch(ModelDoc2 partModel, Feature sketch, Busbar busbar)
        {
            if (busbar == null || busbar.Profile == null || busbar.SheetMetal == null)
                throw new ArgumentException("Busbar profile and sheet-metal options are required.", "busbar");

            partModel.ClearSelection2(true);
            if (!sketch.Select2(false, 0))
                throw new Exception("Failed to select open-profile sketch.");

            CustomBendAllowance bendAllowance = CreateKFactorBendAllowance(partModel.FeatureManager, busbar.SheetMetal.KFactor);
            SheetMetalBaseFlangeExtent widthExtent = GetSheetMetalBaseFlangeExtent(busbar.Profile, busbar.SheetMetal.WidthSide);

            Console.WriteLine(
                "Sheet metal width side [" + busbar.Kind + "]: " +
                busbar.SheetMetal.WidthSide +
                ", mode=" + widthExtent.ModeLabel +
                ", dist1=" + ToMm(widthExtent.Dist1).ToString("F3") +
                " mm, dist2=" + ToMm(widthExtent.Dist2).ToString("F3") +
                " mm, end1=" + widthExtent.EndCondition1 +
                ", end2=" + widthExtent.EndCondition2 +
                ", dirToUse=" + widthExtent.DirToUse);

            Feature feature;
            try
            {
                feature = partModel.FeatureManager.InsertSheetMetalBaseFlange2(
                    busbar.Profile.ThicknessMeters,
                    busbar.SheetMetal.ThickenDirection,
                    Mm(busbar.SheetMetal.BendRadiusMm),
                    widthExtent.Dist1,
                    widthExtent.Dist2,
                    widthExtent.FlipExtrudeDirection,
                    widthExtent.EndCondition1,
                    widthExtent.EndCondition2,
                    widthExtent.DirToUse,
                    bendAllowance,
                    false,
                    (int)swSheetMetalReliefTypes_e.swSheetMetalReliefObround,
                    Mm(0.1),
                    Mm(0.1),
                    0.5,
                    true,
                    false,
                    true,
                    true);
            }
            finally
            {
                SolidWorksCom.Release(bendAllowance);
            }

            partModel.ClearSelection2(true);
            return feature;
        }

        private static SheetMetalBaseFlangeExtent GetSheetMetalBaseFlangeExtent(BusbarProfile profile, SheetMetalWidthSide side)
        {
            const int direction1 = 1;

            if (side == SheetMetalWidthSide.Positive)
            {
                return new SheetMetalBaseFlangeExtent(
                    profile.WidthMeters,
                    0.0,
                    false,
                    (int)swEndConditions_e.swEndCondBlind,
                    (int)swEndConditions_e.swEndCondBlind,
                    direction1,
                    "Direction1");
            }

            if (side == SheetMetalWidthSide.Negative)
            {
                return new SheetMetalBaseFlangeExtent(
                    profile.WidthMeters,
                    0.0,
                    true,
                    (int)swEndConditions_e.swEndCondBlind,
                    (int)swEndConditions_e.swEndCondBlind,
                    direction1,
                    "Direction1Flipped");
            }

            // Use SOLIDWORKS' real Mid Plane end condition.
            // Setting Dist1/Dist2 to half width with Blind conditions is not the same as the UI "Mid Plane" option.
            return new SheetMetalBaseFlangeExtent(
                profile.WidthMeters,
                0.0,
                false,
                (int)swEndConditions_e.swEndCondMidPlane,
                (int)swEndConditions_e.swEndCondBlind,
                direction1,
                "MidPlane");
        }
        private static CustomBendAllowance CreateKFactorBendAllowance(FeatureManager featureManager, double kFactor)
        {
            CustomBendAllowance bendAllowance = featureManager.CreateCustomBendAllowance();
            if (bendAllowance == null)
                throw new Exception("Failed to create custom bend allowance.");

            bendAllowance.Type = (int)swBendAllowanceTypes_e.swBendAllowanceKFactor;
            bendAllowance.KFactor = kFactor;
            return bendAllowance;
        }

        private static void ApplySheetMetalParametersToCreatedFeature(ModelDoc2 partModel, Busbar busbar)
        {
            if (busbar == null || busbar.Profile == null || busbar.SheetMetal == null)
                throw new ArgumentException("Busbar profile and sheet-metal options are required.", "busbar");

            Feature sheetMetalFeature = FindFirstFeatureByType(partModel, "SheetMetal");
            if (sheetMetalFeature == null)
                throw new Exception("The created base flange has no SheetMetal definition: " + busbar.Name);

            SheetMetalFeatureData sheetMetalData = null;
            CustomBendAllowance bendAllowance = null;
            try
            {
                sheetMetalData = sheetMetalFeature.GetDefinition() as SheetMetalFeatureData;
                if (sheetMetalData == null)
                    throw new Exception("Failed to read the SheetMetal definition: " + busbar.Name);

                sheetMetalData.Thickness = busbar.Profile.ThicknessMeters;
                sheetMetalData.BendRadius = Mm(busbar.SheetMetal.BendRadiusMm);
                sheetMetalData.BendAllowanceType = (int)swBendAllowanceTypes_e.swBendAllowanceKFactor;
                sheetMetalData.KFactor = busbar.SheetMetal.KFactor;
                sheetMetalData.UseMaterialSheetMetalParameters = false;
                sheetMetalData.UseAutoRelief = true;
                sheetMetalData.AutoReliefType = (int)swSheetMetalReliefTypes_e.swSheetMetalReliefRectangular;
                bendAllowance = CreateKFactorBendAllowance(partModel.FeatureManager, busbar.SheetMetal.KFactor);
                sheetMetalData.SetCustomBendAllowance(bendAllowance);

                if (!sheetMetalFeature.ModifyDefinition(sheetMetalData, partModel, null))
                    throw new Exception("Failed to apply sheet-metal parameters: " + busbar.Name);
            }
            finally
            {
                SolidWorksCom.Release(bendAllowance);
                SolidWorksCom.Release(sheetMetalData);
                SolidWorksCom.Release(sheetMetalFeature);
            }
        }
    }
}
