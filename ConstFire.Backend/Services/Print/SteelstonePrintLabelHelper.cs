namespace ConstFire.Backend.Services.Print;

internal static class SteelstonePrintLabelHelper
{
    public static string ShortLabel(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            return fieldName;

        var label = fieldName.Trim();
        var paren = label.IndexOf('(');
        if (paren > 0)
            label = label[..paren].Trim();

        return label;
    }
}
