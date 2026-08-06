using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.IO;

namespace SwFeatureDebug
{
    internal sealed partial class SolidWorksBusbarPartBuilder
    {
        private static string SaveBusbarSheetMetalPart(ModelDoc2 partModel, ModelDoc2 assemblyModel, Busbar busbar)
        {
            string assemblyPath = assemblyModel.GetPathName();
            string folder = string.IsNullOrWhiteSpace(assemblyPath)
                ? System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory)
                : Path.GetDirectoryName(assemblyPath);

            string savePath = BuildUniquePartPath(
                folder,
                busbar.Name + "_SheetMetal_" + busbar.Profile.Label);

            int errors = 0;
            int warnings = 0;

            bool ok = partModel.Extension.SaveAs(
                savePath,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null,
                ref errors,
                ref warnings);

            if (!ok || errors != 0)
                throw new Exception("Failed to save sheet metal part. Errors=" + errors + ", Warnings=" + warnings);

            return savePath;
        }

        private static string SaveGeneratedPart(ModelDoc2 partModel, ModelDoc2 assemblyModel, string baseName)
        {
            string assemblyPath = assemblyModel.GetPathName();
            string folder = string.IsNullOrWhiteSpace(assemblyPath)
                ? System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory)
                : Path.GetDirectoryName(assemblyPath);

            string savePath = BuildUniquePartPath(folder, baseName);

            int errors = 0;
            int warnings = 0;

            bool ok = partModel.Extension.SaveAs(
                savePath,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null,
                ref errors,
                ref warnings);

            if (!ok || errors != 0)
                throw new Exception("Failed to save generated part. Errors=" + errors + ", Warnings=" + warnings);

            return savePath;
        }

        private static string BuildUniquePartPath(string folder, string baseName)
        {
            string timestampedName = baseName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            string path = Path.Combine(folder, timestampedName + ".SLDPRT");
            int suffix = 2;

            while (File.Exists(path))
            {
                path = Path.Combine(folder, timestampedName + "_" + suffix + ".SLDPRT");
                suffix++;
            }

            return path;
        }

        private static Component2 InsertPartIntoAssembly(
            SldWorks swApp,
            ModelDoc2 assemblyModel,
            AssemblyDoc assembly,
            string partPath,
            string componentName)
        {
            SolidWorksSession.ActivateDocument(swApp, assemblyModel);

            Component2 component = assembly.AddComponent5(partPath, 0, "", false, "", 0, 0, 0);
            if (component == null)
                throw new Exception("Failed to insert generated part into assembly: " + partPath);

            MathUtility utility = null;
            MathTransform identity = null;
            try
            {
                utility = (MathUtility)swApp.GetMathUtility();
                if (utility == null)
                    throw new Exception("Failed to access the SolidWorks math utility.");

                identity = (MathTransform)utility.CreateTransform(new double[]
                {
                    1, 0, 0,
                    0, 1, 0,
                    0, 0, 1,
                    0, 0, 0,
                    1, 0, 0, 0
                });

                if (identity == null)
                    throw new Exception("Failed to create the identity component transform.");

                component.Transform2 = identity;
                component.Name2 = componentName;
                return component;
            }
            finally
            {
                SolidWorksCom.Release(identity);
                SolidWorksCom.Release(utility);
            }
        }
        private static void CloseBusbarPartDocument(SldWorks swApp, ModelDoc2 assemblyModel, ModelDoc2 partModel)
        {
            if (partModel == null)
                return;

            string partTitle = partModel.GetTitle();
            if (string.IsNullOrWhiteSpace(partTitle))
                return;

            SolidWorksSession.ActivateDocument(swApp, assemblyModel);
            swApp.CloseDoc(partTitle);
        }
    }
}
