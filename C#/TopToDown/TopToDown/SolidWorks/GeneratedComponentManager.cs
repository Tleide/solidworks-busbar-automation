using SolidWorks.Interop.sldworks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SwFeatureDebug
{
    internal static class GeneratedComponentManager
    {
        public static void DeleteExistingBusbars(
            ModelDoc2 model,
            AssemblyDoc assembly,
            ISet<string> retainedComponentNames = null,
            ISet<string> replacementBusbarNames = null)
        {
            object[] components = assembly.GetComponents(false) as object[];
            if (components == null || components.Length == 0)
                return;

            model.ClearSelection2(true);
            int selectedCount = 0;
            foreach (object item in components)
            {
                Component2 component = item as Component2;
                if (component == null)
                    continue;

                try
                {
                    if (ShouldDeleteExistingBusbar(
                            component.Name2,
                            retainedComponentNames,
                            replacementBusbarNames) &&
                        component.Select4(true, null, false))
                    {
                        selectedCount++;
                    }
                }
                finally
                {
                    SolidWorksCom.Release(component);
                }
            }

            if (selectedCount > 0)
            {
                Console.WriteLine("Delete old generated busbar components: " + selectedCount);
                model.EditDelete();
                model.EditRebuild3();
            }

            model.ClearSelection2(true);
            int remainingOldCount = CountOldGeneratedBusbars(
                assembly,
                retainedComponentNames,
                replacementBusbarNames);
            if (remainingOldCount > 0)
                throw new InvalidOperationException("Failed to delete " + remainingOldCount + " old generated busbar components.");
        }

        public static void DeleteSelected(
            ModelDoc2 model,
            AssemblyDoc assembly,
            IEnumerable<Component2> components,
            string description)
        {
            List<Component2> targets = (components ?? Enumerable.Empty<Component2>())
                .Where(component => component != null)
                .ToList();
            HashSet<string> targetNames = new HashSet<string>(
                targets.Select(component => component.Name2),
                StringComparer.OrdinalIgnoreCase);

            model.ClearSelection2(true);
            int selectedCount = 0;
            foreach (Component2 component in targets)
            {
                if (component.Select4(true, null, false))
                    selectedCount++;
            }

            if (selectedCount > 0)
            {
                Console.WriteLine(description + ": " + selectedCount);
                model.EditDelete();
                model.EditRebuild3();
            }

            model.ClearSelection2(true);
            int remainingCount = CountComponentsByName(assembly, targetNames);
            if (remainingCount > 0)
                throw new InvalidOperationException("Failed to delete " + remainingCount + " staged busbar components.");
        }

        private static int CountOldGeneratedBusbars(
            AssemblyDoc assembly,
            ISet<string> retainedComponentNames,
            ISet<string> replacementBusbarNames)
        {
            object[] components = assembly.GetComponents(false) as object[];
            if (components == null)
                return 0;

            int count = 0;
            foreach (object item in components)
            {
                Component2 component = item as Component2;
                if (component == null)
                    continue;

                try
                {
                    if (ShouldDeleteExistingBusbar(
                            component.Name2,
                            retainedComponentNames,
                            replacementBusbarNames))
                        count++;
                }
                finally
                {
                    SolidWorksCom.Release(component);
                }
            }

            return count;
        }

        internal static bool ShouldDeleteExistingBusbar(
            string componentName,
            ISet<string> retainedComponentNames,
            ISet<string> replacementBusbarNames)
        {
            string busbarName = GetGeneratedBusbarBaseName(componentName);
            if (busbarName == null ||
                (retainedComponentNames != null && retainedComponentNames.Contains(componentName)))
            {
                return false;
            }

            return replacementBusbarNames == null || replacementBusbarNames.Contains(busbarName);
        }

        internal static string GetGeneratedBusbarBaseName(string componentName)
        {
            if (string.IsNullOrWhiteSpace(componentName) ||
                !componentName.StartsWith("Busbar_", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            int sheetMetalMarker = componentName.IndexOf("_SheetMetal_", StringComparison.OrdinalIgnoreCase);
            if (sheetMetalMarker > 0)
                return componentName.Substring(0, sheetMetalMarker);

            int instanceSeparator = componentName.LastIndexOf('-');
            int instanceNumber;
            if (instanceSeparator > 0 &&
                instanceSeparator < componentName.Length - 1 &&
                int.TryParse(componentName.Substring(instanceSeparator + 1), out instanceNumber))
            {
                return componentName.Substring(0, instanceSeparator);
            }

            return componentName;
        }

        private static int CountComponentsByName(AssemblyDoc assembly, ISet<string> componentNames)
        {
            if (assembly == null || componentNames == null || componentNames.Count == 0)
                return 0;

            object[] components = assembly.GetComponents(false) as object[];
            if (components == null)
                return 0;

            int count = 0;
            foreach (object item in components)
            {
                Component2 component = item as Component2;
                if (component == null)
                    continue;

                try
                {
                    if (componentNames.Contains(component.Name2))
                        count++;
                }
                finally
                {
                    SolidWorksCom.Release(component);
                }
            }

            return count;
        }
    }
}
