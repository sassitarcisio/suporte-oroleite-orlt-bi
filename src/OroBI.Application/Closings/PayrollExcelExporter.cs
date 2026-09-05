using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace OroBI.Application.Closings;

public static class PayrollExcelExporter
{
    private static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace DocumentRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";

    public static byte[] Export(PayrollClosing closing)
    {
        ArgumentNullException.ThrowIfNull(closing);
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml", new XElement(ContentTypes + "Types",
                new XElement(ContentTypes + "Default", new XAttribute("Extension", "rels"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(ContentTypes + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                ContentType("/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"),
                ContentType("/xl/worksheets/sheet1.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"),
                ContentType("/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml")));
            Write(archive, "_rels/.rels", new XElement(Relationships + "Relationships",
                Relationship("rId1", "officeDocument", "xl/workbook.xml")));
            Write(archive, "xl/workbook.xml", new XElement(Spreadsheet + "workbook",
                new XAttribute(XNamespace.Xmlns + "r", DocumentRelationships),
                new XElement(Spreadsheet + "sheets", new XElement(Spreadsheet + "sheet",
                    new XAttribute("name", "Fechamento RH"), new XAttribute("sheetId", 1),
                    new XAttribute(DocumentRelationships + "id", "rId1")))));
            Write(archive, "xl/_rels/workbook.xml.rels", new XElement(Relationships + "Relationships",
                Relationship("rId1", "worksheet", "worksheets/sheet1.xml"), Relationship("rId2", "styles", "styles.xml")));
            Write(archive, "xl/styles.xml", Styles());
            Write(archive, "xl/worksheets/sheet1.xml", Worksheet(closing));
        }
        return output.ToArray();
    }

    private static XElement Worksheet(PayrollClosing closing)
    {
        var period = FormattableString.Invariant($"{closing.Year:D4}-{closing.Month:D2}");
        string[] headers = ["Vendedor", "Referência", "Faturamento / base", "Salário-base", "Comissão %", "Comissão",
            "PPP Nestlé", "Prêmio metas / equipe", "Incentivos / prêmio troca", "Total previsto"];
        var data = new XElement(Spreadsheet + "sheetData",
            Row(1, Text("A1", $"Fechamento RH · {period}", 1)),
            Row(2, Text("A2", $"Cobertura Tiago: {closing.CoverageSeller}. Incentivos = PPP + metas/equipe + prêmio troca.")),
            Row(3, headers.Select((header, index) => Text($"{(char)('A' + index)}3", header, 1))));
        var rowNumber = 4;
        foreach (var row in closing.Rows)
        {
            data.Add(Row(rowNumber,
                Text($"A{rowNumber}", row.Seller), Text($"B{rowNumber}", $"{period} · {row.Reference}"),
                Money($"C{rowNumber}", row.Revenue), Money($"D{rowNumber}", row.BaseSalary),
                row.CommissionPercent is { } percent
                    ? Number($"E{rowNumber}", percent / 100m, 3)
                    : Text($"E{rowNumber}", "Conforme regra"),
                Money($"F{rowNumber}", row.Commission), Money($"G{rowNumber}", row.PppAward),
                Money($"H{rowNumber}", row.GoalAward), Money($"I{rowNumber}", row.Incentives), Money($"J{rowNumber}", row.Total)));
            rowNumber++;
        }
        data.Add(Row(rowNumber,
            Text($"A{rowNumber}", "TOTAL", 1), Text($"B{rowNumber}", "", 1), Text($"C{rowNumber}", "NÃO CONSOLIDAR", 1),
            Money($"D{rowNumber}", closing.TotalBaseSalary, true), Text($"E{rowNumber}", "", 1),
            Money($"F{rowNumber}", closing.TotalCommission, true), Money($"G{rowNumber}", closing.TotalPppAward, true),
            Money($"H{rowNumber}", closing.TotalGoalAward, true), Money($"I{rowNumber}", closing.TotalIncentives, true),
            Money($"J{rowNumber}", closing.Total, true)));

        return new XElement(Spreadsheet + "worksheet",
            new XElement(Spreadsheet + "dimension", new XAttribute("ref", $"A1:J{rowNumber}")),
            new XElement(Spreadsheet + "sheetViews", new XElement(Spreadsheet + "sheetView",
                new XAttribute("workbookViewId", 0),
                new XElement(Spreadsheet + "pane", new XAttribute("xSplit", 2), new XAttribute("ySplit", 3),
                    new XAttribute("topLeftCell", "C4"), new XAttribute("activePane", "bottomRight"), new XAttribute("state", "frozen")),
                new XElement(Spreadsheet + "selection", new XAttribute("pane", "bottomRight"),
                    new XAttribute("activeCell", "C4"), new XAttribute("sqref", "C4")))),
            new XElement(Spreadsheet + "cols", Column(1, 1, 34), Column(2, 2, 48), Column(3, 4, 22),
                Column(5, 5, 19), Column(6, 7, 20), Column(8, 9, 28), Column(10, 10, 22)),
            data,
            new XElement(Spreadsheet + "mergeCells", new XAttribute("count", 2),
                new XElement(Spreadsheet + "mergeCell", new XAttribute("ref", "A1:J1")),
                new XElement(Spreadsheet + "mergeCell", new XAttribute("ref", "A2:J2"))));
    }

    private static XElement Styles() => new(Spreadsheet + "styleSheet",
        new XElement(Spreadsheet + "numFmts", new XAttribute("count", 1),
            new XElement(Spreadsheet + "numFmt", new XAttribute("numFmtId", 164), new XAttribute("formatCode", "\"R$\" #,##0.00"))),
        new XElement(Spreadsheet + "fonts", new XAttribute("count", 2),
            new XElement(Spreadsheet + "font", new XElement(Spreadsheet + "sz", new XAttribute("val", 11)),
                new XElement(Spreadsheet + "name", new XAttribute("val", "Calibri"))),
            new XElement(Spreadsheet + "font", new XElement(Spreadsheet + "b"),
                new XElement(Spreadsheet + "sz", new XAttribute("val", 11)),
                new XElement(Spreadsheet + "color", new XAttribute("rgb", "FFFFFFFF")),
                new XElement(Spreadsheet + "name", new XAttribute("val", "Calibri")))),
        new XElement(Spreadsheet + "fills", new XAttribute("count", 3),
            new XElement(Spreadsheet + "fill", new XElement(Spreadsheet + "patternFill", new XAttribute("patternType", "none"))),
            new XElement(Spreadsheet + "fill", new XElement(Spreadsheet + "patternFill", new XAttribute("patternType", "gray125"))),
            new XElement(Spreadsheet + "fill", new XElement(Spreadsheet + "patternFill", new XAttribute("patternType", "solid"),
                new XElement(Spreadsheet + "fgColor", new XAttribute("rgb", "FF14283F")),
                new XElement(Spreadsheet + "bgColor", new XAttribute("indexed", 64))))),
        new XElement(Spreadsheet + "borders", new XAttribute("count", 1), new XElement(Spreadsheet + "border")),
        new XElement(Spreadsheet + "cellStyleXfs", new XAttribute("count", 1), Format(0, false)),
        new XElement(Spreadsheet + "cellXfs", new XAttribute("count", 5),
            Format(0, false), Format(0, true), Format(164, false), Format(10, false), Format(164, true)),
        new XElement(Spreadsheet + "cellStyles", new XAttribute("count", 1),
            new XElement(Spreadsheet + "cellStyle", new XAttribute("name", "Normal"), new XAttribute("xfId", 0), new XAttribute("builtinId", 0))));

    private static XElement Format(int numberFormat, bool highlighted) => new(Spreadsheet + "xf",
        new XAttribute("numFmtId", numberFormat), new XAttribute("fontId", highlighted ? 1 : 0),
        new XAttribute("fillId", highlighted ? 2 : 0), new XAttribute("borderId", 0),
        new XAttribute("applyNumberFormat", 1), new XAttribute("applyFont", 1), new XAttribute("applyFill", 1));

    private static XElement Column(int first, int last, int width) => new(Spreadsheet + "col",
        new XAttribute("min", first), new XAttribute("max", last), new XAttribute("width", width), new XAttribute("customWidth", 1));

    private static XElement Row(int number, params object[] cells) => new(Spreadsheet + "row", new XAttribute("r", number), cells);

    // Inline strings ensure even imported text beginning with '=' never becomes a formula.
    private static XElement Text(string address, string value, int style = 0) => new(Spreadsheet + "c",
        new XAttribute("r", address), new XAttribute("s", style), new XAttribute("t", "inlineStr"),
        new XElement(Spreadsheet + "is", new XElement(Spreadsheet + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value)));

    private static XElement Money(string address, decimal value, bool total = false) => Number(address, value, total ? 4 : 2);

    private static XElement Number(string address, decimal value, int style) => new(Spreadsheet + "c",
        new XAttribute("r", address), new XAttribute("s", style), new XAttribute("t", "n"),
        new XElement(Spreadsheet + "v", value.ToString(CultureInfo.InvariantCulture)));

    private static XElement ContentType(string part, string type) => new(ContentTypes + "Override",
        new XAttribute("PartName", part), new XAttribute("ContentType", type));

    private static XElement Relationship(string id, string type, string target) => new(Relationships + "Relationship",
        new XAttribute("Id", id), new XAttribute("Type", DocumentRelationships.NamespaceName + "/" + type), new XAttribute("Target", target));

    private static void Write(ZipArchive archive, string path, XElement root)
    {
        using var stream = archive.CreateEntry(path, CompressionLevel.Optimal).Open();
        new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root).Save(stream);
    }
}
