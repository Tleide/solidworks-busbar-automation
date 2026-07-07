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
        private static ModelDoc2 GetActiveOrOpenAssembly(SldWorks swApp)
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
        private static SldWorks GetOrStartSolidWorks()
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
        private static void ActivateDocument(SldWorks swApp, ModelDoc2 model)
        {
            int errors = 0;
            swApp.ActivateDoc3(model.GetTitle(), false, 0, ref errors);
        }
    }
}