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
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(700, 540);
        FormBorderStyle = FormBorderStyle.FixedSingle;  // 크기 고정 (SPEC 3.5)
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = _colors.Back;
        ForeColor = _colors.Text;

        BuildLayout();
        WireEvents();
        RefreshList(select: null);
        StartNew();
    }

    private void BuildLayout()
    {
        // ── 왼쪽: 러너 목록 ──
        AddLabel("추가된 러너 목록:", 12, 12, bold: true);
        _list.SetBounds(12, 38, 170, 446);
        _list.BorderStyle = BorderStyle.FixedSingle;
        StyleInput(_list);
        Controls.Add(_list);

        StyleButton(_delete, "×", 12, 492, 34, 30);
        _delete.Enabled = false;
        StyleButton(_new, "새로 만들기", 52, 492, 130, 30);

        var divider = new Panel { BackColor = _colors.Border };
        divider.SetBounds(196, 12, 1, 516);
        Controls.Add(divider);

        // ── 오른쪽: 편집 ──
        AddLabel("러너 이름:", 212, 16);
        _name.SetBounds(300, 12, 386, 26);
        _name.BorderStyle = BorderStyle.FixedSingle;
        StyleInput(_name);
        Controls.Add(_name);

        AddLabel("요구 사항:", 212, 50);
        AddLabel($"포맷: PNG | 최대 크기: {CustomRunnerStore.MaxImageSize}x{CustomRunnerStore.MaxImageSize}px", 300, 50, dim: true);

        AddLabel("프레임:", 212, 82);
        _frames.SetBounds(300, 80, 386, 276);
        _frames.BackColor = _inputBack;
        _frames.ForeColor = _colors.Disabled;
        _frames.CellBack = _colors.Back;
        _frames.SelectBack = _colors.Hover;
        _frames.BorderColor = _colors.Border;
        Controls.Add(_frames);

        StyleButton(_add, "+", 300, 362, 34, 28);
        StyleButton(_remove, "−", 340, 362, 34, 28);

        AddLabel("미리보기:", 212, 412);
        _preview.SetBounds(300, 408, 80, 80);
        _preview.BackColor = _inputBack;
        Controls.Add(_preview);

        AddLabel("느림", 396, 438, dim: true);
        _speed.SetBounds(436, 426, 200, 45);
        _speed.Minimum = 1;
        _speed.Maximum = 30;
        _speed.Value = 10;
        _speed.TickStyle = TickStyle.None;
        _speed.BackColor = _colors.Back;
        Controls.Add(_speed);
        AddLabel("빠름", 642, 438, dim: true);

        StyleButton(_save, "저장", 586, 492, 100, 34);
        _save.BackColor = Color.FromArgb(47, 95, 178);
        _save.ForeColor = Color.White;
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

    private void AddLabel(string text, int x, int y, bool bold = false, bool dim = false)
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
        Controls.Add(label);
    }

    private void StyleInput(Control control)
    {
        control.BackColor = _inputBack;
        control.ForeColor = _colors.Text;
    }

    private void StyleButton(Button button, string text, int x, int y, int w, int h)
    {
        button.Text = text;
        button.SetBounds(x, y, w, h);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = _colors.Border;
        button.BackColor = _inputBack;
        button.ForeColor = _colors.Text;
        Controls.Add(button);
    }

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
    private const int Box = 48;
    private const int LabelHeight = 18;
    private const int Gap = 8;
    private const int StrideX = Box + Gap + Gap;           // 칸 너비 + 칸 사이
    private const int StrideY = Box + LabelHeight + Gap;

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
