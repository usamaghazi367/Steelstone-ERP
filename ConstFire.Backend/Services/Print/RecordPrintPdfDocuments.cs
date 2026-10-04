using System.Globalization;
using ConstFire.Backend.DTOs;
using ConstFire.Backend.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ConstFire.Backend.Services.Print;

internal static class RecordPrintPdfDocuments
{
    static RecordPrintPdfDocuments()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Build(
        string moduleCode,
        ModuleDetailDto module,
        RecordDto record,
        ModuleConfig? config)
    {
        var data = record.Data;
        var labels = RecordPrintDataHelper.BuildLabelMap(module.Fields);

        return moduleCode switch
        {
            "09" => BuildQuote(module, record, config, data, labels),
            "11" => BuildInvoice(module, record, config, data, labels),
            _ => SteelstoneControlledPrintComposer.Build(module, record, config, data, labels)
        };
    }

    private static byte[] BuildQuote(
        ModuleDetailDto module,
        RecordDto record,
        ModuleConfig? config,
        Dictionary<string, string> data,
        Dictionary<string, string> labels)
    {
        var lineRefs = config is null
            ? ["3.2", "3.5", "3.4", "3.7", "3.6"]
            : ModuleConfigHelperPrintExtensions.GetSectionFieldRefsFromConfig(config, 3);
        var lineRows = RecordPrintDataHelper.GetRepeatingRows(data, 3, lineRefs);
        var displayCols = PickLineColumns(lineRefs, ["3.2", "3.5", "3.4", "3.7", "3.6"], labels);

        var docTitle = "QUOTE";
        var meta = new (string Label, string Value)[]
        {
            ("DATE", RecordPrintDataHelper.FormatDisplayValue(RecordPrintDataHelper.Val(data, "1.2"))),
            ("QUOTE #", FirstNonEmpty(data, "1.1", record.RecordCode, record.Data.GetValueOrDefault("_recordCode"))),
            ("CUSTOMER ID", RecordPrintDataHelper.Val(data, "2.2")),
            ("VALID UNTIL", RecordPrintDataHelper.FormatDisplayValue(RecordPrintDataHelper.Val(data, "1.9")))
        };

        var customerFields = RecordPrintDataHelper.FieldsInSection(module.Fields, 2).Take(8).ToList();
        var totalFields = RecordPrintDataHelper.FieldsInSection(module.Fields, 4)
            .Where(f => f.FieldName.Contains("total", StringComparison.OrdinalIgnoreCase)
                        || f.FieldName.Contains("tax", StringComparison.OrdinalIgnoreCase)
                        || f.FieldName.Contains("value", StringComparison.OrdinalIgnoreCase))
            .Take(6)
            .ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Content().Column(col =>
                {
                    col.Item().Element(c => ComposeHeader(c, docTitle, meta));
                    col.Item().PaddingTop(8).Element(c => ComposeSectionBlock(c, "CUSTOMER", customerFields, data, labels));
                    col.Item().PaddingTop(6).Element(c => ComposeLineTable(
                        c,
                        displayCols,
                        lineRows,
                        emptyRowCount: Math.Max(12 - lineRows.Count, 4)));
                    col.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem(3).Element(c => ComposeCommentsBox(c, module, data, sectionNum: 6));
                        row.RelativeItem(2).Element(c =>
                            ComposeTotalsPanel(c, totalFields, data, labels, docTitle, summarySectionTitle: "Section 4 — Quotation summary"));
                    });
                    col.Item().PaddingTop(12).Element(ComposeFooter);
                });
            });
        }).GeneratePdf();
    }

    private static byte[] BuildInvoice(
        ModuleDetailDto module,
        RecordDto record,
        ModuleConfig? config,
        Dictionary<string, string> data,
        Dictionary<string, string> labels)
    {
        var lineRefs = config is null
            ? ["4.1", "4.2", "4.3", "4.4", "4.5"]
            : ModuleConfigHelperPrintExtensions.GetSectionFieldRefsFromConfig(config, 4);
        var lineRows = RecordPrintDataHelper.GetRepeatingRows(data, 4, lineRefs);
        var invoiceLineColumns = BuildInvoiceLineColumns();

        var meta = new (string Label, string Value)[]
        {
            ("DATE", RecordPrintDataHelper.FormatDisplayValue(RecordPrintDataHelper.Val(data, "1.2"))),
            ("INVOICE #", FirstNonEmpty(data, "1.1", record.RecordCode)),
            ("CUSTOMER ID", RecordPrintDataHelper.Val(data, "2.1"))
        };

        var billFields = RecordPrintDataHelper.FieldsInSection(module.Fields, 2).Take(6).ToList();
        var shipFields = RecordPrintDataHelper.FieldsInSection(module.Fields, 3).Take(6).ToList();
        var shipBarFields = RecordPrintDataHelper.FieldsInSection(module.Fields, 3).Skip(6).Take(4).ToList();
        if (shipBarFields.Count == 0)
        {
            var fallbackRefs = new[] { "1.6", "3.1", "3.3", "1.3" };
            shipBarFields = fallbackRefs
                .Select(r => module.Fields.FirstOrDefault(f => f.Ref == r))
                .Where(f => f is not null)
                .Cast<ModuleFieldDto>()
                .ToList();
        }

        var linesSubtotal = ComputeLinesSubtotal(lineRows);
        var invoiceSummaryRows = BuildInvoiceSummaryRows(data, linesSubtotal);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Content().Column(col =>
                {
                    col.Item().Element(c => ComposeHeader(c, "INVOICE", meta));
                    col.Item().PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem().Element(c => ComposeAddressBlock(c, "BILL TO", billFields, data, labels));
                        row.ConstantItem(8);
                        row.RelativeItem().Element(c => ComposeAddressBlock(c, "SHIP TO", shipFields.Count > 0 ? shipFields : billFields, data, labels));
                    });
                    col.Item().PaddingTop(6).Element(c => ComposeInfoBar(c, shipBarFields, data, labels));
                    col.Item().PaddingTop(6).Element(c => ComposeInvoiceLineTable(
                        c,
                        invoiceLineColumns,
                        lineRows,
                        emptyRowCount: Math.Max(10 - lineRows.Count, 2),
                        linesSubtotal));
                    col.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem(3).Element(c => ComposeCommentsBox(c, module, data, sectionNum: 6));
                        row.RelativeItem(2).Element(c => ComposeInvoiceSummaryTable(c, invoiceSummaryRows, data, linesSubtotal));
                    });
                    col.Item().PaddingTop(10).Element(ComposeFooter);
                });
            });
        }).GeneratePdf();
    }

    private static byte[] BuildGenericDocument(
        ModuleDetailDto module,
        RecordDto record,
        ModuleConfig? config,
        Dictionary<string, string> data,
        Dictionary<string, string> labels)
    {
        var title = ShortDocumentTitle(module.Name);
        var headerFields = RecordPrintDataHelper.FieldsInSection(module.Fields, 1).Take(6).ToList();
        var leftFields = RecordPrintDataHelper.FieldsInSection(module.Fields, 2).Take(6).ToList();
        var rightFields = RecordPrintDataHelper.FieldsInSection(module.Fields, 3)
            .Where(f => config?.Sections.FirstOrDefault(s => s.Num == 3)?.Repeating != true)
            .Take(6)
            .ToList();

        var repeatingSection = config?.Sections.FirstOrDefault(s => s.Repeating);
        var lineRefs = repeatingSection is null
            ? []
            : ModuleConfigHelperPrintExtensions.GetSectionFieldRefsFromConfig(config!, repeatingSection.Num);
        var lineRows = repeatingSection is null
            ? []
            : RecordPrintDataHelper.GetRepeatingRows(data, repeatingSection.Num, lineRefs);

        var displayCols = lineRefs.Count == 0
            ? []
            : PickLineColumns(lineRefs, lineRefs.Take(5).ToArray(), labels);

        var infoBarFields = config?.Sections
            .Where(s => !s.Repeating && s.Num > 1 && s.Num != repeatingSection?.Num)
            .SelectMany(s => RecordPrintDataHelper.FieldsInSection(module.Fields, s.Num).Take(2))
            .Take(6)
            .ToList() ?? [];

        var lastSection = config?.Sections.Where(s => !s.Repeating).MaxBy(s => s.Num)?.Num
            ?? module.Fields.Max(f => f.Section ?? 1);
        var totalFields = RecordPrintDataHelper.FieldsInSection(module.Fields, lastSection)
            .Where(f => f.FieldName.Contains("total", StringComparison.OrdinalIgnoreCase)
                        || f.FieldName.Contains("tax", StringComparison.OrdinalIgnoreCase)
                        || f.FieldName.Contains("amount", StringComparison.OrdinalIgnoreCase)
                        || f.FieldName.Contains("payable", StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .ToList();

        var meta = BuildGenericMeta(title, headerFields, data, record);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Content().Column(col =>
                {
                    col.Item().Element(c => ComposeHeader(c, title, meta));
                    if (leftFields.Count > 0 || rightFields.Count > 0)
                    {
                        col.Item().PaddingTop(6).Row(row =>
                        {
                            row.RelativeItem().Element(c =>
                                ComposeAddressBlock(c, leftFields.Count > 0 ? "VENDOR" : "PARTY", leftFields, data, labels));
                            row.ConstantItem(8);
                            row.RelativeItem().Element(c =>
                                ComposeAddressBlock(c, "SHIP TO", rightFields.Count > 0 ? rightFields : leftFields, data, labels));
                        });
                    }

                    if (infoBarFields.Count > 0)
                        col.Item().PaddingTop(6).Element(c => ComposeInfoBar(c, infoBarFields, data, labels));

                    if (displayCols.Count > 0)
                    {
                        col.Item().PaddingTop(6).Element(c => ComposeLineTable(
                            c,
                            displayCols,
                            lineRows,
                            emptyRowCount: Math.Max(16 - lineRows.Count, 4)));
                    }

                    col.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem(3).Element(c => ComposeCommentsBox(c, module, data, sectionNum: lastSection));
                        row.RelativeItem(2).Element(c =>
                            ComposeTotalsPanel(c, totalFields, data, labels, title, summarySectionTitle: null));
                    });
                    col.Item().PaddingTop(10).Element(ComposeFooter);
                });
            });
        }).GeneratePdf();
    }

    private static (string Label, string Value)[] BuildGenericMeta(
        string title,
        List<ModuleFieldDto> headerFields,
        Dictionary<string, string> data,
        RecordDto record)
    {
        var rows = new List<(string, string)>();
        foreach (var f in headerFields.Take(4))
        {
            var label = f.FieldName.Contains('#') ? f.FieldName : f.FieldName.ToUpperInvariant();
            if (label.Length > 24)
                label = label[..24];
            rows.Add((label, RecordPrintDataHelper.FormatDisplayValue(RecordPrintDataHelper.Val(data, f.Ref))));
        }

        if (rows.Count == 0)
        {
            rows.Add(("DATE", DateTime.UtcNow.ToString("M/d/yyyy", System.Globalization.CultureInfo.InvariantCulture)));
            rows.Add((title.Contains("ORDER", StringComparison.OrdinalIgnoreCase) ? "PO #" : "REF #",
                FirstNonEmpty(data, record.RecordCode)));
        }

        return rows.ToArray();
    }

    private static string ShortDocumentTitle(string moduleName)
    {
        if (moduleName.Contains("QUOTATION", StringComparison.OrdinalIgnoreCase)) return "QUOTE";
        if (moduleName.Contains("INVOICE", StringComparison.OrdinalIgnoreCase)) return "INVOICE";
        if (moduleName.Contains("PAYMENT VOUCHER", StringComparison.OrdinalIgnoreCase)) return "PAYMENT VOUCHER";
        if (moduleName.Contains("CHALLAN", StringComparison.OrdinalIgnoreCase)) return "DELIVERY CHALLAN";
        if (moduleName.Contains("BILL", StringComparison.OrdinalIgnoreCase)) return "PURCHASE ORDER";
        if (moduleName.Length > 28)
            return moduleName[..28].Trim().ToUpperInvariant();
        return moduleName.ToUpperInvariant();
    }

    private static List<(string Header, string Ref)> PickLineColumns(
        IReadOnlyList<string> lineRefs,
        string[] preferredOrder,
        Dictionary<string, string> labels,
        string[]? fallbackHeaders = null)
    {
        var ordered = new List<string>();
        foreach (var pref in preferredOrder)
        {
            if (lineRefs.Contains(pref, StringComparer.OrdinalIgnoreCase))
                ordered.Add(pref);
        }

        foreach (var r in lineRefs)
        {
            if (!ordered.Contains(r, StringComparer.OrdinalIgnoreCase))
                ordered.Add(r);
        }

        ordered = ordered.Take(5).ToList();
        var headers = fallbackHeaders ?? ordered.Select(r => RecordPrintDataHelper.Label(labels, r)).ToArray();
        var result = new List<(string, string)>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var header = i < headers.Length ? headers[i] : RecordPrintDataHelper.Label(labels, ordered[i]);
            if (header.Length > 18)
                header = header[..18];
            result.Add((header.ToUpperInvariant(), ordered[i]));
        }

        return result;
    }

    private static void ComposeHeader(IContainer container, string docTitle, (string Label, string Value)[] meta)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(SteelstonePrintTheme.CompanyName).FontSize(20).Bold().FontColor(Colors.Red.Darken3);
                    left.Item().Text(SteelstonePrintTheme.StreetAddress);
                    left.Item().Text(SteelstonePrintTheme.CityStateZip);
                    left.Item().Text($"Phone: {SteelstonePrintTheme.Phone}");
                    left.Item().Text($"Fax: {SteelstonePrintTheme.Fax}");
                    left.Item().Text($"Website: {SteelstonePrintTheme.Website}");
                });

                row.ConstantItem(200).Column(right =>
                {
                    right.Item().AlignRight().Text(docTitle).FontSize(22).Bold().FontColor(SteelstonePrintTheme.Navy);
                    right.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(72);
                            cols.RelativeColumn();
                        });

                        foreach (var (label, value) in meta)
                        {
                            table.Cell().Border(0.5f).Background(Colors.Grey.Lighten3).Padding(3)
                                .Text(label).SemiBold().FontSize(8);
                            table.Cell().Border(0.5f).Padding(3).AlignCenter()
                                .Text(string.IsNullOrWhiteSpace(value) ? "—" : value).FontSize(8);
                        }
                    });
                });
            });
        });
    }

    private static void ComposeSectionBlock(
        IContainer container,
        string title,
        IReadOnlyList<ModuleFieldDto> fields,
        Dictionary<string, string> data,
        Dictionary<string, string> labels)
    {
        container.Column(col =>
        {
            col.Item().Background(SteelstonePrintTheme.Navy).Padding(4)
                .Text(title).FontColor(Colors.White).Bold().FontSize(9);
            col.Item().Border(0.5f).Padding(6).Column(inner =>
            {
                foreach (var field in fields)
                {
                    var val = RecordPrintDataHelper.FormatDisplayValue(RecordPrintDataHelper.Val(data, field.Ref));
                    if (string.IsNullOrWhiteSpace(val)) continue;
                    inner.Item().Text(val);
                }

                if (fields.All(f => string.IsNullOrWhiteSpace(RecordPrintDataHelper.Val(data, f.Ref))))
                    inner.Item().Text("—").FontColor(Colors.Grey.Medium);
            });
        });
    }

    private static void ComposeAddressBlock(
        IContainer container,
        string title,
        IReadOnlyList<ModuleFieldDto> fields,
        Dictionary<string, string> data,
        Dictionary<string, string> labels)
    {
        ComposeSectionBlock(container, title, fields, data, labels);
    }

    private static void ComposeInfoBar(
        IContainer container,
        IReadOnlyList<ModuleFieldDto> fields,
        Dictionary<string, string> data,
        Dictionary<string, string> labels)
    {
        if (fields.Count == 0) return;

        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                foreach (var _ in fields)
                    cols.RelativeColumn();
            });

            foreach (var field in fields)
            {
                var header = field.FieldName.Length > 16 ? field.FieldName[..16] : field.FieldName;
                table.Cell().Background(SteelstonePrintTheme.Navy).Padding(4)
                    .Text(header.ToUpperInvariant()).FontColor(Colors.White).Bold().FontSize(7);
            }

            foreach (var field in fields)
            {
                var val = RecordPrintDataHelper.FormatDisplayValue(RecordPrintDataHelper.Val(data, field.Ref));
                table.Cell().Border(0.5f).MinHeight(18).Padding(4).AlignCenter()
                    .Text(string.IsNullOrWhiteSpace(val) ? " " : val).FontSize(8);
            }
        });
    }

    private enum InvoiceLineCellKind
    {
        Text,
        Quantity,
        Money
    }

    private sealed record InvoiceLineColumn(string Header, string FieldRef, InvoiceLineCellKind Kind);

    private sealed record InvoiceSummaryRow(string Label, string? FieldRef, bool IsGrandTotal, bool IsTextOnly = false);

    private static List<InvoiceLineColumn> BuildInvoiceLineColumns() =>
    [
        new("ITEM #", "4.2", InvoiceLineCellKind.Text),
        new("DESCRIPTION", "4.3", InvoiceLineCellKind.Text),
        new("UOM", "4.4", InvoiceLineCellKind.Text),
        new("QTY", "4.5", InvoiceLineCellKind.Quantity),
        new("UNIT PRICE (PKR)", "4.6", InvoiceLineCellKind.Money),
        new("LINE TOTAL (PKR)", "4.7", InvoiceLineCellKind.Money),
    ];

    private static decimal ComputeLinesSubtotal(IReadOnlyList<Dictionary<string, string>> lineRows)
    {
        decimal sum = 0;
        foreach (var row in lineRows)
        {
            var lineTotal = RecordPrintDataHelper.Val(row, "4.7");
            if (string.IsNullOrWhiteSpace(lineTotal))
                lineTotal = RecordPrintDataHelper.Val(row, "4.8");

            if (!string.IsNullOrWhiteSpace(lineTotal))
            {
                sum += RecordPrintDataHelper.ParseDecimal(lineTotal);
                continue;
            }

            var qty = RecordPrintDataHelper.ParseDecimal(RecordPrintDataHelper.Val(row, "4.5"));
            var rate = RecordPrintDataHelper.ParseDecimal(RecordPrintDataHelper.Val(row, "4.6"));
            if (qty > 0 && rate > 0)
                sum += qty * rate;
        }

        return sum;
    }

    private static List<InvoiceSummaryRow> BuildInvoiceSummaryRows(Dictionary<string, string> data, decimal linesSubtotal)
    {
        _ = linesSubtotal; // subtotal shown via null FieldRef row
        var rows = new List<InvoiceSummaryRow>
        {
            new("Subtotal — goods (sum of line totals)", null, false),
        };

        if (!string.IsNullOrWhiteSpace(RecordPrintDataHelper.Val(data, "5.2")))
            rows.Add(new("Sales tax", "5.2", false));
        if (!string.IsNullOrWhiteSpace(RecordPrintDataHelper.Val(data, "5.3")))
            rows.Add(new("Other tax", "5.3", false));

        rows.Add(new("TOTAL INVOICE", "5.4", true));

        if (!string.IsNullOrWhiteSpace(RecordPrintDataHelper.Val(data, "5.5")))
            rows.Add(new("Amount in words", "5.5", false, IsTextOnly: true));

        return rows;
    }

    private static string FormatInvoiceLineCell(
        Dictionary<string, string> row,
        InvoiceLineColumn column)
    {
        var raw = row.TryGetValue(column.FieldRef, out var v) ? v : string.Empty;
        return column.Kind switch
        {
            InvoiceLineCellKind.Quantity => RecordPrintDataHelper.FormatQuantity(
                raw,
                RecordPrintDataHelper.Val(row, "4.4")),
            InvoiceLineCellKind.Money when column.FieldRef == "4.7" && string.IsNullOrWhiteSpace(raw) =>
                RecordPrintDataHelper.FormatMoney(
                    (RecordPrintDataHelper.ParseDecimal(RecordPrintDataHelper.Val(row, "4.5"))
                     * RecordPrintDataHelper.ParseDecimal(RecordPrintDataHelper.Val(row, "4.6"))).ToString(CultureInfo.InvariantCulture)),
            InvoiceLineCellKind.Money => RecordPrintDataHelper.FormatMoney(raw),
            _ => string.IsNullOrWhiteSpace(raw) ? " " : raw,
        };
    }

    private static void ComposeInvoiceLineTable(
        IContainer container,
        IReadOnlyList<InvoiceLineColumn> columns,
        IReadOnlyList<Dictionary<string, string>> rows,
        int emptyRowCount,
        decimal linesSubtotal)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(54);
                cols.RelativeColumn(4);
                cols.ConstantColumn(32);
                cols.ConstantColumn(52);
                cols.ConstantColumn(68);
                cols.ConstantColumn(76);
            });

            foreach (var col in columns)
            {
                table.Cell().Background(SteelstonePrintTheme.Navy).Padding(4)
                    .Text(col.Header).FontColor(Colors.White).Bold().FontSize(7);
            }

            foreach (var row in rows)
            {
                foreach (var col in columns)
                {
                    var display = FormatInvoiceLineCell(row, col);
                    var cell = table.Cell().Border(0.5f).Padding(4).MinHeight(18);
                    if (col.Kind is InvoiceLineCellKind.Money or InvoiceLineCellKind.Quantity)
                        cell.AlignRight().Text(display).FontSize(8);
                    else
                        cell.Text(display).FontSize(8);
                }
            }

            for (var i = 0; i < emptyRowCount; i++)
            {
                foreach (var col in columns)
                {
                    var filler = col.Kind is InvoiceLineCellKind.Money or InvoiceLineCellKind.Quantity ? " " : " ";
                    table.Cell().Border(0.5f).Padding(4).MinHeight(16).Text(filler).FontSize(8);
                }
            }

            if (rows.Count > 0 && linesSubtotal > 0)
            {
                table.Cell().ColumnSpan(5).Border(0.5f).Background(SteelstonePrintTheme.LightGreyHeader)
                    .Padding(4).AlignRight().Text("Subtotal (all lines)").SemiBold().FontSize(8);
                table.Cell().Border(0.5f).Background(SteelstonePrintTheme.LightGreyHeader)
                    .Padding(4).AlignRight().Text(RecordPrintDataHelper.FormatMoney(linesSubtotal.ToString(CultureInfo.InvariantCulture)))
                    .SemiBold().FontSize(8);
            }
        });
    }

    private static void ComposeInvoiceSummaryTable(
        IContainer container,
        IReadOnlyList<InvoiceSummaryRow> summaryRows,
        Dictionary<string, string> data,
        decimal linesSubtotal)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(4).Text("Invoice summary").SemiBold().FontSize(9);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.ConstantColumn(100);
                });

                foreach (var row in summaryRows)
                {
                    if (row.IsTextOnly)
                    {
                        var words = RecordPrintDataHelper.Val(data, row.FieldRef!);
                        table.Cell().ColumnSpan(2).Border(0.5f).Padding(6).MinHeight(28)
                            .Text(text =>
                            {
                                text.Span(row.Label + ": ").SemiBold().FontSize(7);
                                text.Span(words).FontSize(7);
                            });
                        continue;
                    }

                    table.Cell().Border(0.5f).Padding(6).MinHeight(22).AlignMiddle()
                        .Text(row.Label).FontSize(8);
                    var amountCell = table.Cell().Border(0.5f).Padding(6).MinHeight(22).AlignMiddle().AlignRight();
                    var display = row.FieldRef is null
                        ? RecordPrintDataHelper.FormatMoney(linesSubtotal.ToString(CultureInfo.InvariantCulture))
                        : RecordPrintDataHelper.FormatMoney(RecordPrintDataHelper.Val(data, row.FieldRef));
                    if (row.IsGrandTotal)
                    {
                        amountCell.Background(SteelstonePrintTheme.Navy)
                            .Text("PKR " + display).FontColor(Colors.White).Bold().FontSize(9);
                    }
                    else
                    {
                        amountCell.Text("PKR " + display).FontSize(8);
                    }
                }
            });
        });
    }

    private static void ComposeLineTable(
        IContainer container,
        IReadOnlyList<(string Header, string Ref)> columns,
        IReadOnlyList<Dictionary<string, string>> rows,
        int emptyRowCount)
    {
        if (columns.Count == 0) return;

        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                foreach (var (header, _) in columns)
                {
                    if (header.Contains("DESCRIPTION", StringComparison.OrdinalIgnoreCase))
                        cols.RelativeColumn(3);
                    else
                        cols.RelativeColumn();
                }
            });

            foreach (var (header, _) in columns)
            {
                table.Cell().Background(SteelstonePrintTheme.Navy).Padding(4)
                    .Text(header).FontColor(Colors.White).Bold().FontSize(8);
            }

            void DataCell(string? text, bool numeric)
            {
                var display = string.IsNullOrWhiteSpace(text) ? " " : text;
                if (numeric)
                    display = RecordPrintDataHelper.FormatMoney(display == " " ? null : display);

                var cell = table.Cell().Border(0.5f).Padding(4).MinHeight(16);
                if (numeric)
                    cell.AlignRight().Text(display).FontSize(8);
                else
                    cell.Text(display).FontSize(8);
            }

            foreach (var row in rows)
            {
                foreach (var (_, fieldRef) in columns)
                {
                    var raw = row.TryGetValue(fieldRef, out var v) ? v : string.Empty;
                    var numeric = RecordPrintDataHelper.IsMoneyFieldRef(fieldRef)
                        || RecordPrintDataHelper.IsQuantityFieldRef(fieldRef);
                    DataCell(raw, numeric);
                }
            }

            for (var i = 0; i < emptyRowCount; i++)
            {
                foreach (var (_, fieldRef) in columns)
                {
                    var numeric = fieldRef.EndsWith(".5", StringComparison.Ordinal) || fieldRef.EndsWith(".6", StringComparison.Ordinal);
                    DataCell(numeric ? "-" : " ", numeric);
                }
            }
        });
    }

    private static void ComposeCommentsBox(
        IContainer container,
        ModuleDetailDto module,
        Dictionary<string, string> data,
        int sectionNum)
    {
        var termsFields = RecordPrintDataHelper.FieldsInSection(module.Fields, sectionNum)
            .Where(f => f.FieldName.Contains("term", StringComparison.OrdinalIgnoreCase)
                        || f.FieldName.Contains("comment", StringComparison.OrdinalIgnoreCase)
                        || f.FieldName.Contains("instruction", StringComparison.OrdinalIgnoreCase))
            .Take(4)
            .ToList();

        container.Column(col =>
        {
            col.Item().Background(SteelstonePrintTheme.LightGreyHeader).Padding(4)
                .Text("Comments or Special Instructions").SemiBold().FontSize(8);
            col.Item().Border(0.5f).MinHeight(70).Padding(6).Column(inner =>
            {
                if (termsFields.Count == 0)
                {
                    inner.Item().Text("Make all checks payable to " + SteelstonePrintTheme.CompanyLegalLine).FontSize(8);
                    return;
                }

                foreach (var f in termsFields)
                {
                    var val = RecordPrintDataHelper.Val(data, f.Ref);
                    if (string.IsNullOrWhiteSpace(val)) continue;
                    inner.Item().Text($"• {val}").FontSize(8);
                }
            });
        });
    }

    private static void ComposeTotalsPanel(
        IContainer container,
        IReadOnlyList<ModuleFieldDto> totalFields,
        Dictionary<string, string> data,
        Dictionary<string, string> labels,
        string docTitle,
        string? summarySectionTitle = null)
    {
        container.Column(col =>
        {
            if (!string.IsNullOrWhiteSpace(summarySectionTitle))
            {
                col.Item().PaddingBottom(4).Text("Amount source").SemiBold().FontSize(8);
                col.Item().PaddingBottom(4).Text(summarySectionTitle).FontSize(7).FontColor(Colors.Grey.Darken2);
                col.Item().PaddingBottom(2).Text("Values below are read from fields saved on this record (same as the form wizard).")
                    .FontSize(6).Italic().FontColor(Colors.Grey.Darken1);
            }

            if (totalFields.Count == 0)
            {
                col.Item().AlignRight().Text("TOTAL").SemiBold();
                col.Item().AlignRight().Background(SteelstonePrintTheme.TotalHighlight).Padding(6)
                    .Text("PKR —").Bold();
                return;
            }

            for (var i = 0; i < totalFields.Count; i++)
            {
                var field = totalFields[i];
                var isGrandTotal = field.FieldName.Contains("total invoice", StringComparison.OrdinalIgnoreCase)
                    || field.FieldName.Contains("grand total", StringComparison.OrdinalIgnoreCase)
                    || (i == totalFields.Count - 1 && field.FieldName.Contains("total", StringComparison.OrdinalIgnoreCase));
                var label = field.FieldName.ToUpperInvariant();
                if (label.Length > 28)
                    label = label[..28];

                var val = RecordPrintDataHelper.Val(data, field.Ref);
                var display = RecordPrintDataHelper.FormatMoney(val);
                if (string.IsNullOrWhiteSpace(val))
                    display = "-";

                col.Item().Row(row =>
                {
                    row.RelativeItem().AlignRight().Padding(3).Column(labelCol =>
                    {
                        labelCol.Item().AlignRight().Text(label).SemiBold().FontSize(8);
                        labelCol.Item().AlignRight().Text($"Field {field.Ref}").FontSize(6).FontColor(Colors.Grey.Darken1);
                    });
                    var amount = row.ConstantItem(96).Border(0.5f).Padding(3).AlignRight()
                        .Background(isGrandTotal ? SteelstonePrintTheme.Navy : Colors.White);
                    var prefix = field.FieldName.Contains("PKR", StringComparison.OrdinalIgnoreCase) ? "PKR " : string.Empty;
                    var text = amount.Text(isGrandTotal ? prefix + display : display).FontSize(8)
                        .FontColor(isGrandTotal ? Colors.White : Colors.Black);
                    if (isGrandTotal)
                        text.Bold();
                });
            }
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter().Text("If you have any questions about this document, please contact Steelstone support.")
                .FontSize(8);
            col.Item().PaddingTop(4).AlignCenter().Text("Thank You For Your Business!")
                .Italic().Bold().FontSize(11);
        });
    }

    private static string FirstNonEmpty(Dictionary<string, string> data, params string?[] keys)
    {
        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            var v = RecordPrintDataHelper.Val(data, key);
            if (!string.IsNullOrWhiteSpace(v))
                return v;
        }

        return "—";
    }
}

internal static class ModuleConfigHelperPrintExtensions
{
    public static List<string> GetSectionFieldRefsFromConfig(ModuleConfig config, int sectionNum) =>
        config.Fields
            .Where(f => f.Section == sectionNum)
            .Select(f => f.Ref)
            .ToList();
}
