using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Suarakata
{
    /// <summary>Penyimpanan preferensi sederhana di %AppData%\Suarakata\settings.cfg</summary>
    internal static class Settings
    {
        private static readonly Dictionary<string, string> Map =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static string Dir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Suarakata");
            }
        }

        private static string FilePath { get { return Path.Combine(Dir, "settings.cfg"); } }

        static Settings()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    foreach (var line in File.ReadAllLines(FilePath))
                    {
                        int i = line.IndexOf('=');
                        if (i > 0) Map[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                    }
                }
            }
            catch { /* abaikan */ }
        }

        public static string Get(string key, string fallback = null)
        {
            string v;
            return Map.TryGetValue(key, out v) ? v : fallback;
        }

        public static int GetInt(string key, int fallback)
        {
            int v;
            return int.TryParse(Get(key), out v) ? v : fallback;
        }

        public static bool GetBool(string key, bool fallback)
        {
            string v = Get(key);
            if (v == null) return fallback;
            return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public static void Set(string key, string value)
        {
            Map[key] = value ?? string.Empty;
            try
            {
                Directory.CreateDirectory(Dir);
                var sb = new StringBuilder();
                foreach (var kv in Map) sb.AppendLine(kv.Key + "=" + kv.Value);
                File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
            }
            catch { /* abaikan */ }
        }

        public static void Set(string key, bool value) { Set(key, value ? "1" : "0"); }
        public static void Set(string key, int value) { Set(key, value.ToString()); }
    }

    internal enum ThemeMode { Light, Dark }

    /// <summary>Palet warna aplikasi, mendukung mode terang dan gelap.</summary>
    internal static class Theme
    {
        private static ThemeMode _mode =
            string.Equals(Settings.Get("theme", "light"), "dark", StringComparison.OrdinalIgnoreCase)
                ? ThemeMode.Dark : ThemeMode.Light;

        public static event Action Changed;

        public static ThemeMode Mode
        {
            get { return _mode; }
            set
            {
                if (_mode == value) return;
                _mode = value;
                Settings.Set("theme", value == ThemeMode.Dark ? "dark" : "light");
                var h = Changed;
                if (h != null) h();
            }
        }

        public static bool IsDark { get { return _mode == ThemeMode.Dark; } }
        public static void Toggle() { Mode = IsDark ? ThemeMode.Light : ThemeMode.Dark; }

        private static Color C(int rgb)
        {
            return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        public static Color Bg { get { return IsDark ? C(0x0F1116) : C(0xF4F5F7); } }
        public static Color Card { get { return IsDark ? C(0x181B22) : C(0xFFFFFF); } }
        public static Color Field { get { return IsDark ? C(0x1F232B) : C(0xFFFFFF); } }
        public static Color Border { get { return IsDark ? C(0x2C313C) : C(0xE3E6EA); } }
        public static Color Text { get { return IsDark ? C(0xE6E8EC) : C(0x111827); } }
        public static Color Muted { get { return IsDark ? C(0x98A1B0) : C(0x6B7280); } }
        public static Color Subtle { get { return IsDark ? C(0x232833) : C(0xF3F4F6); } }
        public static Color Accent { get { return IsDark ? C(0x3B82F6) : C(0x2563EB); } }
        public static Color AccentHover { get { return IsDark ? C(0x60A5FA) : C(0x1D4ED8); } }
        public static Color AccentDown { get { return IsDark ? C(0x2563EB) : C(0x1E40AF); } }
        public static Color AccentSoft { get { return IsDark ? C(0x1E293B) : C(0xEFF4FF); } }
        public static Color HeaderA { get { return IsDark ? C(0x1D2537) : C(0x2563EB); } }
        public static Color HeaderB { get { return IsDark ? C(0x3B1D5E) : C(0x7C3AED); } }
        public static Color Success { get { return IsDark ? C(0x34D399) : C(0x059669); } }
        public static Color Warn { get { return IsDark ? C(0xFBBF24) : C(0xD97706); } }
        public static Color Danger { get { return IsDark ? C(0xF87171) : C(0xDC2626); } }
        public static Color OnAccent { get { return Color.White; } }
    }

    internal static class Ui
    {
        public const string GlyphFontName = "Segoe MDL2 Assets";

        // Glyph Segoe MDL2 Assets
        public const string GlyphMic = "";
        public const string GlyphPlay = "";
        public const string GlyphStop = "";
        public const string GlyphPause = "";
        public const string GlyphDownload = "";
        public const string GlyphCopy = "";
        public const string GlyphSave = "";
        public const string GlyphFolder = "";
        public const string GlyphGlobe = "";
        public const string GlyphImport = "";
        public const string GlyphDelete = "";
        public const string GlyphSettings = "";
        public const string GlyphSun = "";
        public const string GlyphMoon = "";
        public const string GlyphCheck = "";
        public const string GlyphCancel = "";
        public const string GlyphAudio = "";
        public const string GlyphRefresh = "";
        public const string GlyphChip = "";
        public const string GlyphChevronDown = "";
        public const string GlyphWarning = "";
        public const string GlyphInfo = "";
        public const string GlyphHelp = "";
        public const string GlyphMore = "";

        public static Font F(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font("Segoe UI", size, style);
        }

        public static Font Glyph(float size)
        {
            try { return new Font(GlyphFontName, size); }
            catch { return new Font("Segoe UI Symbol", size); }
        }

        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0 || r.Width <= 0 || r.Height <= 0) { path.AddRectangle(r); return path; }
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Color Mix(Color a, Color b, double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static string HumanBytes(long bytes)
        {
            if (bytes < 0) return "-";
            if (bytes < 1024) return bytes + " B";
            double kb = bytes / 1024.0;
            if (kb < 1024) return string.Format("{0:0} KB", kb);
            double mb = kb / 1024.0;
            if (mb < 1024) return string.Format("{0:0.0} MB", mb);
            return string.Format("{0:0.00} GB", mb / 1024.0);
        }

        public static string HumanSpeed(double bytesPerSec)
        {
            if (bytesPerSec <= 0) return "-";
            return HumanBytes((long)bytesPerSec) + "/dtk";
        }

        public static string HumanEta(TimeSpan t)
        {
            if (t.TotalSeconds < 1 || t.TotalDays > 1) return "-";
            if (t.TotalMinutes < 1) return string.Format("{0:0} dtk", t.TotalSeconds);
            if (t.TotalHours < 1) return string.Format("{0:0} mnt {1:00} dtk", Math.Floor(t.TotalMinutes), t.Seconds);
            return string.Format("{0:0} jam {1:00} mnt", Math.Floor(t.TotalHours), t.Minutes);
        }

        /// <summary>Warna gradien horizontal pada posisi x (untuk latar kontrol di atas header).</summary>
        public static Color GradientAt(Color a, Color b, int x, int width)
        {
            if (width <= 0) return a;
            return Mix(a, b, (double)x / width);
        }

        public static void StyleCombo(ComboBox cbo)
        {
            cbo.DropDownStyle = ComboBoxStyle.DropDownList;
            cbo.FlatStyle = FlatStyle.Flat;
            cbo.DrawMode = DrawMode.OwnerDrawFixed;
            cbo.ItemHeight = 22;
            cbo.Font = F(9.75f);
            cbo.BackColor = Theme.Field;
            cbo.ForeColor = Theme.Text;
            cbo.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                using (var back = new SolidBrush(sel ? Theme.AccentSoft : Theme.Field))
                    e.Graphics.FillRectangle(back, e.Bounds);
                TextRenderer.DrawText(e.Graphics, cbo.Items[e.Index].ToString(), cbo.Font,
                    new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height),
                    sel ? Theme.Accent : Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };
        }
    }

    /// <summary>Tombol datar bersudut membulat, ikon opsional, state hover/press.</summary>
    internal class RoundedButton : Control
    {
        public int Radius { get; set; }
        public bool Primary { get; set; }
        public bool Ghost { get; set; }
        public bool Danger { get; set; }
        public string Glyph { get; set; }
        public float GlyphSize { get; set; }
        public Color Surface { get; set; }

        private bool _hover, _down;

        public RoundedButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Radius = 9;
            GlyphSize = 11f;
            Cursor = Cursors.Hand;
            Font = Ui.F(9.75f);
            Surface = Color.Empty;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color surface = Surface.IsEmpty ? (Parent != null ? Parent.BackColor : Theme.Card) : Surface;
            g.Clear(surface);

            Color accent = Danger ? Theme.Danger : Theme.Accent;
            Color accentHover = Danger ? Ui.Mix(Theme.Danger, Color.Black, 0.15) : Theme.AccentHover;
            Color accentDown = Danger ? Ui.Mix(Theme.Danger, Color.Black, 0.3) : Theme.AccentDown;
            Color back, fore, border;

            if (Primary)
            {
                back = !Enabled ? Ui.Mix(surface, accent, 0.22)
                     : _down ? accentDown
                     : _hover ? accentHover : accent;
                fore = Enabled ? Theme.OnAccent : Theme.Muted;
                border = back;
            }
            else if (Ghost)
            {
                back = _down ? Color.FromArgb(72, 255, 255, 255)
                     : _hover ? Color.FromArgb(46, 255, 255, 255)
                     : Color.FromArgb(24, 255, 255, 255);
                fore = Color.White;
                border = Color.FromArgb(70, 255, 255, 255);
            }
            else
            {
                back = !Enabled ? surface
                     : _down ? Theme.Border
                     : _hover ? Theme.Subtle : Theme.Card;
                fore = !Enabled ? Theme.Muted : (Danger ? Theme.Danger : Theme.Text);
                border = Theme.Border;
            }

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Ui.RoundedRect(rect, Radius))
            using (var fill = new SolidBrush(back))
            using (var pen = new Pen(border, 1))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            string text = Text ?? string.Empty;
            Size textSize = string.IsNullOrEmpty(text)
                ? Size.Empty
                : TextRenderer.MeasureText(g, text, Font, new Size(Width, Height), TextFormatFlags.NoPadding);

            int gap = (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(Glyph)) ? 0 : 7;
            int glyphW = 0, glyphH = 0;
            Font glyphFont = null;
            if (!string.IsNullOrEmpty(Glyph))
            {
                glyphFont = Ui.Glyph(GlyphSize);
                var gs = TextRenderer.MeasureText(g, Glyph, glyphFont, new Size(Width, Height), TextFormatFlags.NoPadding);
                glyphW = gs.Width;
                glyphH = gs.Height;
            }

            int total = glyphW + gap + textSize.Width;
            int x = (Width - total) / 2;
            int cy = Height / 2;

            if (glyphFont != null)
            {
                TextRenderer.DrawText(g, Glyph, glyphFont,
                    new Point(x, cy - glyphH / 2), fore, TextFormatFlags.NoPadding);
                glyphFont.Dispose();
                x += glyphW + gap;
            }
            if (!string.IsNullOrEmpty(text))
            {
                TextRenderer.DrawText(g, text, Font,
                    new Point(x, cy - textSize.Height / 2), fore, TextFormatFlags.NoPadding);
            }
        }
    }

    /// <summary>Kartu membulat dengan bayangan lembut.</summary>
    internal class CardPanel : Panel
    {
        public int Radius { get; set; }
        public bool Shadow { get; set; }

        public CardPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Radius = 14;
            Shadow = true;
            BackColor = Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            if (rect.Width <= 0 || rect.Height <= 0) return;

            if (Shadow && !Theme.IsDark)
            {
                for (int i = 4; i >= 1; i--)
                {
                    var sr = new Rectangle(rect.X - i, rect.Y - i + 2, rect.Width + i * 2, rect.Height + i * 2);
                    if (sr.Width <= 0 || sr.Height <= 0) continue;
                    using (var sp = Ui.RoundedRect(sr, Radius + i))
                    using (var pen = new Pen(Color.FromArgb(8, 15, 23, 42), 1))
                        g.DrawPath(pen, sp);
                }
            }

            using (var path = Ui.RoundedRect(rect, Radius))
            using (var fill = new SolidBrush(Theme.Card))
            using (var pen = new Pen(Theme.Border, 1))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }
            base.OnPaint(e);
        }
    }

    /// <summary>Bingkai membulat untuk membungkus ComboBox atau TextBox.</summary>
    internal class FieldPanel : Panel
    {
        public int Radius { get; set; }

        public FieldPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Radius = 8;
            BackColor = Theme.Card;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Ui.RoundedRect(rect, Radius))
            using (var fill = new SolidBrush(Theme.Field))
            using (var pen = new Pen(Theme.Border, 1))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }
            base.OnPaint(e);
        }
    }

    /// <summary>Progress bar membulat: determinate (0..1) atau indeterminate.</summary>
    internal class RoundedProgress : Control
    {
        private double _value;
        private bool _indeterminate;
        private int _offset;
        private readonly Timer _timer;

        public RoundedProgress()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 8;
            BarColor = Color.Empty;
            _timer = new Timer { Interval = 30 };
            _timer.Tick += (s, e) =>
            {
                _offset = (_offset + 7) % Math.Max(1, Width + Math.Max(60, Width / 3));
                Invalidate();
            };
        }

        public Color BarColor { get; set; }

        public double Value
        {
            get { return _value; }
            set
            {
                double v = value < 0 ? 0 : (value > 1 ? 1 : value);
                if (Math.Abs(v - _value) < 0.0005) return;
                _value = v;
                Invalidate();
            }
        }

        public bool Indeterminate
        {
            get { return _indeterminate; }
            set
            {
                if (_indeterminate == value) return;
                _indeterminate = value;
                if (value && Visible) _timer.Start(); else _timer.Stop();
                Invalidate();
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible) _timer.Stop();
            else if (_indeterminate) _timer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Card);
            if (Width < 4 || Height < 2) return;

            int r = Height / 2;
            var track = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Ui.RoundedRect(track, r))
            using (var fill = new SolidBrush(Theme.IsDark ? Theme.Subtle : Theme.Border))
                g.FillPath(fill, path);

            Color bar = BarColor.IsEmpty ? Theme.Accent : BarColor;

            if (_indeterminate)
            {
                int w = Math.Max(60, Width / 3);
                int x = _offset - w;
                using (var clip = Ui.RoundedRect(track, r))
                {
                    var saved = g.Clip;
                    g.SetClip(clip);
                    var seg = new Rectangle(x, 0, w, Height - 1);
                    using (var path = Ui.RoundedRect(seg, r))
                    using (var fill = new LinearGradientBrush(
                        new Rectangle(x, 0, Math.Max(1, w), Math.Max(1, Height)), bar, Theme.AccentHover, 0f))
                        g.FillPath(fill, path);
                    g.Clip = saved;
                }
            }
            else if (_value > 0)
            {
                int w = (int)Math.Round((Width - 1) * _value);
                if (w < Height) w = Math.Min(Height, Width - 1);
                var seg = new Rectangle(0, 0, w, Height - 1);
                using (var path = Ui.RoundedRect(seg, r))
                using (var fill = new SolidBrush(bar))
                    g.FillPath(fill, path);
            }
        }
    }

    /// <summary>Area seret-dan-lepas file audio.</summary>
    internal class DropZone : Control
    {
        private bool _hoverDrag;
        private bool _hoverMouse;
        private string _file = string.Empty;

        public event EventHandler FileChanged;

        public DropZone()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AllowDrop = true;
            Cursor = Cursors.Hand;
            Font = Ui.F(9.75f);
        }

        public string SelectedFile
        {
            get { return _file; }
            set
            {
                _file = value ?? string.Empty;
                Invalidate();
                var h = FileChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { _hoverMouse = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hoverMouse = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnDragEnter(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
                _hoverDrag = true;
                Invalidate();
            }
            base.OnDragEnter(e);
        }

        protected override void OnDragLeave(EventArgs e) { _hoverDrag = false; Invalidate(); base.OnDragLeave(e); }

        protected override void OnDragDrop(DragEventArgs e)
        {
            _hoverDrag = false;
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0) SelectedFile = files[0];
            Invalidate();
            base.OnDragDrop(e);
        }

        protected override void OnClick(EventArgs e)
        {
            Browse();
            base.OnClick(e);
        }

        public void Browse()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "File audio|*.ogg;*.opus;*.mp3;*.m4a;*.wav;*.aac;*.flac;*.wma;*.webm;*.mp4;*.mkv|Semua file|*.*";
                dlg.Title = "Pilih file audio";
                if (dlg.ShowDialog(FindForm()) == DialogResult.OK) SelectedFile = dlg.FileName;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Card);
            if (Width < 8 || Height < 8) return;

            bool active = _hoverDrag || _hoverMouse;
            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            Color line = _hoverDrag ? Theme.Accent : (active ? Ui.Mix(Theme.Border, Theme.Accent, 0.55) : Theme.Border);
            Color back = _hoverDrag ? Theme.AccentSoft : (active ? Theme.Subtle : (Theme.IsDark ? Theme.Field : Theme.Card));

            using (var path = Ui.RoundedRect(rect, 12))
            using (var fill = new SolidBrush(back))
            using (var pen = new Pen(line, _hoverDrag ? 2f : 1.4f))
            {
                pen.DashStyle = DashStyle.Custom;
                pen.DashPattern = new float[] { 5f, 4f };
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            bool has = !string.IsNullOrEmpty(_file);
            string glyph = has ? Ui.GlyphAudio : Ui.GlyphMic;
            string title = has ? Path.GetFileName(_file) : "Seret file audio ke sini";
            string sub;
            if (has)
            {
                long size = 0;
                try { size = new FileInfo(_file).Length; } catch { }
                sub = (size > 0 ? Ui.HumanBytes(size) + "   |   " : string.Empty) + "klik untuk mengganti file";
            }
            else
            {
                sub = "atau klik untuk memilih   |   ogg, opus, mp3, m4a, wav, flac, mp4";
            }

            using (var gf = Ui.Glyph(24f))
            {
                var gs = TextRenderer.MeasureText(g, glyph, gf, new Size(Width, Height), TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, glyph, gf,
                    new Point((Width - gs.Width) / 2, Height / 2 - 40),
                    has || _hoverDrag ? Theme.Accent : Theme.Muted,
                    TextFormatFlags.NoPadding);
            }

            using (var tf = Ui.F(11f, FontStyle.Bold))
                TextRenderer.DrawText(g, title, tf,
                    new Rectangle(16, Height / 2 - 4, Width - 32, 24), Theme.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis);

            using (var sf = Ui.F(8.75f))
                TextRenderer.DrawText(g, sub, sf,
                    new Rectangle(16, Height / 2 + 20, Width - 32, 20), Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Header gradien dengan judul, subjudul, dan ikon.</summary>
    internal class HeaderPanel : Control
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Glyph { get; set; }

        public HeaderPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Title = "Suarakata";
            Subtitle = string.Empty;
            Glyph = Ui.GlyphMic;
        }

        public Color SurfaceAt(int x)
        {
            return Ui.GradientAt(Theme.HeaderA, Theme.HeaderB, x, Math.Max(1, Width));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Width < 2 || Height < 2) return;

            using (var brush = new LinearGradientBrush(
                new Rectangle(0, 0, Width, Height), Theme.HeaderA, Theme.HeaderB, LinearGradientMode.Horizontal))
                g.FillRectangle(brush, 0, 0, Width, Height);

            var badge = new Rectangle(20, (Height - 46) / 2, 46, 46);
            using (var path = Ui.RoundedRect(badge, 13))
            using (var fill = new SolidBrush(Color.FromArgb(48, 255, 255, 255)))
                g.FillPath(fill, path);

            using (var gf = Ui.Glyph(19f))
            {
                var gs = TextRenderer.MeasureText(g, Glyph, gf, new Size(Width, Height), TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, Glyph, gf,
                    new Point(badge.X + (badge.Width - gs.Width) / 2, badge.Y + (badge.Height - gs.Height) / 2),
                    Color.White, TextFormatFlags.NoPadding);
            }

            int tx = badge.Right + 14;
            bool hasSub = !string.IsNullOrEmpty(Subtitle);
            using (var tf = Ui.F(15f, FontStyle.Bold))
            {
                int ty = hasSub ? badge.Y - 3 : badge.Y + (badge.Height - tf.Height) / 2;
                TextRenderer.DrawText(g, Title, tf, new Point(tx, ty), Color.White, TextFormatFlags.NoPadding);
            }
            if (hasSub)
            {
                using (var sf = Ui.F(8.75f))
                    TextRenderer.DrawText(g, Subtitle, sf, new Point(tx + 1, badge.Y + 29),
                        Color.FromArgb(228, 236, 255), TextFormatFlags.NoPadding);
            }
        }
    }

    /// <summary>Label kecil berbentuk pil (badge status).</summary>
    internal class Badge : Control
    {
        public Color Accent { get; set; }
        public bool ShowDot { get; set; }
        public Color Surface { get; set; }

        public Badge()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Accent = Theme.Muted;
            ShowDot = true;
            Surface = Color.Empty;
            Font = Ui.F(8.5f, FontStyle.Bold);
            Height = 22;
        }

        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Surface.IsEmpty ? (Parent != null ? Parent.BackColor : Theme.Card) : Surface);
            if (Width < 4 || Height < 4) return;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Ui.RoundedRect(rect, Height / 2))
            using (var fill = new SolidBrush(Color.FromArgb(Theme.IsDark ? 48 : 28, Accent)))
                g.FillPath(fill, path);

            int x = 10;
            if (ShowDot)
            {
                using (var dot = new SolidBrush(Accent))
                    g.FillEllipse(dot, x, Height / 2 - 3, 6, 6);
                x += 12;
            }
            TextRenderer.DrawText(g, Text, Font,
                new Rectangle(x, 0, Width - x - 8, Height), Accent,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Menerapkan tema Explorer gelap/terang pada kontrol native (mis. scrollbar TextBox).</summary>
    internal static class NativeTheme
    {
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        // Ordinal tak terdokumentasi milik uxtheme; tanpa ini scrollbar native tetap putih
        // walau tema DarkMode_Explorer dipasang. Dibungkus try/catch: bila tidak tersedia
        // (Windows lama), tampilan hanya kembali ke scrollbar terang.
        [DllImport("uxtheme.dll", EntryPoint = "#135", CharSet = CharSet.Unicode)]
        private static extern int SetPreferredAppMode(int mode);

        [DllImport("uxtheme.dll", EntryPoint = "#133", CharSet = CharSet.Unicode)]
        private static extern bool AllowDarkModeForWindow(IntPtr hWnd, bool allow);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);

        private const int WM_THEMECHANGED = 0x031A;

        /// <summary>Dipanggil sekali sebelum jendela pertama dibuat.</summary>
        public static void Init()
        {
            try { SetPreferredAppMode(Theme.IsDark ? 2 : 1); } catch { }
        }

        public static void Apply(Control c)
        {
            if (c == null || !c.IsHandleCreated) return;
            try { SetPreferredAppMode(Theme.IsDark ? 2 : 0); } catch { }
            try { AllowDarkModeForWindow(c.Handle, Theme.IsDark); } catch { }
            try
            {
                SetWindowTheme(c.Handle, Theme.IsDark ? "DarkMode_Explorer" : "Explorer", null);
                SendMessage(c.Handle, WM_THEMECHANGED, IntPtr.Zero, IntPtr.Zero);
                c.Invalidate();
            }
            catch { /* versi Windows lama */ }
        }
    }

    /// <summary>Kotak centang yang mengikuti palet aplikasi (CheckBox bawaan tidak ikut mode gelap).</summary>
    internal class ThemedCheckBox : CheckBox
    {
        private bool _hover;

        public ThemedCheckBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoSize = false;
            Cursor = Cursors.Hand;
            Font = Ui.F(9.75f);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Card);

            int size = 17;
            var box = new Rectangle(0, (Height - size) / 2, size, size);
            using (var path = Ui.RoundedRect(box, 5))
            using (var fill = new SolidBrush(Checked ? Theme.Accent : Theme.Field))
            using (var pen = new Pen(Checked ? Theme.Accent : (_hover ? Theme.Accent : Theme.Border), 1.4f))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            if (Checked)
            {
                using (var gf = Ui.Glyph(9f))
                {
                    var gs = TextRenderer.MeasureText(g, Ui.GlyphCheck, gf, new Size(size, size), TextFormatFlags.NoPadding);
                    TextRenderer.DrawText(g, Ui.GlyphCheck, gf,
                        new Point(box.X + (size - gs.Width) / 2, box.Y + (size - gs.Height) / 2),
                        Theme.OnAccent, TextFormatFlags.NoPadding);
                }
            }

            TextRenderer.DrawText(g, Text, Font,
                new Rectangle(box.Right + 8, 0, Width - box.Right - 8, Height),
                Enabled ? Theme.Text : Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
