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
        private static string SaveBusbarSheetMetalPart(ModelDoc2 partModel, ModelDoc2 assemblyModel, Busbar busbar)
        {
            string assemblyPath = assemblyModel.GetPathName();
            string folder = string.IsNullOrWhiteSpace(assemblyPath)
                ? System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory)
                : Path.GetDirectoryName(assemblyPath);

            string savePath = Path.Combine(folder, busbar.Name + "_SheetMetal_" + busbar.Profile.Label + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".SLDPRT");

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

            string savePath = Path.Combine(folder, baseName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".SLDPRT");

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

        private static void InsertPartIntoAssembly(SldWorks swApp, ModelDoc2 assemblyModel, AssemblyDoc assembly, string partPath, string componentName)
        {
            ActivateDocument(swApp, assemblyModel);

            Component2 component = assembly.AddComponent5(partPath, 0, "", false, "", 0, 0, 0);
            if (component == null)
                throw new Exception("Failed to insert generated part into assembly.");

            MathUtility utility = (MathUtility)swApp.GetMathUtility();
            MathTransform identity = (MathTransform)utility.CreateTransform(new double[]
            {
                1, 0, 0,
                0, 1, 0,
                0, 0, 1,
                0, 0, 0,
                1, 0, 0, 0
            });

            component.Transform2 = identity;
            component.Name2 = componentName;
            assemblyModel.EditRebuild3();
        }
        private static void CloseBusbarPartDocument(SldWorks swApp, ModelDoc2 assemblyModel, ModelDoc2 partModel)
        {
            if (partModel == null)
                return;

            string partTitle = partModel.GetTitle();
            if (string.IsNullOrWhiteSpace(partTitle))
                return;

            ActivateDocument(swApp, assemblyModel);
            swApp.CloseDoc(partTitle);
            ActivateDocument(swApp, assemblyModel);
        }
    }
}
