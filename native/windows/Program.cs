using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Mochi.Native;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MochiForm(args.Contains("--niangao", StringComparer.OrdinalIgnoreCase)));
    }
}

internal sealed class MochiForm : Form
{
    private enum MicroAction { None, SideLook, SideLie, Groom }
    private enum PetKind { Mochi, Niangao }
    private const int WindowWidth = 200;
    private const int WindowHeight = 155;
    private readonly string _assetsPath;
    private PetAssets _pet;
    private PetKind _petKind;
    private readonly Bitmap _surface = new(WindowWidth, WindowHeight, PixelFormat.Format32bppArgb);
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly Random _random = new();
    private readonly NotifyIcon _tray;
    private readonly Icon _appIcon;
    // This timer exists only while a context menu is open. It lets the
    // non-activating layered sprite dismiss its menu on a desktop click.
    private readonly System.Windows.Forms.Timer _menuMonitorTimer = new() { Interval = 100 };
    private ContextMenuStrip? _menu;
    private ToolStripMenuItem? _menuStatus;
    private ToolStripMenuItem? _normalMenuItem;
    private ToolStripMenuItem? _quietMenuItem;
    private ToolStripMenuItem? _menuTitle;
    private ToolStripMenuItem? _mochiMenuItem;
    private ToolStripMenuItem? _niangaoMenuItem;
    private int _menuButtonsDown;
    private Point _dragOffset;
    private bool _dragging;
    private bool _quiet;
    private bool _walking;
    private Point _walkTarget;
    private bool _facingRight;
    private int _walkFrame;
    private long _nextWalkFrameMs;
    private long _nextWalkMs;
    private long _nextBlinkMs;
    private long _blinkStartedMs;
    private long _blinkEndsMs;
    private MicroAction _microAction;
    private long _nextMicroActionMs;
    private long _microActionEndsMs;
    private long _nextGroomFrameMs;
    private int _groomFrame;

    public MochiForm(bool startWithNiangao = false)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(WindowWidth, WindowHeight);
        var area = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(area.Right - WindowWidth - 30, area.Bottom - WindowHeight - 30);

        _assetsPath = Path.Combine(AppContext.BaseDirectory, "assets", "cats");
        _appIcon = new Icon(Path.Combine(AppContext.BaseDirectory, "assets", "icons", "mochi.ico"));
        Icon = _appIcon;
        _petKind = startWithNiangao ? PetKind.Niangao : PetKind.Mochi;
        _pet = new PetAssets(_assetsPath, startWithNiangao ? "niangao" : "mochi");
        var now = PetTiming.NowMs;
        _nextWalkMs = now + PetTiming.WalkDelay(_random);
        _nextBlinkMs = now + PetTiming.InitialBlinkDelay(_random);
        _nextMicroActionMs = now + PetTiming.MicroDelay(_random);

        var menu = CreateMenu();
        ContextMenuStrip = menu;
        _tray = new NotifyIcon { Icon = _appIcon, Text = startWithNiangao ? "Niangao · 年糕" : "Mochi · 麻薯", Visible = true, ContextMenuStrip = menu };
        _tray.DoubleClick += (_, _) => Show();

        _timer.Interval = PetTiming.IdleTickMs; // idle is event-driven; no continuous 60 FPS renderer
        _timer.Tick += (_, _) => Tick();
        _menuMonitorTimer.Tick += (_, _) => MonitorOpenMenu();
        _timer.Start();
        RenderSurface();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            // Layered transparent sprite; TOOLWINDOW removes it from Alt+Tab and
            // Win+Tab, while NOACTIVATE keeps it out of normal app switching.
            cp.ExStyle |= 0x00080000 | 0x00000080 | 0x08000000;
            return cp;
        }
    }

    private void SetQuiet(bool quiet)
    {
        _quiet = quiet;
        _walking = false;
        _microAction = MicroAction.None;
        _blinkEndsMs = 0;
        var now = PetTiming.NowMs;
        _nextWalkMs = now + PetTiming.WalkDelay(_random);
        _nextMicroActionMs = now + PetTiming.MicroDelay(_random);
        _timer.Interval = PetTiming.IdleTickMs;
        RenderSurface();
        RefreshMenuState();
    }

    private ContextMenuStrip CreateMenu()
    {
        var menu = new ContextMenuStrip
        {
            Renderer = new MochiMenuRenderer(),
            ShowImageMargin = false,
            ShowCheckMargin = true,
            Font = new Font("Microsoft YaHei UI", 9F),
            Padding = new Padding(7, 7, 7, 7),
        };
        var title = new ToolStripMenuItem("🐾  麻薯  ·  Mochi") { Enabled = false, AutoSize = false, Size = new Size(244, 30) };
        var status = new ToolStripMenuItem { Enabled = false, AutoSize = false, Size = new Size(244, 26) };
        var normal = new ToolStripMenuItem("✨  普通模式", null, (_, _) => SetQuiet(false));
        var quiet = new ToolStripMenuItem("🌙  安静模式", null, (_, _) => SetQuiet(true));
        var pets = new ToolStripMenuItem("🐱  切换桌宠");
        var mochi = new ToolStripMenuItem("🐾  麻薯", null, (_, _) => SelectPet(PetKind.Mochi));
        var niangao = new ToolStripMenuItem("🐾  年糕", null, (_, _) => SelectPet(PetKind.Niangao));
        pets.DropDownItems.Add(mochi);
        pets.DropDownItems.Add(niangao);
        var actions = new ToolStripMenuItem("✦  做个动作");
        actions.DropDownItems.Add("👀  侧头看看", null, (_, _) => StartMicroAction(MicroAction.SideLook));
        actions.DropDownItems.Add("☁  躺一会", null, (_, _) => StartMicroAction(MicroAction.SideLie));
        actions.DropDownItems.Add("✦  舔舔爪", null, (_, _) => StartMicroAction(MicroAction.Groom));
        var quit = new ToolStripMenuItem("退出 Mochi", null, (_, _) => Close());
        _menu = menu;
        _menuStatus = status;
        _normalMenuItem = normal;
        _quietMenuItem = quiet;
        _menuTitle = title;
        _mochiMenuItem = mochi;
        _niangaoMenuItem = niangao;
        menu.Items.Add(title);
        menu.Items.Add(status);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(normal);
        menu.Items.Add(quiet);
        menu.Items.Add(pets);
        menu.Items.Add(actions);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(quit);
        menu.Opening += (_, _) =>
        {
            RefreshMenuState(force: true);
            _menuButtonsDown = PressedMouseButtons();
            _menuMonitorTimer.Start();
        };
        menu.Closed += (_, _) => _menuMonitorTimer.Stop();
        return menu;
    }

    // State is pushed only at state transitions; there is no polling just to
    // update a label. The short-lived monitor above is solely for dismissing
    // a non-activating popup after an outside click.
    private void RefreshMenuState(bool force = false)
    {
        if (_menuStatus == null || _normalMenuItem == null || _quietMenuItem == null || (!force && _menu?.Visible != true)) return;
        var text = _quiet ? "   当前状态：安静休息" : _walking ? "   当前状态：散步中" : _microAction == MicroAction.None ? "   当前状态：自在发呆" : "   当前状态：" + ActionLabel(_microAction);
        if (_menuStatus.Text != text) _menuStatus.Text = text;
        _normalMenuItem.Checked = !_quiet;
        _quietMenuItem.Checked = _quiet;
        if (_menuTitle != null) _menuTitle.Text = _petKind == PetKind.Mochi ? "🐾  麻薯  ·  Mochi" : "🐾  年糕  ·  Niangao";
        if (_mochiMenuItem != null) _mochiMenuItem.Checked = _petKind == PetKind.Mochi;
        if (_niangaoMenuItem != null) _niangaoMenuItem.Checked = _petKind == PetKind.Niangao;
    }

    private void SelectPet(PetKind kind)
    {
        if (_petKind == kind) return;
        var replacement = new PetAssets(_assetsPath, kind == PetKind.Mochi ? "mochi" : "niangao");
        _pet.Dispose();
        _petKind = kind;
        _pet = replacement;
        // Pet selection is visual only. Keeping the shared deadlines and active
        // state guarantees that switching cats cannot accelerate the schedule.
        _tray.Text = kind == PetKind.Mochi ? "Mochi · 麻薯" : "Niangao · 年糕";
        RenderSurface();
        RefreshMenuState(force: true);
    }

    private void MonitorOpenMenu()
    {
        if (_menu?.Visible != true) { _menuMonitorTimer.Stop(); return; }
        var buttons = PressedMouseButtons();
        if ((buttons & ~_menuButtonsDown) != 0 && !PointerIsInOpenMenu(Cursor.Position)) _menu.Close();
        _menuButtonsDown = buttons;
    }

    private bool PointerIsInOpenMenu(Point point)
    {
        if (_menu?.Bounds.Contains(point) == true) return true;
        return _menu?.Items.OfType<ToolStripMenuItem>().Any(item => item.DropDown.Visible && item.DropDown.Bounds.Contains(point)) == true;
    }

    private static int PressedMouseButtons()
    {
        const int VK_LBUTTON = 0x01, VK_RBUTTON = 0x02, VK_MBUTTON = 0x04;
        var result = 0;
        if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0) result |= VK_LBUTTON;
        if ((GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0) result |= VK_RBUTTON;
        if ((GetAsyncKeyState(VK_MBUTTON) & 0x8000) != 0) result |= VK_MBUTTON;
        return result;
    }

    private static string ActionLabel(MicroAction action) => action switch
    {
        MicroAction.SideLook => "侧头看看",
        MicroAction.SideLie => "躺一会",
        MicroAction.Groom => "舔舔爪",
        _ => "自在发呆",
    };

    private void Tick()
    {
        var now = PetTiming.NowMs;
        var changed = false;
        if (!_quiet && !_walking && _microAction == MicroAction.None && now >= _nextWalkMs)
        {
            var area = Screen.FromControl(this).WorkingArea;
            _walkTarget = new Point(
                Math.Clamp(Left + _random.Next(-70, 71), area.Left, area.Right - Width),
                Math.Clamp(Top + _random.Next(-45, 46), area.Top, area.Bottom - Height));
            _facingRight = _walkTarget.X > Left;
            _walking = true;
            _walkFrame = 0;
            _nextWalkFrameMs = now + PetTiming.WalkFrameMs;
            _timer.Interval = PetTiming.WalkTickMs;
            RefreshMenuState();
            changed = true;
        }
        if (_walking)
        {
            if (now >= _nextWalkFrameMs) { _walkFrame = (_walkFrame + 1) % _pet.WalkFrames.Length; _nextWalkFrameMs = now + PetTiming.WalkFrameMs; }
            var next = new Point(MoveTowards(Left, _walkTarget.X, 3), MoveTowards(Top, _walkTarget.Y, 2));
            Location = next;
            changed = true;
            if (next == _walkTarget)
            {
                _walking = false;
                _walkFrame = 0;
                _nextWalkMs = now + PetTiming.WalkDelay(_random);
                _nextMicroActionMs = PetTiming.PostponeIfDue(_nextMicroActionMs, now, PetTiming.MicroDelay(_random));
                _nextBlinkMs = PetTiming.PostponeIfDue(_nextBlinkMs, now, PetTiming.BlinkDelay(_random));
                _timer.Interval = PetTiming.IdleTickMs;
                RefreshMenuState();
            }
        }
        if (!_quiet && !_walking && _microAction == MicroAction.None && now >= _nextMicroActionMs)
        {
            StartMicroAction((MicroAction)_random.Next(1, 4));
            changed = true;
        }
        if (_microAction != MicroAction.None)
        {
            if (_microAction == MicroAction.Groom && now >= _nextGroomFrameMs)
            {
                _groomFrame = (_groomFrame + 1) % _pet.GroomFrames.Length;
                _nextGroomFrameMs = now + PetTiming.GroomFrameMs;
                changed = true;
            }
            if (now >= _microActionEndsMs)
            {
                _microAction = MicroAction.None;
                _nextMicroActionMs = now + PetTiming.MicroDelay(_random);
                _nextWalkMs = PetTiming.PostponeIfDue(_nextWalkMs, now, PetTiming.WalkDelay(_random));
                _nextBlinkMs = PetTiming.PostponeIfDue(_nextBlinkMs, now, PetTiming.BlinkDelay(_random));
                _timer.Interval = PetTiming.IdleTickMs;
                RefreshMenuState();
                changed = true;
            }
        }
        if (!_quiet && !_walking && _microAction == MicroAction.None && now >= _nextBlinkMs && now >= _blinkEndsMs)
        {
            _blinkStartedMs = now;
            _blinkEndsMs = now + PetTiming.BlinkDurationMs;
            _nextBlinkMs = now + PetTiming.BlinkDelay(_random);
            _timer.Interval = PetTiming.BlinkTickMs;
            changed = true;
        }
        if (_blinkEndsMs > now)
        {
            changed = true;
        }
        else if (_blinkEndsMs != 0)
        {
            _blinkEndsMs = 0;
            _timer.Interval = _walking ? PetTiming.WalkTickMs : PetTiming.IdleTickMs;
            changed = true;
        }
        if (changed) RenderSurface();
    }

    private static int MoveTowards(int value, int target, int step) => value < target ? Math.Min(value + step, target) : Math.Max(value - step, target);

    private void StartMicroAction(MicroAction action)
    {
        if (_quiet || _dragging) return;
        var now = PetTiming.NowMs;
        if (_walking) _nextWalkMs = now + PetTiming.WalkDelay(_random);
        _walking = false;
        _blinkEndsMs = 0;
        _microAction = action;
        _microActionEndsMs = now + PetTiming.ActionDurationMs((int)action);
        _groomFrame = 0;
        _nextGroomFrameMs = now + PetTiming.GroomFrameMs;
        _timer.Interval = action == MicroAction.Groom ? 80 : PetTiming.IdleTickMs;
        RenderSurface();
        RefreshMenuState();
    }

    private void RenderSurface()
    {
        using (var g = Graphics.FromImage(_surface))
        {
            g.Clear(Color.Transparent);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            if (_walking)
            {
                if (_facingRight)
                {
                    g.TranslateTransform(WindowWidth, 0);
                    g.ScaleTransform(-1, 1);
                }
                var frame = _pet.WalkFrames[_walkFrame % _pet.WalkFrames.Length];
                g.DrawImage(frame, _pet.WalkBounds, 0, 0, frame.Width, frame.Height, GraphicsUnit.Pixel);
                if (_facingRight) g.ResetTransform();
            }
            else if (_microAction == MicroAction.Groom)
            {
                var frame = _pet.GroomFrames[_groomFrame % _pet.GroomFrames.Length];
                g.DrawImage(frame, _pet.GroomBounds, 0, 0, frame.Width, frame.Height, GraphicsUnit.Pixel);
            }
            else
            {
                var pose = _microAction == MicroAction.SideLook ? _pet.SideLook : _microAction == MicroAction.SideLie ? _pet.SideLie : _pet.Rest;
                g.DrawImage(pose, _pet.PoseBounds, 0, 0, pose.Width, pose.Height, GraphicsUnit.Pixel);
                var now = PetTiming.NowMs;
                if (_microAction == MicroAction.None && _blinkEndsMs > now)
                {
                    var elapsed = now - _blinkStartedMs;
                    var remaining = _blinkEndsMs - now;
                    var blink = elapsed < PetTiming.BlinkHalfPhaseMs || remaining < PetTiming.BlinkHalfPhaseMs ? _pet.BlinkHalf : _pet.Blink;
                    using var eyes = new GraphicsPath();
                    foreach (var eye in _pet.BlinkEyeClips) eyes.AddEllipse(eye);
                    var saved = g.Save();
                    g.SetClip(eyes);
                    g.DrawImage(blink, _pet.PoseBounds, 0, 0, blink.Width, blink.Height, GraphicsUnit.Pixel);
                    g.Restore(saved);
                }
            }
        }
        PresentLayered();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left) { _dragging = true; _dragOffset = e.Location; _walking = false; _microAction = MicroAction.None; _blinkEndsMs = 0; _timer.Interval = PetTiming.IdleTickMs; RenderSurface(); }
        if (e.Button == MouseButtons.Right) ContextMenuStrip?.Show(this, e.Location);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging && e.Button == MouseButtons.Left)
            Location = new Point(Cursor.Position.X - _dragOffset.X, Cursor.Position.Y - _dragOffset.Y);
    }

    protected override void OnMouseUp(MouseEventArgs e) { _dragging = false; base.OnMouseUp(e); }
    protected override void OnFormClosed(FormClosedEventArgs e) { _timer.Dispose(); _menuMonitorTimer.Dispose(); _tray.Dispose(); _appIcon.Dispose(); _pet.Dispose(); _surface.Dispose(); base.OnFormClosed(e); }

    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x0084, HTTRANSPARENT = -1;
        if (m.Msg == WM_NCHITTEST)
        {
            var p = PointToClient(Cursor.Position);
            if (p.X < 0 || p.Y < 0 || p.X >= Width || p.Y >= Height || _surface.GetPixel(p.X, p.Y).A < 24) { m.Result = (IntPtr)HTTRANSPARENT; return; }
        }
        base.WndProc(ref m);
    }

    private void PresentLayered()
    {
        var screenDc = GetDC(IntPtr.Zero);
        var memoryDc = CreateCompatibleDC(screenDc);
        var hBitmap = _surface.GetHbitmap(Color.FromArgb(0));
        var old = SelectObject(memoryDc, hBitmap);
        var size = new SIZE(Width, Height);
        var source = new POINT(0, 0);
        var destination = new POINT(Left, Top);
        var blend = new BLENDFUNCTION(0, 0, 255, 1);
        UpdateLayeredWindow(Handle, screenDc, ref destination, ref size, memoryDc, ref source, 0, ref blend, 2);
        SelectObject(memoryDc, old); DeleteObject(hBitmap); DeleteDC(memoryDc); ReleaseDC(IntPtr.Zero, screenDc);
    }

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)] private struct SIZE { public int cx, cy; public SIZE(int x, int y) { cx = x; cy = y; } }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; public BLENDFUNCTION(byte op, byte flags, byte alpha, byte format) { BlendOp = op; BlendFlags = flags; SourceConstantAlpha = alpha; AlphaFormat = format; } }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION blend, int flags);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
}

internal sealed class PetAssets : IDisposable
{
    public Bitmap Rest { get; }
    public Bitmap BlinkHalf { get; }
    public Bitmap Blink { get; }
    public Bitmap[] WalkFrames { get; }
    public Bitmap SideLook { get; }
    public Bitmap SideLie { get; }
    public Bitmap[] GroomFrames { get; }
    public Rectangle PoseBounds { get; }
    public Rectangle WalkBounds { get; }
    public Rectangle GroomBounds { get; }
    public RectangleF[] BlinkEyeClips { get; }

    public PetAssets(string assets, string name)
    {
        Rest = new Bitmap(Path.Combine(assets, $"{name}-rest.png"));
        Blink = new Bitmap(Path.Combine(assets, $"{name}-blink.png"));
        SideLook = new Bitmap(Path.Combine(assets, $"{name}-side-look.png"));
        SideLie = new Bitmap(Path.Combine(assets, $"{name}-side-lie.png"));
        PoseBounds = new Rectangle(10, 18, 180, 120);
        if (name == "niangao")
        {
            BlinkHalf = new Bitmap(Path.Combine(assets, "niangao-blink-half.png"));
            WalkFrames = LoadFrames(assets, name, "walk", 4);
            GroomFrames = LoadFrames(assets, name, "groom", 3);
            WalkBounds = PoseBounds;
            GroomBounds = PoseBounds;
            BlinkEyeClips = [new RectangleF(32, 70, 16, 13), new RectangleF(55, 70, 17, 13)];
        }
        else
        {
            BlinkHalf = new Bitmap(Blink);
            using var walkSheet = new Bitmap(Path.Combine(assets, "mochi-walk.png"));
            using var groomSheet = new Bitmap(Path.Combine(assets, "mochi-groom.png"));
            WalkFrames = SliceFrames(walkSheet, 4, 120, 430);
            GroomFrames = SliceFrames(groomSheet, 3, 100, 540);
            WalkBounds = new Rectangle(17, 12, 165, 131);
            GroomBounds = new Rectangle(32, 28, 135, 100);
            BlinkEyeClips = [new RectangleF(46, 54, 14, 12), new RectangleF(69, 56, 14, 12)];
        }
    }

    private static Bitmap[] LoadFrames(string assets, string name, string action, int count) =>
        Enumerable.Range(1, count).Select(index => new Bitmap(Path.Combine(assets, $"{name}-{action}-{index:00}.png"))).ToArray();

    private static Bitmap[] SliceFrames(Bitmap sheet, int count, int sourceY, int sourceHeight)
    {
        var width = sheet.Width / count;
        return Enumerable.Range(0, count).Select(index => sheet.Clone(new Rectangle(index * width, sourceY, width, sourceHeight), PixelFormat.Format32bppArgb)).ToArray();
    }

    public void Dispose()
    {
        Rest.Dispose(); BlinkHalf.Dispose(); Blink.Dispose(); SideLook.Dispose(); SideLie.Dispose();
        foreach (var frame in WalkFrames) frame.Dispose();
        foreach (var frame in GroomFrames) frame.Dispose();
    }
}

internal sealed class MochiMenuRenderer : ToolStripProfessionalRenderer
{
    public MochiMenuRenderer() : base(new MochiMenuColorTable()) { }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Color.FromArgb(48, 45, 61) : Color.FromArgb(132, 126, 146);
        base.OnRenderItemText(e);
    }
}

internal sealed class MochiMenuColorTable : ProfessionalColorTable
{
    private static readonly Color Surface = Color.FromArgb(255, 252, 255);
    public override Color ToolStripDropDownBackground => Surface;
    public override Color MenuBorder => Color.FromArgb(220, 211, 231);
    public override Color MenuItemSelected => Color.FromArgb(239, 233, 249);
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(244, 239, 252);
    public override Color MenuItemSelectedGradientEnd => Color.FromArgb(235, 229, 247);
    public override Color MenuItemBorder => Color.FromArgb(206, 192, 226);
    public override Color MenuItemPressedGradientBegin => Color.FromArgb(231, 223, 244);
    public override Color MenuItemPressedGradientEnd => Color.FromArgb(231, 223, 244);
    public override Color SeparatorDark => Color.FromArgb(227, 220, 235);
    public override Color SeparatorLight => Surface;
    public override Color CheckBackground => Color.FromArgb(220, 210, 239);
    public override Color CheckSelectedBackground => Color.FromArgb(205, 191, 232);
    public override Color ImageMarginGradientBegin => Surface;
    public override Color ImageMarginGradientMiddle => Surface;
    public override Color ImageMarginGradientEnd => Surface;
}
