using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ConstFire.Backend.Models;

namespace ConstFire.Backend.Services;

/// <summary>
/// Updates cell values in the master Excel template without rewriting styles, tabs, or layout.
/// </summary>
internal static class TemplateWorkbookExporter
{
    private const int ModuleDataStartRow = 3;
    private const int IndexDataStartRow = 5;
    private const int IndexDataEndRow = 19;

    private static readonly Regex FieldRefRegex = new(@"^(\d+\.\d+)", RegexOptions.Compiled);

    public static byte[] FillTemplate(
        byte[] templateBytes,
        IReadOnlyDictionary<string, ErpModule> moduleByCode,
        IReadOnlyDictionary<int, List<ErpRecord>> recordsByModuleId)
    {
        using var stream = new MemoryStream();
        stream.Write(templateBytes, 0, templateBytes.Length);
        stream.Position = 0;

        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var workbookPart = document.WorkbookPart
                ?? throw new InvalidOperationException("Workbook part missing in template.");

            var sharedStrings = workbookPart.SharedStringTablePart
                ?? throw new InvalidOperationException("Shared string table missing in template.");

            if (TryGetWorksheet(workbookPart, "T00", out var indexSheet))
                FillIndexSheet(indexSheet, sharedStrings, moduleByCode, recordsByModuleId);

            foreach (var sheet in workbookPart.Workbook.Sheets!.Elements<Sheet>())
            {
                var name = sheet.Name?.Value ?? "";
                if (!name.StartsWith('T') || name.Equals("T00", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!TryGetWorksheet(workbookPart, name, out var moduleSheet))
                    continue;

                var code = NormalizeModuleCode(name.Length > 1 ? name[1..] : "");
                moduleByCode.TryGetValue(code, out var module);
                var records = module is not null && recordsByModuleId.TryGetValue(module.Id, out var list)
                    ? list
                    : [];

                FillModuleSheet(moduleSheet, sharedStrings, records);
            }

            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    private static void FillIndexSheet(
        WorksheetPart worksheetPart,
        SharedStringTablePart sharedStrings,
        IReadOnlyDictionary<string, ErpModule> moduleByCode,
        IReadOnlyDictionary<int, List<ErpRecord>> recordsByModuleId)
    {
        var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>()
            ?? throw new InvalidOperationException("T00 sheet data missing.");

        for (uint row = IndexDataStartRow; row <= IndexDataEndRow; row++)
        {
            var pageCode = NormalizeModuleCode(GetCellText(sheetData, row, "B", sharedStrings));
            if (string.IsNullOrEmpty(pageCode))
                continue;

            if (!moduleByCode.TryGetValue(pageCode, out var module))
                continue;

            var count = recordsByModuleId.TryGetValue(module.Id, out var recs) ? recs.Count : 0;
            SetCellNumber(sheetData, row, "A", (int)(row - IndexDataStartRow + 1));
            var indexRow = GetRow(sheetData, row);
            if (indexRow is null)
                continue;

            SetCellSharedStringOnRow(indexRow, sharedStrings, row, "B", NormalizeModuleCode(module.Code), null);
            SetCellSharedStringOnRow(indexRow, sharedStrings, row, "C", module.Name, null);
            SetCellSharedStringOnRow(indexRow, sharedStrings, row, "D", module.Category ?? "", null);
            SetCellNumber(sheetData, row, "E", count);
        }

        worksheetPart.Worksheet.Save();
    }

    private static void FillModuleSheet(
        WorksheetPart worksheetPart,
        SharedStringTablePart sharedStrings,
        IReadOnlyList<ErpRecord> records)
    {
        var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>()
            ?? throw new InvalidOperationException("Module sheet data missing.");

        var lastCol = GetLastColumnIndex(sheetData, 2);
        if (lastCol == 0)
            return;

        var columnByRef = BuildColumnMap(sheetData, sharedStrings, 2, lastCol);
        if (columnByRef.Count == 0)
            return;

        var templateRow = GetRow(sheetData, ModuleDataStartRow);
        if (templateRow is null)
            return;

        var styleTemplateCells = templateRow.Elements<Cell>().ToDictionary(
            c => ColumnNameFromReference(c.CellReference?.Value ?? ""),
            c => c.StyleIndex?.Value,
            StringComparer.OrdinalIgnoreCase);

        RemoveDataRowsBelow(sheetData, ModuleDataStartRow);

        if (records.Count == 0)
        {
            ClearRowValues(sheetData, ModuleDataStartRow, lastCol);
            worksheetPart.Worksheet.Save();
            return;
        }

        for (var i = 0; i < records.Count; i++)
        {
            var rowIndex = (uint)(ModuleDataStartRow + i);
            var row = i == 0
                ? templateRow
                : InsertClonedDataRow(sheetData, templateRow, rowIndex);

            var data = ParseJsonDict(records[i].DataJson);
            foreach (var (fieldRef, colIndex) in columnByRef)
            {
                data.TryGetValue(fieldRef, out var val);
                var colName = ColumnNameFromIndex(colIndex);
                styleTemplateCells.TryGetValue(colName, out var styleIndex);
                SetCellSharedStringOnRow(row, sharedStrings, rowIndex, colName, FormatCellValue(val), styleIndex);
            }
        }

        worksheetPart.Worksheet.Save();
    }

    private static Dictionary<string, int> BuildColumnMap(
        SheetData sheetData,
        SharedStringTablePart sharedStrings,
        uint headerRow,
        int lastCol)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var col = 1; col <= lastCol; col++)
        {
            var colName = ColumnNameFromIndex(col);
            var header = GetCellText(sheetData, headerRow, colName, sharedStrings).Trim();
            if (string.IsNullOrEmpty(header))
                continue;

            var match = FieldRefRegex.Match(header);
            if (!match.Success)
                continue;

            map.TryAdd(match.Groups[1].Value, col);
        }

        return map;
    }

    private static void RemoveDataRowsBelow(SheetData sheetData, int startRow)
    {
        var toRemove = sheetData.Elements<Row>()
            .Where(r => r.RowIndex is not null && r.RowIndex > (uint)startRow)
            .ToList();

        foreach (var row in toRemove)
            row.Remove();
    }

    private static void ClearRowValues(SheetData sheetData, uint rowIndex, int lastCol)
    {
        var row = GetRow(sheetData, rowIndex);
        if (row is null)
            return;

        for (var col = 1; col <= lastCol; col++)
        {
            var colName = ColumnNameFromIndex(col);
            var cell = GetCell(row, colName, rowIndex);
            if (cell is null)
                continue;

            cell.CellValue = null;
            cell.DataType = null;
            cell.InlineString = null;
        }
    }

    private static Row InsertClonedDataRow(SheetData sheetData, Row templateRow, uint rowIndex)
    {
        var clone = (Row)templateRow.CloneNode(deep: true);
        clone.RowIndex = rowIndex;

        foreach (var cell in clone.Elements<Cell>())
        {
            if (cell.CellReference?.Value is not null)
            {
                var col = ColumnNameFromReference(cell.CellReference.Value);
                cell.CellReference = col + rowIndex;
            }

            cell.CellValue = null;
            cell.DataType = null;
            cell.InlineString = null;
        }

        var following = sheetData.Elements<Row>().FirstOrDefault(r => r.RowIndex is not null && r.RowIndex.Value > rowIndex);
        if (following is not null)
            sheetData.InsertBefore(clone, following);
        else
            sheetData.Append(clone);

        return clone;
    }

    private static Row? GetRow(SheetData sheetData, uint rowIndex) =>
        sheetData.Elements<Row>().FirstOrDefault(r => r.RowIndex == rowIndex);

    private static Cell? GetCell(Row row, string columnName, uint rowIndex)
    {
        var reference = columnName + rowIndex;
        return row.Elements<Cell>().FirstOrDefault(c => c.CellReference?.Value == reference);
    }

    private static string GetCellText(SheetData sheetData, uint rowIndex, string columnName, SharedStringTablePart sharedStrings)
    {
        var row = GetRow(sheetData, rowIndex);
        if (row is null)
            return "";

        var cell = GetCell(row, columnName, rowIndex);
        if (cell is null)
            return "";

        if (cell.DataType?.Value == CellValues.SharedString && cell.CellValue?.Text is not null)
        {
            if (int.TryParse(cell.CellValue.Text, out var index))
                return GetSharedString(sharedStrings, index);
        }

        if (cell.InlineString?.Text?.Text is not null)
            return cell.InlineString.Text.Text;

        return cell.CellValue?.Text ?? "";
    }

    private static void SetCellSharedStringOnRow(
        Row row,
        SharedStringTablePart sharedStrings,
        uint rowIndex,
        string columnName,
        string text,
        uint? styleIndex = null)
    {
        var reference = columnName + rowIndex;
        var cell = GetCell(row, columnName, rowIndex) ?? row.AppendChild(new Cell { CellReference = reference });

        if (styleIndex.HasValue)
            cell.StyleIndex = styleIndex;

        var index = InsertSharedStringItem(sharedStrings, text);
        cell.CellValue = new CellValue(index.ToString());
        cell.DataType = CellValues.SharedString;
        cell.InlineString = null;
    }

    private static void SetCellNumber(SheetData sheetData, uint rowIndex, string columnName, int number)
    {
        var row = GetRow(sheetData, rowIndex);
        if (row is null)
            return;

        var reference = columnName + rowIndex;
        var cell = GetCell(row, columnName, rowIndex);
        if (cell is null)
            return;

        cell.CellValue = new CellValue(number.ToString());
        cell.DataType = CellValues.Number;
        cell.InlineString = null;
    }

    private static int GetLastColumnIndex(SheetData sheetData, uint rowIndex)
    {
        var row = GetRow(sheetData, rowIndex);
        if (row is null)
            return 0;

        var max = 0;
        foreach (var cell in row.Elements<Cell>())
        {
            var col = ColumnIndexFromName(ColumnNameFromReference(cell.CellReference?.Value ?? ""));
            if (col > max)
                max = col;
        }

        return max;
    }

    private static bool TryGetWorksheet(WorkbookPart workbookPart, string sheetName, out WorksheetPart worksheetPart)
    {
        worksheetPart = null!;
        var sheet = workbookPart.Workbook.Sheets!.Elements<Sheet>()
            .FirstOrDefault(s => string.Equals(s.Name?.Value, sheetName, StringComparison.OrdinalIgnoreCase));

        if (sheet?.Id?.Value is null)
            return false;

        var part = (WorksheetPart)workbookPart.GetPartById(sheet.Id.Value);
        worksheetPart = part;
        return true;
    }

    private static string GetSharedString(SharedStringTablePart sharedStringPart, int index)
    {
        var item = sharedStringPart.SharedStringTable?.Elements<SharedStringItem>().ElementAtOrDefault(index);
        return item?.InnerText ?? "";
    }

    private static int InsertSharedStringItem(SharedStringTablePart sharedStringPart, string text)
    {
        var table = sharedStringPart.SharedStringTable;
        if (table is null)
        {
            table = new SharedStringTable();
            sharedStringPart.SharedStringTable = table;
        }
        var i = 0;
        foreach (var item in table.Elements<SharedStringItem>())
        {
            if (item.InnerText == text)
                return i;
            i++;
        }

        table.AppendChild(new SharedStringItem(new Text(text)));
        table.Count = (uint)table.ChildElements.Count;
        table.UniqueCount = table.Count;
        return i;
    }

    private static string ColumnNameFromReference(string cellReference)
    {
        return new string(cellReference.TakeWhile(char.IsLetter).ToArray());
    }

    private static int ColumnIndexFromName(string columnName)
    {
        var sum = 0;
        foreach (var ch in columnName)
            sum = sum * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
        return sum;
    }

    private static string ColumnNameFromIndex(int index)
    {
        var dividend = index;
        var columnName = string.Empty;
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }

    private static string NormalizeModuleCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "";

        code = code.Trim();
        return code.Length == 1 && char.IsDigit(code[0]) ? "0" + code : code;
    }

    private static string FormatCellValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        if (value.StartsWith('[') || value.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(value);
                return doc.RootElement.ValueKind switch
                {
                    JsonValueKind.Array => string.Join("; ", doc.RootElement.EnumerateArray()
                        .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.GetRawText())),
                    JsonValueKind.Object => doc.RootElement.GetRawText(),
                    _ => value,
                };
            }
            catch
            {
                return value;
            }
        }

        return value;
    }

    private static Dictionary<string, string> ParseJsonDict(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
