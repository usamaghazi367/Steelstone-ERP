using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ConstFire.Backend.Services.Print;

internal static class SteelstoneErpPrintChrome
{
    internal static void ComposePageHeader(IContainer container, SteelstoneControlledPrintComposer.PrintMeta meta)
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
                .Padding(3).AlignMiddle().AlignCenter().MinHeight(52);
            if (logo is not null)
                logoCell.Image(logo).FitArea();
            else
                logoCell.Text("LOGO").FontSize(6).FontColor(Colors.Grey.Medium);

            table.Cell().Border(0.75f).BorderColor(SteelstonePrintTheme.HeaderBorder).PaddingVertical(2).PaddingHorizontal(4)
                .Column(center =>
                {
                    center.Item().AlignCenter().Text(SteelstonePrintTheme.CompanyLegalName)
                        .FontSize(8.5f).FontColor(SteelstonePrintTheme.HeaderCompanyGrey);
                    center.Item().AlignCenter().Text(meta.DocumentTitle)
                        .Bold().FontSize(12f).FontColor(SteelstonePrintTheme.Navy);
                    center.Item().AlignCenter().Text(meta.Subtitle)
                        .FontSize(7f).FontColor(SteelstonePrintTheme.HeaderLabelGrey);
                });

            table.Cell().Border(0.75f).BorderColor(SteelstonePrintTheme.HeaderBorder).PaddingVertical(2).PaddingHorizontal(5)
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

    internal static void ComposePageFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
            col.Item().AlignCenter().Text(SteelstonePrintTheme.FooterAddressLine).FontSize(6f);
            col.Item().AlignCenter().Text(SteelstonePrintTheme.FooterTaxLine).FontSize(6.5f);
            col.Item().AlignCenter().Text(SteelstonePrintTheme.FooterControlledLine).FontSize(6.5f).Italic();
        });
    }

    internal static void ComposeDualColumnPairBlock(
        IContainer container,
        string leftTitle,
        string rightTitle,
        IReadOnlyList<(string LLabel, string LVal, string RLabel, string RVal)> pairs)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(92);
                c.RelativeColumn(2);
                c.ConstantColumn(92);
                c.RelativeColumn(2);
            });

            table.Cell().ColumnSpan(2).Background(SteelstonePrintTheme.LightGreyHeader).Border(0.5f).Padding(2)
                .Text(leftTitle.ToUpperInvariant()).Bold().FontSize(7.5f);
            table.Cell().ColumnSpan(2).Background(SteelstonePrintTheme.LightGreyHeader).Border(0.5f).Padding(2)
                .Text(rightTitle.ToUpperInvariant()).Bold().FontSize(7.5f);

            foreach (var (lLabel, lVal, rLabel, rVal) in pairs)
            {
                ComposeLabelValueCells(table, lLabel, lVal);
                ComposeLabelValueCells(table, rLabel, rVal);
            }
        });
    }

    internal static void ComposeLabelValueTable(
        IContainer container,
        IReadOnlyList<(string Label, string Value)> rows)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(110);
                c.RelativeColumn();
            });

            foreach (var (label, value) in rows)
                ComposeLabelValueCells(table, label, value);
        });
    }

    internal static void ComposeSignatureBlockQuotation(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Element(c => ComposeSignatureBox(c, "Prepared by", "Name / Designation", "Date"));
            row.ConstantItem(4);
            row.RelativeItem().Element(c => ComposeSignatureBox(c, "Approved by", "Name / Designation", "Date"));
            row.ConstantItem(4);
            row.RelativeItem().Element(c =>
                ComposeSignatureBox(c, "Customer acceptance", "Name, signature & stamp", "Date"));
        });
    }

    private static void ComposeSignatureBox(IContainer container, string title, string nameHint, string dateHint)
    {
        container.Border(0.75f).BorderColor(SteelstonePrintTheme.HeaderBorder).Column(col =>
        {
            col.Item().Background(SteelstonePrintTheme.LightGreyHeader).BorderBottom(0.5f)
                .BorderColor(SteelstonePrintTheme.HeaderBorder).Padding(1)
                .AlignCenter().Text(title).SemiBold().FontSize(7f)
                .FontColor(SteelstonePrintTheme.FieldLabelText);
            col.Item().MinHeight(14).Padding(1);
            col.Item().PaddingHorizontal(3).AlignCenter()
                .Text("__________________________").FontSize(6.5f);
            col.Item().PaddingHorizontal(3).Text(nameHint).FontSize(6f)
                .FontColor(SteelstonePrintTheme.FieldValueText);
            col.Item().PaddingHorizontal(3).Text(dateHint).FontSize(6f)
                .FontColor(SteelstonePrintTheme.FieldValueText);
        });
    }

    private static void ComposeHeaderMetaLine(IContainer container, string label, string value)
    {
        container.Text(text =>
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

    private static void ComposeLabelValueCells(TableDescriptor table, string label, string value)
    {
        var empty = string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(value);

        table.Cell().Background(SteelstonePrintTheme.FieldLabelBackground).Border(0.5f).BorderColor(SteelstonePrintTheme.HeaderBorder)
            .PaddingVertical(1).PaddingHorizontal(3).AlignMiddle()
            .Text(empty ? " " : label.Trim())
            .FontSize(7f).SemiBold().FontColor(SteelstonePrintTheme.FieldLabelText);

        table.Cell().Background(Colors.White).Border(0.5f).BorderColor(SteelstonePrintTheme.HeaderBorder)
            .PaddingVertical(1).PaddingHorizontal(3).AlignMiddle()
            .Text(text =>
            {
                if (empty)
                {
                    text.Span(" ").FontSize(7.5f);
                    return;
                }

                text.Span(string.IsNullOrWhiteSpace(value) ? "—" : value.Trim())
                    .FontSize(7.5f).FontColor(SteelstonePrintTheme.FieldValueText);
            });
    }
}
