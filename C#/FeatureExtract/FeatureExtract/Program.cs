using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace FeatureExtract
{
    internal class Program
    {
        // 是否打印全部特征。
        // true：完整输出特征树，便于看模型里到底有哪些 Feature。
        // false：只重点输出 RefPoint / CoordSys 这类对数据层有用的特征。
        private const bool PrintAllFeatures = true;

        // 是否在当前打开的是装配体时，同时扫描每个组件内部的特征。
        // 用来对比“零件局部坐标”和“装配体全局坐标”。
        private const bool ScanAssemblyComponents = true;

        // 程序入口：连接 SolidWorks，读取当前活动文档，并按文档类型提取特征信息。
        [STAThread]
        private static void Main()
        {
            try
            {
                SldWorks swApp = GetRunningSolidWorks();
                ModelDoc2 model = swApp.ActiveDoc as ModelDoc2;

                if (model == null)
                    throw new Exception("请先打开 SolidWorks，并打开一个零件或装配体。");

                PrintDocumentHeader(model);

                int docType = model.GetType();
                if (docType == (int)swDocumentTypes_e.swDocPART)
                {
                    Console.WriteLine("===== 当前零件特征 =====");
                    ScanModelFeatures(swApp, model, null, null);
                }
                else if (docType == (int)swDocumentTypes_e.swDocASSEMBLY)
                {
                    Console.WriteLine("===== 装配体自身特征 =====");
                    ScanModelFeatures(swApp, model, "装配体自身", null);

                    if (ScanAssemblyComponents)
                        ScanAssembly(swApp, (AssemblyDoc)model);
                }
                else
                {
                    throw new Exception("当前文档不是零件或装配体，请切换到 .SLDPRT 或 .SLDASM 后再运行。");
                }

                Console.WriteLine();
                Console.WriteLine("提取完成。按任意键退出。");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine("错误：" + ex.Message);
                Console.WriteLine();
                Console.WriteLine(ex);
                Console.ReadKey();
            }
        }

        // 连接当前已经打开的 SolidWorks。
        // 这个工具是读模型信息用的，不主动启动新 SW，避免你误以为已经连接到目标文件。
        private static SldWorks GetRunningSolidWorks()
        {
            try
            {
                return (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
            }
            catch (COMException)
            {
                throw new Exception("没有找到正在运行的 SolidWorks。请先打开 SolidWorks 和目标零件/装配体。");
            }
        }

        // 打印当前活动文档的基本信息，方便确认程序读到的是不是你想看的模型。
        private static void PrintDocumentHeader(ModelDoc2 model)
        {
            Console.WriteLine("===== 当前活动文档 =====");
            Console.WriteLine("标题：" + model.GetTitle());
            Console.WriteLine("路径：" + model.GetPathName());
            Console.WriteLine("类型：" + GetDocumentTypeName(model.GetType()));
            Console.WriteLine();
        }

        // 扫描装配体中的每个组件。
        // comp.Transform2 是零件局部坐标到装配体全局坐标的转换矩阵。
        private static void ScanAssembly(SldWorks swApp, AssemblyDoc assembly)
        {
            Console.WriteLine();
            Console.WriteLine("===== 装配体组件特征 =====");

            object[] components = assembly.GetComponents(false) as object[];
            if (components == null || components.Length == 0)
            {
                Console.WriteLine("未找到组件。");
                return;
            }

            foreach (object item in components)
            {
                Component2 component = item as Component2;
                if (component == null)
                    continue;

                ModelDoc2 componentModel = component.GetModelDoc2() as ModelDoc2;
                if (componentModel == null)
                {
                    Console.WriteLine();
                    Console.WriteLine("[跳过] 组件未加载或轻化：" + component.Name2);
                    continue;
                }

                Console.WriteLine();
                Console.WriteLine("----- 组件：" + component.Name2 + " -----");
                Console.WriteLine("文件：" + component.GetPathName());
                ScanModelFeatures(swApp, componentModel, component.Name2, component.Transform2);
            }
        }

        // 遍历一个文档的 FeatureManager 特征树。
        // 对普通特征打印名称和类型；对 RefPoint / CoordSys 额外提取坐标或坐标系矩阵。
        private static void ScanModelFeatures(SldWorks swApp, ModelDoc2 model, string ownerName, MathTransform ownerTransform)
        {
            Feature feature = model.FirstFeature() as Feature;
            int index = 0;

            while (feature != null)
            {
                index++;

                string featureName = feature.Name;
                string featureType = feature.GetTypeName2();
                bool important = IsImportantFeatureType(featureType);

                if (PrintAllFeatures || important)
                    Console.WriteLine($"{index,4}. 特征：{featureName}    类型：{featureType}");

                if (featureType == "RefPoint")
                    TryPrintReferencePoint(swApp, feature, ownerName, ownerTransform);
                else if (featureType == "CoordSys")
                    TryPrintCoordinateSystem(feature, ownerName, ownerTransform);

                feature = feature.GetNextFeature() as Feature;
            }
        }

        // 读取 RefPoint 的局部坐标。
        // 如果来自装配体组件，还会通过组件 Transform2 计算装配体全局坐标。
        private static void TryPrintReferencePoint(SldWorks swApp, Feature feature, string ownerName, MathTransform ownerTransform)
        {
            RefPoint refPoint = GetSpecificFeature<RefPoint>(feature);
            if (refPoint == null)
                return;

            MathPoint localMathPoint = refPoint.GetRefPoint();
            Point3 localPoint = Point3.FromMathPoint(localMathPoint);

            Console.WriteLine("      >>> 参考点：" + feature.Name);
            Console.WriteLine("          所属：" + GetOwnerName(ownerName));
            Console.WriteLine("          局部坐标：" + localPoint.ToMillimeterText());

            if (ownerTransform != null)
            {
                Point3 assemblyPoint = TransformPoint(swApp, localPoint, ownerTransform);
                Console.WriteLine("          装配体坐标：" + assemblyPoint.ToMillimeterText());
            }
        }

        // 读取坐标系特征的变换矩阵。
        // 当前阶段主要用来确认是否存在 MOUNT_CS 这类元件基准坐标系。
        private static void TryPrintCoordinateSystem(Feature feature, string ownerName, MathTransform ownerTransform)
        {
            object specific = null;
            try
            {
                specific = feature.GetSpecificFeature2();
            }
            catch
            {
                return;
            }

            if (specific == null)
                return;

            Console.WriteLine("      >>> 坐标系：" + feature.Name);
            Console.WriteLine("          所属：" + GetOwnerName(ownerName));

            // 不同 SolidWorks 版本的 CoordSys 具体接口在 Interop 中表现可能不同。
            // 这里先稳妥打印接口类型，后续如果需要精确提取坐标系原点/轴向，再针对你的版本补充强类型读取。
            Console.WriteLine("          API对象类型：" + specific.GetType().FullName);

            if (ownerTransform != null)
                Console.WriteLine("          说明：该坐标系位于组件内部，组件 Transform2 可用于换算到装配体坐标。");
        }

        // 把零件局部点乘以组件变换矩阵，得到装配体全局点。
        private static Point3 TransformPoint(SldWorks swApp, Point3 point, MathTransform transform)
        {
            MathUtility mathUtility = (MathUtility)swApp.GetMathUtility();
            MathPoint mathPoint = (MathPoint)mathUtility.CreatePoint(new[] { point.X, point.Y, point.Z });
            MathPoint transformed = (MathPoint)mathPoint.MultiplyTransform(transform);
            return Point3.FromMathPoint(transformed);
        }

        // 安全地把通用 Feature 转成具体 API 对象。
        // SolidWorks 某些特征在 GetSpecificFeature2 时可能抛异常，所以统一在这里兜底。
        private static T GetSpecificFeature<T>(Feature feature) where T : class
        {
            try
            {
                return feature.GetSpecificFeature2() as T;
            }
            catch
            {
                return null;
            }
        }

        // 判断当前特征类型是否是数据层重点关注对象。
        private static bool IsImportantFeatureType(string featureType)
        {
            return featureType == "RefPoint" ||
                   featureType == "CoordSys" ||
                   featureType == "RefPlane" ||
                   featureType == "RefAxis";
        }

        // 把 SolidWorks 文档类型枚举转成中文说明。
        private static string GetDocumentTypeName(int type)
        {
            if (type == (int)swDocumentTypes_e.swDocPART)
                return "零件";

            if (type == (int)swDocumentTypes_e.swDocASSEMBLY)
                return "装配体";

            if (type == (int)swDocumentTypes_e.swDocDRAWING)
                return "工程图";

            return "未知类型：" + type;
        }

        // 格式化所属对象名称。
        private static string GetOwnerName(string ownerName)
        {
            return string.IsNullOrWhiteSpace(ownerName) ? "当前零件" : ownerName;
        }
    }

    // 三维点结构。
    // SolidWorks API 内部长度单位是米，显示时统一换算为 mm。
    internal struct Point3
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public Point3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        // 从 SolidWorks MathPoint 读取坐标数组。
        public static Point3 FromMathPoint(MathPoint point)
        {
            double[] data = point.ArrayData as double[];
            if (data == null || data.Length < 3)
                throw new Exception("读取 MathPoint 坐标失败。");

            return new Point3(data[0], data[1], data[2]);
        }

        // 以毫米输出坐标，方便复制到 Excel 数据层模板。
        public string ToMillimeterText()
        {
            return $"X={X * 1000.0:F3} mm, Y={Y * 1000.0:F3} mm, Z={Z * 1000.0:F3} mm";
        }
    }
}
