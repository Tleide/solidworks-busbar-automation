using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Runtime.InteropServices;

namespace SwFeatureDebug
{
    internal static class SolidWorksSession
    {
        public static ModelDoc2 GetActiveOrOpenAssembly(SldWorks swApp)
        {
            ModelDoc2 model = swApp.ActiveDoc as ModelDoc2;
            if (model != null && model.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
                return model;

            object[] documents = swApp.GetDocuments() as object[];
            if (documents != null)
            {
                foreach (object item in documents)
                {
                    ModelDoc2 openModel = item as ModelDoc2;
                    if (openModel == null || openModel.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                        continue;

                    Console.WriteLine("Active document is not an assembly. Switching to open assembly: " + openModel.GetTitle());
                    ActivateDocument(swApp, openModel);
                    return openModel;
                }
            }

            throw new Exception("Open SolidWorks and activate a target assembly first.");
        }
        public static SldWorks GetOrStartSolidWorks()
        {
            try
            {
                Console.WriteLine("Connecting to running SolidWorks...");
                return (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
            }
            catch (COMException)
            {
                Console.WriteLine("No running SolidWorks instance found. Starting SolidWorks...");

                Type swType = Type.GetTypeFromProgID("SldWorks.Application");
                if (swType == null)
                    throw new Exception("SolidWorks.Application is not registered on this computer.");

                SldWorks swApp = (SldWorks)Activator.CreateInstance(swType);
                swApp.Visible = true;
                return swApp;
            }
        }
        public static void ActivateDocument(SldWorks swApp, ModelDoc2 model)
        {
            if (swApp == null || model == null)
                throw new ArgumentNullException(swApp == null ? "swApp" : "model");

            int errors = 0;
            ModelDoc2 activated = swApp.ActivateDoc3(model.GetTitle(), false, 0, ref errors) as ModelDoc2;
            if (errors != 0 || activated == null)
            {
                throw new InvalidOperationException(
                    "Failed to activate SolidWorks document '" + model.GetTitle() + "'. Error=" + errors + ".");
            }
        }
    }
}
