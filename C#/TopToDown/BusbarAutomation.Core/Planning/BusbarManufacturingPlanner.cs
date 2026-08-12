using System;

using BusbarAutomation.Core.Domain;

namespace BusbarAutomation.Core.Planning
{
    internal static class BusbarManufacturingPlanner
    {
        public static BusbarManufacturingPlan Build(
            BusbarDesignPlan design,
            EngineeringConfigurationSnapshot configuration)
        {
            if (design == null)
                throw new ArgumentNullException("design");

            if (configuration == null)
                throw new ArgumentNullException("configuration");

            BusbarSettings settings = configuration.ToPlanningSettings();
            ContactTopologyResolver topology = new ContactTopologyResolver();

            foreach (Busbar busbar in design.Busbars)
            {
                busbar.SheetMetal = CreateSheetMetalOptions(design, settings, busbar.Kind);
                busbar.SheetMetalSketchLine = topology.CreateSheetMetalSketchLine(busbar);
            }

            BusbarManufacturingPlan manufacturing = new BusbarManufacturingPlan
            {
                Design = design
            };
            FastenerPlanBuilder.BuildCollectorJoints(manufacturing, settings);
            return manufacturing;
        }

        private static SheetMetalOptions CreateSheetMetalOptions(
            BusbarDesignPlan design,
            BusbarSettings settings,
            BusbarKind kind)
        {
            return new SheetMetalOptions
            {
                BendRadiusMm = design.Rules.BendRadiusMm,
                KFactor = design.Rules.KFactor,
                WidthMode = design.Rules.WidthMode,
                WidthSide = settings.GetSheetMetalWidthSide(kind),
                ThickenDirection = settings.SheetMetalThickenDirection
            };
        }
    }
}
