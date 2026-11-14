using UnityEngine;

public static class ShiftOSVisualTokens
{
    public static readonly Color DesktopInk = Hex("10151F");
    public static readonly Color PanelSlate = Hex("1B2432");
    public static readonly Color RaisedPanel = Hex("253144");
    public static readonly Color Field = Hex("0F1722");
    public static readonly Color Border = Hex("3B4A60");
    public static readonly Color TextPrimary = Hex("F1F5F9");
    public static readonly Color TextMuted = Hex("9DABC0");
    public static readonly Color Focus = Hex("7DD3FC");
    public static readonly Color Warning = Hex("F6B94A");
    public static readonly Color Error = Hex("F06A72");
    public static readonly Color Success = Hex("5BC89A");

    public static readonly Color TickTock = Hex("FF6B72");
    public static readonly Color LinkedOut = Hex("42C8E8");
    public static readonly Color InWatch = Hex("E2A94A");
    public static readonly Color Swarms = Hex("35E0C1");
    public static readonly Color Excess = Hex("65A67B");
    public static readonly Color Deleg8 = Hex("F49A42");
    public static readonly Color BlamePoint = Hex("B94A5C");

    public const float AppTitleSize = 18f;
    public const float SectionTitleSize = 16f;
    public const float BodySize = 14f;
    public const float MetadataSize = 11f;
    public const float NumericSize = 48f;

    public static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static Color Hex(string value)
    {
        return ColorUtility.TryParseHtmlString("#" + value, out Color color) ? color : Color.magenta;
    }
}
