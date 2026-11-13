using System.Drawing.Drawing2D;

namespace PcAngerManager;

/// <summary>
/// 개발용 미리보기 창 (SPEC 5.4). --preview 옵션으로 실행할 때만 뜸.
/// 위: 지금 트레이에 보이는 프레임을 크게 확대
/// 아래: 현재 캐릭터의 모든 프레임 + 사용률/단계/속도
/// 그림 글자(Sprites.cs)를 고치면 다음 그리기 때 바로 반영됨.
/// </summary>
internal sealed class PreviewForm : Form
{
    private const int BigZoom = 12;
    private const int SmallZoom = 4;
    private const int Margin_ = 20;

    private readonly TrayApp _app;
    private readonly System.Windows.Forms.Timer _redraw;

    public PreviewForm(TrayApp app)
    {
        _app = app;
        Text = "PC분노관리 - 미리보기";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(Frames.IconSize * BigZoom + Margin_ * 2, Frames.IconSize * BigZoom + 200);
        TopMost = true;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(32, 32, 32);

        // 화면 오른쪽 아래(트레이 근처)에 배치
        var area = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(area.Right - Width - 16, area.Bottom - Height - 16);

        _redraw = new System.Windows.Forms.Timer { Interval = 30 };
        _redraw.Tick += (_, _) => Invalidate();
        _redraw.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.NearestNeighbor; // 픽셀이 또렷하게 보이게
        g.PixelOffsetMode = PixelOffsetMode.Half;

        var anim = _app.Animator;

        // 기본 캐릭터는 그림 글자를 매번 새로 읽어서 수정 내용 즉시 반영, 커스텀 러너는 불러온 그림 사용
        // 지금 트레이에 보이는 모습(천사/노랑/악마 토끼 포함)을 매번 새로 그려서 수정 내용 즉시 반영
        List<Bitmap> frames = anim.RenderActiveFrames();
        try
        {
            if (frames.Count > 0) PaintFrames(g, anim, frames);
        }
        finally
        {
            foreach (var bmp in frames) bmp.Dispose();
        }
    }

    private void PaintFrames(Graphics g, Animator anim, List<Bitmap> frames)
    {
        // 검은 아이콘(다크 모드)이면 밝은 배경, 흰 아이콘이면 어두운 배경 (작업 표시줄처럼)
        bool darkIcon = anim.ActiveLook == anim.Runner && !anim.IsCustom && anim.Color.GetBrightness() < 0.5f;
        g.Clear(darkIcon ? Color.FromArgb(225, 225, 225) : Color.FromArgb(32, 32, 32));
        int index = anim.FrameIndex % frames.Count;

        // 큰 화면: 지금 프레임
        int bigSize = Frames.IconSize * BigZoom;
        var bigBox = new Rectangle(Margin_, Margin_, bigSize, bigSize);
        if (!anim.IsCustom || anim.ActiveLook != anim.Runner) DrawGrid(g, bigBox, BigZoom);
        Frames.DrawScaled(g, frames[index], bigBox);

        // 작은 화면: 모든 프레임 (지금 프레임은 테두리 강조)
        int smallSize = Frames.IconSize * SmallZoom;
        int y = bigBox.Bottom + 16;
        for (int i = 0; i < frames.Count; i++)
        {
            var box = new Rectangle(Margin_ + i * (smallSize + 8), y, smallSize, smallSize);
            using (var back = new SolidBrush(Color.FromArgb(45, 45, 45))) g.FillRectangle(back, box);
            Frames.DrawScaled(g, frames[i], box);
            if (i == index)
            {
                using var pen = new Pen(Color.DodgerBlue, 2);
                g.DrawRectangle(pen, box);
            }
            TextRenderer.DrawText(g, i.ToString(), Font, new Point(box.Left, box.Bottom + 2), Color.Gray);
        }

        // 상태 숫자
        double basisValue = _app.Status.BasisValue(_app.Basis);
        string status =
            $"캐릭터: {anim.Runner} → 지금 모습: {anim.ActiveLook} ({anim.Mood})\n" +
            $"속도 기준 {_app.Basis}: 3초 평균 {basisValue:0.0}%\n" +
            $"단계 {SpeedRule.Level(basisValue)} → {anim.Fps} fps (최대 {_app.MaxFps})\n" +
            $"툴팁: {_app.TooltipText.Replace("\n", " / ")}";
        TextRenderer.DrawText(g, status, Font,
            new Point(Margin_, y + smallSize + 22), darkIcon ? Color.Black : Color.Gainsboro);
    }

    private static void DrawGrid(Graphics g, Rectangle box, int zoom)
    {
        using var grid = new Pen(Color.FromArgb(50, 50, 50));
        for (int i = 0; i <= Frames.IconSize; i++)
        {
            g.DrawLine(grid, box.Left + i * zoom, box.Top, box.Left + i * zoom, box.Bottom);
            g.DrawLine(grid, box.Left, box.Top + i * zoom, box.Right, box.Top + i * zoom);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _redraw.Dispose();
        base.Dispose(disposing);
    }
}
