using System.Diagnostics;

namespace PcAngerManager;

/// <summary>기분 단계 (SPEC 5.4)</summary>
internal enum Mood { Calm, Normal, Angry }

/// <summary>
/// 트레이 아이콘을 정해진 초당 프레임 수로 바꿔 주는 장치.
///
/// CPU를 아끼는 방법:
///  - 아이콘은 캐릭터·배율이 바뀔 때만 만들어 두고, 프레임마다 새로 그리지 않음
///  - 다음 프레임 시각까지 타이머를 재워 두고, 프레임을 바꿀 때만 깨어남
///  - Windows 타이머는 약 15.6ms 단위로만 깨어나서 정확하지 않으므로,
///    "다음 프레임 예정 시각"을 시계로 계산해 평균 속도가 설정값과 맞게 함
///
/// 분노 게이지 테마(SPEC 5.4): 기본 캐릭터는 평온 = 천사, 보통 = 노랑, 분노 = 악마 버전.
/// 커스텀 러너는 분노일 때만 악마 토끼. 모든 아이콘 묶음을 미리 만들어 두므로 바꿀 때 비용이 거의 없음.
/// </summary>
internal sealed class Animator : IDisposable
{
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private Func<int, Icon[]> _buildRunnerIcons = _ => [];
    private Icon[] _runnerIcons = [];
    private readonly Dictionary<Mood, Icon[]> _moodIcons = new();
    private List<Bitmap> _customFrames = [];
    private string _signature = "";
    private int _iconSize;
    private double _lastFrameMs;
    private double _nextFrameMs;

    public string Runner { get; private set; } = "";
    public bool IsCustom { get; private set; }
    public bool MoodEnabled { get; private set; }
    public Mood Mood { get; private set; } = Mood.Calm;
    public IReadOnlyList<Bitmap> CustomFrames => _customFrames;
    public Color Color { get; private set; } = Color.White;
    public int FrameIndex { get; private set; }
    public int Fps { get; private set; } = 5;

    /// <summary>지금 기분 그림을 쓰는지 (아니면 러너 원래 그림)</summary>
    private bool ShowsMoodLook => MoodEnabled && (!IsCustom || Mood == Mood.Angry);

    private Icon[] ActiveIcons => ShowsMoodLook ? _moodIcons[Mood] : _runnerIcons;

    /// <summary>지금 보여 주는 모습의 이름 (미리보기용)</summary>
    public string ActiveLook => !ShowsMoodLook ? Runner : Mood switch
    {
        Mood.Calm => $"천사 {Runner}",
        Mood.Normal => $"노란 {Runner}",
        _ => IsCustom ? "악마 토끼" : $"악마 {Runner}",
    };

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
        _signature = SpritesSignature();
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

    /// <summary>분노 게이지 테마 켜기/끄기</summary>
    public void SetMoodEnabled(bool enabled)
    {
        if (enabled == MoodEnabled) return;
        MoodEnabled = enabled;
        ShowCurrentFrame();
    }

    /// <summary>기분 단계 변경. 프레임 위치는 이어서 재생</summary>
    public void SetMood(Mood mood)
    {
        if (mood == Mood) return;
        Mood = mood;
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
        if (SpritesSignature() != _signature) SetRunner(Runner, Color);
    }

    /// <summary>미리보기용: 지금 보여 주는 모습의 프레임 이미지들 (새로 만든 복사본, 받은 쪽에서 해제)</summary>
    public List<Bitmap> RenderActiveFrames()
    {
        if (ShowsMoodLook)
        {
            var (frames, color, palette) = Sprites.MoodLook(IsCustom ? Sprites.Names[0] : Runner, Mood);
            return frames.Select(f => Frames.Render(f, color, palette)).ToList();
        }
        return IsCustom
            ? _customFrames.Select(b => new Bitmap(b)).ToList()
            : Sprites.Get(Runner).Select(f => Frames.Render(f, Color)).ToList();
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
        var old = _moodIcons.Values.Append(_runnerIcons).ToList();

        _runnerIcons = _buildRunnerIcons(_iconSize);
        // 커스텀 러너는 자기 천사·노랑 버전이 없으므로 기분 그림은 토끼 것을 씀 (실제로는 분노 = 악마 토끼만 쓰임)
        string moodRunner = IsCustom ? Sprites.Names[0] : Runner;
        foreach (var mood in Enum.GetValues<Mood>())
        {
            var (frames, color, palette) = Sprites.MoodLook(moodRunner, mood);
            _moodIcons[mood] = frames.Select(f => Frames.RenderIcon(f, color, _iconSize, palette)).ToArray();
        }

        ShowCurrentFrame();
        foreach (var set in old)
            foreach (var icon in set) icon.Dispose();
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

    /// <summary>개발용: 지금 러너의 모든 그림(기본 + 기분 3단계)을 글자로 이어 붙인 값</summary>
    private string SpritesSignature() =>
        Signature(Sprites.Get(Runner)) +
        string.Concat(Enum.GetValues<Mood>().Select(m => Signature(Sprites.MoodLook(Runner, m).Frames)));

    private static string Signature(string[][] frames) =>
        string.Join("|", frames.Select(f => string.Join("", f)));

    public void Dispose()
    {
        _timer.Dispose();
        foreach (var set in _moodIcons.Values.Append(_runnerIcons))
            foreach (var icon in set) icon.Dispose();
        DisposeCustomFrames();
    }
}
