using ConstFire.Backend.DTOs;
using ConstFire.Backend.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ConstFire.Backend.Services.Print;

internal static class SteelstoneControlledPrintComposer
{
    static SteelstoneControlledPrintComposer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    internal sealed record PrintMeta(
        string DocumentTitle,
        string Subtitle,
        string DocNo,
        string Version,
        string ApprovalNo,
        string Effective);

    public static PrintMeta ResolveMeta(ModuleDetailDto module, ModuleConfig? config, RecordDto record)
    {
        var p = config?.Print;
        var title = p?.DocumentTitle
            ?? module.Name.ToUpperInvariant();
        var subtitle = p?.Subtitle ?? module.Name;
        return new PrintMeta(
            title,
            subtitle,
            p?.DocNo ?? $"SSIT-F-{module.Code.PadLeft(2, '0')}",
            p?.Version ?? "1.0 (Rev 00)",
            p?.ApprovalNo ?? "QMS-26-000",
            p?.Effective ?? DateTime.UtcNow.ToString("dd-MMM-yyyy", System.Globalization.CultureInfo.InvariantCulture));
    }

    public static byte[] Build(
        ModuleDetailDto module,
        RecordDto record,
        ModuleConfig? config,
        Dictionary<string, string> data,
        Dictionary<string, string> labels)
    {
        var meta = ResolveMeta(module, config, record);
        var sections = (config?.Sections ?? [])
            .OrderBy(s => s.Num)
            .ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(24);
                page.MarginVertical(22);
                page.DefaultTextStyle(x => x.FontSize(8.5f));

                page.Header().Element(c => ComposePageHeader(c, meta));
                page.Footer().Element(ComposePageFooter);

                page.Content().PaddingTop(6).Column(col =>
                {
                    var sectionIndex = 0;
                    foreach (var section in sections)
                    {
                        if (section.Repeating)
                        {
                            sectionIndex++;
                            var refs = config is null
                                ? []
                                : ModuleConfigHelperPrintExtensions.GetSectionFieldRefsFromConfig(config, section.Num);
                            var rows = RecordPrintDataHelper.GetRepeatingRows(data, section.Num, refs);
                            var cols = BuildTableColumns(refs, labels, maxCols: 6);
                            col.Item().PaddingTop(sectionIndex == 1 ? 4 : 10)
                                .Element(c => ComposeNumberedSection(
                                    c,
                                    $"{sectionIndex}. {CleanSectionTitle(section.Title)}",
                                    cols,
                                    rows));
                            continue;
                        }

                        var fields = RecordPrintDataHelper.FieldsInSection(module.Fields, section.Num).ToList();
                        if (fields.Count == 0)
                            continue;

                        if (section.Num == 1)
                        {
                            col.Item().PaddingTop(4).Element(c =>
                                ComposeDualColumnDetailBlock(c, section.Title, fields, data, labels, record));
                            continue;
                        }

                        sectionIndex++;
                        col.Item().PaddingTop(10).Element(c =>
                            ComposeNumberedStaticSection(c, $"{sectionIndex}. {CleanSectionTitle(section.Title)}", fields, data, labels));
                    }

                    col.Item().PaddingTop(14).Element(ComposeSignatureBlock);
                });
            });
        }).GeneratePdf();
    }

    public static byte[] BuildPreviewSampleModule02(ModuleDetailDto module, ModuleConfig? config)
    {
        var data = SampleModule02Data();
        var labels = RecordPrintDataHelper.BuildLabelMap(module.Fields);
        var record = new RecordDto
        {
            Id = 0,
            RecordCode = "MFR-00021",
            Data = data,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        return Build(module, record, config, data, labels);
    }

    private static Dictionary<string, string> SampleModule02Data()
    {
        var contacts = new[]
        {
            new Dictionary<string, string> { ["3.1"] = "Kamran Baig", ["3.2"] = "General Manager Sales", ["3.3"] = "+92-333-1122334", ["3.4"] = "kamran.baig@pakcement.com", ["3.5"] = "Primary commercial contact" },
            new Dictionary<string, string> { ["3.1"] = "Sana Malik", ["3.2"] = "Dispatch Coordinator", ["3.3"] = "+92-300-4455667", ["3.4"] = "dispatch@pakcement.com", ["3.5"] = "Plant dispatch window" },
            new Dictionary<string, string> { ["3.1"] = "Engr. Tariq Mahmood", ["3.2"] = "Plant Manager", ["3.3"] = "+92-300-8877665", ["3.4"] = "tariq.m@pakcement.com", ["3.5"] = "Site escalation" },
            new Dictionary<string, string> { ["3.1"] = "Accounts Team", ["3.2"] = "Finance / Billing", ["3.3"] = "+92-25-4670011", ["3.4"] = "accounts@pakcement.com", ["3.5"] = "Invoice & WHT queries" },
        };

        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["_recordCode"] = "MFR-00021",
            ["1.1"] = "Pak Cement Industries Limited",
            ["1.2"] = "Company",
            ["1.3"] = "0098765-4",
            ["1.4"] = "Filer",
            ["__section_2"] = System.Text.Json.JsonSerializer.Serialize(new[]
            {
                new Dictionary<string, string>
                {
                    ["2.1"] = "1", ["2.2"] = "Nooriabad Cement Plant", ["2.3"] = "PLANT-01",
                    ["2.4"] = "Deh Kohistan, Main Super Highway, Nooriabad, Jamshoro, Sindh", ["2.5"] = "Nooriabad / Sindh"
                }
            }),
            ["2.1#0"] = "1",
            ["2.2#0"] = "Nooriabad Cement Plant",
            ["2.3#0"] = "PLANT-01",
            ["2.4#0"] = "Deh Kohistan, Main Super Highway, Nooriabad, Jamshoro, Sindh",
            ["2.5#0"] = "Nooriabad / Sindh",
            ["__section_3"] = System.Text.Json.JsonSerializer.Serialize(contacts),
        };

        for (var i = 0; i < contacts.Length; i++)
        {
            foreach (var (k, v) in contacts[i])
                data[$"{k}#{i}"] = v;
        }

        return data;
    }

    private static string CleanSectionTitle(string title)
    {
        var t = title;
        var idx = t.IndexOf("(repeating", StringComparison.OrdinalIgnoreCase);
        if (idx > 0) t = t[..idx].Trim();
        idx = t.IndexOf("  (", StringComparison.Ordinal);
        if (idx > 0) t = t[..idx].Trim();
        return t.Trim().TrimEnd('.');
    }

    private static List<(string Header, string Ref)> BuildTableColumns(
        IReadOnlyList<string> refs,
        Dictionary<string, string> labels,
        int maxCols)
    {
        var result = new List<(string, string)> { ("SR.", "__sr__") };
        foreach (var r in refs.Take(maxCols - 1))
        {
            var h = RecordPrintDataHelper.Label(labels, r);
            if (h.Length > 22) h = h[..22];
            result.Add((h.ToUpperInvariant(), r));
        }

        return result;
    }

    private static void ComposePageHeader(IContainer container, PrintMeta meta)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter().Text(SteelstonePrintTheme.CompanyLegalName).Bold().FontSize(11);
            col.Item().AlignCenter().Text(meta.DocumentTitle).Bold().FontSize(14).FontColor(SteelstonePrintTheme.Navy);
            col.Item().AlignCenter().Text(meta.Subtitle).FontSize(9).FontColor(Colors.Grey.Darken2);

            col.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(58);
                    c.RelativeColumn();
                    c.ConstantColumn(52);
                    c.RelativeColumn();
                    c.ConstantColumn(36);
                    c.RelativeColumn();
                });

                table.Cell().Border(0.5f).Background(SteelstonePrintTheme.LightGreyHeader).Padding(3)
                    .Text("Doc No.").SemiBold().FontSize(7);
                table.Cell().Border(0.5f).Padding(3).Text(meta.DocNo).FontSize(7);
                table.Cell().Border(0.5f).Background(SteelstonePrintTheme.LightGreyHeader).Padding(3)
                    .Text("Version").SemiBold().FontSize(7);
                table.Cell().Border(0.5f).Padding(3).Text(meta.Version).FontSize(7);
                table.Cell().Border(0.5f).Background(SteelstonePrintTheme.LightGreyHeader).Padding(3)
                    .Text("Page").SemiBold().FontSize(7);
                table.Cell().Border(0.5f).Padding(3).AlignCenter().Text(text =>
                {
                    text.CurrentPageNumber().FontSize(7);
                    text.Span(" of ").FontSize(7);
                    text.TotalPages().FontSize(7);
                });

                table.Cell().Border(0.5f).Background(SteelstonePrintTheme.LightGreyHeader).Padding(3)
                    .Text("Approval No.").SemiBold().FontSize(7);
                table.Cell().Border(0.5f).Padding(3).Text(meta.ApprovalNo).FontSize(7);
                table.Cell().Border(0.5f).Background(SteelstonePrintTheme.LightGreyHeader).Padding(3)
                    .Text("Effective").SemiBold().FontSize(7);
                table.Cell().ColumnSpan(3).Border(0.5f).Padding(3).Text(meta.Effective).FontSize(7);
            });
        });
    }

    private static void ComposePageFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
            col.Item().PaddingTop(3).AlignCenter().Text(SteelstonePrintTheme.FooterAddressLine).FontSize(6.5f);
            col.Item().AlignCenter().Text(SteelstonePrintTheme.FooterTaxLine).FontSize(6.5f);
            col.Item().AlignCenter().Text(SteelstonePrintTheme.FooterControlledLine).FontSize(6.5f).Italic();
        });
    }

    private static void ComposeDualColumnDetailBlock(
        IContainer container,
        string sectionTitle,
        IReadOnlyList<ModuleFieldDto> fields,
        Dictionary<string, string> data,
        Dictionary<string, string> labels,
        RecordDto record)
    {
        var leftTitle = CleanSectionTitle(sectionTitle).ToUpperInvariant();
        var rightTitle = "RECORD";
        var mid = (fields.Count + 1) / 2;
        var left = fields.Take(mid).ToList();
        var right = fields.Skip(mid).ToList();

        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn();
                c.RelativeColumn();
            });

            table.Cell().Background(SteelstonePrintTheme.Navy).Padding(4)
                .Text(leftTitle).FontColor(Colors.White).Bold().FontSize(8);
            table.Cell().Background(SteelstonePrintTheme.Navy).Padding(4)
                .Text(rightTitle).FontColor(Colors.White).Bold().FontSize(8);

            var maxRows = Math.Max(left.Count, right.Count + 1);
            for (var i = 0; i < maxRows; i++)
            {
                ComposeKeyValueCell(table, i < left.Count ? left[i] : null, data, labels, record);
                if (i == 0)
                {
                    ComposeRecordKeyValueCell(
                        table,
                        "Record ID",
                        record.RecordCode ?? RecordPrintDataHelper.Val(data, "_recordCode"));
                }
                else
                {
                    ComposeKeyValueCell(table, i - 1 < right.Count ? right[i - 1] : null, data, labels, record);
                }
            }
        });
    }

    private static void ComposeRecordKeyValueCell(TableDescriptor table, string label, string? value)
    {
        table.Cell().Border(0.5f).Padding(4).Text(text =>
        {
            text.Span(label).SemiBold().FontSize(7);
            text.Span("  ").FontSize(7);
            text.Span(string.IsNullOrWhiteSpace(value) ? "—" : value).FontSize(8);
        });
    }

    private static void ComposeKeyValueCell(
        TableDescriptor table,
        ModuleFieldDto? field,
        Dictionary<string, string> data,
        Dictionary<string, string> labels,
        RecordDto record)
    {
        if (field is null)
        {
            table.Cell().Border(0.5f).Padding(4).Text(" ");
            return;
        }

        var val = RecordPrintDataHelper.FormatDisplayValue(RecordPrintDataHelper.Val(data, field.Ref));
        var label = RecordPrintDataHelper.Label(labels, field.Ref);
        ComposeRecordKeyValueCell(table, label, val);
    }

    private static void ComposeNumberedStaticSection(
        IContainer container,
        string title,
        IReadOnlyList<ModuleFieldDto> fields,
        Dictionary<string, string> data,
        Dictionary<string, string> labels)
    {
        container.Column(col =>
        {
            col.Item().Background(SteelstonePrintTheme.LightGreyHeader).Padding(4).Text(title).Bold().FontSize(9);
            col.Item().Border(0.5f).Padding(6).Column(inner =>
            {
                foreach (var field in fields)
                {
                    var val = RecordPrintDataHelper.FormatDisplayValue(RecordPrintDataHelper.Val(data, field.Ref));
                    if (string.IsNullOrWhiteSpace(val)) continue;
                    inner.Item().PaddingBottom(2).Text(text =>
                    {
                        text.Span(RecordPrintDataHelper.Label(labels, field.Ref) + ": ").SemiBold().FontSize(8);
                        text.Span(val).FontSize(8);
                    });
                }
            });
        });
    }

    private static void ComposeNumberedSection(
        IContainer container,
        string title,
        IReadOnlyList<(string Header, string Ref)> columns,
        IReadOnlyList<Dictionary<string, string>> rows)
    {
        container.Column(col =>
        {
            col.Item().Background(SteelstonePrintTheme.LightGreyHeader).Padding(4).Text(title).Bold().FontSize(9);
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    foreach (var (header, _) in columns)
                    {
                        if (header is "SR.")
                            cols.ConstantColumn(28);
                        else
                            cols.RelativeColumn();
                    }
                });

                foreach (var (header, _) in columns)
                {
                    table.Cell().Background(SteelstonePrintTheme.Navy).Padding(3)
                        .Text(header).FontColor(Colors.White).Bold().FontSize(7);
                }

                if (rows.Count == 0)
                {
                    table.Cell().ColumnSpan((uint)columns.Count).Border(0.5f).Padding(8)
                        .Text("No entries saved in this section.").FontColor(Colors.Grey.Darken1).Italic();
                    return;
                }

                for (var i = 0; i < rows.Count; i++)
                {
                    foreach (var (_, fieldRef) in columns)
                    {
                        var display = fieldRef == "__sr__"
                            ? (i + 1).ToString()
                            : RecordPrintDataHelper.FormatDisplayValue(
                                rows[i].TryGetValue(fieldRef, out var v) ? v : string.Empty);
                        if (string.IsNullOrWhiteSpace(display)) display = "—";
                        table.Cell().Border(0.5f).Padding(3).Text(display).FontSize(7.5f);
                    }
                }
            });
        });
    }

    private static void ComposeSignatureBlock(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("Prepared by").SemiBold().FontSize(8);
                c.Item().PaddingTop(16).Text("____________________________").FontSize(8);
                c.Item().Text("Name / Designation").FontSize(7);
                c.Item().Text("Date").FontSize(7);
            });
            row.ConstantItem(12);
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("Approved by").SemiBold().FontSize(8);
                c.Item().PaddingTop(16).Text("____________________________").FontSize(8);
                c.Item().Text("Name / Designation").FontSize(7);
                c.Item().Text("Date").FontSize(7);
            });
            row.ConstantItem(12);
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("Party acceptance").SemiBold().FontSize(8);
                c.Item().PaddingTop(16).Text("____________________________").FontSize(8);
                c.Item().Text("Name, signature & stamp").FontSize(7);
                c.Item().Text("Date").FontSize(7);
            });
        });
    }
}
