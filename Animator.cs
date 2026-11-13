using System.Diagnostics;

namespace PcAngerManager;

/// <summary>
/// 트레이 아이콘을 정해진 초당 프레임 수로 바꿔 주는 장치.
///
/// CPU를 아끼는 방법:
///  - 아이콘은 캐릭터·배율이 바뀔 때만 만들어 두고, 프레임마다 새로 그리지 않음
///  - 다음 프레임 시각까지 타이머를 재워 두고, 프레임을 바꿀 때만 깨어남
///  - Windows 타이머는 약 15.6ms 단위로만 깨어나서 정확하지 않으므로,
///    "다음 프레임 예정 시각"을 시계로 계산해 평균 속도가 설정값과 맞게 함
///
/// 분노 모드(SPEC 5.4): 켜지면 지금 캐릭터 대신 악마 토끼 아이콘을 보여 줌.
/// 두 아이콘 묶음을 미리 만들어 두므로 바꿀 때 비용이 거의 없음.
/// </summary>
internal sealed class Animator : IDisposable
{
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private Func<int, Icon[]> _buildRunnerIcons = _ => [];
    private Icon[] _runnerIcons = [];
    private Icon[] _devilIcons = [];
    private List<Bitmap> _customFrames = [];
    private string _signature = "";
    private int _iconSize;
    private double _lastFrameMs;
    private double _nextFrameMs;

    public string Runner { get; private set; } = "";
    public bool IsCustom { get; private set; }
    public bool IsAngry { get; private set; }
    public IReadOnlyList<Bitmap> CustomFrames => _customFrames;
    public Color Color { get; private set; } = Color.White;
    public int FrameIndex { get; private set; }
    public int Fps { get; private set; } = 5;

    private Icon[] ActiveIcons => IsAngry ? _devilIcons : _runnerIcons;

    public Animator(NotifyIcon tray)
    {
        _tray = tray;
        _timer.Tick += (_, _) => NextFrame();
    }

    /// <summary>기본 캐릭터 (테마 색으로 칠함)</summary>
    public void SetRunner(string runner, Color color)
    {
        Runner = runner;
        Color = color;
        IsCustom = false;
        DisposeCustomFrames();
        var frames = Sprites.Get(runner);
        _signature = Signature(frames) + Signature(Sprites.Devil);
        _buildRunnerIcons = size => frames.Select(f => Frames.RenderIcon(f, color, size)).ToArray();
        RebuildIcons();
    }

    /// <summary>커스텀 러너 (그림 원래 색 그대로, SPEC 5.4). 프레임이 없으면 false</summary>
    public bool SetCustomRunner(string name, List<Bitmap> frames)
    {
        if (frames.Count == 0) return false;
        Runner = name;
        IsCustom = true;
        DisposeCustomFrames();
        _customFrames = frames;
        _buildRunnerIcons = size => _customFrames.Select(b => Frames.ToTrayIcon(b, size)).ToArray();
        RebuildIcons();
        return true;
    }

    /// <summary>테마 색 변경. 커스텀 러너는 원래 색을 쓰므로 영향 없음</summary>
    public void SetColor(Color color)
    {
        Color = color;
        if (!IsCustom) SetRunner(Runner, color);
    }

    /// <summary>분노 모드 켜기/끄기. 프레임 위치는 이어서 재생</summary>
    public void SetAngry(bool angry)
    {
        if (angry == IsAngry) return;
        IsAngry = angry;
        ShowCurrentFrame();
    }

    /// <summary>작업 표시줄 모니터의 배율이 바뀌었으면 아이콘을 다시 만듦. 바뀌었으면 true (SPEC 5.4)</summary>
    public bool CheckIconSize()
    {
        if (Frames.TrayIconSize() == _iconSize) return false;
        RebuildIcons();
        return true;
    }

    /// <summary>개발용: 그림 글자가 바뀌었으면 아이콘을 다시 만듦 (핫 리로드 반영)</summary>
    public void RefreshIfChanged()
    {
        if (IsCustom) return;
        if (Signature(Sprites.Get(Runner)) + Signature(Sprites.Devil) != _signature) SetRunner(Runner, Color);
    }

    public void SetFps(int fps)
    {
        if (fps == Fps && _timer.Enabled) return;
        Fps = fps;
        // 지금 프레임은 그대로 두고, 다음 프레임 시각만 새 속도로 다시 계산 (SPEC 5.1)
        _nextFrameMs = _lastFrameMs + 1000.0 / Fps;
        Schedule();
    }

    private void RebuildIcons()
    {
        _iconSize = Frames.TrayIconSize();
        var oldRunner = _runnerIcons;
        var oldDevil = _devilIcons;
        _runnerIcons = _buildRunnerIcons(_iconSize);
        _devilIcons = Sprites.Devil
            .Select(f => Frames.RenderIcon(f, Sprites.DevilColor, _iconSize, Sprites.DevilPalette))
            .ToArray();
        ShowCurrentFrame();
        foreach (var icon in oldRunner) icon.Dispose();
        foreach (var icon in oldDevil) icon.Dispose();
    }

    private void ShowCurrentFrame()
    {
        var icons = ActiveIcons;
        if (icons.Length == 0) return;
        FrameIndex %= icons.Length;
        _tray.Icon = icons[FrameIndex];
    }

    private void NextFrame()
    {
        var icons = ActiveIcons;
        FrameIndex = (FrameIndex + 1) % icons.Length;
        _tray.Icon = icons[FrameIndex];

        double now = _clock.Elapsed.TotalMilliseconds;
        _lastFrameMs = now;
        _nextFrameMs += 1000.0 / Fps;
        if (_nextFrameMs < now) _nextFrameMs = now + 1000.0 / Fps; // 많이 밀렸으면 따라잡지 말고 새로 시작
        Schedule();
    }

    private void Schedule()
    {
        double wait = _nextFrameMs - _clock.Elapsed.TotalMilliseconds;
        _timer.Interval = Math.Max(1, (int)Math.Ceiling(wait));
        _timer.Start();
    }

    private void DisposeCustomFrames()
    {
        foreach (var bmp in _customFrames) bmp.Dispose();
        _customFrames = [];
    }

    private static string Signature(string[][] frames) =>
        string.Join("|", frames.Select(f => string.Join("", f)));

    public void Dispose()
    {
        _timer.Dispose();
        foreach (var icon in _runnerIcons) icon.Dispose();
        foreach (var icon in _devilIcons) icon.Dispose();
        DisposeCustomFrames();
    }
}
