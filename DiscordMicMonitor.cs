using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace DiscordMicMonitor
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            bool createdNew;
            using (new Mutex(true, "DiscordMicMonitor_SingleInstance", out createdNew))
            {
                if (!createdNew) return;
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
                {
                    LogError(e.Exception);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
                {
                    LogError(e.ExceptionObject as Exception);
                };
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MonitorForm());
            }
        }

        public static void LogError(Exception ex)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DiscordMicMonitor");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "error.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + ex + Environment.NewLine);
            }
            catch (Exception) { }
        }
    }

    public enum MicState { Disconnected, Unmuted, Muted }

    public class MonitorForm : Form
    {
        private const int BaseSize = 68;   // design size at 100% scale
        private static readonly int[] ScaleOptions = { 50, 100, 125, 150, 200 };

        private readonly DiscordRpc _rpc;
        private readonly ToolTip _tip = new ToolTip();
        private ToolStripMenuItem _scaleMenu;
        private int _scalePercent = 100;
        private MicState _state = MicState.Disconnected;
        private Point _dragOffset;
        private Point _downScreen;
        private bool _mouseDown;
        private bool _dragged;

        public MonitorForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            ClientSize = new Size(68, 68);
            Text = "Discord Mic Monitor";

            _rpc = new DiscordRpc();
            _rpc.StateChanged += OnRpcState;

            var menu = new ContextMenuStrip();
            _scaleMenu = new ToolStripMenuItem("Scale");
            foreach (int pct in ScaleOptions)
            {
                var item = new ToolStripMenuItem(pct + "%");
                int chosen = pct;
                item.Click += delegate
                {
                    try { ApplyScale(chosen, true); }
                    catch (Exception ex) { Program.LogError(ex); }
                };
                _scaleMenu.DropDownItems.Add(item);
            }
            menu.Items.Add(_scaleMenu);
            menu.Items.Add("Re-authorize", null, delegate
            {
                try
                {
                    Config.Token = null;
                    Config.Save();
                    _rpc.Reconnect();
                }
                catch (Exception ex) { Program.LogError(ex); }
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { Close(); });
            ContextMenuStrip = menu;

            Config.Load();
            ApplyScale(Config.Scale, false);
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int x = Config.X, y = Config.Y;
            if (x == int.MinValue || y == int.MinValue ||
                x < wa.Left - 10 || y < wa.Top - 10 || x > wa.Right - 20 || y > wa.Bottom - 20)
            {
                x = wa.Right - Width - 16;
                y = wa.Bottom - Height - 16;
            }
            Location = new Point(x, y);
            UpdateTip();

            _rpc.Start();
        }

        private void OnRpcState(MicState state)
        {
            if (IsDisposed) return;
            try
            {
                BeginInvoke((Action)delegate
                {
                    if (_state == state) return;
                    _state = state;
                    UpdateTip();
                    Render();
                });
            }
            catch (Exception) { }
        }

        private void ApplyScale(int pct, bool save)
        {
            bool valid = false;
            foreach (int s in ScaleOptions)
                if (s == pct) { valid = true; break; }
            if (!valid) pct = 100;

            _scalePercent = pct;
            int size = (int)Math.Round(BaseSize * pct / 100.0);
            Size = new Size(size, size);

            foreach (ToolStripItem it in _scaleMenu.DropDownItems)
            {
                var mi = it as ToolStripMenuItem;
                if (mi != null) mi.Checked = mi.Text == pct + "%";
            }

            Render();
            if (save)
            {
                Config.Scale = pct;
                Config.Save();
            }
        }

        private void UpdateTip()
        {
            string text;
            switch (_state)
            {
                case MicState.Unmuted: text = "Discord: unmuted — click to mute"; break;
                case MicState.Muted: text = "Discord: MUTED — click to unmute"; break;
                default: text = "Discord not connected"; break;
            }
            _tip.SetToolTip(this, text);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _mouseDown = true;
                _dragged = false;
                _downScreen = Cursor.Position;
                _dragOffset = new Point(Cursor.Position.X - Left, Cursor.Position.Y - Top);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_mouseDown)
            {
                Point p = Cursor.Position;
                if (!_dragged && (Math.Abs(p.X - _downScreen.X) > 4 || Math.Abs(p.Y - _downScreen.Y) > 4))
                    _dragged = true;
                if (_dragged)
                    Location = new Point(p.X - _dragOffset.X, p.Y - _dragOffset.Y);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _mouseDown)
            {
                _mouseDown = false;
                if (_dragged)
                {
                    Config.X = Left;
                    Config.Y = Top;
                    Config.Save();
                }
                else
                {
                    _rpc.ToggleMute();
                }
            }
            base.OnMouseUp(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _rpc.Stop();
            base.OnFormClosed(e);
        }

        // Per-pixel alpha layered window: smooth rounded corners + soft shadow.
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80000;  // WS_EX_LAYERED
                cp.ExStyle |= 0x80;     // WS_EX_TOOLWINDOW (keep out of Alt-Tab)
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Render();
        }

        private void Render()
        {
            if (!IsHandleCreated || IsDisposed) return;
            using (var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.ScaleTransform(_scalePercent / 100f, _scalePercent / 100f);
                    DrawCard(g);
                }
                PushLayeredBitmap(bmp);
            }
        }

        private void DrawCard(Graphics g)
        {
            var card = new Rectangle(8, 7, 52, 52);
            Color cardBg = Color.FromArgb(244, 30, 31, 34);   // near-opaque dark card

            // soft drop shadow
            for (int i = 5; i >= 1; i--)
            {
                var r = new Rectangle(card.X - i, card.Y - i + 2, card.Width + i * 2, card.Height + i * 2);
                using (GraphicsPath sp = RoundedRect(r, 14 + i))
                using (var sb = new SolidBrush(Color.FromArgb(9 + (5 - i) * 5, 0, 0, 0)))
                    g.FillPath(sb, sp);
            }

            using (GraphicsPath path = RoundedRect(card, 14))
            {
                using (var b = new SolidBrush(cardBg))
                    g.FillPath(b, path);
                using (var border = new Pen(Color.FromArgb(30, 255, 255, 255), 1f))
                    g.DrawPath(border, path);
            }

            Color micColor, dotColor;
            switch (_state)
            {
                case MicState.Unmuted:
                    micColor = Color.White;
                    dotColor = Color.FromArgb(35, 165, 90);    // green
                    break;
                case MicState.Muted:
                    micColor = Color.FromArgb(242, 63, 67);    // red
                    dotColor = Color.FromArgb(242, 63, 67);
                    break;
                default:
                    micColor = Color.FromArgb(128, 132, 142);  // gray
                    dotColor = Color.FromArgb(128, 132, 142);
                    break;
            }

            using (var pen = new Pen(micColor, 2.6f))
            using (var fill = new SolidBrush(micColor))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                // capsule body
                using (var capsule = new GraphicsPath())
                {
                    capsule.AddArc(28, 18, 12, 12, 180, 180);
                    capsule.AddArc(28, 25, 12, 12, 0, 180);
                    capsule.CloseFigure();
                    g.FillPath(fill, capsule);
                }
                // cradle arc, stem, base
                g.DrawArc(pen, 23, 22, 22, 20, 20, 140);
                g.DrawLine(pen, 34, 42, 34, 47);
                g.DrawLine(pen, 28, 48, 40, 48);
            }

            if (_state == MicState.Muted)
            {
                using (var gap = new Pen(Color.FromArgb(30, 31, 34), 7f))
                using (var slash = new Pen(micColor, 3f))
                {
                    gap.StartCap = LineCap.Round; gap.EndCap = LineCap.Round;
                    slash.StartCap = LineCap.Round; slash.EndCap = LineCap.Round;
                    g.DrawLine(gap, 23, 17, 45, 49);
                    g.DrawLine(slash, 23, 17, 45, 49);
                }
            }

            // status dot, bottom-right, with a card-colored ring
            var dot = new Rectangle(card.Right - 19, card.Bottom - 19, 13, 13);
            var ring = new Rectangle(dot.X - 2, dot.Y - 2, dot.Width + 4, dot.Height + 4);
            using (var rb = new SolidBrush(Color.FromArgb(30, 31, 34)))
                g.FillEllipse(rb, ring);
            using (var db = new SolidBrush(dotColor))
                g.FillEllipse(db, dot);
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void PushLayeredBitmap(Bitmap bitmap)
        {
            IntPtr screenDc = NativeMethods.GetDC(IntPtr.Zero);
            IntPtr memDc = NativeMethods.CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;
            try
            {
                hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                oldBitmap = NativeMethods.SelectObject(memDc, hBitmap);
                var size = new NativeMethods.SIZE(bitmap.Width, bitmap.Height);
                var src = new NativeMethods.POINT(0, 0);
                var topPos = new NativeMethods.POINT(Left, Top);
                var blend = new NativeMethods.BLENDFUNCTION();
                blend.BlendOp = 0;              // AC_SRC_OVER
                blend.BlendFlags = 0;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = 1;          // AC_SRC_ALPHA
                NativeMethods.UpdateLayeredWindow(Handle, screenDc, ref topPos, ref size,
                    memDc, ref src, 0, ref blend, 2 /* ULW_ALPHA */);
            }
            finally
            {
                NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
                if (hBitmap != IntPtr.Zero)
                {
                    NativeMethods.SelectObject(memDc, oldBitmap);
                    NativeMethods.DeleteObject(hBitmap);
                }
                NativeMethods.DeleteDC(memDc);
            }
        }
    }

    internal static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X, Y;
            public POINT(int x, int y) { X = x; Y = y; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE
        {
            public int Cx, Cy;
            public SIZE(int cx, int cy) { Cx = cx; Cy = cy; }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
        }

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
            ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc,
            int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        public static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        public static extern bool DeleteObject(IntPtr hObject);
    }

    public class DiscordRpc
    {
        // Discord's own StreamKit Overlay application. Using it means no dev-portal
        // app or client secret is needed: the local client hands us an OAuth code and
        // streamkit.discord.com exchanges it for an RPC access token.
        private const string ClientId = "207646673902501888";
        private const string TokenEndpoint = "https://streamkit.discord.com/overlay/token";

        public event Action<MicState> StateChanged;

        private NamedPipeClientStream _pipe;
        private readonly object _writeLock = new object();
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
        private volatile bool _running = true;
        private volatile bool _reconnectRequested;
        private volatile bool _ready;
        private bool _mute;
        private bool _deaf;
        private int _nonce;

        public void Start()
        {
            var t = new Thread(RunLoop);
            t.IsBackground = true;
            t.Start();
        }

        public void Stop()
        {
            _running = false;
            ClosePipe();
        }

        public void Reconnect()
        {
            _reconnectRequested = true;
            ClosePipe();
        }

        public void ToggleMute()
        {
            if (!_ready) return;
            var args = new Dictionary<string, object>();
            if (_deaf)
            {
                args["deaf"] = false;
                args["mute"] = false;
            }
            else
            {
                args["mute"] = !_mute;
            }
            // Never touch the pipe from the UI thread.
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { SendCommand("SET_VOICE_SETTINGS", args, null); }
                catch (Exception) { }
            });
        }

        private void RunLoop()
        {
            while (_running)
            {
                _reconnectRequested = false;
                try { Session(); }
                catch (Exception) { }
                _ready = false;
                Fire(MicState.Disconnected);
                ClosePipe();
                for (int i = 0; i < 30 && _running && !_reconnectRequested; i++)
                    Thread.Sleep(100);
            }
        }

        private void Session()
        {
            _pipe = null;
            for (int i = 0; i < 10 && _pipe == null; i++)
            {
                // PipeOptions.Asynchronous is required: on a synchronous pipe handle
                // Windows serializes all I/O, so the reader thread's blocking Read
                // would block any Write from other threads (UI freeze on click).
                var p = new NamedPipeClientStream(".", "discord-ipc-" + i, PipeDirection.InOut, PipeOptions.Asynchronous);
                try { p.Connect(200); _pipe = p; }
                catch (Exception) { p.Dispose(); }
            }
            if (_pipe == null) return;

            SendFrame(0, _json.Serialize(new Dictionary<string, object> { { "v", 1 }, { "client_id", ClientId } }));

            while (_running)
            {
                int op;
                string payload;
                if (!ReadFrame(out op, out payload)) return;
                if (op == 3) { SendFrame(4, payload); continue; }  // ping -> pong
                if (op == 2) return;                               // close
                if (op != 0 && op != 1) continue;
                var msg = _json.DeserializeObject(payload) as Dictionary<string, object>;
                if (msg != null) Handle(msg);
            }
        }

        private void Handle(Dictionary<string, object> msg)
        {
            string cmd = GetStr(msg, "cmd");
            string evt = GetStr(msg, "evt");
            object dataObj;
            msg.TryGetValue("data", out dataObj);
            var data = dataObj as Dictionary<string, object>;

            if (evt == "ERROR")
                Program.LogError(new Exception("Discord RPC error, cmd=" + cmd + ": "
                    + (data != null ? GetStr(data, "message") : "(no message)")));

            if (cmd == "DISPATCH" && evt == "READY")
            {
                if (!string.IsNullOrEmpty(Config.Token)) Authenticate(Config.Token);
                else Authorize();
            }
            else if (cmd == "AUTHORIZE")
            {
                if (evt == "ERROR") throw new Exception("authorize rejected");
                string code = data != null ? GetStr(data, "code") : null;
                string token = ExchangeCode(code);
                Config.Token = token;
                Config.Save();
                Authenticate(token);
            }
            else if (cmd == "AUTHENTICATE")
            {
                if (evt == "ERROR")
                {
                    // cached token expired/revoked: fall back to a fresh authorize
                    Config.Token = null;
                    Config.Save();
                    Authorize();
                    return;
                }
                SendCommand("SUBSCRIBE", new Dictionary<string, object>(), "VOICE_SETTINGS_UPDATE");
                SendCommand("GET_VOICE_SETTINGS", new Dictionary<string, object>(), null);
            }
            else if (cmd == "GET_VOICE_SETTINGS" && evt != "ERROR" && data != null)
            {
                _ready = true;
                UpdateVoice(data);
            }
            else if (cmd == "SET_VOICE_SETTINGS" && evt != "ERROR" && data != null)
            {
                UpdateVoice(data);
            }
            else if (cmd == "DISPATCH" && evt == "VOICE_SETTINGS_UPDATE" && data != null)
            {
                UpdateVoice(data);
            }
        }

        private void Authorize()
        {
            var args = new Dictionary<string, object>
            {
                { "client_id", ClientId },
                { "scopes", new object[] { "rpc" } },
                { "prompt", "none" }
            };
            SendCommand("AUTHORIZE", args, null);
        }

        private void Authenticate(string token)
        {
            SendCommand("AUTHENTICATE", new Dictionary<string, object> { { "access_token", token } }, null);
        }

        private void UpdateVoice(Dictionary<string, object> data)
        {
            object v;
            if (data.TryGetValue("mute", out v) && v is bool) _mute = (bool)v;
            if (data.TryGetValue("deaf", out v) && v is bool) _deaf = (bool)v;
            Fire((_mute || _deaf) ? MicState.Muted : MicState.Unmuted);
        }

        private void Fire(MicState state)
        {
            Action<MicState> h = StateChanged;
            if (h != null) h(state);
        }

        private string ExchangeCode(string code)
        {
            if (string.IsNullOrEmpty(code)) throw new Exception("no auth code");
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var req = (HttpWebRequest)WebRequest.Create(TokenEndpoint);
            req.Method = "POST";
            req.ContentType = "application/json";
            byte[] body = Encoding.UTF8.GetBytes(_json.Serialize(new Dictionary<string, object> { { "code", code } }));
            req.ContentLength = body.Length;
            using (Stream s = req.GetRequestStream())
                s.Write(body, 0, body.Length);
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var r = new StreamReader(resp.GetResponseStream()))
            {
                var obj = _json.DeserializeObject(r.ReadToEnd()) as Dictionary<string, object>;
                string token = obj != null ? GetStr(obj, "access_token") : null;
                if (string.IsNullOrEmpty(token)) throw new Exception("token exchange failed");
                return token;
            }
        }

        private void SendCommand(string cmd, Dictionary<string, object> args, string evt)
        {
            var d = new Dictionary<string, object>
            {
                { "cmd", cmd },
                { "args", args },
                { "nonce", Interlocked.Increment(ref _nonce).ToString() }
            };
            if (evt != null) d["evt"] = evt;
            SendFrame(1, _json.Serialize(d));
        }

        private void SendFrame(int op, string payload)
        {
            NamedPipeClientStream p = _pipe;
            if (p == null) return;
            byte[] data = Encoding.UTF8.GetBytes(payload);
            byte[] buf = new byte[8 + data.Length];
            WriteInt(buf, 0, op);
            WriteInt(buf, 4, data.Length);
            Buffer.BlockCopy(data, 0, buf, 8, data.Length);
            lock (_writeLock)
            {
                p.Write(buf, 0, buf.Length);
                p.Flush();
            }
        }

        private bool ReadFrame(out int op, out string payload)
        {
            op = 0;
            payload = null;
            byte[] header = new byte[8];
            if (!ReadExact(header, 8)) return false;
            op = ReadInt(header, 0);
            int len = ReadInt(header, 4);
            if (len < 0 || len > 8 * 1024 * 1024) return false;
            byte[] data = new byte[len];
            if (!ReadExact(data, len)) return false;
            payload = Encoding.UTF8.GetString(data);
            return true;
        }

        private bool ReadExact(byte[] buf, int len)
        {
            NamedPipeClientStream p = _pipe;
            if (p == null) return false;
            int off = 0;
            while (off < len)
            {
                int n;
                try { n = p.Read(buf, off, len - off); }
                catch (Exception) { return false; }
                if (n <= 0) return false;
                off += n;
            }
            return true;
        }

        private static void WriteInt(byte[] b, int o, int v)
        {
            b[o] = (byte)v;
            b[o + 1] = (byte)(v >> 8);
            b[o + 2] = (byte)(v >> 16);
            b[o + 3] = (byte)(v >> 24);
        }

        private static int ReadInt(byte[] b, int o)
        {
            return b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24);
        }

        private static string GetStr(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) ? v as string : null;
        }

        private void ClosePipe()
        {
            NamedPipeClientStream p = _pipe;
            _pipe = null;
            if (p != null)
            {
                try { p.Dispose(); }
                catch (Exception) { }
            }
        }
    }

    public static class Config
    {
        public static string Token;
        public static int X = int.MinValue;
        public static int Y = int.MinValue;
        public static int Scale = 100;

        private static readonly object Lock = new object();

        private static string FilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DiscordMicMonitor", "config.txt");
            }
        }

        public static void Load()
        {
            lock (Lock)
            {
                try
                {
                    if (!File.Exists(FilePath)) return;
                    foreach (string line in File.ReadAllLines(FilePath))
                    {
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        string key = line.Substring(0, eq);
                        string val = line.Substring(eq + 1);
                        int n;
                        if (key == "token") Token = val;
                        else if (key == "x" && int.TryParse(val, out n)) X = n;
                        else if (key == "y" && int.TryParse(val, out n)) Y = n;
                        else if (key == "scale" && int.TryParse(val, out n)) Scale = n;
                    }
                }
                catch (Exception) { }
            }
        }

        public static void Save()
        {
            lock (Lock)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    var lines = new List<string>();
                    if (!string.IsNullOrEmpty(Token)) lines.Add("token=" + Token);
                    if (X != int.MinValue) lines.Add("x=" + X);
                    if (Y != int.MinValue) lines.Add("y=" + Y);
                    if (Scale != 100) lines.Add("scale=" + Scale);
                    File.WriteAllLines(FilePath, lines.ToArray());
                }
                catch (Exception) { }
            }
        }
    }
}
