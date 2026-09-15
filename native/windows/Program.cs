using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Mochi.Native;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MochiForm());
    }
}

internal sealed class MochiForm : Form
{
    private enum MicroAction { None, SideLook, SideLie, Groom }
    private enum PetKind { Mochi, Niangao }
    private const int WindowWidth = 200;
    private const int WindowHeight = 155;
    private readonly PetAssets _mochi;
    private readonly PetAssets _niangao;
    private PetAssets _pet;
    private PetKind _petKind = PetKind.Mochi;
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
    private DateTime _nextWalkFrame;
    private DateTime _nextWalk;
    private DateTime _nextBlink;
    private DateTime _blinkEnds;
    private MicroAction _microAction;
    private DateTime _nextMicroAction;
    private DateTime _microActionEnds;
    private DateTime _nextGroomFrame;
    private int _groomFrame;

    public MochiForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(WindowWidth, WindowHeight);
        var area = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(area.Right - WindowWidth - 30, area.Bottom - WindowHeight - 30);

        var assets = Path.Combine(AppContext.BaseDirectory, "assets", "cats");
        _appIcon = new Icon(Path.Combine(AppContext.BaseDirectory, "assets", "icons", "mochi.ico"));
        Icon = _appIcon;
        _mochi = new PetAssets(assets, "mochi");
        _niangao = new PetAssets(assets, "niangao");
        _pet = _mochi;
        _nextWalk = DateTime.UtcNow.AddSeconds(40 + _random.Next(41));
        _nextBlink = DateTime.UtcNow.AddSeconds(4 + _random.Next(5));
        _nextMicroAction = DateTime.UtcNow.AddSeconds(18 + _random.Next(18));

        var menu = CreateMenu();
        ContextMenuStrip = menu;
        _tray = new NotifyIcon { Icon = _appIcon, Text = "Mochi", Visible = true, ContextMenuStrip = menu };
        _tray.DoubleClick += (_, _) => Show();

        _timer.Interval = 250; // idle is event-driven; no continuous 60 FPS renderer
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
        _blinkEnds = default;
        _nextWalk = DateTime.UtcNow.AddSeconds(40 + _random.Next(41));
        _timer.Interval = 250;
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
        _petKind = kind;
        _pet = kind == PetKind.Mochi ? _mochi : _niangao;
        _walking = false;
        _microAction = MicroAction.None;
        _blinkEnds = default;
        var now = DateTime.UtcNow;
        _nextWalk = now.AddSeconds(40 + _random.Next(41));
        _nextMicroAction = now.AddSeconds(18 + _random.Next(18));
        _nextBlink = now.AddSeconds(4 + _random.Next(5));
        _timer.Interval = 250;
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
        var now = DateTime.UtcNow;
        var changed = false;
        if (!_quiet && !_walking && _microAction == MicroAction.None && now >= _nextWalk)
        {
            var area = Screen.FromControl(this).WorkingArea;
            _walkTarget = new Point(
                Math.Clamp(Left + _random.Next(-70, 71), area.Left, area.Right - Width),
                Math.Clamp(Top + _random.Next(-45, 46), area.Top, area.Bottom - Height));
            _facingRight = _walkTarget.X > Left;
            _walking = true;
            _walkFrame = 0;
            _nextWalkFrame = now;
            _timer.Interval = 33;
            RefreshMenuState();
            changed = true;
        }
        if (_walking)
        {
            if (now >= _nextWalkFrame) { _walkFrame = (_walkFrame + 1) % 4; _nextWalkFrame = now.AddMilliseconds(110); }
            var next = new Point(MoveTowards(Left, _walkTarget.X, 3), MoveTowards(Top, _walkTarget.Y, 2));
            Location = next;
            changed = true;
            if (next == _walkTarget)
            {
                _walking = false;
                _walkFrame = 0;
                _nextWalk = now.AddSeconds(40 + _random.Next(41));
                _timer.Interval = 250;
                RefreshMenuState();
            }
        }
        if (!_quiet && !_walking && _microAction == MicroAction.None && now >= _nextMicroAction)
        {
            StartMicroAction((MicroAction)_random.Next(1, 4));
            changed = true;
        }
        if (_microAction != MicroAction.None)
        {
            if (_microAction == MicroAction.Groom && now >= _nextGroomFrame)
            {
                _groomFrame = (_groomFrame + 1) % 3;
                _nextGroomFrame = now.AddMilliseconds(220);
                changed = true;
            }
            if (now >= _microActionEnds)
            {
                _microAction = MicroAction.None;
                _nextMicroAction = now.AddSeconds(18 + _random.Next(18));
                _timer.Interval = 250;
                RefreshMenuState();
                changed = true;
            }
        }
        if (!_walking && _microAction == MicroAction.None && now >= _nextBlink && now >= _blinkEnds)
        {
            _blinkEnds = now.AddMilliseconds(220);
            _nextBlink = now.AddSeconds(7 + _random.Next(6));
            _timer.Interval = 40;
            changed = true;
        }
        if (_blinkEnds > now)
        {
            changed = true;
        }
        else if (_blinkEnds != default)
        {
            _blinkEnds = default;
            _timer.Interval = _walking ? 33 : 250;
            changed = true;
        }
        if (changed) RenderSurface();
    }

    private static int MoveTowards(int value, int target, int step) => value < target ? Math.Min(value + step, target) : Math.Max(value - step, target);

    private void StartMicroAction(MicroAction action)
    {
        if (_quiet || _dragging) return;
        _walking = false;
        _blinkEnds = default;
        _microAction = action;
        var now = DateTime.UtcNow;
        _microActionEnds = now.AddSeconds(action == MicroAction.SideLook ? 3 : action == MicroAction.SideLie ? 7 : 5);
        _groomFrame = 0;
        _nextGroomFrame = now;
        _timer.Interval = action == MicroAction.Groom ? 80 : 250;
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
                var frameWidth = _pet.Walk.Width / 4;
                if (_facingRight)
                {
                    g.TranslateTransform(WindowWidth, 0);
                    g.ScaleTransform(-1, 1);
                }
                g.DrawImage(_pet.Walk, new Rectangle(17, 12, 165, 131), _walkFrame * frameWidth, _pet.WalkSourceY, frameWidth, _pet.WalkSourceHeight, GraphicsUnit.Pixel);
                if (_facingRight) g.ResetTransform();
            }
            else if (_microAction == MicroAction.Groom)
            {
                var frameWidth = _pet.Groom.Width / 3;
                // Grooming is deliberately a little smaller than the resting pose;
                // its raised paw must not make Mochi visually "pop" larger.
                g.DrawImage(_pet.Groom, new Rectangle(32, 28, 135, 100), _groomFrame * frameWidth, _pet.GroomSourceY, frameWidth, _pet.GroomSourceHeight, GraphicsUnit.Pixel);
            }
            else
            {
                var pose = _microAction == MicroAction.SideLook ? _pet.SideLook : _microAction == MicroAction.SideLie ? _pet.SideLie : _pet.Rest;
                g.DrawImage(pose, new Rectangle(10, 18, 180, 120), 0, 0, pose.Width, pose.Height, GraphicsUnit.Pixel);
                if (_microAction == MicroAction.None && _blinkEnds > DateTime.UtcNow)
                {
                    if (_pet.UseEyeClip)
                    {
                        // Mochi's generated closed-eye pose has a slightly different body silhouette.
                        // Restrict it to two oval eye regions so Mochi's back cannot "pop".
                        using var eyes = new GraphicsPath();
                        eyes.AddEllipse(46, 54, 14, 12);
                        eyes.AddEllipse(69, 56, 14, 12);
                        var saved = g.Save();
                        g.SetClip(eyes);
                        g.DrawImage(_pet.Blink, new Rectangle(10, 18, 180, 120), 0, 0, _pet.Blink.Width, _pet.Blink.Height, GraphicsUnit.Pixel);
                        g.Restore(saved);
                    }
                    else
                    {
                        g.DrawImage(_pet.Blink, new Rectangle(10, 18, 180, 120), 0, 0, _pet.Blink.Width, _pet.Blink.Height, GraphicsUnit.Pixel);
                    }
                }
            }
        }
        PresentLayered();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left) { _dragging = true; _dragOffset = e.Location; _walking = false; _microAction = MicroAction.None; _blinkEnds = default; _timer.Interval = 250; RenderSurface(); }
        if (e.Button == MouseButtons.Right) ContextMenuStrip?.Show(this, e.Location);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging && e.Button == MouseButtons.Left)
            Location = new Point(Cursor.Position.X - _dragOffset.X, Cursor.Position.Y - _dragOffset.Y);
    }

    protected override void OnMouseUp(MouseEventArgs e) { _dragging = false; base.OnMouseUp(e); }
    protected override void OnFormClosed(FormClosedEventArgs e) { _timer.Dispose(); _menuMonitorTimer.Dispose(); _tray.Dispose(); _appIcon.Dispose(); _mochi.Dispose(); _niangao.Dispose(); _surface.Dispose(); base.OnFormClosed(e); }

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
    public Bitmap Blink { get; }
    public Bitmap Walk { get; }
    public Bitmap SideLook { get; }
    public Bitmap SideLie { get; }
    public Bitmap Groom { get; }
    public int WalkSourceY { get; }
    public int WalkSourceHeight { get; }
    public int GroomSourceY { get; }
    public int GroomSourceHeight { get; }
    public bool UseEyeClip { get; }

    public PetAssets(string assets, string name)
    {
        Rest = new Bitmap(Path.Combine(assets, $"{name}-rest.png"));
        Blink = new Bitmap(Path.Combine(assets, $"{name}-blink.png"));
        Walk = new Bitmap(Path.Combine(assets, $"{name}-walk.png"));
        SideLook = new Bitmap(Path.Combine(assets, $"{name}-side-look.png"));
        SideLie = new Bitmap(Path.Combine(assets, $"{name}-side-lie.png"));
        Groom = new Bitmap(Path.Combine(assets, $"{name}-groom.png"));
        var generatedSheet = name == "niangao";
        WalkSourceY = generatedSheet ? 0 : 120;
        WalkSourceHeight = generatedSheet ? Walk.Height : 430;
        GroomSourceY = generatedSheet ? 0 : 100;
        GroomSourceHeight = generatedSheet ? Groom.Height : 540;
        UseEyeClip = !generatedSheet;
    }

    public void Dispose()
    {
        Rest.Dispose(); Blink.Dispose(); Walk.Dispose(); SideLook.Dispose(); SideLie.Dispose(); Groom.Dispose();
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
