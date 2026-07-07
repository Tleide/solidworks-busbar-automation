using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SwFeatureDebug
{
    internal partial class Program
    {
        private static Feature CreateSheetMetalBaseFlangeFromSelectedSketch(ModelDoc2 partModel, Feature sketch, BusbarProfile profile, BusbarKind kind)
        {
            partModel.ClearSelection2(true);
            if (!sketch.Select2(false, 0))
                throw new Exception("Failed to select open-profile sketch.");

            CustomBendAllowance bendAllowance = CreateKFactorBendAllowance(partModel.FeatureManager);
            SheetMetalBaseFlangeExtent widthExtent = GetSheetMetalBaseFlangeExtent(profile, Settings.GetSheetMetalWidthSide(kind));

            Console.WriteLine(
                "Sheet metal width side [" + kind + "]: " +
                Settings.GetSheetMetalWidthSide(kind) +
                ", mode=" + widthExtent.ModeLabel +
                ", dist1=" + ToMm(widthExtent.Dist1).ToString("F3") +
                " mm, dist2=" + ToMm(widthExtent.Dist2).ToString("F3") +
                " mm, end1=" + widthExtent.EndCondition1 +
                ", end2=" + widthExtent.EndCondition2 +
                ", dirToUse=" + widthExtent.DirToUse);

            Feature feature = partModel.FeatureManager.InsertSheetMetalBaseFlange2(
                profile.Thickness,
                Settings.SheetMetalThickenDirection,
                Settings.SheetMetalBendRadius,
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

            partModel.ClearSelection2(true);
            return feature;
        }

        private static SheetMetalBaseFlangeExtent GetSheetMetalBaseFlangeExtent(BusbarProfile profile, SheetMetalWidthSide side)
        {
            const int direction1 = 1;
            double halfWidth = profile.Width / 2.0;

            if (side == SheetMetalWidthSide.Positive)
            {
                return new SheetMetalBaseFlangeExtent(
                    profile.Width,
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
                    profile.Width,
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
                profile.Width,
                0.0,
                false,
                (int)swEndConditions_e.swEndCondMidPlane,
                (int)swEndConditions_e.swEndCondBlind,
                direction1,
                "MidPlane");
        }
        private static CustomBendAllowance CreateKFactorBendAllowance(FeatureManager featureManager)
        {
            CustomBendAllowance bendAllowance = featureManager.CreateCustomBendAllowance();
            if (bendAllowance == null)
                throw new Exception("Failed to create custom bend allowance.");

            bendAllowance.Type = (int)swBendAllowanceTypes_e.swBendAllowanceKFactor;
            bendAllowance.KFactor = Settings.SheetMetalKFactor;
            return bendAllowance;
        }

        private static void ApplySheetMetalParametersToCreatedFeature(Feature createdFeature, ModelDoc2 partModel, BusbarProfile profile)
        {
            Feature sheetMetalFeature = FindFirstFeatureByType(partModel, "SheetMetal");
            if (sheetMetalFeature == null)
                return;

            SheetMetalFeatureData sheetMetalData = sheetMetalFeature.GetDefinition() as SheetMetalFeatureData;
            if (sheetMetalData == null)
                return;

            sheetMetalData.Thickness = profile.Thickness;
            sheetMetalData.BendRadius = Settings.SheetMetalBendRadius;
            sheetMetalData.BendAllowanceType = (int)swBendAllowanceTypes_e.swBendAllowanceKFactor;
            sheetMetalData.KFactor = Settings.SheetMetalKFactor;
            sheetMetalData.UseMaterialSheetMetalParameters = false;
            sheetMetalData.UseAutoRelief = true;
            sheetMetalData.AutoReliefType = (int)swSheetMetalReliefTypes_e.swSheetMetalReliefRectangular;
            sheetMetalData.SetCustomBendAllowance(CreateKFactorBendAllowance(partModel.FeatureManager));
            sheetMetalFeature.ModifyDefinition(sheetMetalData, partModel, null);
        }
    }
}