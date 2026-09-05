using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using OroBI.Application.Closings;

namespace OroBI.Application.Tests.Closings;

public sealed class PayrollExcelExporterTests
{
    private static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace DocumentRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    [Fact]
    public void Export_creates_readable_workbook_with_resolvable_relationships_and_content_types()
    {
        using var archive = OpenExport(Sample());
        foreach (var entry in archive.Entries)
        {
            using var stream = entry.Open();
            Assert.NotNull(XDocument.Load(stream).Root);
        }

        var rootRelationships = Read(archive, "_rels/.rels");
        var officeDocument = Assert.Single(rootRelationships.Root!.Elements(Relationships + "Relationship"));
        Assert.Equal(DocumentRelationships.NamespaceName + "/officeDocument", (string?)officeDocument.Attribute("Type"));
        var workbookPath = (string)officeDocument.Attribute("Target")!;
        var workbook = Read(archive, workbookPath);
        var sheet = Assert.Single(workbook.Descendants(Spreadsheet + "sheet"));
        var workbookRelationships = Read(archive, "xl/_rels/workbook.xml.rels");
        var sheetRelationship = workbookRelationships.Root!.Elements().Single(item =>
            (string?)item.Attribute("Id") == (string?)sheet.Attribute(DocumentRelationships + "id"));
        Assert.Equal(DocumentRelationships.NamespaceName + "/worksheet", (string?)sheetRelationship.Attribute("Type"));
        Assert.Equal(Spreadsheet + "worksheet", Read(archive, "xl/" + (string)sheetRelationship.Attribute("Target")!).Root!.Name);
        var stylesRelationship = workbookRelationships.Root.Elements().Single(item =>
            (string?)item.Attribute("Type") == DocumentRelationships.NamespaceName + "/styles");
        Assert.Equal(Spreadsheet + "styleSheet", Read(archive, "xl/" + (string)stylesRelationship.Attribute("Target")!).Root!.Name);

        XNamespace contentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
        var types = Read(archive, "[Content_Types].xml").Root!;
        Assert.Contains(types.Elements(contentTypes + "Default"), item =>
            (string?)item.Attribute("Extension") == "rels" &&
            (string?)item.Attribute("ContentType") == "application/vnd.openxmlformats-package.relationships+xml");
        foreach (var path in new[] { "/xl/workbook.xml", "/xl/worksheets/sheet1.xml", "/xl/styles.xml" })
            Assert.Contains(types.Elements(contentTypes + "Override"), item => (string?)item.Attribute("PartName") == path);
    }

    [Fact]
    public void Export_preserves_decimal_values_and_dto_totals_without_consolidating_revenue()
    {
        var closing = Sample();
        using var archive = OpenExport(closing);
        var worksheet = Read(archive, "xl/worksheets/sheet1.xml");
        for (var index = 0; index < closing.Rows.Count; index++)
        {
            var row = closing.Rows[index];
            var cells = SellerRow(worksheet, row.Seller).Elements(Spreadsheet + "c").ToArray();
            Assert.Equal($"2026-08 · {row.Reference}", CellText(cells[1]));
            decimal[] values = [row.Revenue, row.BaseSalary, row.Commission, row.PppAward, row.GoalAward, row.Incentives, row.Total];
            int[] columns = [2, 3, 5, 6, 7, 8, 9];
            for (var valueIndex = 0; valueIndex < values.Length; valueIndex++)
                Assert.Equal(values[valueIndex], Number(cells[columns[valueIndex]]));
            if (row.CommissionPercent is { } percent)
                Assert.Equal(percent / 100m, Number(cells[4]));
            else
                Assert.Equal("Conforme regra", CellText(cells[4]));
        }

        var totals = SellerRow(worksheet, "TOTAL").Elements(Spreadsheet + "c").ToArray();
        Assert.Equal("NÃO CONSOLIDAR", CellText(totals[2]));
        Assert.Equal(closing.TotalBaseSalary, Number(totals[3]));
        Assert.Equal(closing.TotalCommission, Number(totals[5]));
        Assert.Equal(closing.TotalPppAward, Number(totals[6]));
        Assert.Equal(closing.TotalGoalAward, Number(totals[7]));
        Assert.Equal(closing.TotalIncentives, Number(totals[8]));
        Assert.Equal(closing.Total, Number(totals[9]));
        Assert.Empty(worksheet.Descendants(Spreadsheet + "f"));
    }

    [Theory]
    [InlineData("=SUM(1,2)")]
    [InlineData("+1+2")]
    [InlineData("@SUM(A1:A2)")]
    [InlineData("-2+3")]
    [InlineData("  José & <Maria> \"RH\"  ")]
    public void Export_keeps_seller_and_reference_as_literal_text(string text)
    {
        var closing = Sample() with { Rows = [Sample().Rows[0] with { Seller = text, Reference = text }] };
        using var archive = OpenExport(closing);
        var worksheet = Read(archive, "xl/worksheets/sheet1.xml");
        var cells = SellerRow(worksheet, text).Elements(Spreadsheet + "c").ToArray();
        Assert.Equal("inlineStr", (string?)cells[0].Attribute("t"));
        Assert.Equal(text, CellText(cells[0]));
        Assert.Equal("inlineStr", (string?)cells[1].Attribute("t"));
        Assert.Equal($"2026-08 · {text}", CellText(cells[1]));
        Assert.Empty(worksheet.Descendants(Spreadsheet + "f"));
    }

    [Fact]
    public void Export_formats_currency_and_percentage_and_freezes_headers_and_identity_columns()
    {
        using var archive = OpenExport(Sample());
        var worksheet = Read(archive, "xl/worksheets/sheet1.xml");
        var styles = Read(archive, "xl/styles.xml");
        var cells = SellerRow(worksheet, "VENDEDOR A").Elements(Spreadsheet + "c").ToArray();
        var formats = styles.Root!.Element(Spreadsheet + "cellXfs")!.Elements().ToArray();
        var currencyStyle = formats[int.Parse((string)cells[3].Attribute("s")!, CultureInfo.InvariantCulture)];
        var percentStyle = formats[int.Parse((string)cells[4].Attribute("s")!, CultureInfo.InvariantCulture)];
        Assert.Equal("10", (string?)percentStyle.Attribute("numFmtId"));
        var currencyFormat = styles.Descendants(Spreadsheet + "numFmt").Single(item =>
            (string?)item.Attribute("numFmtId") == (string?)currencyStyle.Attribute("numFmtId"));
        Assert.Contains("#,##0.00", (string)currencyFormat.Attribute("formatCode")!);
        Assert.Contains("R$", (string)currencyFormat.Attribute("formatCode")!);
        var pane = Assert.Single(worksheet.Descendants(Spreadsheet + "pane"));
        Assert.Equal("frozen", (string?)pane.Attribute("state"));
        Assert.Equal("2", (string?)pane.Attribute("xSplit"));
        Assert.True(int.Parse((string)pane.Attribute("ySplit")!, CultureInfo.InvariantCulture) > 0);
        Assert.NotEmpty(worksheet.Descendants(Spreadsheet + "col"));
    }

    private static PayrollClosing Sample() => new(2026, 8, "VENDEDOR A", ["VENDEDOR A"],
    [
        new("VENDEDOR A", "VENDEDOR A", "Vendas próprias", 242807.82m, 1951m, 1.25m, 3035.09775m, 180.3333333333m, 50.50m, 27.125m),
        new("TIAGO", "VENDEDOR A", "Cobertura: VENDEDOR A", 242807.82m, 2999.99m, null, 5469.288975m, 91.25m, 71.75m, 0m)
    ]);

    private static ZipArchive OpenExport(PayrollClosing closing) => new(new MemoryStream(PayrollExcelExporter.Export(closing)), ZipArchiveMode.Read);

    private static XDocument Read(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path);
        Assert.NotNull(entry);
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static XElement SellerRow(XDocument worksheet, string seller) => worksheet.Descendants(Spreadsheet + "row")
        .Single(row => CellText(row.Elements(Spreadsheet + "c").First()) == seller);

    private static string CellText(XElement cell) => string.Concat(cell.Descendants(Spreadsheet + "t").Select(text => text.Value));

    private static decimal Number(XElement cell)
    {
        Assert.True(cell.Attribute("t") is null || (string?)cell.Attribute("t") == "n");
        return decimal.Parse(cell.Element(Spreadsheet + "v")!.Value, CultureInfo.InvariantCulture);
    }
}
