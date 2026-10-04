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
        var title = p?.DocumentTitle ?? module.Name.ToUpperInvariant();
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
        var sections = (config?.Sections ?? []).OrderBy(s => s.Num).ToList();
        var blockNum = 0;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(22);
                page.MarginVertical(18);
                page.DefaultTextStyle(x => x.FontSize(8f));

                page.Header().Element(c => ComposePageHeader(c, meta));
                page.Footer().Element(ComposePageFooter);

                page.Content().PaddingTop(4).Column(col =>
                {
                    foreach (var section in sections)
                    {
                        if (section.Num == 1)
                        {
                            col.Item().PaddingTop(2).Element(c =>
                                ComposeModule02StyleHeaderBlock(c, module, record, data, labels, section.Title));
                            continue;
                        }

                        if (section.Repeating)
                        {
                            blockNum++;
                            var refs = config is null
                                ? []
                                : ModuleConfigHelperPrintExtensions.GetSectionFieldRefsFromConfig(config, section.Num);
                            var rows = RecordPrintDataHelper.GetRepeatingRows(data, section.Num, refs);
                            var cols = BuildTableColumns(refs, labels);
                            col.Item().PaddingTop(8).Element(c =>
                                ComposeNumberedDataTable(c, blockNum, CleanSectionTitle(section.Title), cols, rows));
                            continue;
                        }

                        var fields = RecordPrintDataHelper.FieldsInSection(module.Fields, section.Num).ToList();
                        if (fields.Count == 0)
                            continue;

                        blockNum++;
                        var (leftTitle, rightTitle) = SplitSectionTitles(section.Title);
                        col.Item().PaddingTop(8).Element(c =>
                            ComposeNumberedDualFieldSection(
                                c,
                                blockNum,
                                CleanSectionTitle(section.Title),
                                leftTitle,
                                rightTitle,
                                fields,
                                data,
                                labels));
                    }

                    col.Item().PaddingTop(12).Element(ComposeSignatureBlock);
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
            new Dictionary<string, string> { ["3.1"] = "Kamran Baig", ["3.2"] = "General Manager Sales", ["3.3"] = "+92-333-1122334" },
            new Dictionary<string, string> { ["3.1"] = "Sana Malik", ["3.2"] = "Dispatch Coordinator", ["3.3"] = "+92-300-4455667" },
            new Dictionary<string, string> { ["3.1"] = "Engr. Tariq Mahmood", ["3.2"] = "Plant Manager", ["3.3"] = "+92-300-8877665" },
            new Dictionary<string, string> { ["3.1"] = "Accounts Team", ["3.2"] = "Finance / Billing", ["3.3"] = "+92-25-4670011" },
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
                    ["2.4"] = "Deh Kohistan, Main Super Highway, Nooriabad, Jamshoro, Sindh",
                    ["2.5"] = "Nooriabad / Sindh", ["2.7"] = "Engr. Tariq Mahmood / Plant Manager",
                    ["2.8"] = "+92-300-8877665", ["2.13"] = "Operational"
                }
            }),
            ["2.1#0"] = "1",
            ["2.2#0"] = "Nooriabad Cement Plant",
            ["2.3#0"] = "PLANT-01",
            ["2.4#0"] = "Deh Kohistan, Main Super Highway, Nooriabad, Jamshoro, Sindh",
            ["2.5#0"] = "Nooriabad / Sindh",
            ["2.7#0"] = "Engr. Tariq Mahmood / Plant Manager",
            ["2.8#0"] = "+92-300-8877665",
            ["2.13#0"] = "Operational",
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

    private static (string Left, string Right) SplitSectionTitles(string sectionTitle)
    {
        var clean = CleanSectionTitle(sectionTitle).ToUpperInvariant();
        if (clean.Contains("WORKING CALENDAR", StringComparison.OrdinalIgnoreCase))
            return ("WORKING CALENDAR", "SHIFTS & DISPATCH");
        if (clean.Contains("WITHHOLDING", StringComparison.OrdinalIgnoreCase))
            return ("WITHHOLDING TAX", "EXEMPTION DETAILS");
        var mid = clean.Length / 2;
        var split = clean.LastIndexOf(' ', mid);
        if (split <= 0) split = mid;
        return (clean[..split].Trim(), clean[split..].Trim());
    }

    private static List<(string Header, string Ref)> BuildTableColumns(
        IReadOnlyList<string> refs,
        Dictionary<string, string> labels)
    {
        var result = new List<(string, string)> { ("Sr.", "__sr__") };
        foreach (var r in refs)
        {
            var h = SteelstonePrintLabelHelper.ShortLabel(RecordPrintDataHelper.Label(labels, r));
            if (h.Length > 28) h = h[..28];
            result.Add((h, r));
        }

        return result;
    }

    private static void ComposePageHeader(IContainer container, PrintMeta meta)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(78);
                c.RelativeColumn(5);
                c.ConstantColumn(132);
            });

            var logo = SteelstonePrintAssets.TryLoadLogoBytes();
            var logoCell = table.Cell().Border(0.75f).BorderColor(SteelstonePrintTheme.HeaderBorder)
                .Padding(6).AlignMiddle().AlignCenter().MinHeight(78);
            if (logo is not null)
                logoCell.Image(logo).FitArea();
            else
                logoCell.Text("LOGO").FontSize(6).FontColor(Colors.Grey.Medium);

            table.Cell().Border(0.75f).BorderColor(SteelstonePrintTheme.HeaderBorder).PaddingVertical(8).PaddingHorizontal(6)
                .Column(center =>
                {
                    center.Item().AlignCenter().Text(SteelstonePrintTheme.CompanyLegalName)
                        .FontSize(9.5f).FontColor(SteelstonePrintTheme.HeaderCompanyGrey);
                    center.Item().PaddingTop(2).AlignCenter().Text(meta.DocumentTitle)
                        .Bold().FontSize(14f).FontColor(SteelstonePrintTheme.Navy);
                    center.Item().PaddingTop(2).AlignCenter().Text(meta.Subtitle)
                        .FontSize(8f).FontColor(SteelstonePrintTheme.HeaderLabelGrey);
                });

            table.Cell().Border(0.75f).BorderColor(SteelstonePrintTheme.HeaderBorder).PaddingVertical(6).PaddingHorizontal(8)
                .Column(right =>
                {
                    right.Item().Element(c => ComposeHeaderMetaLine(c, "Doc No.:", meta.DocNo));
                    right.Item().Element(c => ComposeHeaderMetaLine(c, "Version:", meta.Version));
                    right.Item().Element(c => ComposeHeaderMetaLine(c, "Approval No.:", meta.ApprovalNo));
                    right.Item().Element(c => ComposeHeaderMetaLine(c, "Effective:", meta.Effective));
                    right.Item().Element(c => ComposeHeaderMetaPageLine(c));
                });
        });
    }

    private static void ComposeHeaderMetaLine(IContainer container, string label, string value)
    {
        container.PaddingBottom(1).Text(text =>
        {
            text.Span(label + " ").FontSize(7.5f).FontColor(SteelstonePrintTheme.HeaderLabelGrey);
            text.Span(string.IsNullOrWhiteSpace(value) ? "—" : value).Bold().FontSize(7.5f).FontColor(SteelstonePrintTheme.Navy);
        });
    }

    private static void ComposeHeaderMetaPageLine(IContainer container)
    {
        container.Text(text =>
        {
            text.Span("Page: ").FontSize(7.5f).FontColor(SteelstonePrintTheme.HeaderLabelGrey);
            text.CurrentPageNumber().Bold().FontSize(7.5f).FontColor(SteelstonePrintTheme.Navy);
            text.Span(" of ").FontSize(7.5f).FontColor(SteelstonePrintTheme.HeaderLabelGrey);
            text.TotalPages().Bold().FontSize(7.5f).FontColor(SteelstonePrintTheme.Navy);
        });
    }

    private static void ComposePageFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
            col.Item().PaddingTop(2).AlignCenter().Text(SteelstonePrintTheme.FooterAddressLine).FontSize(6.5f);
            col.Item().AlignCenter().Text(SteelstonePrintTheme.FooterTaxLine).FontSize(6.5f);
            col.Item().AlignCenter().Text(SteelstonePrintTheme.FooterControlledLine).FontSize(6.5f).Italic();
        });
    }

    private static void ComposeModule02StyleHeaderBlock(
        IContainer container,
        ModuleDetailDto module,
        RecordDto record,
        Dictionary<string, string> data,
        Dictionary<string, string> labels,
        string sectionTitle)
    {
        var business = RecordPrintDataHelper.Val(data, "1.1");
        var code = record.RecordCode ?? RecordPrintDataHelper.Val(data, "_recordCode");
        var manufacturerDisplay = string.IsNullOrWhiteSpace(code) ? business : $"{code} - {business}";

        var pairs = new List<(string LLabel, string LVal, string RLabel, string RVal)>
        {
            ("Business name", business, "Manufacturer", manufacturerDisplay),
            ("Legal status", RecordPrintDataHelper.Val(data, "1.2"), "NTN / CNIC", RecordPrintDataHelper.Val(data, "1.3")),
            ("Filer status", RecordPrintDataHelper.Val(data, "1.4"), "", ""),
        };

        ComposeDualColumnPairBlock(
            container,
            "REGISTRATION DETAILS",
            "MANUFACTURER",
            pairs);
    }

    private static void ComposeNumberedDualFieldSection(
        IContainer container,
        int blockNum,
        string sectionTitle,
        string leftTitle,
        string rightTitle,
        IReadOnlyList<ModuleFieldDto> fields,
        Dictionary<string, string> data,
        Dictionary<string, string> labels)
    {
        container.Column(col =>
        {
            col.Item().Background(SteelstonePrintTheme.LightGreyHeader).Padding(4)
                .Text($"{blockNum}. {sectionTitle.ToUpperInvariant()}").Bold().FontSize(9);

            var pairs = BuildFieldPairs(fields, data, labels);
            col.Item().Element(c => ComposeDualColumnPairBlock(c, leftTitle, rightTitle, pairs));
        });
    }

    private static List<(string LLabel, string LVal, string RLabel, string RVal)> BuildFieldPairs(
        IReadOnlyList<ModuleFieldDto> fields,
        Dictionary<string, string> data,
        Dictionary<string, string> labels)
    {
        var pairs = new List<(string, string, string, string)>();
        for (var i = 0; i < fields.Count; i += 2)
        {
            var left = fields[i];
            var lLabel = SteelstonePrintLabelHelper.ShortLabel(RecordPrintDataHelper.Label(labels, left.Ref));
            var lVal = RecordPrintDataHelper.FormatDisplayValue(RecordPrintDataHelper.Val(data, left.Ref));

            if (i + 1 >= fields.Count)
            {
                pairs.Add((lLabel, lVal, "", ""));
                continue;
            }

            var right = fields[i + 1];
            var rLabel = SteelstonePrintLabelHelper.ShortLabel(RecordPrintDataHelper.Label(labels, right.Ref));
            var rVal = RecordPrintDataHelper.FormatDisplayValue(RecordPrintDataHelper.Val(data, right.Ref));
            pairs.Add((lLabel, lVal, rLabel, rVal));
        }

        return pairs;
    }

    private static void ComposeDualColumnPairBlock(
        IContainer container,
        string leftTitle,
        string rightTitle,
        IReadOnlyList<(string LLabel, string LVal, string RLabel, string RVal)> pairs)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn();
                c.RelativeColumn();
            });

            table.Cell().Background(SteelstonePrintTheme.LightGreyHeader).Border(0.5f).Padding(4)
                .Text(leftTitle.ToUpperInvariant()).Bold().FontSize(8);
            table.Cell().Background(SteelstonePrintTheme.LightGreyHeader).Border(0.5f).Padding(4)
                .Text(rightTitle.ToUpperInvariant()).Bold().FontSize(8);

            foreach (var (lLabel, lVal, rLabel, rVal) in pairs)
            {
                ComposePairCell(table, lLabel, lVal);
                ComposePairCell(table, rLabel, rVal);
            }
        });
    }

    private static void ComposePairCell(TableDescriptor table, string label, string value)
    {
        table.Cell().Border(0.5f).Padding(4).MinHeight(16).Text(text =>
        {
            if (string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(value))
            {
                text.Span(" ").FontSize(8);
                return;
            }

            text.Span(string.IsNullOrWhiteSpace(label) ? " " : label).FontSize(8);
            text.Span(" ").FontSize(8);
            text.Span(string.IsNullOrWhiteSpace(value) ? "—" : value).FontSize(8);
        });
    }

    private static void ComposeNumberedDataTable(
        IContainer container,
        int blockNum,
        string sectionTitle,
        IReadOnlyList<(string Header, string Ref)> columns,
        IReadOnlyList<Dictionary<string, string>> rows)
    {
        container.Column(col =>
        {
            col.Item().Background(SteelstonePrintTheme.LightGreyHeader).Padding(4)
                .Text($"{blockNum}. {sectionTitle.ToUpperInvariant()}").Bold().FontSize(9);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    foreach (var (header, _) in columns)
                    {
                        if (header.Equals("Sr.", StringComparison.OrdinalIgnoreCase))
                            cols.ConstantColumn(24);
                        else
                            cols.RelativeColumn();
                    }
                });

                foreach (var (header, _) in columns)
                {
                    table.Cell().Background(SteelstonePrintTheme.Navy).Border(0.5f).Padding(3)
                        .Text(header.ToUpperInvariant()).FontColor(Colors.White).Bold().FontSize(7);
                }

                if (rows.Count == 0)
                {
                    table.Cell().ColumnSpan((uint)columns.Count).Border(0.5f).Padding(8)
                        .Text("No entries saved in this section.").FontColor(Colors.Grey.Darken1).Italic().FontSize(8);
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
                        table.Cell().Border(0.5f).Padding(3).MinHeight(14).Text(display).FontSize(7.5f);
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
                c.Item().PaddingTop(14).Text("____________________________").FontSize(8);
                c.Item().Text("Name / Designation").FontSize(7);
                c.Item().Text("Date").FontSize(7);
            });
            row.ConstantItem(10);
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("Approved by").SemiBold().FontSize(8);
                c.Item().PaddingTop(14).Text("____________________________").FontSize(8);
                c.Item().Text("Name / Designation").FontSize(7);
                c.Item().Text("Date").FontSize(7);
            });
            row.ConstantItem(10);
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("Accepted by supplier").SemiBold().FontSize(8);
                c.Item().PaddingTop(14).Text("____________________________").FontSize(8);
                c.Item().Text("Name, signature & stamp").FontSize(7);
                c.Item().Text("Date").FontSize(7);
            });
        });
    }
}
