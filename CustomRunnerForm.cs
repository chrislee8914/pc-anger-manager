using System.ComponentModel;

namespace PcAngerManager;

/// <summary>
/// 커스텀 러너 설정 창 (SPEC 3.5, 5.4).
/// 왼쪽: 추가된 러너 목록 + 삭제(×) / 새로 만들기
/// 오른쪽: 이름, 요구 사항, 프레임 목록(+/−), 미리보기 + 속도 슬라이더, 저장
/// </summary>
internal sealed class CustomRunnerForm : Form
{
    /// <summary>러너가 저장되거나 삭제됨. (이름, 삭제 여부)</summary>
    public event Action<string, bool>? RunnerChanged;

    private readonly ListBox _list = new();
    private readonly Button _delete = new();
    private readonly Button _new = new();
    private readonly TextBox _name = new();
    private readonly FrameStrip _frames = new();
    private readonly Button _add = new();
    private readonly Button _remove = new();
    private readonly PreviewBox _preview = new();
    private readonly TrackBar _speed = new();
    private readonly Button _save = new();
    private readonly System.Windows.Forms.Timer _previewTimer = new();
    private readonly MenuColors _colors = Theme.AppsAreLight ? MenuColors.Light : MenuColors.Dark;
    private readonly Color _inputBack;
    private readonly Color _deleteActive = Color.FromArgb(196, 43, 28);

    public CustomRunnerForm()
    {
        _inputBack = Theme.AppsAreLight ? Color.White : Color.FromArgb(56, 56, 56);

        Text = "커스텀 러너";
        // 화면 배율(150% 등)에 맞춰 모든 위치·크기를 직접 계산함 (S 함수). Windows 자동 확대는 끔
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.Sizable;  // 크기 조절 가능 (SPEC 5.4, 원본은 고정)
        MaximizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = _colors.Back;
        ForeColor = _colors.Text;

        BuildLayout();
        WireEvents();
        RefreshList(select: null);
        StartNew();
    }

    /// <summary>100% 배율 기준 크기 → 지금 화면 배율의 실제 픽셀 (150%면 1.5배)</summary>
    private int S(int logical) => LogicalToDeviceUnits(logical);

    /// <summary>
    /// 배치. 라벨 폭은 실제 글자 길이를 재서 정하고, 나머지는 위에서 아래로 차례로 쌓음
    /// → 배율이나 글꼴이 달라도 겹치거나 잘리지 않음.
    /// </summary>
    private void BuildLayout()
    {
        int margin = S(14), gap = S(10);
        int rowH = S(32);              // 버튼 높이
        int leftW = S(180);            // 왼쪽 목록 폭
        int fieldW = S(400);           // 오른쪽 입력 영역 폭

        // ── 가로 위치 ──
        int dividerX = margin + leftW + S(14);
        int labelX = dividerX + S(14);
        int captionW = new[] { "러너 이름:", "요구 사항:", "프레임:", "미리보기:" }
            .Max(c => TextRenderer.MeasureText(c, Font).Width);
        int fieldX = labelX + captionW + gap;
        int right = fieldX + fieldW;

        // ── 오른쪽: 위에서 아래로 ──
        int y = margin;
        _name.BorderStyle = BorderStyle.FixedSingle;
        StyleInput(_name);
        _name.SetBounds(fieldX, y, fieldW, _name.Height);
        Controls.Add(_name);
        AddCaption("러너 이름:", labelX, _name);

        y = _name.Bottom + gap;
        var requirement = AddLabel($"포맷: PNG | 최대 크기: {CustomRunnerStore.MaxImageSize}x{CustomRunnerStore.MaxImageSize}px", fieldX, y, dim: true);
        AddCaption("요구 사항:", labelX, requirement);

        y = requirement.Bottom + gap;
        _frames.SetBounds(fieldX, y, fieldW, S(260));
        _frames.BackColor = _inputBack;
        _frames.ForeColor = _colors.Disabled;
        _frames.CellBack = _colors.Back;
        _frames.SelectBack = _colors.Hover;
        _frames.BorderColor = _colors.Border;
        Controls.Add(_frames);
        var framesCaption = AddLabel("프레임:", labelX, y + S(2));

        y = _frames.Bottom + S(6);
        StyleButton(_add, "+", fieldX, y, rowH, rowH, symbol: true);
        StyleButton(_remove, "−", _add.Right + S(6), y, rowH, rowH, symbol: true);

        y = _add.Bottom + gap * 2;
        _preview.SetBounds(fieldX, y, S(80), S(80));
        _preview.BackColor = _inputBack;
        Controls.Add(_preview);
        var previewCaption = AddLabel("미리보기:", labelX, y + S(2));

        // 속도 슬라이더 줄: 미리보기 칸 가운데 높이에 "느림 ───●─── 빠름"
        int sliderH = S(30);
        int sliderTop = _preview.Top + (_preview.Height - sliderH) / 2;
        var slow = AddLabel("느림", _preview.Right + S(16), sliderTop, dim: true);
        var fast = AddLabel("빠름", 0, sliderTop, dim: true);
        fast.Left = right - fast.Width;
        _speed.AutoSize = false;
        _speed.SetBounds(slow.Right + S(8), sliderTop, fast.Left - S(8) - (slow.Right + S(8)), sliderH);
        _speed.Minimum = 1;
        _speed.Maximum = 30;
        _speed.Value = 10;
        _speed.TickStyle = TickStyle.None;
        _speed.BackColor = _colors.Back;
        Controls.Add(_speed);
        slow.Top = fast.Top = sliderTop + (sliderH - slow.Height) / 2;

        // ── 맨 아래 버튼 줄 (왼쪽 ×·새로 만들기, 오른쪽 저장) ──
        int bottomY = _preview.Bottom + S(20);
        StyleButton(_save, "저장", right - S(100), bottomY, S(100), rowH);
        _save.BackColor = Color.FromArgb(47, 95, 178);
        _save.ForeColor = Color.White;
        _save.FlatAppearance.BorderColor = _save.BackColor;
        _save.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 112, 196);

        // ── 왼쪽: 러너 목록 (오른쪽 높이에 맞춤) ──
        var listTitle = AddLabel("추가된 러너 목록:", margin, margin, bold: true);
        int listTop = listTitle.Bottom + S(6);
        _list.BorderStyle = BorderStyle.FixedSingle;
        _list.IntegralHeight = false;  // 정한 높이 그대로 (줄 높이에 맞춰 줄어들지 않게)
        StyleInput(_list);
        _list.SetBounds(margin, listTop, leftW, bottomY - gap - listTop);
        Controls.Add(_list);

        StyleButton(_delete, "×", margin, bottomY, rowH, rowH, symbol: true);
        _delete.Enabled = false;
        StyleButton(_new, "새로 만들기", _delete.Right + S(6), bottomY, leftW - rowH - S(6), rowH);

        var divider = new Panel { BackColor = _colors.Border };
        divider.SetBounds(dividerX, margin, 1, bottomY + rowH - margin);
        Controls.Add(divider);

        ClientSize = new Size(right + margin, bottomY + rowH + margin);
        MinimumSize = Size;   // 처음 크기보다 작게는 못 줄임 (줄이면 겹침)

        // ── 창 크기를 바꿀 때 따라 움직이는 방식 (창 크기를 정한 뒤에 지정해야 위치가 안 틀어짐) ──
        const AnchorStyles Top = AnchorStyles.Top, Bottom = AnchorStyles.Bottom,
                           Left = AnchorStyles.Left, Right = AnchorStyles.Right;
        _list.Anchor = Top | Bottom | Left;                 // 세로로 늘어남
        divider.Anchor = Top | Bottom | Left;
        _delete.Anchor = _new.Anchor = Bottom | Left;       // 창 아래에 붙음
        _name.Anchor = Top | Left | Right;                  // 가로로 늘어남
        _frames.Anchor = Top | Bottom | Left | Right;       // 가로·세로로 늘어남
        framesCaption.Anchor = Top | Left;
        _add.Anchor = _remove.Anchor = Bottom | Left;
        _preview.Anchor = previewCaption.Anchor = slow.Anchor = Bottom | Left;
        _speed.Anchor = Bottom | Left | Right;              // 가로로 늘어남
        fast.Anchor = Bottom | Right;
        _save.Anchor = Bottom | Right;                      // 오른쪽 아래에 붙음
    }

    private void WireEvents()
    {
        _list.SelectedIndexChanged += (_, _) => OnListSelectionChanged();
        _delete.Click += (_, _) => DeleteSelected();
        _new.Click += (_, _) => { _list.ClearSelected(); StartNew(); };
        _add.Click += (_, _) => AddFrames();
        _remove.Click += (_, _) => RemoveFrame();
        _frames.FilesDropped += files => AddFrameFiles(files);
        _frames.Reordered += () => _preview.Invalidate();
        _save.Click += (_, _) => Save();

        _speed.ValueChanged += (_, _) => _previewTimer.Interval = 1000 / _speed.Value;
        _previewTimer.Interval = 1000 / _speed.Value;
        _previewTimer.Tick += (_, _) => _preview.Next();
        _previewTimer.Start();

        _preview.Frames = _frames.Images;
    }

    // ── 목록 ──

    private void RefreshList(string? select)
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var profile in CustomRunnerStore.LoadProfiles()) _list.Items.Add(profile.Name);
        _list.EndUpdate();
        if (select is not null) _list.SelectedItem = select;
    }

    private void OnListSelectionChanged()
    {
        bool selected = _list.SelectedItem is string;
        _delete.Enabled = selected;
        _delete.BackColor = selected ? _deleteActive : _inputBack; // 선택하면 빨갛게 (SPEC 3.5)
        if (_list.SelectedItem is string name) LoadRunner(name);
    }

    private void StartNew()
    {
        _name.Text = "";
        _frames.SetImages([]);
        _preview.Invalidate();
    }

    private void LoadRunner(string name)
    {
        _name.Text = name;
        _frames.SetImages(CustomRunnerStore.LoadFrames(name));
        _preview.Invalidate();
    }

    private void DeleteSelected()
    {
        if (_list.SelectedItem is not string name) return;
        var answer = MessageBox.Show(this, $"'{name}' 러너를 삭제할까요?", "커스텀 러너",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;

        CustomRunnerStore.Delete(name);
        RefreshList(select: null);
        StartNew();
        RunnerChanged?.Invoke(name, true);
    }

    // ── 프레임 ──

    private void AddFrames()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "프레임 이미지 추가",
            Filter = "PNG 이미지 (*.png)|*.png",
            Multiselect = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        AddFrameFiles(dialog.FileNames);
    }

    /// <summary>+ 버튼과 끌어다 놓기 공통: 파일 이름 순서로 확인 후 맨 뒤에 추가 (SPEC 3.5, 5.4)</summary>
    private void AddFrameFiles(IEnumerable<string> paths)
    {
        var problems = new List<string>();
        foreach (var path in paths.Order(StringComparer.CurrentCultureIgnoreCase))
        {
            string? problem = CustomRunnerStore.Validate(path, out var bitmap);
            if (problem is null && bitmap is not null) _frames.Add(bitmap);
            else problems.Add($"{Path.GetFileName(path)}: {problem}");
        }
        _preview.Invalidate();

        if (problems.Count > 0)
        {
            MessageBox.Show(this,
                "다음 파일은 추가하지 않았어요.\n\n" + string.Join("\n", problems),
                "커스텀 러너", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void RemoveFrame()
    {
        _frames.RemoveSelected();
        _preview.Invalidate();
    }

    // ── 저장 ──

    private void Save()
    {
        string name = _name.Text.Trim();
        string? problem =
            name.Length == 0 ? "러너 이름을 입력해 주세요." :
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name is "." or ".." ? "이름에 쓸 수 없는 글자가 있어요. (\\ / : * ? \" < > | 등)" :
            Sprites.Names.Contains(name) ? "기본 캐릭터와 같은 이름은 쓸 수 없어요." :
            _frames.Images.Count == 0 ? "프레임을 1장 이상 추가해 주세요." :
            null;
        if (problem is not null)
        {
            MessageBox.Show(this, problem, "커스텀 러너", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            CustomRunnerStore.Save(name, _frames.Images);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"저장하지 못했어요.\n{ex.Message}", "커스텀 러너", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        RefreshList(select: name);   // 저장 후 목록에 추가되고 선택된 상태 (SPEC 3.5)
        RunnerChanged?.Invoke(name, false);
    }

    // ── 모양 ──

    private Label AddLabel(string text, int x, int y, bool bold = false, bool dim = false)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            Location = new Point(x, y),
            ForeColor = dim ? _colors.Disabled : _colors.Text,
            BackColor = Color.Transparent,
        };
        if (bold) label.Font = new Font(Font, FontStyle.Bold);
        label.Size = label.PreferredSize;   // 위치 계산에 바로 쓸 수 있게 크기 확정
        Controls.Add(label);
        return label;
    }

    /// <summary>입력칸 왼쪽 라벨. 입력칸과 세로 가운데를 맞춤</summary>
    private void AddCaption(string text, int x, Control target)
    {
        var label = AddLabel(text, x, 0);
        label.Top = target.Top + (target.Height - label.Height) / 2;
    }

    private void StyleInput(Control control)
    {
        control.BackColor = _inputBack;
        control.ForeColor = _colors.Text;
    }

    /// <summary>평평한 버튼. symbol이면 × + − 같은 기호를 크고 가운데에 보이게 기호용 글꼴 사용</summary>
    private void StyleButton(Button button, string text, int x, int y, int w, int h, bool symbol = false)
    {
        button.Text = text;
        button.SetBounds(x, y, w, h);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = _colors.Border;
        button.BackColor = _inputBack;
        button.ForeColor = _colors.Text;
        button.Padding = Padding.Empty;
        button.TextAlign = ContentAlignment.MiddleCenter;
        if (symbol) button.Font = new Font("Segoe UI", Font.SizeInPoints + 3, FontStyle.Regular);
        Controls.Add(button);
    }

    // 창 제목 표시줄도 어두운 모드면 어둡게 (Windows 10 2004 이상)
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!Theme.AppsAreLight)
        {
            int on = 1;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
        }
    }

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _previewTimer.Dispose();
            _frames.SetImages([]);
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// 프레임 썸네일 목록. 각 칸 아래에 0부터 번호, 클릭하면 선택 (SPEC 3.5).
/// PNG 파일을 끌어다 놓으면 FilesDropped, 썸네일을 끌어서 옮기면 순서가 바뀌고 Reordered (SPEC 5.4).
/// </summary>
internal sealed class FrameStrip : Control
{
    // 크기는 100% 배율 기준 → 지금 화면 배율에 맞춰 키움
    private int Box => LogicalToDeviceUnits(48);
    private int LabelHeight => LogicalToDeviceUnits(20);
    private int Gap => LogicalToDeviceUnits(8);
    private int StrideX => Box + Gap + Gap;           // 칸 너비 + 칸 사이
    private int StrideY => Box + LabelHeight + Gap;

    /// <summary>탐색기에서 파일을 끌어다 놓음</summary>
    public event Action<string[]>? FilesDropped;
    /// <summary>썸네일을 끌어서 순서를 바꿈</summary>
    public event Action? Reordered;

    private int _pressIndex = -1;   // 누른 썸네일
    private Point _pressPoint;
    private bool _dragging;
    private int _dropIndex = -1;    // 놓일 자리 (0 ~ 개수)

    public List<Bitmap> Images { get; } = [];
    public int SelectedIndex { get; private set; } = -1;
    // 코드로만 정하는 색 (화면 디자이너에 저장하지 않음)
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color CellBack { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color SelectBack { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; }

    public FrameStrip()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        AllowDrop = true;
    }

    /// <summary>목록 교체. 이전 그림은 메모리에서 해제</summary>
    public void SetImages(List<Bitmap> images)
    {
        foreach (var old in Images) old.Dispose();
        Images.Clear();
        Images.AddRange(images);
        SelectedIndex = -1;
        Invalidate();
    }

    public void Add(Bitmap image)
    {
        Images.Add(image);
        Invalidate();
    }

    public void RemoveSelected()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Images.Count) return;
        Images[SelectedIndex].Dispose();
        Images.RemoveAt(SelectedIndex);
        SelectedIndex = Math.Min(SelectedIndex, Images.Count - 1);
        Invalidate();
    }

    private int PerRow => Math.Max(1, (Width - Gap) / StrideX);

    private Rectangle CellBounds(int index)
    {
        int row = index / PerRow, col = index % PerRow;
        return new Rectangle(Gap + col * StrideX, Gap + row * StrideY, Box + Gap, Box + LabelHeight);
    }

    /// <summary>마우스 위치에서 썸네일이 놓일 자리 (0 ~ 개수)</summary>
    private int InsertionIndex(Point p)
    {
        int rows = Math.Max(1, (Images.Count + PerRow - 1) / PerRow);
        int row = Math.Clamp((p.Y - Gap) / StrideY, 0, rows - 1);
        int col = Math.Clamp((int)Math.Round((p.X - Gap) / (double)StrideX), 0, PerRow);
        return Math.Clamp(row * PerRow + col, 0, Images.Count);
    }

    private void MoveFrame(int from, int insertAt)
    {
        int to = insertAt > from ? insertAt - 1 : insertAt;
        if (to == from) return;
        var image = Images[from];
        Images.RemoveAt(from);
        Images.Insert(to, image);
        SelectedIndex = to;
        Reordered?.Invoke();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        for (int i = 0; i < Images.Count; i++)
        {
            var cell = CellBounds(i);
            if (i == SelectedIndex)
            {
                using var select = new SolidBrush(SelectBack);
                g.FillRectangle(select, cell);
            }
            var box = new Rectangle(cell.X + Gap / 2, cell.Y + 2, Box, Box);
            using (var back = new SolidBrush(CellBack)) g.FillRectangle(back, box);
            Frames.DrawScaled(g, Images[i], box);
            TextRenderer.DrawText(g, i.ToString(), Font,
                new Rectangle(cell.X, box.Bottom, cell.Width, LabelHeight), ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        // 끄는 중: 놓일 자리에 세로선
        if (_dragging && _dropIndex >= 0)
        {
            var at = _dropIndex < Images.Count ? CellBounds(_dropIndex) : CellBounds(Images.Count - 1);
            int x = _dropIndex < Images.Count ? at.Left - Gap / 2 : at.Right + Gap / 2;
            using var marker = new Pen(Color.DodgerBlue, 3);
            g.DrawLine(marker, x, at.Top, x, at.Bottom);
        }

        // 비어 있으면 안내 문구 (SPEC 5.4)
        if (Images.Count == 0)
        {
            TextRenderer.DrawText(g, "PNG 파일을 여기로 끌어다 놓거나\n아래 + 버튼을 누르세요", Font,
                ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        using var border = new Pen(BorderColor);
        g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
    }

    // ── 클릭 선택 + 끌어서 순서 바꾸기 ──

    protected override void OnMouseDown(MouseEventArgs e)
    {
        SelectedIndex = -1;
        for (int i = 0; i < Images.Count; i++)
        {
            if (CellBounds(i).Contains(e.Location)) { SelectedIndex = i; break; }
        }
        _pressIndex = e.Button == MouseButtons.Left ? SelectedIndex : -1;
        _pressPoint = e.Location;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_pressIndex >= 0 && e.Button == MouseButtons.Left)
        {
            var drag = SystemInformation.DragSize;
            if (!_dragging && (Math.Abs(e.X - _pressPoint.X) > drag.Width || Math.Abs(e.Y - _pressPoint.Y) > drag.Height))
                _dragging = true;
            if (_dragging)
            {
                _dropIndex = InsertionIndex(e.Location);
                Cursor = Cursors.SizeAll;
                Invalidate();
            }
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_dragging && _pressIndex >= 0 && _dropIndex >= 0) MoveFrame(_pressIndex, _dropIndex);
        _pressIndex = -1;
        _dragging = false;
        _dropIndex = -1;
        Cursor = Cursors.Default;
        Invalidate();
        base.OnMouseUp(e);
    }

    // ── 탐색기에서 파일 끌어다 놓기 ──

    protected override void OnDragEnter(DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        base.OnDragEnter(e);
    }

    protected override void OnDragDrop(DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            FilesDropped?.Invoke(files);
        base.OnDragDrop(e);
    }
}

/// <summary>미리보기 칸: 프레임을 순서대로 재생 (SPEC 3.5)</summary>
internal sealed class PreviewBox : Control
{
    private int _index;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<Bitmap> Frames { get; set; } = [];

    public PreviewBox() => DoubleBuffered = true;

    public void Next()
    {
        if (Frames.Count == 0) return;
        _index = (_index + 1) % Frames.Count;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Frames.Count == 0) return;
        var box = new Rectangle(8, 8, Width - 16, Height - 16);
        PcAngerManager.Frames.DrawScaled(e.Graphics, Frames[_index % Frames.Count], box);
    }
}
