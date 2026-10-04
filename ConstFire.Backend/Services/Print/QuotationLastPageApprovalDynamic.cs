using QuestPDF.Elements;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace ConstFire.Backend.Services.Print;

/// <summary>
/// Draws the approval band at the physical bottom of the last page only (foreground overlay).
/// </summary>
internal sealed class QuotationLastPageApprovalDynamic : IDynamicComponent
{
    public DynamicComponentComposeResult Compose(DynamicContext context)
    {
        if (context.PageNumber != context.TotalPages)
        {
            return new DynamicComponentComposeResult
            {
                Content = context.CreateElement(_ => { }),
                HasMoreContent = false
            };
        }

        var content = context.CreateElement(element =>
        {
            element
                .AlignBottom()
                .PaddingHorizontal(14)
                .PaddingBottom(34)
                .Height(SteelstonePrintTheme.QuotationApprovalBandHeight)
                .Element(SteelstoneErpPrintChrome.ComposeSignatureBlockQuotation);
        });

        return new DynamicComponentComposeResult
        {
            Content = content,
            HasMoreContent = false
        };
    }
}
