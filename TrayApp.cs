using System.Reflection;
using Microsoft.Win32;

namespace PcAngerManager;

/// <summary>
/// 트레이 아이콘과 우클릭 메뉴를 관리하는 앱 본체. 메인 창은 없음 (SPEC 1).
/// </summary>
internal sealed class TrayApp : ApplicationContext
{
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu;        // 우클릭: 러너 ~ 종료
    private readonly ContextMenuStrip _statusMenu;  // 좌클릭: PC 상태
    private readonly Animator _animator;
    private readonly SystemStatus _status = new();
    private readonly System.Windows.Forms.Timer _sampler;
    private readonly SynchronizationContext _ui;
    private readonly bool _devMode;
    private PreviewForm? _preview;
    private CustomRunnerForm? _customForm;
    private ToolStripMenuItem _runnerMenu = null!;

    public Animator Animator => _animator;
    public SystemStatus Status => _status;
    public string Basis => _settings.Basis;
    public int MaxFps => _settings.MaxFps;
    public string TooltipText => _tray.Text;

    public TrayApp(bool showPreview)
    {
        _devMode = showPreview;
        if (!Sprites.Names.Contains(_settings.Runner)) _settings.Runner = Sprites.Names[0];

        Theme.ApplyMenuTheme();
        _menu = BuildMenu();
        _menu.Opening += (_, _) => ApplyMenuSpacing(_menu);
        _menu.Opened += (_, _) => DockToTaskbar(_menu);

        // 상태 메뉴: 체크 표시·그림 칸이 필요 없으므로 왼쪽 빈 칸 없이 글자부터 시작
        _statusMenu = new ContextMenuStrip { ShowImageMargin = false, ShowCheckMargin = false };
        _statusMenu.Opening += (_, e) =>
        {
            RefreshStatusLines();   // 열 때 최신 값 바로 표시
            e.Cancel = false;
        };
        _statusMenu.Opened += (_, _) => DockToTaskbar(_statusMenu);
        _statusMenu.Closed += (_, _) => _statusMenu.MinimumSize = Size.Empty; // 다음에 열 때는 폭을 새로 계산
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _tray = new NotifyIcon
        {
            Text = "",              // 툴팁은 1초마다 UpdateTooltip()에서 채움 (SPEC 5.4)
            ContextMenuStrip = _menu,
        };
        // 좌클릭 = PC 상태, 우클릭 = 러너 ~ 종료 (SPEC 5.4, 원본과 다름)
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) ShowStatusMenu();
        };

        _animator = new Animator(_tray);
        ApplySelectedRunner();
        _animator.SetFps(SpeedRule.Fps(0, _settings.MaxFps));
        _tray.Visible = true;

        // Windows 테마가 바뀌면 바로 따라감 (SPEC 5.4)
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        // 1초마다 측정 → 속도 결정 (SPEC 5.1)
        _sampler = new System.Windows.Forms.Timer { Interval = 1000 };
        _sampler.Tick += (_, _) => OnSample();
        _sampler.Start();

        if (showPreview)
        {
            _preview = new PreviewForm(this);
            _preview.Show();
        }
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General) return;
        _ui.Post(_ =>
        {
            Theme.ApplyMenuTheme();
            ApplyIconColor();
            RebuildRunnerMenu(); // 메뉴 그림 색도 메뉴 글자색에 맞춤
        }, null);
    }

    /// <summary>
    /// 좌클릭: PC 상태 메뉴를 띄움. 트레이 아이콘이 우클릭 때 쓰는 내부 기능을 잠깐 빌려서
    /// 위치·바깥 클릭 시 닫힘이 우클릭과 같게 동작. 못 찾으면 마우스 위치에 직접 띄움.
    /// </summary>
    private void ShowStatusMenu()
    {
        var show = typeof(NotifyIcon).GetMethod("ShowContextMenu",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (show is null)
        {
            _statusMenu.Show(Cursor.Position);
            return;
        }
        _tray.ContextMenuStrip = _statusMenu;
        try { show.Invoke(_tray, null); }
        finally { _tray.ContextMenuStrip = _menu; } // 우클릭은 다시 원래 메뉴
    }

    private void ApplyIconColor() => _animator.SetColor(Theme.IconColor(_settings.Theme));

    /// <summary>설정에 저장된 러너를 트레이에 적용. 커스텀 러너를 못 읽으면 기본 캐릭터로 (SPEC 5.4)</summary>
    private void ApplySelectedRunner()
    {
        if (_settings.CustomRunner != "" &&
            _animator.SetCustomRunner(_settings.CustomRunner, CustomRunnerStore.LoadFrames(_settings.CustomRunner)))
            return;

        _settings.CustomRunner = "";
        _animator.SetRunner(_settings.Runner, Theme.IconColor(_settings.Theme));
    }

    /// <summary>
    /// 러너 ▶ 하위 메뉴: 기본 3종 → 구분선 → 사용자 러너 (SPEC 3.5, 5.4).
    /// 항목 왼쪽에 첫 프레임 그림. Tag로 "기본:이름" / "커스텀:이름" 구분.
    /// </summary>
    private void RebuildRunnerMenu()
    {
        foreach (ToolStripItem old in _runnerMenu.DropDownItems) old.Image?.Dispose();
        _runnerMenu.DropDownItems.Clear();

        int imageSize = SystemInformation.SmallIconSize.Width;
        _runnerMenu.DropDown.ImageScalingSize = new Size(imageSize, imageSize);
        Color menuText = Theme.AppsAreLight ? MenuColors.Light.Text : MenuColors.Dark.Text;

        foreach (var name in Sprites.Names)
        {
            using var first = Frames.Render(Sprites.Get(name)[0], menuText);
            var item = new ToolStripMenuItem(name, Frames.ToSquare(first, imageSize)) { Tag = "기본:" + name };
            item.Click += (_, _) =>
            {
                _settings.Runner = name;
                _settings.CustomRunner = "";
                _animator.SetRunner(name, Theme.IconColor(_settings.Theme));
                _settings.Save();
                UpdateRunnerChecks();
            };
            _runnerMenu.DropDownItems.Add(item);
        }

        var customs = CustomRunnerStore.LoadProfiles();
        if (customs.Count > 0) _runnerMenu.DropDownItems.Add(new ToolStripSeparator());
        foreach (var profile in customs)
        {
            string name = profile.Name;
            var frames = CustomRunnerStore.LoadFrames(name);
            Image? picture = frames.Count > 0 ? Frames.ToSquare(frames[0], imageSize) : null;
            foreach (var bmp in frames) bmp.Dispose();

            var item = new ToolStripMenuItem(name, picture) { Tag = "커스텀:" + name };
            item.Click += (_, _) =>
            {
                if (!_animator.SetCustomRunner(name, CustomRunnerStore.LoadFrames(name))) return;
                _settings.CustomRunner = name;
                _settings.Save();
                UpdateRunnerChecks();
            };
            _runnerMenu.DropDownItems.Add(item);
        }

        UpdateRunnerChecks();
    }

    /// <summary>지금 쓰는 러너에 체크 표시 (SPEC 3.2)</summary>
    private void UpdateRunnerChecks()
    {
        string current = _settings.CustomRunner != "" ? "커스텀:" + _settings.CustomRunner : "기본:" + _settings.Runner;
        foreach (ToolStripItem item in _runnerMenu.DropDownItems)
        {
            if (item is ToolStripMenuItem menuItem) menuItem.Checked = (string?)menuItem.Tag == current;
        }
    }

    /// <summary>커스텀 러너 설정 창. 이미 열려 있으면 앞으로 가져옴 (SPEC 5.4)</summary>
    private void OpenCustomRunnerForm()
    {
        if (_customForm is { IsDisposed: false })
        {
            _customForm.Activate();
            return;
        }
        _customForm = new CustomRunnerForm();
        _customForm.RunnerChanged += OnCustomRunnerChanged;
        _customForm.Show();
    }

    private void OnCustomRunnerChanged(string name, bool deleted)
    {
        if (name == _settings.CustomRunner)
        {
            // 지금 쓰던 러너: 삭제됐으면 기본 캐릭터로, 고쳐졌으면 새 그림으로
            if (deleted) _settings.CustomRunner = "";
            ApplySelectedRunner();
            _settings.Save();
        }
        RebuildRunnerMenu();
    }

    private void OnSample()
    {
        bool slowTurn = _status.Sample(_settings.Basis);
        ApplySpeed();
        UpdateTooltip();

        // 메뉴가 열려 있으면 5초마다 화면 갱신 (SPEC 3.1)
        if (slowTurn && _statusMenu.Visible) RefreshStatusLines();

        if (_devMode) _animator.RefreshIfChanged();
    }

    private void ApplySpeed() =>
        _animator.SetFps(SpeedRule.Fps(_status.BasisValue(_settings.Basis), _settings.MaxFps));

    /// <summary>툴팁: 속도 기준 값 + 온도 (SPEC 5.4)</summary>
    private void UpdateTooltip()
    {
        string text = $"{_settings.Basis}: {_status.BasisValue(_settings.Basis):0.0}%";
        if (_status.Temperature.Available)
            text += $"\n온도: {FormatTemperature(_status.Temperature.MaxC)}";
        _tray.Text = text;
    }

    private string FormatTemperature(double celsius)
    {
        bool fahrenheit = _settings.TemperatureUnit switch
        {
            "섭씨" => false,
            "화씨" => true,
            _ => !System.Globalization.RegionInfo.CurrentRegion.IsMetric, // "시스템"
        };
        return fahrenheit ? $"{celsius * 9 / 5 + 32:0.0}°F" : $"{celsius:0.0}°C";
    }

    /// <summary>좌클릭 상태 메뉴를 최신 값으로 다시 채움 (SPEC 3.1 형식)</summary>
    private void RefreshStatusLines()
    {
        var lines = _status.BuildLines(FormatTemperature);

        _statusMenu.SuspendLayout();
        var old = _statusMenu.Items.Cast<ToolStripItem>().ToList();
        _statusMenu.Items.Clear();
        foreach (var item in old) item.Dispose();
        foreach (var line in lines) _statusMenu.Items.Add(InfoLine(line));
        ApplyMenuSpacing(_statusMenu);   // 숫자를 고치면 다음 갱신 때 바로 반영
        _statusMenu.ResumeLayout();

        if (_statusMenu.Visible)
        {
            // 열려 있는 동안 숫자 자릿수가 바뀌어도 폭이 줄었다 늘었다 하지 않게
            _statusMenu.MinimumSize = new Size(Math.Max(_statusMenu.MinimumSize.Width, _statusMenu.Width), 0);
            DockToTaskbar(_statusMenu);
        }
    }

    /// <summary>
    /// 메뉴를 작업 표시줄 바로 위에 딱 붙임 (SPEC 5.4).
    /// 작업 표시줄이 아래면 메뉴 아래 끝 = 작업 표시줄 위 끝. 위·왼쪽·오른쪽에 있으면 그쪽에 붙임.
    /// 가로(또는 세로) 위치는 클릭한 곳 근처를 유지하되 화면 밖으로 나가지 않게.
    /// </summary>
    private static void DockToTaskbar(ToolStripDropDown menu)
    {
        var screen = Screen.FromPoint(Cursor.Position);
        var full = screen.Bounds;
        var work = screen.WorkingArea;   // 작업 표시줄을 뺀 영역
        int left = menu.Left, top = menu.Top;

        if (work.Top > full.Top) top = work.Top;                        // 작업 표시줄이 위
        else if (work.Left > full.Left) left = work.Left;               // 왼쪽
        else if (work.Right < full.Right) left = work.Right - menu.Width; // 오른쪽
        else top = work.Bottom - menu.Height;                           // 아래 (자동 숨김 포함)

        left = Math.Clamp(left, work.Left, Math.Max(work.Left, work.Right - menu.Width));
        top = Math.Clamp(top, work.Top, Math.Max(work.Top, work.Bottom - menu.Height));
        if (left != menu.Left || top != menu.Top) menu.Location = new Point(left, top);
    }

    /// <summary>우클릭 메뉴: 러너 ~ 종료 (SPEC 3.1의 (B) 부분)</summary>
    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        _runnerMenu = new ToolStripMenuItem("러너");
        RebuildRunnerMenu();
        menu.Items.Add(_runnerMenu);
        menu.Items.Add(new ToolStripMenuItem("커스텀 러너 설정...", null, (_, _) => OpenCustomRunnerForm()));

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(BuildSettingsMenu());
        menu.Items.Add(BuildInfoMenu());

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("종료", null, (_, _) => ExitThread()));

        return menu;
    }

    private ToolStripMenuItem BuildSettingsMenu()
    {
        var settings = new ToolStripMenuItem("설정");

        settings.DropDownItems.Add(RadioSubmenu("테마",
            [AppSettings.ThemeSystem, AppSettings.ThemeLight, AppSettings.ThemeDark], _settings.Theme, option =>
            {
                _settings.Theme = option;
                ApplyIconColor();
            }));

        settings.DropDownItems.Add(RadioSubmenu("달리기 속도 기준",
            [SystemStatus.BasisCpu, SystemStatus.BasisGpu, SystemStatus.BasisMemory], _settings.Basis, option =>
            {
                _settings.Basis = option;
                ApplySpeed();
                UpdateTooltip();
            }));

        string[] fpsOptions = ["10 fps", "20 fps", "30 fps", "40 fps"];
        settings.DropDownItems.Add(RadioSubmenu("최대 FPS 제한", fpsOptions, $"{_settings.MaxFps} fps", option =>
        {
            _settings.MaxFps = int.Parse(option.Split(' ')[0]);
            ApplySpeed();
        }));

        settings.DropDownItems.Add(RadioSubmenu("온도 단위", ["시스템", "섭씨", "화씨"], _settings.TemperatureUnit, option =>
        {
            _settings.TemperatureUnit = option;
            UpdateTooltip();
        }));

        // 체크 표시 = 실제 시작 프로그램 등록 여부 (SPEC 5.4)
        var autoStart = new ToolStripMenuItem("윈도우 시작 시 자동 실행") { Checked = AutoStart.IsEnabled };
        autoStart.Click += (_, _) =>
        {
            AutoStart.Set(!AutoStart.IsEnabled);
            autoStart.Checked = AutoStart.IsEnabled;
        };
        settings.DropDownItems.Add(autoStart);
        return settings;
    }

    private static ToolStripMenuItem BuildInfoMenu()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var info = new ToolStripMenuItem("정보");
        info.DropDownItems.Add(InfoLine($"PC분노관리 v{version?.ToString(3)}"));
        return info;
    }

    // 메뉴 항목 여백 (왼쪽, 위, 오른쪽, 아래). 숫자를 키우면 메뉴가 넓고 여유 있어짐
    private static Padding ItemPadding => new(8, 2, 32, 2);
    private static Padding LabelPadding => new(10, 0, 24, 0);
    private static Padding LabelMargin => new(0, 0, 0, 0);

    /// <summary>메뉴와 모든 하위 메뉴에 여백 적용</summary>
    private static void ApplyMenuSpacing(ToolStrip strip)
    {
        foreach (ToolStripItem item in strip.Items)
        {
            if (item is ToolStripSeparator) continue;
            item.Padding = item is StatusLine ? LabelPadding : ItemPadding;
            if (item is StatusLine) item.Margin = LabelMargin;
            if (item is ToolStripMenuItem { HasDropDownItems: true } sub)
                ApplyMenuSpacing(sub.DropDown);
        }
    }

    /// <summary>클릭용이 아닌 정보 표시용 줄</summary>
    private static StatusLine InfoLine(string text) => new(text);

    /// <summary>여러 선택지 중 하나만 체크되는 하위 메뉴. 고르면 설정 파일에 바로 저장</summary>
    private ToolStripMenuItem RadioSubmenu(string title, string[] options, string selected, Action<string> onSelect)
    {
        var parent = new ToolStripMenuItem(title);
        foreach (var option in options)
        {
            var item = new ToolStripMenuItem(option) { Checked = option == selected };
            item.Click += (_, _) =>
            {
                foreach (ToolStripMenuItem sibling in parent.DropDownItems)
                    sibling.Checked = sibling == item;
                onSelect(option);
                _settings.Save();
            };
            parent.DropDownItems.Add(item);
        }
        return parent;
    }

    /// <summary>
    /// 정보 표시용 메뉴 줄. 일반 메뉴 항목과 같은 부품이라 메뉴 폭 계산에 포함되지만,
    /// 마우스를 올려도 강조되지 않고 클릭해도 아무 일 없음.
    /// </summary>
    private sealed class StatusLine(string text) : ToolStripMenuItem(text)
    {
        public override bool CanSelect => false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged; // 해제 안 하면 메모리가 샘
            _sampler.Dispose();
            _tray.Visible = false;   // 종료 후 트레이에 유령 아이콘이 남지 않게
            _tray.Dispose();
            _menu.Dispose();
            _statusMenu.Dispose();
            _animator.Dispose();
            _status.Dispose();
            _preview?.Dispose();
            _customForm?.Dispose();
        }
        base.Dispose(disposing);
    }
}
