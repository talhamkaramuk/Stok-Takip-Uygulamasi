using System.IO.Compression;
using System.Text;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Dtos.Exports;

namespace STOKIO.Infrastructure.Services;

public sealed class ExportService(IReportService reportService, IStockService stockService) : IExportService
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task<ExportFile> CurrentStockAsync(CancellationToken cancellationToken)
    {
        var rows = await reportService.CurrentStockAsync(cancellationToken);
        return Create(
            "stokio-current-stock.xlsx",
            "Current Stock",
            ["SKU", "Product", "Category", "Current Stock", "Critical Level", "Is Critical"],
            rows.Select(x => new[]
            {
                x.Sku,
                x.ProductName,
                x.CategoryName ?? string.Empty,
                x.CurrentStock.ToString(System.Globalization.CultureInfo.InvariantCulture),
                x.CriticalStockLevel.ToString(System.Globalization.CultureInfo.InvariantCulture),
                x.IsCritical ? "Yes" : "No"
            }));
    }

    public async Task<ExportFile> CriticalStockAsync(CancellationToken cancellationToken)
    {
        var rows = await stockService.ListCriticalStockAsync(cancellationToken);
        return Create(
            "stokio-critical-stock.xlsx",
            "Critical Stock",
            ["SKU", "Product", "Current Stock", "Critical Level"],
            rows.Select(x => new[]
            {
                x.Sku,
                x.ProductName,
                x.CurrentStock.ToString(System.Globalization.CultureInfo.InvariantCulture),
                x.CriticalStockLevel.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }));
    }

    public async Task<ExportFile> MovementsAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken)
    {
        var rows = await reportService.MovementsAsync(from, to, cancellationToken);
        return Create(
            "stokio-stock-movements.xlsx",
            "Movements",
            ["Date", "SKU", "Product", "Warehouse", "Type", "Quantity", "Previous", "New", "Reason"],
            rows.Select(x => new[]
            {
                x.CreatedAt.ToString("u", System.Globalization.CultureInfo.InvariantCulture),
                x.Sku,
                x.ProductName,
                x.WarehouseName ?? string.Empty,
                x.Type.ToString(),
                x.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                x.PreviousQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                x.NewQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                x.Reason ?? string.Empty
            }));
    }

    public async Task<ExportFile> CountDifferencesAsync(Guid countId, CancellationToken cancellationToken)
    {
        var rows = await reportService.CountDifferencesAsync(countId, cancellationToken);
        return Create(
            "stokio-count-differences.xlsx",
            "Count Differences",
            ["SKU", "Product", "Expected", "Counted", "Difference"],
            rows.Select(x => new[]
            {
                x.Sku,
                x.ProductName,
                x.ExpectedQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                x.CountedQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                x.Difference.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }));
    }

    private static ExportFile Create(string fileName, string sheetName, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        var content = SimpleXlsx.Create(sheetName, headers, rows);
        return new ExportFile(fileName, XlsxContentType, content);
    }

    private static class SimpleXlsx
    {
        public static byte[] Create(string sheetName, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
        {
            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                AddEntry(archive, "[Content_Types].xml", ContentTypesXml());
                AddEntry(archive, "_rels/.rels", RootRelationshipsXml());
                AddEntry(archive, "xl/workbook.xml", WorkbookXml(sheetName));
                AddEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationshipsXml());
                AddEntry(archive, "xl/worksheets/sheet1.xml", SheetXml(headers, rows));
                AddEntry(archive, "xl/styles.xml", StylesXml());
            }

            return stream.ToArray();
        }

        private static void AddEntry(ZipArchive archive, string path, string content)
        {
            var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(content);
        }

        private static string ContentTypesXml()
        {
            return """
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
                </Types>
                """;
        }

        private static string RootRelationshipsXml()
        {
            return """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """;
        }

        private static string WorkbookXml(string sheetName)
        {
            return $"""
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets>
                    <sheet name="{Escape(sheetName)}" sheetId="1" r:id="rId1"/>
                  </sheets>
                </workbook>
                """;
        }

        private static string WorkbookRelationshipsXml()
        {
            return """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
                </Relationships>
                """;
        }

        private static string StylesXml()
        {
            return """
                <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts>
                  <fills count="1"><fill><patternFill patternType="none"/></fill></fills>
                  <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
                  <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
                  <cellXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/></cellXfs>
                </styleSheet>
                """;
        }

        private static string SheetXml(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
        {
            var builder = new StringBuilder();
            builder.Append("""
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <sheetData>
                """);
            AppendRow(builder, 1, headers);

            var rowIndex = 2;
            foreach (var row in rows)
            {
                AppendRow(builder, rowIndex, row);
                rowIndex++;
            }

            builder.Append("""
                  </sheetData>
                </worksheet>
                """);
            return builder.ToString();
        }

        private static void AppendRow(StringBuilder builder, int rowIndex, IReadOnlyList<string> cells)
        {
            builder.Append(CultureInvariant($"    <row r=\"{rowIndex}\">"));
            for (var index = 0; index < cells.Count; index++)
            {
                builder.Append(CultureInvariant($"<c r=\"{ColumnName(index + 1)}{rowIndex}\" t=\"inlineStr\"><is><t>{Escape(cells[index])}</t></is></c>"));
            }

            builder.AppendLine("</row>");
        }

        private static string ColumnName(int number)
        {
            var name = string.Empty;
            while (number > 0)
            {
                var modulo = (number - 1) % 26;
                name = Convert.ToChar('A' + modulo) + name;
                number = (number - modulo) / 26;
            }

            return name;
        }

        private static string Escape(string value)
        {
            return System.Security.SecurityElement.Escape(value) ?? string.Empty;
        }

        private static string CultureInvariant(FormattableString value)
        {
            return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
