using System.Drawing.Drawing2D;
using Microsoft.Win32;

namespace PcAngerManager;

/// <summary>
/// 테마 판단과 메뉴 색 (SPEC 3.7, 5.4).
/// </summary>
internal static class Theme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>작업 표시줄이 밝은 색인지 (Windows "기본 모드")</summary>
    public static bool TaskbarIsLight => ReadFlag("SystemUsesLightTheme");

    /// <summary>앱이 밝은 색인지 (Windows "앱 모드")</summary>
    public static bool AppsAreLight => ReadFlag("AppsUseLightTheme");

    /// <summary>
    /// 아이콘 색. 원본 관찰(3.7) 그대로: 라이트 모드 = 흰색, 다크 모드 = 검은색.
    /// 시스템 = 작업 표시줄이 어두우면 흰색, 밝으면 검은색.
    /// </summary>
    public static Color IconColor(string themeSetting) => themeSetting switch
    {
        AppSettings.ThemeLight => Color.White,
        AppSettings.ThemeDark => Color.Black,
        _ => TaskbarIsLight ? Color.Black : Color.White,
    };

    /// <summary>메뉴 색을 Windows 앱 모드에 맞춤</summary>
    public static void ApplyMenuTheme()
    {
        ToolStripManager.Renderer = AppsAreLight
            ? new MenuRenderer(MenuColors.Light)
            : new MenuRenderer(MenuColors.Dark);
    }

    private static bool ReadFlag(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue(name) is int value && value != 0;
    }
}

internal sealed class MenuColors : ProfessionalColorTable
{
    public static readonly MenuColors Dark = new(
        back: Color.FromArgb(43, 43, 43), hover: Color.FromArgb(65, 65, 65),
        border: Color.FromArgb(70, 70, 70), text: Color.FromArgb(240, 240, 240),
        disabled: Color.FromArgb(130, 130, 130));

    public static readonly MenuColors Light = new(
        back: Color.FromArgb(249, 249, 249), hover: Color.FromArgb(225, 225, 225),
        border: Color.FromArgb(210, 210, 210), text: Color.FromArgb(25, 25, 25),
        disabled: Color.FromArgb(150, 150, 150));

    public Color Back { get; }
    public Color Hover { get; }
    public Color Border { get; }
    public Color Text { get; }
    public Color Disabled { get; }

    private MenuColors(Color back, Color hover, Color border, Color text, Color disabled)
    {
        Back = back; Hover = hover; Border = border; Text = text; Disabled = disabled;
        UseSystemColors = false;
    }

    public override Color ToolStripDropDownBackground => Back;
    public override Color ImageMarginGradientBegin => Back;
    public override Color ImageMarginGradientMiddle => Back;
    public override Color ImageMarginGradientEnd => Back;
    public override Color MenuBorder => Border;
    public override Color MenuItemBorder => Hover;
    public override Color MenuItemSelected => Hover;
    public override Color MenuItemSelectedGradientBegin => Hover;
    public override Color MenuItemSelectedGradientEnd => Hover;
    public override Color MenuItemPressedGradientBegin => Hover;
    public override Color MenuItemPressedGradientEnd => Hover;
    public override Color SeparatorDark => Border;
    public override Color SeparatorLight => Back;
    public override Color CheckBackground => Back;
    public override Color CheckSelectedBackground => Hover;
    public override Color CheckPressedBackground => Hover;
}

/// <summary>메뉴 글자·화살표·체크 표시를 테마 색으로 그림</summary>
internal sealed class MenuRenderer(MenuColors colors) : ToolStripProfessionalRenderer(colors)
{
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? colors.Text : colors.Disabled;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = colors.Text;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        // 기본 체크 그림은 검은색이라 어두운 메뉴에서 안 보임 → 직접 그림
        var r = e.ImageRectangle;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(colors.Text, 1.6f);
        g.DrawLines(pen,
        [
            new PointF(r.Left + r.Width * 0.2f, r.Top + r.Height * 0.52f),
            new PointF(r.Left + r.Width * 0.42f, r.Top + r.Height * 0.74f),
            new PointF(r.Left + r.Width * 0.8f, r.Top + r.Height * 0.28f),
        ]);
    }
}
