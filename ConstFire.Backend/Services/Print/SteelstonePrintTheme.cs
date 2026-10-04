namespace ConstFire.Backend.Services.Print;

internal static class SteelstonePrintTheme
{
    public const string CompanyName = "Steelstone";
    public const string CompanyLegalName = "STEELSTONE IT (PRIVATE) LIMITED";
    public const string CompanyLegalLine = "Steelstone IT (Pvt) Ltd";
    public const string StreetAddress = "Office 402, 4th Floor, Arfa Software Technology Park, 346-B Ferozepur Road";
    public const string CityStateZip = "Lahore, Punjab, Pakistan";
    public const string Phone = "+92-42-35880011";
    public const string Fax = "+92-42-0000001";
    public const string Website = "www.steelstoneit.com";
    public const string FooterAddressLine =
        "Steelstone IT (Pvt) Ltd | Office 402, 4th Floor, Arfa Software Technology Park, 346-B Ferozepur Road, Lahore | +92-42-35880011";
    public const string FooterTaxLine = "NTN 1234567-8 | STRN 3277876543210";
    public const string FooterControlledLine =
        "System-generated from Cloud ERP - controlled copy only when issued from the system";

    public static readonly string Navy = "#2F5597";
    public static readonly string LightGreyHeader = "#D9D9D9";
    /// <summary>Light blue field-name column (ERP-FORMATS label cells).</summary>
    public static readonly string FieldLabelBackground = "#B4C6E7";
    public static readonly string FieldLabelText = "#1E293B";
    public static readonly string FieldValueText = "#000000";

    /// <summary>Reserved height at page bottom for quotation approval boxes (1 inch).</summary>
    public const float QuotationApprovalBandHeight = 72f;

    /// <summary>Item rows kept with approval band when paginating.</summary>
    public const int QuotationTailRowsWithApproval = 3;
    public static readonly string TotalHighlight = "#B4C6E7";
    public static readonly string HeaderBorder = "#94A3B8";
    public static readonly string HeaderLabelGrey = "#64748B";
    public static readonly string HeaderCompanyGrey = "#475569";
}
