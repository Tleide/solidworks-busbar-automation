using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;

using BusbarAutomation.Core.Domain;

namespace BusbarAutomation.Reporting
{
    // Produces a CAD-independent production report from the planning model.
    // It intentionally uses the planned sheet-metal route, not the generated SolidWorks body.
    internal static class ProductionReportExporter
    {
        private const double DirectionTolerance = 0.000001;

        public static string Export(BusbarPlan plan, string outputDirectory)
        {
            if (plan == null)
                throw new ArgumentNullException("plan");

            if (plan.Busbars == null || plan.Busbars.Count == 0)
                throw new InvalidOperationException("Cannot export a production report without planned busbars.");

            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new ArgumentException("Report output directory is required.", "outputDirectory");

            Directory.CreateDirectory(outputDirectory);

            List<BusbarReportItem> busbars = plan.Busbars
                .OrderBy(item => GetPhase(item.Name), StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Kind)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(CreateBusbarReportItem)
                .ToList();

            List<ProductionReportSheet> sheets = new List<ProductionReportSheet>
            {
                BuildReadmeSheet(plan, busbars),
                BuildLoubaoSelectionSheet(plan.Loubaos),
                BuildCopperSummarySheet(busbars),
                BuildCopperDetailSheet(busbars),
                BuildHoleSheet(busbars),
                BuildFastenerSummarySheet(plan.FastenerJoints),
                BuildFastenerDetailSheet(plan.FastenerJoints)
            };

            string fileName = "Busbar_ProductionReport_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx";
            string outputPath = Path.Combine(outputDirectory, fileName);
            ProductionReportWorkbookWriter.Write(outputPath, sheets);
            return outputPath;
        }

        private static ProductionReportSheet BuildReadmeSheet(BusbarPlan plan, List<BusbarReportItem> busbars)
        {
            int holeCount = busbars.Sum(item => item.HoleCount);
            int bendCount = busbars.Sum(item => item.RouteMetrics.BendCount);
            double sketchLengthMm = busbars.Sum(item => item.RouteMetrics.SketchRouteLengthMm);
            double developedLengthMm = busbars.Sum(item => item.RouteMetrics.EstimatedDevelopedLengthMm);
            int fastenerCount = plan.FastenerJoints == null ? 0 : plan.FastenerJoints.Count(joint => joint != null && joint.IsValid);

            ProductionReportSheet sheet = new ProductionReportSheet(
                "说明",
                "配电箱铜排生产清单",
                "本工作簿由 BusbarPlan 导出。铜排长度采用用于建模的 SheetMetalSketchLine，包含端部余量；估算展开长度按当前折弯半径和 K 因子计算，最终下料仍应以 SolidWorks 展开图复核。",
                new[] { "项目", "数值" },
                new[] { 28.0, 100.0 });

            sheet.AddRow("生成时间", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sheet.AddRow("规划铜排数量", busbars.Count);
            sheet.AddRow("草图中心线总长 (mm)", sketchLengthMm);
            sheet.AddRow("估算展开总长 (mm)", developedLengthMm);
            sheet.AddRow("估算展开总长 (m)", developedLengthMm / 1000.0);
            sheet.AddRow("折弯总数", bendCount);
            sheet.AddRow("实体孔总数", holeCount);
            sheet.AddRow("搭接螺栓套数", fastenerCount);
            sheet.AddRow("ABC 单排漏保数量", plan.Loubaos.Count(group => group.BranchArrangement == BranchArrangement.Single));
            sheet.AddRow("ABC 双排夹接漏保数量", plan.Loubaos.Count(group => group.BranchArrangement == BranchArrangement.DoubleClamp));
            sheet.AddRow("N 单排漏保数量", plan.Loubaos.Count(group => group.NeutralBranchArrangement == BranchArrangement.Single));
            sheet.AddRow("N 双排夹接漏保数量", plan.Loubaos.Count(group => group.NeutralBranchArrangement == BranchArrangement.DoubleClamp));
            sheet.AddRow("螺栓范围", "当前仅统计铜排与汇流排搭接；漏保、刀熔端子厚度尚未进入数据层，因此不计入标准件汇总。");
            sheet.AddRow("长度口径", "草图中心线长度 = SheetMetalSketchLine 折线总长；估算展开长度 = 中性层折弯弧长与直段切线长度的合成结果。");
            sheet.AddRow("展开估算参数", "每根铜排使用各自的折弯半径、K 因子和厚度；该值不替代后续从 SolidWorks Flat Pattern 读取的最终展开长度。");
            return sheet;
        }

        private static ProductionReportSheet BuildLoubaoSelectionSheet(List<LoubaoGroup> loubaos)
        {
            ProductionReportSheet sheet = new ProductionReportSheet(
                "漏保选型",
                "漏保与分支铜排选型",
                "每一行对应扫描到的一台漏保。ABC 与 N 使用独立选型表，双排夹接表示每相生成两根分支铜排。",
                new[] { "漏保组件", "额定电流 (A)", "ABC 铜排规格", "ABC 拓扑", "N 铜排规格", "N 拓扑" },
                new[] { 36.0, 16.0, 18.0, 18.0, 18.0, 18.0 });

            foreach (LoubaoGroup loubao in (loubaos ?? new List<LoubaoGroup>())
                .OrderBy(group => group.CenterX)
                .ThenBy(group => group.ComponentName, StringComparer.OrdinalIgnoreCase))
            {
                sheet.AddRow(
                    loubao.ComponentName,
                    loubao.RatedCurrentA,
                    loubao.BranchProfile == null ? string.Empty : loubao.BranchProfile.Label + " mm",
                    FormatArrangement(loubao.BranchArrangement),
                    loubao.NeutralBranchProfile == null ? string.Empty : loubao.NeutralBranchProfile.Label + " mm",
                    FormatArrangement(loubao.NeutralBranchArrangement));
            }

            return sheet;
        }

        private static ProductionReportSheet BuildCopperSummarySheet(List<BusbarReportItem> busbars)
        {
            ProductionReportSheet sheet = new ProductionReportSheet(
                "铜排汇总",
                "铜排规格汇总",
                "按铜排厚度 x 宽度汇总。估算展开长度用于材料备料，不能替代最终展开图下料长度。",
                new[]
                {
                    "规格", "数量", "草图中心线总长 (mm)", "估算展开总长 (mm)",
                    "估算展开总长 (m)", "折弯总数", "孔总数"
                },
                new[] { 16.0, 10.0, 24.0, 24.0, 24.0, 14.0, 12.0 });

            foreach (IGrouping<string, BusbarReportItem> group in busbars.GroupBy(item => item.Busbar.Profile.Label))
            {
                sheet.AddRow(
                    group.Key + " mm",
                    group.Count(),
                    group.Sum(item => item.RouteMetrics.SketchRouteLengthMm),
                    group.Sum(item => item.RouteMetrics.EstimatedDevelopedLengthMm),
                    group.Sum(item => item.RouteMetrics.EstimatedDevelopedLengthMm) / 1000.0,
                    group.Sum(item => item.RouteMetrics.BendCount),
                    group.Sum(item => item.HoleCount));
            }

            return sheet;
        }

        private static ProductionReportSheet BuildCopperDetailSheet(List<BusbarReportItem> busbars)
        {
            ProductionReportSheet sheet = new ProductionReportSheet(
                "铜排明细",
                "逐根铜排下料明细",
                "草图中心线长度包含端部余量。估算展开长度已按本根铜排的折弯半径、K 因子和厚度折算。",
                new[]
                {
                    "铜排名称", "相别", "类别", "排角色", "规格", "宽 (mm)", "厚 (mm)",
                    "草图中心线长度 (mm)", "估算展开长度 (mm)", "折弯数", "孔数", "起点", "终点"
                },
                new[] { 42.0, 9.0, 14.0, 12.0, 12.0, 11.0, 11.0, 24.0, 24.0, 11.0, 10.0, 32.0, 32.0 });

            foreach (BusbarReportItem item in busbars)
            {
                sheet.AddRow(
                    item.Busbar.Name,
                    item.Phase,
                    FormatKind(item.Busbar.Kind),
                    FormatLegRole(item.Busbar.BranchLegRole),
                    item.Busbar.Profile.Label,
                    item.Busbar.Profile.WidthMm,
                    item.Busbar.Profile.ThicknessMm,
                    item.RouteMetrics.SketchRouteLengthMm,
                    item.RouteMetrics.EstimatedDevelopedLengthMm,
                    item.RouteMetrics.BendCount,
                    item.HoleCount,
                    FormatPort(item.Busbar.StartPort),
                    FormatPort(item.Busbar.EndPort));
            }

            return sheet;
        }

        private static ProductionReportSheet BuildHoleSheet(List<BusbarReportItem> busbars)
        {
            ProductionReportSheet sheet = new ProductionReportSheet(
                "钻孔清单",
                "铜排钻孔清单",
                "坐标为装配体坐标，单位 mm。每一行对应一根实体铜排上的一个孔；上下排夹接在各自铜排上保留对应孔位。",
                new[] { "铜排名称", "相别", "类别", "孔位名称", "孔用途", "孔径 (mm)", "X (mm)", "Y (mm)", "Z (mm)", "连接端" },
                new[] { 42.0, 9.0, 14.0, 28.0, 16.0, 13.0, 14.0, 14.0, 14.0, 30.0 });

            foreach (BusbarReportItem item in busbars)
            {
                if (item.Busbar.MountingPorts == null)
                    continue;

                foreach (ConnectionPort port in item.Busbar.MountingPorts
                    .Where(port => port != null && port.HoleDiameterMm > 0.0)
                    .OrderBy(port => port.Name, StringComparer.OrdinalIgnoreCase))
                {
                    sheet.AddRow(
                        item.Busbar.Name,
                        item.Phase,
                        FormatKind(item.Busbar.Kind),
                        port.Name,
                        FormatPortPurpose(port.Kind),
                        port.HoleDiameterMm,
                        ToMm(port.HoleCenter.X),
                        ToMm(port.HoleCenter.Y),
                        ToMm(port.HoleCenter.Z),
                        FormatPort(port));
                }
            }

            return sheet;
        }

        private static ProductionReportSheet BuildFastenerSummarySheet(List<FastenerJointPlan> joints)
        {
            ProductionReportSheet sheet = new ProductionReportSheet(
                "标准件汇总",
                "搭接标准件汇总",
                "每个搭接孔对应一套贯穿螺栓。平垫按每套 2 件、弹垫和螺母按每套 1 件统计。",
                new[] { "类别", "规格/说明", "单位", "数量" },
                new[] { 16.0, 44.0, 10.0, 12.0 });

            List<FastenerJointPlan> validJoints = (joints ?? new List<FastenerJointPlan>())
                .Where(joint => joint != null && joint.IsValid)
                .ToList();

            foreach (IGrouping<string, FastenerJointPlan> group in validJoints
                .GroupBy(joint => joint.Fastener.NominalSize + " x " + FormatMm(joint.SelectedNominalLengthMm) + "mm")
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                sheet.AddRow("螺栓", group.Key, "件", group.Count());
            }

            foreach (IGrouping<string, FastenerJointPlan> group in validJoints
                .GroupBy(joint => joint.Fastener.NominalSize)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                FastenerSpec spec = group.First().Fastener;
                sheet.AddRow("平垫", spec.NominalSize + " / 外径 " + FormatMm(spec.FlatWasherDiameterMm) + "mm", "件", group.Count() * 2);
                sheet.AddRow("弹垫", spec.NominalSize + " / 压平厚 " + FormatMm(spec.SpringWasherCompressedThicknessMm) + "mm", "件", group.Count());
                sheet.AddRow("螺母", spec.NominalSize + " / 高 " + FormatMm(spec.NutHeightMm) + "mm", "件", group.Count());
            }

            if (validJoints.Count == 0)
                sheet.AddRow("提示", "当前计划中没有可用的搭接螺栓选型结果。", "", "");

            return sheet;
        }

        private static ProductionReportSheet BuildFastenerDetailSheet(List<FastenerJointPlan> joints)
        {
            ProductionReportSheet sheet = new ProductionReportSheet(
                "螺栓明细",
                "搭接螺栓选型明细",
                "露出量为标准长度扣除夹紧层、两片平垫、压平弹垫和螺母后的实际螺纹露出量。",
                new[]
                {
                    "连接编号", "相别", "螺栓规格", "公称长度 (mm)", "孔径 (mm)",
                    "X (mm)", "Y (mm)", "Z (mm)", "夹紧层厚 (mm)", "所需最小长度 (mm)",
                    "实际露出量 (mm)", "连接铜排"
                },
                new[] { 36.0, 9.0, 14.0, 17.0, 13.0, 14.0, 14.0, 14.0, 18.0, 21.0, 18.0, 70.0 });

            foreach (FastenerJointPlan joint in (joints ?? new List<FastenerJointPlan>())
                .OrderBy(joint => joint == null ? string.Empty : joint.JointId, StringComparer.OrdinalIgnoreCase))
            {
                if (joint == null)
                    continue;

                sheet.AddRow(
                    joint.JointId,
                    joint.CollectorPhase,
                    joint.IsValid ? joint.Fastener.NominalSize : "选型失败",
                    joint.IsValid ? (object)joint.SelectedNominalLengthMm : string.Empty,
                    joint.HoleDiameterMm,
                    ToMm(joint.HoleCenter.X),
                    ToMm(joint.HoleCenter.Y),
                    ToMm(joint.HoleCenter.Z),
                    joint.ClampedThicknessMm,
                    joint.RequiredNominalLengthMm,
                    joint.ActualThreadProjectionMm,
                    joint.IsValid
                        ? string.Join(", ", joint.ConnectedBusbars.ToArray())
                        : joint.SelectionError);
            }

            return sheet;
        }

        private static BusbarReportItem CreateBusbarReportItem(Busbar busbar)
        {
            return new BusbarReportItem
            {
                Busbar = busbar,
                Phase = GetPhase(busbar.Name),
                RouteMetrics = CalculateRouteMetrics(busbar),
                HoleCount = busbar.MountingPorts == null
                    ? 0
                    : busbar.MountingPorts.Count(port => port != null && port.HoleDiameterMm > 0.0)
            };
        }

        private static RouteMetrics CalculateRouteMetrics(Busbar busbar)
        {
            List<Point3> route = busbar.SheetMetalSketchLine;
            if (route == null || route.Count < 2)
                route = busbar.LogicalCenterline;

            if (route == null || route.Count < 2)
                return new RouteMetrics();

            double routeLengthMm = 0.0;
            for (int i = 0; i < route.Count - 1; i++)
                routeLengthMm += ToMm(route[i].DistanceTo(route[i + 1]));

            double estimatedDevelopedLengthMm = routeLengthMm;
            int bendCount = 0;
            double neutralRadiusMm = (busbar.SheetMetal == null ? 0.0 : busbar.SheetMetal.BendRadiusMm) +
                (busbar.SheetMetal == null ? 0.0 : busbar.SheetMetal.KFactor) * busbar.Profile.ThicknessMm;

            for (int i = 1; i < route.Count - 1; i++)
            {
                Point3 incoming = Normalize(Subtract(route[i], route[i - 1]));
                Point3 outgoing = Normalize(Subtract(route[i + 1], route[i]));
                double dot = Clamp(Dot(incoming, outgoing), -1.0, 1.0);
                double turningAngle = Math.Acos(dot);

                if (turningAngle <= DirectionTolerance)
                    continue;

                bendCount++;

                // A 180-degree reversal has no finite tangent setback. The current route planner does not emit one,
                // but retaining the original polyline length is safer than inventing a value if a future rule does.
                if (turningAngle >= Math.PI - DirectionTolerance || neutralRadiusMm <= 0.0)
                    continue;

                double tangentSetbackMm = neutralRadiusMm * Math.Tan(turningAngle / 2.0);
                double bendAllowanceMm = neutralRadiusMm * turningAngle;
                estimatedDevelopedLengthMm += bendAllowanceMm - 2.0 * tangentSetbackMm;
            }

            return new RouteMetrics
            {
                SketchRouteLengthMm = routeLengthMm,
                EstimatedDevelopedLengthMm = estimatedDevelopedLengthMm,
                BendCount = bendCount
            };
        }

        private static string GetPhase(string busbarName)
        {
            const string prefix = "Busbar_";
            if (string.IsNullOrWhiteSpace(busbarName) || !busbarName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            int separator = busbarName.IndexOf('_', prefix.Length);
            return separator < 0 ? string.Empty : busbarName.Substring(prefix.Length, separator - prefix.Length);
        }

        private static string FormatKind(BusbarKind kind)
        {
            if (kind == BusbarKind.MainFeed)
                return "主排";
            if (kind == BusbarKind.Collector)
                return "汇流排";
            return "分支排";
        }

        private static string FormatLegRole(BranchLegRole role)
        {
            if (role == BranchLegRole.Lower)
                return "下搭接排";
            if (role == BranchLegRole.Upper)
                return "上搭接排";
            return "单排";
        }

        private static string FormatArrangement(BranchArrangement arrangement)
        {
            return arrangement == BranchArrangement.DoubleClamp ? "双排夹接" : "单排";
        }

        private static string FormatPortPurpose(PortKind kind)
        {
            if (kind == PortKind.CollectorTap)
                return "汇流排搭接";
            if (kind == PortKind.FuseOut)
                return "刀熔端子";
            return "漏保端子";
        }

        private static string FormatPort(ConnectionPort port)
        {
            if (port == null)
                return string.Empty;

            if (string.IsNullOrWhiteSpace(port.ComponentName))
                return port.Name;

            return port.ComponentName + " / " + port.Name;
        }

        private static Point3 Subtract(Point3 left, Point3 right)
        {
            return new Point3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }

        private static Point3 Normalize(Point3 value)
        {
            double length = Math.Sqrt(value.X * value.X + value.Y * value.Y + value.Z * value.Z);
            if (length <= DirectionTolerance)
                return new Point3(0.0, 0.0, 0.0);

            return new Point3(value.X / length, value.Y / length, value.Z / length);
        }

        private static double Dot(Point3 left, Point3 right)
        {
            return left.X * right.X + left.Y * right.Y + left.Z * right.Z;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static double ToMm(double valueInMeters)
        {
            return valueInMeters * 1000.0;
        }

        private static string FormatMm(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private class BusbarReportItem
        {
            public Busbar Busbar;
            public string Phase;
            public RouteMetrics RouteMetrics;
            public int HoleCount;
        }

        private class RouteMetrics
        {
            public double SketchRouteLengthMm;
            public double EstimatedDevelopedLengthMm;
            public int BendCount;
        }
    }

    internal class ProductionReportSheet
    {
        public string Name;
        public string Title;
        public string Note;
        public List<string> Headers;
        public List<List<object>> Rows = new List<List<object>>();
        public List<double> ColumnWidths;

        public ProductionReportSheet(
            string name,
            string title,
            string note,
            IEnumerable<string> headers,
            IEnumerable<double> columnWidths)
        {
            Name = name;
            Title = title;
            Note = note;
            Headers = headers.ToList();
            ColumnWidths = columnWidths.ToList();
        }

        public void AddRow(params object[] values)
        {
            Rows.Add(values == null ? new List<object>() : values.ToList());
        }
    }

    internal static class ProductionReportWorkbookWriter
    {
        private const string WorkbookNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        public static void Write(string outputPath, List<ProductionReportSheet> sheets)
        {
            if (sheets == null || sheets.Count == 0)
                throw new ArgumentException("At least one report sheet is required.", "sheets");

            using (FileStream file = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (ZipArchive archive = new ZipArchive(file, ZipArchiveMode.Create))
            {
                WriteContentTypes(archive, sheets.Count);
                WritePackageRelationships(archive);
                WriteCoreProperties(archive);
                WriteApplicationProperties(archive, sheets);
                WriteWorkbook(archive, sheets);
                WriteWorkbookRelationships(archive, sheets.Count);
                WriteStyles(archive);

                for (int i = 0; i < sheets.Count; i++)
                    WriteWorksheet(archive, sheets[i], i + 1);
            }
        }

        private static void WriteContentTypes(ZipArchive archive, int sheetCount)
        {
            WriteXmlEntry(archive, "[Content_Types].xml", writer =>
            {
                writer.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
                writer.WriteStartElement("Default");
                writer.WriteAttributeString("Extension", "rels");
                writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-package.relationships+xml");
                writer.WriteEndElement();
                writer.WriteStartElement("Default");
                writer.WriteAttributeString("Extension", "xml");
                writer.WriteAttributeString("ContentType", "application/xml");
                writer.WriteEndElement();
                WriteOverride(writer, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
                WriteOverride(writer, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
                WriteOverride(writer, "/docProps/core.xml", "application/vnd.openxmlformats-package.core-properties+xml");
                WriteOverride(writer, "/docProps/app.xml", "application/vnd.openxmlformats-officedocument.extended-properties+xml");
                for (int i = 1; i <= sheetCount; i++)
                    WriteOverride(writer, "/xl/worksheets/sheet" + i + ".xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
                writer.WriteEndElement();
            });
        }

        private static void WriteOverride(XmlWriter writer, string partName, string contentType)
        {
            writer.WriteStartElement("Override");
            writer.WriteAttributeString("PartName", partName);
            writer.WriteAttributeString("ContentType", contentType);
            writer.WriteEndElement();
        }

        private static void WritePackageRelationships(ZipArchive archive)
        {
            WriteXmlEntry(archive, "_rels/.rels", writer =>
            {
                writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                WriteRelationship(writer, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "xl/workbook.xml");
                WriteRelationship(writer, "rId2", "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties", "docProps/core.xml");
                WriteRelationship(writer, "rId3", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties", "docProps/app.xml");
                writer.WriteEndElement();
            });
        }

        private static void WriteRelationship(XmlWriter writer, string id, string type, string target)
        {
            writer.WriteStartElement("Relationship");
            writer.WriteAttributeString("Id", id);
            writer.WriteAttributeString("Type", type);
            writer.WriteAttributeString("Target", target);
            writer.WriteEndElement();
        }

        private static void WriteCoreProperties(ZipArchive archive)
        {
            WriteXmlEntry(archive, "docProps/core.xml", writer =>
            {
                writer.WriteStartElement("cp", "coreProperties", "http://schemas.openxmlformats.org/package/2006/metadata/core-properties");
                writer.WriteAttributeString("xmlns", "dc", null, "http://purl.org/dc/elements/1.1/");
                writer.WriteAttributeString("xmlns", "dcterms", null, "http://purl.org/dc/terms/");
                writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
                writer.WriteElementString("dc", "creator", "http://purl.org/dc/elements/1.1/", "TopToDown");
                writer.WriteElementString("dc", "title", "http://purl.org/dc/elements/1.1/", "Busbar Production Report");
                writer.WriteStartElement("dcterms", "created", "http://purl.org/dc/terms/");
                writer.WriteAttributeString("xsi", "type", "http://www.w3.org/2001/XMLSchema-instance", "dcterms:W3CDTF");
                writer.WriteString(DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture) + "Z");
                writer.WriteEndElement();
                writer.WriteEndElement();
            });
        }

        private static void WriteApplicationProperties(ZipArchive archive, List<ProductionReportSheet> sheets)
        {
            WriteXmlEntry(archive, "docProps/app.xml", writer =>
            {
                writer.WriteStartElement("Properties", "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties");
                writer.WriteElementString("Application", "TopToDown");
                writer.WriteStartElement("HeadingPairs");
                writer.WriteStartElement("vt", "vector", "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes");
                writer.WriteAttributeString("size", "2");
                writer.WriteAttributeString("baseType", "variant");
                writer.WriteStartElement("vt", "variant", "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes");
                writer.WriteElementString("vt", "lpstr", "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes", "Worksheets");
                writer.WriteEndElement();
                writer.WriteStartElement("vt", "variant", "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes");
                writer.WriteElementString("vt", "i4", "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes", sheets.Count.ToString(CultureInfo.InvariantCulture));
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteStartElement("TitlesOfParts");
                writer.WriteStartElement("vt", "vector", "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes");
                writer.WriteAttributeString("size", sheets.Count.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("baseType", "lpstr");
                foreach (ProductionReportSheet sheet in sheets)
                    writer.WriteElementString("vt", "lpstr", "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes", sheet.Name);
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();
            });
        }

        private static void WriteWorkbook(ZipArchive archive, List<ProductionReportSheet> sheets)
        {
            WriteXmlEntry(archive, "xl/workbook.xml", writer =>
            {
                writer.WriteStartElement("workbook", WorkbookNamespace);
                writer.WriteAttributeString("xmlns", "r", null, RelationshipNamespace);
                writer.WriteStartElement("sheets");
                for (int i = 0; i < sheets.Count; i++)
                {
                    writer.WriteStartElement("sheet");
                    writer.WriteAttributeString("name", sheets[i].Name);
                    writer.WriteAttributeString("sheetId", (i + 1).ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("r", "id", RelationshipNamespace, "rId" + (i + 1));
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteEndElement();
            });
        }

        private static void WriteWorkbookRelationships(ZipArchive archive, int sheetCount)
        {
            WriteXmlEntry(archive, "xl/_rels/workbook.xml.rels", writer =>
            {
                writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                for (int i = 1; i <= sheetCount; i++)
                    WriteRelationship(writer, "rId" + i, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", "worksheets/sheet" + i + ".xml");
                WriteRelationship(writer, "rId" + (sheetCount + 1), "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml");
                writer.WriteEndElement();
            });
        }

        private static void WriteStyles(ZipArchive archive)
        {
            WriteXmlEntry(archive, "xl/styles.xml", writer =>
            {
                writer.WriteStartElement("styleSheet", WorkbookNamespace);
                writer.WriteStartElement("numFmts");
                writer.WriteAttributeString("count", "1");
                writer.WriteStartElement("numFmt");
                writer.WriteAttributeString("numFmtId", "164");
                writer.WriteAttributeString("formatCode", "#,##0.000");
                writer.WriteEndElement();
                writer.WriteEndElement();

                writer.WriteStartElement("fonts");
                writer.WriteAttributeString("count", "3");
                WriteFont(writer, "Calibri", 11, false, "FF1F2937");
                WriteFont(writer, "Microsoft YaHei", 14, true, "FFFFFFFF");
                WriteFont(writer, "Microsoft YaHei", 10, false, "FF4B5563", true);
                writer.WriteEndElement();

                writer.WriteStartElement("fills");
                writer.WriteAttributeString("count", "4");
                writer.WriteStartElement("fill");
                writer.WriteStartElement("patternFill");
                writer.WriteAttributeString("patternType", "none");
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteStartElement("fill");
                writer.WriteStartElement("patternFill");
                writer.WriteAttributeString("patternType", "gray125");
                writer.WriteEndElement();
                writer.WriteEndElement();
                WriteSolidFill(writer, "FF0F766E");
                WriteSolidFill(writer, "FF115E59");
                writer.WriteEndElement();

                writer.WriteStartElement("borders");
                writer.WriteAttributeString("count", "1");
                writer.WriteStartElement("border");
                writer.WriteElementString("left", string.Empty);
                writer.WriteElementString("right", string.Empty);
                writer.WriteElementString("top", string.Empty);
                writer.WriteElementString("bottom", string.Empty);
                writer.WriteElementString("diagonal", string.Empty);
                writer.WriteEndElement();
                writer.WriteEndElement();

                writer.WriteStartElement("cellStyleXfs");
                writer.WriteAttributeString("count", "1");
                writer.WriteStartElement("xf");
                writer.WriteAttributeString("numFmtId", "0");
                writer.WriteAttributeString("fontId", "0");
                writer.WriteAttributeString("fillId", "0");
                writer.WriteAttributeString("borderId", "0");
                writer.WriteEndElement();
                writer.WriteEndElement();

                writer.WriteStartElement("cellXfs");
                writer.WriteAttributeString("count", "7");
                WriteCellFormat(writer, 0, 0, 0, 0, false, false, "left", false);
                WriteCellFormat(writer, 0, 1, 2, 0, false, false, "left", false);
                WriteCellFormat(writer, 0, 2, 0, 0, false, false, "left", true);
                WriteCellFormat(writer, 0, 1, 3, 0, false, false, "center", true);
                WriteCellFormat(writer, 164, 0, 0, 0, true, false, "right", false);
                WriteCellFormat(writer, 1, 0, 0, 0, true, false, "right", false);
                WriteCellFormat(writer, 0, 0, 0, 0, false, true, "left", true);
                writer.WriteEndElement();

                writer.WriteStartElement("cellStyles");
                writer.WriteAttributeString("count", "1");
                writer.WriteStartElement("cellStyle");
                writer.WriteAttributeString("name", "Normal");
                writer.WriteAttributeString("xfId", "0");
                writer.WriteAttributeString("builtinId", "0");
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();
            });
        }

        private static void WriteFont(XmlWriter writer, string name, int size, bool bold, string color, bool italic = false)
        {
            writer.WriteStartElement("font");
            if (bold)
                writer.WriteElementString("b", string.Empty);
            if (italic)
                writer.WriteElementString("i", string.Empty);
            writer.WriteStartElement("sz");
            writer.WriteAttributeString("val", size.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
            writer.WriteStartElement("color");
            writer.WriteAttributeString("rgb", color);
            writer.WriteEndElement();
            writer.WriteStartElement("name");
            writer.WriteAttributeString("val", name);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteSolidFill(XmlWriter writer, string color)
        {
            writer.WriteStartElement("fill");
            writer.WriteStartElement("patternFill");
            writer.WriteAttributeString("patternType", "solid");
            writer.WriteStartElement("fgColor");
            writer.WriteAttributeString("rgb", color);
            writer.WriteEndElement();
            writer.WriteStartElement("bgColor");
            writer.WriteAttributeString("indexed", "64");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteCellFormat(
            XmlWriter writer,
            int numberFormatId,
            int fontId,
            int fillId,
            int borderId,
            bool applyNumberFormat,
            bool applyWrap,
            string horizontal,
            bool wrapText)
        {
            writer.WriteStartElement("xf");
            writer.WriteAttributeString("numFmtId", numberFormatId.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("fontId", fontId.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("fillId", fillId.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("borderId", borderId.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("xfId", "0");
            if (applyNumberFormat)
                writer.WriteAttributeString("applyNumberFormat", "1");
            if (applyWrap || wrapText || !string.IsNullOrWhiteSpace(horizontal))
            {
                writer.WriteStartElement("alignment");
                writer.WriteAttributeString("horizontal", horizontal);
                writer.WriteAttributeString("vertical", "center");
                if (applyWrap || wrapText)
                    writer.WriteAttributeString("wrapText", "1");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WriteWorksheet(ZipArchive archive, ProductionReportSheet sheet, int sheetIndex)
        {
            WriteXmlEntry(archive, "xl/worksheets/sheet" + sheetIndex + ".xml", writer =>
            {
                int columnCount = sheet.Headers.Count;
                writer.WriteStartElement("worksheet", WorkbookNamespace);
                writer.WriteStartElement("sheetViews");
                writer.WriteStartElement("sheetView");
                writer.WriteAttributeString("workbookViewId", "0");
                writer.WriteAttributeString("showGridLines", "0");
                writer.WriteStartElement("pane");
                writer.WriteAttributeString("ySplit", "3");
                writer.WriteAttributeString("topLeftCell", "A4");
                writer.WriteAttributeString("activePane", "bottomLeft");
                writer.WriteAttributeString("state", "frozen");
                writer.WriteEndElement();
                writer.WriteStartElement("selection");
                writer.WriteAttributeString("pane", "bottomLeft");
                writer.WriteAttributeString("activeCell", "A4");
                writer.WriteAttributeString("sqref", "A4");
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();

                writer.WriteStartElement("sheetFormatPr");
                writer.WriteAttributeString("defaultRowHeight", "18");
                writer.WriteEndElement();

                writer.WriteStartElement("cols");
                for (int i = 0; i < columnCount; i++)
                {
                    writer.WriteStartElement("col");
                    writer.WriteAttributeString("min", (i + 1).ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("max", (i + 1).ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("width", sheet.ColumnWidths[i].ToString("0.##", CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("customWidth", "1");
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();

                writer.WriteStartElement("sheetData");
                WriteSingleCellRow(writer, 1, sheet.Title, 1, 26.0);
                WriteSingleCellRow(writer, 2, sheet.Note, 2, 42.0);
                WriteHeaderRow(writer, 3, sheet.Headers);
                for (int rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++)
                    WriteDataRow(writer, rowIndex + 4, sheet.Rows[rowIndex], columnCount);
                writer.WriteEndElement();

                string lastColumn = ColumnName(columnCount);
                string lastRow = (sheet.Rows.Count + 3).ToString(CultureInfo.InvariantCulture);
                writer.WriteStartElement("autoFilter");
                writer.WriteAttributeString("ref", "A3:" + lastColumn + lastRow);
                writer.WriteEndElement();

                writer.WriteStartElement("mergeCells");
                writer.WriteAttributeString("count", "2");
                writer.WriteStartElement("mergeCell");
                writer.WriteAttributeString("ref", "A1:" + lastColumn + "1");
                writer.WriteEndElement();
                writer.WriteStartElement("mergeCell");
                writer.WriteAttributeString("ref", "A2:" + lastColumn + "2");
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();
            });
        }

        private static void WriteSingleCellRow(XmlWriter writer, int rowIndex, string value, int styleIndex, double height)
        {
            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", rowIndex.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("ht", height.ToString("0.##", CultureInfo.InvariantCulture));
            writer.WriteAttributeString("customHeight", "1");
            WriteCell(writer, "A" + rowIndex, value, styleIndex);
            writer.WriteEndElement();
        }

        private static void WriteHeaderRow(XmlWriter writer, int rowIndex, List<string> headers)
        {
            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", rowIndex.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("ht", "30");
            writer.WriteAttributeString("customHeight", "1");
            for (int i = 0; i < headers.Count; i++)
                WriteCell(writer, ColumnName(i + 1) + rowIndex, headers[i], 3);
            writer.WriteEndElement();
        }

        private static void WriteDataRow(XmlWriter writer, int rowIndex, List<object> values, int columnCount)
        {
            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", rowIndex.ToString(CultureInfo.InvariantCulture));
            double rowHeight = values.Any(value => value is string && ((string)value).Length > 24) ? 36.0 : 20.0;
            writer.WriteAttributeString("ht", rowHeight.ToString("0.##", CultureInfo.InvariantCulture));
            writer.WriteAttributeString("customHeight", "1");
            for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                object value = columnIndex < values.Count ? values[columnIndex] : string.Empty;
                WriteCell(writer, ColumnName(columnIndex + 1) + rowIndex, value, GetDataStyle(value));
            }
            writer.WriteEndElement();
        }

        private static int GetDataStyle(object value)
        {
            if (value is int || value is long)
                return 5;
            if (value is double || value is float || value is decimal)
                return 4;
            if (value is string)
                return 6;
            return 0;
        }

        private static void WriteCell(XmlWriter writer, string reference, object value, int styleIndex)
        {
            writer.WriteStartElement("c");
            writer.WriteAttributeString("r", reference);
            writer.WriteAttributeString("s", styleIndex.ToString(CultureInfo.InvariantCulture));

            if (value == null)
            {
                writer.WriteEndElement();
                return;
            }

            if (value is string)
            {
                string text = (string)value;
                writer.WriteAttributeString("t", "inlineStr");
                writer.WriteStartElement("is");
                writer.WriteStartElement("t");
                if (text.Length > 0 && (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[text.Length - 1])))
                    writer.WriteAttributeString("xml", "space", null, "preserve");
                writer.WriteString(text);
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();
                return;
            }

            if (value is bool)
            {
                writer.WriteAttributeString("t", "b");
                writer.WriteElementString("v", (bool)value ? "1" : "0");
                writer.WriteEndElement();
                return;
            }

            writer.WriteElementString("v", Convert.ToString(value, CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        private static string ColumnName(int index)
        {
            StringBuilder result = new StringBuilder();
            while (index > 0)
            {
                index--;
                result.Insert(0, (char)('A' + (index % 26)));
                index /= 26;
            }

            return result.ToString();
        }

        private static void WriteXmlEntry(ZipArchive archive, string entryName, Action<XmlWriter> writeAction)
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using (Stream stream = entry.Open())
            using (XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                OmitXmlDeclaration = false
            }))
            {
                writeAction(writer);
            }
        }
    }
}
