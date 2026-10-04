using ConstFire.Backend.DTOs;
using ConstFire.Backend.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ConstFire.Backend.Services.Print;

internal static class SteelstoneQuotationPrintComposer
{
    private const int MinQuotedLineRows = 5;

    static SteelstoneQuotationPrintComposer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Build(
        ModuleDetailDto module,
        RecordDto record,
        ModuleConfig? config,
        Dictionary<string, string> data,
        Dictionary<string, string> labels)
    {
        var meta = SteelstoneControlledPrintComposer.ResolveMeta(module, config, record);
        var lineRefs = config is null
            ? new[] { "3.2", "3.3", "3.4", "3.5", "3.6" }
            : ModuleConfigHelperPrintExtensions.GetSectionFieldRefsFromConfig(config, 3)
                .Where(r => r is not "3.1")
                .ToArray();
        var lineRows = RecordPrintDataHelper.GetRepeatingRows(data, 3, lineRefs);

        return Document.Create(doc =>
        {
            doc.Page(page => ComposeQuotationDocument(page, meta, module, data, labels, lineRows));
        }).GeneratePdf();
    }

    public static byte[] BuildPreviewSample(ModuleDetailDto module, ModuleConfig? config)
    {
        var data = SampleQuotationData();
        var labels = RecordPrintDataHelper.BuildLabelMap(module.Fields);
        var record = new RecordDto
        {
            Id = 0,
            RecordCode = "QOT-00312",
            Data = data,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        return Build(module, record, config, data, labels);
    }

    private static void ComposeQuotationDocument(
        PageDescriptor page,
        SteelstoneControlledPrintComposer.PrintMeta meta,
        ModuleDetailDto module,
        Dictionary<string, string> data,
        Dictionary<string, string> labels,
        IReadOnlyList<Dictionary<string, string>> lineRows)
    {
        page.Size(PageSizes.A4);
        page.MarginHorizontal(14);
        page.MarginTop(8);
        page.MarginBottom(10);
        page.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(SteelstonePrintTheme.FieldValueText));

        page.Header().Element(c => SteelstoneErpPrintChrome.ComposePageHeader(c, meta));
        page.Footer().Element(SteelstoneErpPrintChrome.ComposePageFooter);
        page.Foreground().Dynamic(new QuotationLastPageApprovalDynamic());

        var totalItemRows = Math.Max(lineRows.Count, MinQuotedLineRows);

        page.Content().Column(col =>
        {
            col.Spacing(1);

            col.Item().Element(c => SteelstoneErpPrintChrome.ComposeDualColumnPairBlock(
                c,
                "QUOTATION DETAILS",
                "CUSTOMER",
                BuildQuotationCustomerPairs(data, labels)));

            col.Item().Text(text =>
            {
                text.Span("Subject: ").SemiBold().FontColor(SteelstonePrintTheme.FieldValueText);
                text.Span(V(data, "__printSubject",
                    "Supply of OPC 53 Grade Cement (Bulk & Bag) for Raiwind Road Batching Plant Project"));
            });

            col.Item().Text(
                "Dear Sir, With reference to your enquiry, we are pleased to submit our quotation for supply and delivery as detailed below.");

            col.Item().Element(c => ComposeQuotedItemsSection(
                c, lineRows, data, totalItemRows, startIndex: 0, endIndexExclusive: totalItemRows,
                showSectionTitle: true, showPriceNote: true));

            col.Item().Element(c => ComposeSummaryBlock(c, data, labels));
            col.Item().Element(c => ComposeNumberedDualSection(
                c, 2, "DELIVERY, FREIGHT & LOGISTICS TERMS",
                "DELIVERY & FREIGHT", "LOGISTICS",
                RecordPrintDataHelper.FieldsInSection(module.Fields, 5).ToList(),
                data, labels));
            col.Item().Element(c => ComposeNumberedDualSection(
                c, 3, "COMMERCIAL TERMS & VALIDITY",
                "COMMERCIAL TERMS", "VALIDITY",
                RecordPrintDataHelper.FieldsInSection(module.Fields, 6).ToList(),
                data, labels,
                extraCommercialPairs(data)));
            col.Item().Element(c => ComposePaymentToSection(c, data));

            // Reserve bottom band on the last page so body text does not draw over approval boxes.
            col.Item().Height(SteelstonePrintTheme.QuotationApprovalBandHeight + 8);
        });
    }

    private static List<(string, string, string, string)> extraCommercialPairs(Dictionary<string, string> data)
    {
        var validity = FormatDate(V(data, "1.8")) + " to " + FormatDate(V(data, "1.9"));
        var currency = V(data, "1.7", "PKR");
        return
        [
            ("Quotation valid from / until", validity, "Currency", currency),
            ("Price basis note", V(data, "4.7"), "", ""),
        ];
    }

    private static List<(string LLabel, string LVal, string RLabel, string RVal)> BuildQuotationCustomerPairs(
        Dictionary<string, string> data,
        Dictionary<string, string> labels)
    {
        string L(string fieldRef) => SteelstonePrintLabelHelper.ShortLabel(RecordPrintDataHelper.Label(labels, fieldRef));
        string Vf(string fieldRef) => RecordPrintDataHelper.FormatDisplayValue(RecordPrintDataHelper.Val(data, fieldRef));

        return
        [
            (L("1.1"), Vf("1.1"), "Customer", V(data, "2.2")),
            (L("1.2"), Vf("1.2"), "NTN / CNIC", V(data, "__printCustomerNtn", "1234567-8")),
            (L("1.3"), FormatDate(V(data, "1.3")), "Address", V(data, "__printCustomerAddress",
                "Plot 14, Industrial Zone, Raiwind Road, Lahore")),
            (L("1.9"), FormatDate(V(data, "1.9")), "Attention", V(data, "2.8")),
            (L("1.4"), Vf("1.4"), "Mobile", V(data, "2.9")),
            ("Supply model", V(data, "__printSupplyModel", "Direct plant supply"), "Project / site", V(data, "2.7")),
            (L("1.6"), Vf("1.6"), "Delivery mode", V(data, "5.3")),
        ];
    }

    private static void ComposeQuotedItemsSection(
        IContainer container,
        IReadOnlyList<Dictionary<string, string>> lineRows,
        Dictionary<string, string> data,
        int totalRowCount,
        int startIndex,
        int endIndexExclusive,
        bool showSectionTitle,
        bool showPriceNote)
    {
        if (endIndexExclusive <= startIndex)
            return;

        container.Column(col =>
        {
            if (showSectionTitle)
            {
                col.Item().Background(SteelstonePrintTheme.LightGreyHeader).Padding(2)
                    .Text("1. QUOTED ITEMS & PRICING").Bold().FontSize(8);
            }

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(22);
                    c.RelativeColumn(3);
                    c.ConstantColumn(36);
                    c.ConstantColumn(48);
                    c.ConstantColumn(52);
                    c.ConstantColumn(62);
                });

                foreach (var h in new[] { "Sr.", "Material code & description", "UoM", "Qty", "Rate (PKR)", "Amount (PKR)" })
                {
                    table.Cell().Background(SteelstonePrintTheme.Navy).Padding(1).AlignMiddle()
                        .Text(h).FontColor(Colors.White).Bold().FontSize(7f);
                }

                for (var i = startIndex; i < endIndexExclusive; i++)
                {
                    var sr = i + 1;
                    var hasData = i < lineRows.Count;
                    var row = hasData ? lineRows[i] : null;

                    table.Cell().Border(0.5f).Padding(1).MinHeight(12).AlignMiddle().AlignCenter()
                        .Text(sr.ToString()).FontColor(SteelstonePrintTheme.FieldValueText).FontSize(7.5f);
                    table.Cell().Border(0.5f).Padding(1).MinHeight(12).AlignMiddle()
                        .Text(hasData ? V(row!, "3.2") : " ")
                        .FontColor(SteelstonePrintTheme.FieldValueText).FontSize(7.5f);
                    table.Cell().Border(0.5f).Padding(1).MinHeight(12).AlignMiddle().AlignCenter()
                        .Text(hasData ? V(row!, "3.3") : " ")
                        .FontColor(SteelstonePrintTheme.FieldValueText).FontSize(7.5f);
                    table.Cell().Border(0.5f).Padding(1).MinHeight(12).AlignMiddle().AlignRight()
                        .Text(hasData
                            ? RecordPrintDataHelper.FormatQuantity(V(row!, "3.4"), V(row!, "3.3"))
                            : " ")
                        .FontColor(SteelstonePrintTheme.FieldValueText).FontSize(7.5f);
                    table.Cell().Border(0.5f).Padding(1).MinHeight(12).AlignMiddle().AlignRight()
                        .Text(hasData ? RecordPrintDataHelper.FormatMoney(V(row!, "3.5")) : " ")
                        .FontColor(SteelstonePrintTheme.FieldValueText).FontSize(7.5f);
                    table.Cell().Border(0.5f).Padding(1).MinHeight(12).AlignMiddle().AlignRight()
                        .Text(hasData ? RecordPrintDataHelper.FormatMoney(V(row!, "3.6")) : " ")
                        .FontColor(SteelstonePrintTheme.FieldValueText).FontSize(7.5f);
                }
            });

            if (showPriceNote)
            {
                var note = V(data, "4.7");
                if (!string.IsNullOrWhiteSpace(note))
                {
                    col.Item().Text(text =>
                    {
                        text.Span("Note: ").SemiBold();
                        text.Span(note);
                    });
                }
            }
        });
    }

    private static void ComposeSummaryBlock(IContainer container, Dictionary<string, string> data, Dictionary<string, string> labels)
    {
        container.Column(col =>
        {
            col.Item().AlignRight().Width(220).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn();
                    c.ConstantColumn(88);
                });

                void Row(string label, string fieldRef, bool highlight = false)
                {
                    var val = fieldRef switch
                    {
                        "4.1" => RecordPrintDataHelper.FormatQuantity(V(data, fieldRef), "MT"),
                        _ => RecordPrintDataHelper.FormatMoney(V(data, fieldRef))
                    };
                    var bg = highlight ? SteelstonePrintTheme.TotalHighlight : "#FFFFFF";
                    table.Cell().Background(bg).Border(0.5f).Padding(1).Text(label).SemiBold().FontSize(7f);
                    table.Cell().Background(bg).Border(0.5f).Padding(1).AlignRight().Text(val)
                        .FontSize(7f).FontColor(SteelstonePrintTheme.FieldValueText);
                }

                Row("Total quantity quoted (MT)", "4.1");
                Row("Total value before tax (PKR)", "4.2");
                Row("Total sales tax (PKR)", "4.3");
                Row("Total other taxes (PKR)", "4.4");
                Row("Grand total (PKR)", "4.5", highlight: true);
            });

            var words = V(data, "4.6");
            if (!string.IsNullOrWhiteSpace(words))
            {
                col.Item().Text(text =>
                {
                    text.Span("Amount in words: ").SemiBold();
                    text.Span(words);
                });
            }
        });
    }

    private static void ComposeNumberedDualSection(
        IContainer container,
        int blockNum,
        string sectionTitle,
        string leftTitle,
        string rightTitle,
        IReadOnlyList<ModuleFieldDto> fields,
        Dictionary<string, string> data,
        Dictionary<string, string> labels,
        IReadOnlyList<(string, string, string, string)>? extraPairs = null)
    {
        container.Column(col =>
        {
            col.Item().Background(SteelstonePrintTheme.LightGreyHeader).Padding(2)
                .Text($"{blockNum}. {sectionTitle.ToUpperInvariant()}").Bold().FontSize(8);

            var pairs = BuildFieldPairs(fields, data, labels);
            if (extraPairs is not null)
                pairs.AddRange(extraPairs);

            if (pairs.Count == 0)
            {
                pairs.Add(("Payment terms offered", V(data, "6.1", "Net 30"), "", ""));
            }

            col.Item().Element(c => SteelstoneErpPrintChrome.ComposeDualColumnPairBlock(c, leftTitle, rightTitle, pairs));
        });
    }

    private static void ComposePaymentToSection(IContainer container, Dictionary<string, string> data)
    {
        container.Column(col =>
        {
            col.Item().Background(SteelstonePrintTheme.LightGreyHeader).Padding(2)
                .Text("4. PAYMENT TO").Bold().FontSize(8);

            var entity = V(data, "1.5", SteelstonePrintTheme.CompanyLegalLine);
            var bank = V(data, "__printBankName", "Meezan Bank Limited");
            var branch = V(data, "__printBankBranch", "Main Branch, Lahore");
            var account = V(data, "__printBankAccount", "0123456789012345");
            var iban = V(data, "__printBankIban", "PK00MEZN0001234567890123");

            col.Item().Element(c => SteelstoneErpPrintChrome.ComposeLabelValueTable(c,
            [
                ("Beneficiary", entity),
                ("Bank", bank),
                ("Branch", branch),
                ("Account title", SteelstonePrintTheme.CompanyLegalLine),
                ("Account number", account),
                ("IBAN", iban),
                ("Payment reference", V(data, "1.1", "As per quotation number")),
            ]));

            col.Item().Text(
                "Please remit payment to the account above and quote the quotation number as reference. Official tax invoice will be issued upon acceptance and delivery scheduling.");
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

    private static string V(Dictionary<string, string> data, string key, string fallback = "") =>
        string.IsNullOrWhiteSpace(RecordPrintDataHelper.Val(data, key)) ? fallback : RecordPrintDataHelper.Val(data, key);

    private static string V(Dictionary<string, string> row, string key) =>
        row.TryGetValue(key, out var v) ? v.Trim() : string.Empty;

    private static string FormatDate(string raw) =>
        string.IsNullOrWhiteSpace(raw) ? "—" : RecordPrintDataHelper.FormatDisplayValue(raw);

    private static Dictionary<string, string> SampleQuotationData()
    {
        var lines = new[]
        {
            new Dictionary<string, string>
            {
                ["3.1"] = "1",
                ["3.2"] = "MAT-CEM-OPC53-BULK - OPC 53 Grade, bulk",
                ["3.3"] = "MT",
                ["3.4"] = "10800",
                ["3.5"] = "13900",
                ["3.6"] = "150120000",
                ["3.7"] = "0.18",
                ["3.8"] = "21600000"
            },
            new Dictionary<string, string>
            {
                ["3.1"] = "2",
                ["3.2"] = "MAT-CEM-OPC53-BAG - OPC 53 Grade, 50 kg bags",
                ["3.3"] = "MT",
                ["3.4"] = "7200",
                ["3.5"] = "14800",
                ["3.6"] = "106560000",
                ["3.7"] = "0.18",
                ["3.8"] = "14400000"
            }
        };

        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["_recordCode"] = "QOT-00312",
            ["1.1"] = "SSIT-QTN-2026-00312",
            ["1.2"] = "0",
            ["1.3"] = "46248",
            ["1.4"] = "Project-based",
            ["1.5"] = "ENT-000145 - Steelstone IT (Pvt) Ltd",
            ["1.6"] = "Regional Sales Manager - A. Sheikh",
            ["1.7"] = "PKR",
            ["1.8"] = "46248",
            ["1.9"] = "46278",
            ["2.2"] = "CUST-00512 - Zameen Builders (Pvt) Ltd",
            ["2.7"] = "BULK-01 Batching Plant, Raiwind Road",
            ["2.8"] = "Bilal Nadeem / Procurement Manager",
            ["2.9"] = "+92-300-1239876",
            ["4.1"] = "18000",
            ["4.2"] = "256680000",
            ["4.3"] = "46202400",
            ["4.4"] = "36000000",
            ["4.5"] = "338882400",
            ["4.6"] = "Rupees three hundred thirty-eight million eight hundred eighty-two thousand four hundred only",
            ["4.7"] = "Rates are FOR site, inclusive of freight, exclusive of unloading at destination",
            ["5.1"] = "FOR destination",
            ["5.2"] = "Raiwind Road site - 1,180 km from plant",
            ["5.3"] = "Both",
            ["5.4"] = "Arranged by seller",
            ["5.5"] = "Standard force majeure clause applies (kiln shutdown, fuel shortage, government restrictions)",
            ["6.1"] = "Net 30 days from date of delivery",
            ["__section_3"] = System.Text.Json.JsonSerializer.Serialize(lines),
        };

        for (var i = 0; i < lines.Length; i++)
        {
            foreach (var (k, v) in lines[i])
                data[$"{k}#{i}"] = v;
        }

        return data;
    }
}
