namespace ConstFire.Backend.Services.Print;

/// <summary>
/// ERP document modules that use Steelstone ERP-FORMATS PDF (not legacy Vertex/raw layouts).
/// </summary>
internal static class SteelstoneErpDocumentPrintModuleCodes
{
    public static readonly HashSet<string> Codes = new(StringComparer.OrdinalIgnoreCase)
    {
        "08", // Vendor bill
        "09", // Quotation (sales)
        "10", // Payment / expense voucher
        "11", // Sales tax invoice
        "14", // Delivery challan
        "15", // Payment receipt
        "16", // Purchase order
        "17", // Customer account statement
        "18", // Factory ledger
    };

    public static bool IsErpDocument(string moduleCode) =>
        !string.IsNullOrWhiteSpace(moduleCode) && Codes.Contains(moduleCode.Trim());
}
