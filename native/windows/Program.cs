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
    private const int WindowWidth = 200;
    private const int WindowHeight = 155;
    private readonly Bitmap _rest;
    private readonly Bitmap _blink;
    private readonly Bitmap _walk;
    private readonly Bitmap _sideLook;
    private readonly Bitmap _sideLie;
    private readonly Bitmap _groom;
    private readonly Bitmap _surface = new(WindowWidth, WindowHeight, PixelFormat.Format32bppArgb);
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly Random _random = new();
    private readonly NotifyIcon _tray;
    private Point _dragOffset;
    private bool _dragging;
    private bool _quiet;
    private bool _walking;
    private Point _walkTarget;
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
        _rest = new Bitmap(Path.Combine(assets, "mochi-rest.png"));
        _blink = new Bitmap(Path.Combine(assets, "mochi-blink.png"));
        _walk = new Bitmap(Path.Combine(assets, "mochi-walk.png"));
        _sideLook = new Bitmap(Path.Combine(assets, "mochi-side-look.png"));
        _sideLie = new Bitmap(Path.Combine(assets, "mochi-side-lie.png"));
        _groom = new Bitmap(Path.Combine(assets, "mochi-groom.png"));
        _nextWalk = DateTime.UtcNow.AddSeconds(40 + _random.Next(41));
        _nextBlink = DateTime.UtcNow.AddSeconds(4 + _random.Next(5));
        _nextMicroAction = DateTime.UtcNow.AddSeconds(18 + _random.Next(18));

        var menu = new ContextMenuStrip();
        var normal = new ToolStripMenuItem("普通模式", null, (_, _) => SetQuiet(false)) { Checked = true };
        var quiet = new ToolStripMenuItem("安静模式", null, (_, _) => SetQuiet(true));
        menu.Items.Add(normal);
        menu.Items.Add(quiet);
        var actions = new ToolStripMenuItem("做个动作");
        actions.DropDownItems.Add("侧头看看", null, (_, _) => StartMicroAction(MicroAction.SideLook));
        actions.DropDownItems.Add("躺一会", null, (_, _) => StartMicroAction(MicroAction.SideLie));
        actions.DropDownItems.Add("舔舔爪", null, (_, _) => StartMicroAction(MicroAction.Groom));
        menu.Items.Add(actions);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Close());
        menu.Opening += (_, _) => { normal.Checked = !_quiet; quiet.Checked = _quiet; };
        ContextMenuStrip = menu;
        _tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "Mochi", Visible = true, ContextMenuStrip = menu };
        _tray.DoubleClick += (_, _) => Show();

        _timer.Interval = 250; // idle is event-driven; no continuous 60 FPS renderer
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        RenderSurface();
    }

    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x00080000; return cp; } // WS_EX_LAYERED
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
    }

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
            _walking = true;
            _walkFrame = 0;
            _nextWalkFrame = now;
            _timer.Interval = 33;
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
    }

    private void RenderSurface()
    {
        using (var g = Graphics.FromImage(_surface))
        {
            g.Clear(Color.Transparent);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            if (_walking)
            {
                var frameWidth = _walk.Width / 4;
                g.DrawImage(_walk, new Rectangle(17, 12, 165, 131), _walkFrame * frameWidth, 120, frameWidth, 430, GraphicsUnit.Pixel);
            }
            else if (_microAction == MicroAction.Groom)
            {
                var frameWidth = _groom.Width / 3;
                // Grooming is deliberately a little smaller than the resting pose;
                // its raised paw must not make Mochi visually "pop" larger.
                g.DrawImage(_groom, new Rectangle(32, 28, 135, 100), _groomFrame * frameWidth, 100, frameWidth, 540, GraphicsUnit.Pixel);
            }
            else
            {
                var pose = _microAction == MicroAction.SideLook ? _sideLook : _microAction == MicroAction.SideLie ? _sideLie : _blinkEnds > DateTime.UtcNow ? _blink : _rest;
                g.DrawImage(pose, new Rectangle(10, 18, 180, 120), 0, 0, pose.Width, pose.Height, GraphicsUnit.Pixel);
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
    protected override void OnFormClosed(FormClosedEventArgs e) { _timer.Dispose(); _tray.Dispose(); _rest.Dispose(); _blink.Dispose(); _walk.Dispose(); _sideLook.Dispose(); _sideLie.Dispose(); _groom.Dispose(); _surface.Dispose(); base.OnFormClosed(e); }

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
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
}
