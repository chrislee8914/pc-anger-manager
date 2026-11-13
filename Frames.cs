using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace PcAngerManager;

/// <summary>
/// 그림 도구: 글자 그림(Sprites) → 이미지, 이미지 → 트레이 아이콘, 픽셀 그대로 확대해서 그리기.
/// </summary>
internal static class Frames
{
    public const int IconSize = 16;

    /// <summary>'#' 칸을 지정한 색으로 칠한 16x16 이미지</summary>
    public static Bitmap Render(string[] rows, Color color)
    {
        var bmp = new Bitmap(IconSize, IconSize);
        for (int y = 0; y < IconSize && y < rows.Length; y++)
        {
            string row = rows[y];
            for (int x = 0; x < IconSize && x < row.Length; x++)
            {
                if (row[x] == '#') bmp.SetPixel(x, y, color);
            }
        }
        return bmp;
    }

    public static Icon RenderIcon(string[] rows, Color color)
    {
        using var bmp = Render(rows, color);
        return ToTrayIcon(bmp);
    }

    /// <summary>
    /// 트레이용 아이콘. 화면 배율에 맞는 크기(100% = 16, 150% = 24 등)로 직접 키워서 만듦.
    /// Windows가 늘리면 흐릿해지므로 픽셀을 그대로 키움 (비율 유지, 가운데 정렬).
    /// </summary>
    public static Icon ToTrayIcon(Bitmap source)
    {
        using var square = ToSquare(source, SystemInformation.SmallIconSize.Width);
        return ToIcon(square);
    }

    /// <summary>정사각형 크기로 픽셀 그대로 확대한 새 이미지 (메뉴 그림 등)</summary>
    public static Bitmap ToSquare(Bitmap source, int size)
    {
        var result = new Bitmap(size, size);
        using var g = Graphics.FromImage(result);
        DrawScaled(g, source, new Rectangle(0, 0, size, size));
        return result;
    }

    /// <summary>상자 안에 비율을 유지하며 픽셀 그대로 확대해서 가운데에 그림</summary>
    public static void DrawScaled(Graphics g, Bitmap source, Rectangle box)
    {
        float scale = Math.Min((float)box.Width / source.Width, (float)box.Height / source.Height);
        int w = (int)Math.Round(source.Width * scale);
        int h = (int)Math.Round(source.Height * scale);
        var oldMode = g.InterpolationMode;
        var oldOffset = g.PixelOffsetMode;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(source, box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
        g.InterpolationMode = oldMode;
        g.PixelOffsetMode = oldOffset;
    }

    /// <summary>
    /// Bitmap → Icon 변환. Windows가 만든 아이콘 핸들을 복사본으로 옮긴 뒤 바로 해제해
    /// 메모리가 새지 않게 함.
    /// </summary>
    public static Icon ToIcon(Bitmap bmp)
    {
        IntPtr handle = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
