using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Suarakata
{
    /// <summary>Satu baris dalam menu popup modern.</summary>
    internal sealed class MenuEntry
    {
        public string Text;
        public string Glyph;
        public Action Action;
        public bool Separator;
        public bool Danger;
        public bool Checked;
        public bool Disabled;

        public static MenuEntry Sep()
        {
            return new MenuEntry { Separator = true };
        }
    }

    /// <summary>
    /// Menu popup bergaya modern: sudut membulat, sorotan lembut, ikon, dan mengikuti tema.
    /// Dipakai menggantikan ContextMenuStrip bawaan yang tampilannya kaku.
    /// </summary>
    internal class PopupMenu : Form
    {
        private const int ItemHeight = 36;
        private const int SeparatorHeight = 9;
        private const int PadV = 7;
        private const int PadH = 7;
        private const int Radius = 12;
        private const int CS_DROPSHADOW = 0x00020000;

        private readonly List<MenuEntry> _items;
        private readonly bool _showGlyphColumn;
        private int _hover = -1;
        private Action _pending;
        internal Action _onClosed;

        private PopupMenu(List<MenuEntry> items, int minWidth)
        {
            _items = items;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            BackColor = Theme.Card;
            Font = Ui.F(9.75f);
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            foreach (var it in items)
                if (!it.Separator && !string.IsNullOrEmpty(it.Glyph)) { _showGlyphColumn = true; break; }

            Size = new Size(Math.Max(minWidth, MeasureWidth()), MeasureHeight());
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;   // bayangan halus dari sistem
                return cp;
            }
        }

        private int GlyphColumn { get { return _showGlyphColumn ? 30 : 0; } }

        private int MeasureWidth()
        {
            // Diukur tanpa Graphics dari kontrol: memakai CreateGraphics() di konstruktor
            // akan memaksa handle dibuat lebih awal, sehingga Region terpasang pada
            // ukuran form bawaan dan memotong jendela.
            int w = 0;
            foreach (var it in _items)
            {
                if (it.Separator) continue;
                int tw = TextRenderer.MeasureText(it.Text, Font, new Size(600, 40), TextFormatFlags.NoPadding).Width;
                if (tw > w) w = tw;
            }
            return w + GlyphColumn + PadH * 2 + 30 + 24;   // ruang untuk padding + tanda centang
        }

        private int MeasureHeight()
        {
            int h = PadV * 2;
            foreach (var it in _items) h += it.Separator ? SeparatorHeight : ItemHeight;
            return h;
        }

        /// <summary>Menampilkan menu menempel pada sebuah kontrol.</summary>
        public static void Show(Control anchor, IEnumerable<MenuEntry> entries,
                                bool alignRight = true, int minWidth = 210, Action onClosed = null)
        {
            var list = new List<MenuEntry>(entries);
            if (list.Count == 0) return;

            var menu = new PopupMenu(list, minWidth) { _onClosed = onClosed };
            var origin = anchor.PointToScreen(Point.Empty);
            int x = alignRight ? origin.X + anchor.Width - menu.Width : origin.X;
            int y = origin.Y + anchor.Height + 6;

            var area = Screen.FromControl(anchor).WorkingArea;
            if (y + menu.Height > area.Bottom) y = origin.Y - menu.Height - 6;
            if (x + menu.Width > area.Right) x = area.Right - menu.Width - 8;
            if (x < area.Left) x = area.Left + 8;
            if (y < area.Top) y = area.Top + 8;

            menu.Location = new Point(x, y);
            menu.Owner = anchor.FindForm();
            menu.Show();
            menu.Activate();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Close();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateRegion();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateRegion();
        }

        /// <summary>Bingkai membulat harus mengikuti ukuran terkini, bukan ukuran saat handle dibuat.</summary>
        private void UpdateRegion()
        {
            if (!IsHandleCreated || Width <= 0 || Height <= 0) return;
            using (var path = Ui.RoundedRect(new Rectangle(0, 0, Width, Height), Radius))
                Region = new Region(path);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (_onClosed != null) { var closed = _onClosed; _onClosed = null; closed(); }

            var action = _pending;
            _pending = null;
            if (action == null) return;

            // Dijalankan setelah menu benar-benar tertutup supaya dialog yang dibuka
            // tidak muncul di belakang popup.
            var owner = Owner;
            if (owner != null && owner.IsHandleCreated) owner.BeginInvoke(action);
            else action();
        }

        private int IndexAt(int y)
        {
            int top = PadV;
            for (int i = 0; i < _items.Count; i++)
            {
                int h = _items[i].Separator ? SeparatorHeight : ItemHeight;
                if (y >= top && y < top + h) return _items[i].Separator ? -1 : i;
                top += h;
            }
            return -1;
        }

        private int TopOf(int index)
        {
            int top = PadV;
            for (int i = 0; i < index; i++) top += _items[i].Separator ? SeparatorHeight : ItemHeight;
            return top;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int idx = IndexAt(e.Y);
            if (idx != _hover) { _hover = idx; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hover != -1) { _hover = -1; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int idx = IndexAt(e.Y);
            if (idx >= 0 && !_items[idx].Disabled)
            {
                _pending = _items[idx].Action;
                Close();
                return;
            }
            base.OnMouseClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { Close(); return; }
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)
            {
                int step = e.KeyCode == Keys.Down ? 1 : -1;
                int i = _hover;
                for (int n = 0; n < _items.Count; n++)
                {
                    i += step;
                    if (i < 0) i = _items.Count - 1;
                    if (i >= _items.Count) i = 0;
                    if (!_items[i].Separator && !_items[i].Disabled) break;
                }
                _hover = i;
                Invalidate();
                return;
            }
            if ((e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) && _hover >= 0)
            {
                _pending = _items[_hover].Action;
                Close();
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Card);

            var frame = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Ui.RoundedRect(frame, Radius))
            using (var fill = new SolidBrush(Theme.Card))
            using (var pen = new Pen(Theme.Border, 1))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                int top = TopOf(i);

                if (it.Separator)
                {
                    using (var pen = new Pen(Theme.Border, 1))
                        g.DrawLine(pen, PadH + 6, top + SeparatorHeight / 2, Width - PadH - 6, top + SeparatorHeight / 2);
                    continue;
                }

                var row = new Rectangle(PadH, top, Width - PadH * 2, ItemHeight);
                Color fore = it.Disabled ? Theme.Border : (it.Danger ? Theme.Danger : Theme.Text);

                if (i == _hover && !it.Disabled)
                {
                    using (var path = Ui.RoundedRect(new Rectangle(row.X, row.Y + 1, row.Width, row.Height - 2), 8))
                    using (var fill = new SolidBrush(it.Danger
                        ? Color.FromArgb(Theme.IsDark ? 46 : 26, Theme.Danger)
                        : Theme.Subtle))
                        g.FillPath(fill, path);
                }

                int x = row.X + 12;
                if (_showGlyphColumn)
                {
                    if (!string.IsNullOrEmpty(it.Glyph))
                    {
                        using (var gf = Ui.Glyph(11f))
                        {
                            var gs = TextRenderer.MeasureText(g, it.Glyph, gf, new Size(30, ItemHeight), TextFormatFlags.NoPadding);
                            TextRenderer.DrawText(g, it.Glyph, gf,
                                new Point(x, row.Y + (ItemHeight - gs.Height) / 2),
                                it.Disabled ? Theme.Border : (it.Danger ? Theme.Danger : Theme.Muted),
                                TextFormatFlags.NoPadding);
                        }
                    }
                    x += GlyphColumn;
                }

                TextRenderer.DrawText(g, it.Text, Font,
                    new Rectangle(x, row.Y, row.Right - x - 30, ItemHeight), fore,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                if (it.Checked)
                {
                    using (var gf = Ui.Glyph(11f))
                    {
                        var gs = TextRenderer.MeasureText(g, Ui.GlyphCheck, gf, new Size(30, ItemHeight), TextFormatFlags.NoPadding);
                        TextRenderer.DrawText(g, Ui.GlyphCheck, gf,
                            new Point(row.Right - 22, row.Y + (ItemHeight - gs.Height) / 2),
                            Theme.Accent, TextFormatFlags.NoPadding);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Pengganti ComboBox: kotak membulat dengan chevron yang membuka daftar bergaya
    /// <see cref="PopupMenu"/>, sehingga tampilannya konsisten dan tidak seperti kontrol lama.
    /// </summary>
    internal class ModernDropdown : Control
    {
        private readonly List<string> _items = new List<string>();
        private int _selected = -1;
        private bool _hover, _open;

        public event EventHandler SelectedIndexChanged;

        public ModernDropdown()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Font = Ui.F(9.75f);
            Height = 34;
        }

        public IList<string> Items { get { return _items; } }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                if (value == _selected || value < -1 || value >= _items.Count) return;
                _selected = value;
                Invalidate();
                var h = SelectedIndexChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public string SelectedItem
        {
            get { return _selected >= 0 && _selected < _items.Count ? _items[_selected] : string.Empty; }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (!Enabled) return;
            OpenList();
        }

        private void OpenList()
        {
            var entries = new List<MenuEntry>();
            for (int i = 0; i < _items.Count; i++)
            {
                int index = i;
                entries.Add(new MenuEntry
                {
                    Text = _items[i],
                    Checked = i == _selected,
                    Action = () => SelectedIndex = index
                });
            }
            _open = true;
            Invalidate();
            PopupMenu.Show(this, entries, alignRight: false, minWidth: Width,
                onClosed: () => { _open = false; Invalidate(); });
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Card);
            if (Width < 8 || Height < 8) return;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color border = !Enabled ? Theme.Border : (_open || _hover ? Theme.Accent : Theme.Border);

            using (var path = Ui.RoundedRect(rect, 8))
            using (var fill = new SolidBrush(Enabled ? Theme.Field : Theme.Subtle))
            using (var pen = new Pen(border, _open || _hover ? 1.6f : 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            using (var gf = Ui.Glyph(9f))
            {
                var gs = TextRenderer.MeasureText(g, Ui.GlyphChevronDown, gf, new Size(20, Height), TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, Ui.GlyphChevronDown, gf,
                    new Point(Width - 12 - gs.Width, (Height - gs.Height) / 2),
                    Enabled ? Theme.Muted : Theme.Border, TextFormatFlags.NoPadding);
            }

            TextRenderer.DrawText(g, SelectedItem, Font,
                new Rectangle(12, 0, Width - 12 - 28, Height),
                Enabled ? Theme.Text : Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal enum DialogKind { Info, Question, Warning, Error, Success }

    /// <summary>Kotak dialog bertema, pengganti MessageBox bawaan.</summary>
    internal class ModernDialog : Form
    {
        private const int CS_DROPSHADOW = 0x00020000;

        private const int TextLeft = 76;   // 20 margin + 40 lencana + 16 jarak
        private const int TextTop = 58;

        private readonly int _defaultIndex;
        private readonly int _cancelIndex;

        private readonly string _title;
        private readonly string _message;
        private readonly DialogKind _kind;

        private ModernDialog(string title, string message, DialogKind kind, string[] buttons, int defaultIndex)
        {
            _title = title ?? string.Empty;
            _message = message ?? string.Empty;
            _kind = kind;
            _defaultIndex = defaultIndex;
            _cancelIndex = buttons.Length - 1;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Card;
            Font = Ui.F(9.75f);
            KeyPreview = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            // Diukur tanpa CreateGraphics(): memanggilnya di sini akan membuat handle
            // sebelum ukuran final ditetapkan, dan Region ikut terkunci pada ukuran bawaan.
            int width = 460;
            int textWidth = width - TextLeft - 20;
            int textHeight;
            using (var mf = Ui.F(10f))
                textHeight = TextRenderer.MeasureText(_message, mf,
                    new Size(textWidth, 2000), TextFormatFlags.WordBreak).Height;

            int height = TextTop + textHeight + 24 + 40 + 20;
            ClientSize = new Size(width, Math.Max(170, height));

            int bx = width - 20;
            for (int i = buttons.Length - 1; i >= 0; i--)
            {
                int index = i;
                var text = buttons[i];
                int bw = Math.Max(96, TextRenderer.MeasureText(text, Ui.F(9.75f)).Width + 40);
                var btn = new RoundedButton
                {
                    Text = text,
                    Primary = i == defaultIndex,
                    Danger = kind == DialogKind.Error && i == defaultIndex,
                    Bounds = new Rectangle(bx - bw, ClientSize.Height - 20 - 40, bw, 40),
                    Surface = Theme.Card
                };
                btn.Click += (s, e) =>
                {
                    DialogResult = index == 0 ? DialogResult.OK : DialogResult.Cancel;
                    Tag = index;
                    Close();
                };
                Controls.Add(btn);
                bx -= bw + 10;
            }

        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateRegion();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateRegion();
        }

        private void UpdateRegion()
        {
            if (!IsHandleCreated || Width <= 0 || Height <= 0) return;
            using (var path = Ui.RoundedRect(new Rectangle(0, 0, Width, Height), 14))
                Region = new Region(path);
        }

        // Esc dan Enter harus tetap bekerja walau fokus ada di tombol.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Tag = _cancelIndex;
                DialogResult = DialogResult.Cancel;
                Close();
                return true;
            }
            if (keyData == Keys.Enter)
            {
                Tag = _defaultIndex;
                DialogResult = DialogResult.OK;
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // Dialog tanpa bilah judul tetap bisa digeser dari area kosong.
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            NativeMethods.DragWindow(Handle);
        }

        private Color AccentOf(DialogKind kind)
        {
            switch (kind)
            {
                case DialogKind.Error: return Theme.Danger;
                case DialogKind.Warning: return Theme.Warn;
                case DialogKind.Success: return Theme.Success;
                default: return Theme.Accent;
            }
        }

        private string GlyphOf(DialogKind kind)
        {
            switch (kind)
            {
                case DialogKind.Error: return Ui.GlyphCancel;
                case DialogKind.Warning: return Ui.GlyphWarning;
                case DialogKind.Success: return Ui.GlyphCheck;
                case DialogKind.Question: return Ui.GlyphHelp;
                default: return Ui.GlyphInfo;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Card);

            var frame = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Ui.RoundedRect(frame, 14))
            using (var fill = new SolidBrush(Theme.Card))
            using (var pen = new Pen(Theme.Border, 1))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            Color accent = AccentOf(_kind);
            var badge = new Rectangle(20, 24, 40, 40);
            using (var path = Ui.RoundedRect(badge, 12))
            using (var fill = new SolidBrush(Color.FromArgb(Theme.IsDark ? 52 : 30, accent)))
                g.FillPath(fill, path);

            using (var gf = Ui.Glyph(15f))
            {
                var gs = TextRenderer.MeasureText(g, GlyphOf(_kind), gf, badge.Size, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, GlyphOf(_kind), gf,
                    new Point(badge.X + (badge.Width - gs.Width) / 2, badge.Y + (badge.Height - gs.Height) / 2),
                    accent, TextFormatFlags.NoPadding);
            }

            using (var tf = Ui.F(12.5f, FontStyle.Bold))
                TextRenderer.DrawText(g, _title, tf, new Point(TextLeft, 26), Theme.Text, TextFormatFlags.NoPadding);

            using (var mf = Ui.F(10f))
                TextRenderer.DrawText(g, _message, mf,
                    new Rectangle(TextLeft, TextTop, Width - TextLeft - 20, ClientSize.Height - TextTop - 66),
                    Theme.Muted, TextFormatFlags.WordBreak | TextFormatFlags.Top);
        }

        // ---------- API ----------

        private static int ShowCore(IWin32Window owner, string title, string message,
                                    DialogKind kind, string[] buttons, int defaultIndex)
        {
            using (var dlg = new ModernDialog(title, message, kind, buttons, defaultIndex))
            {
                dlg.ShowDialog(owner);
                return dlg.Tag is int ? (int)dlg.Tag : buttons.Length - 1;
            }
        }

        public static void Info(IWin32Window owner, string title, string message)
        {
            ShowCore(owner, title, message, DialogKind.Info, new[] { "Mengerti" }, 0);
        }

        public static void Success(IWin32Window owner, string title, string message)
        {
            ShowCore(owner, title, message, DialogKind.Success, new[] { "Mengerti" }, 0);
        }

        public static void Error(IWin32Window owner, string title, string message)
        {
            ShowCore(owner, title, message, DialogKind.Error, new[] { "Tutup" }, 0);
        }

        public static void Warn(IWin32Window owner, string title, string message)
        {
            ShowCore(owner, title, message, DialogKind.Warning, new[] { "Mengerti" }, 0);
        }

        /// <summary>Menampilkan konfirmasi; true bila tombol utama dipilih.</summary>
        public static bool Confirm(IWin32Window owner, string title, string message,
                                   string yes = "Lanjutkan", string no = "Batal",
                                   DialogKind kind = DialogKind.Question)
        {
            return ShowCore(owner, title, message, kind, new[] { yes, no }, 0) == 0;
        }
    }

    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int HTCAPTION = 2;

        public static void DragWindow(IntPtr handle)
        {
            try
            {
                ReleaseCapture();
                SendMessage(handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
            }
            catch { /* abaikan */ }
        }
    }

    /// <summary>
    /// TextBox tanpa scrollbar bawaan; posisi gulir dibaca lewat pesan edit control
    /// supaya bisa dipasangi <see cref="SlimScrollBar"/> yang mengikuti tema.
    /// </summary>
    internal class TextView : TextBox
    {
        private const int WM_VSCROLL = 0x0115;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
        private const int EM_LINESCROLL = 0x00B6;
        private const int EM_GETLINECOUNT = 0x00BA;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public event EventHandler ViewChanged;

        public TextView()
        {
            Multiline = true;
            WordWrap = true;
            ScrollBars = ScrollBars.None;
            BorderStyle = BorderStyle.None;
        }

        public int FirstVisibleLine
        {
            get { return IsHandleCreated ? (int)SendMessage(Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero) : 0; }
        }

        /// <summary>Jumlah baris setelah pembungkusan kata.</summary>
        public int TotalLines
        {
            get { return IsHandleCreated ? (int)SendMessage(Handle, EM_GETLINECOUNT, IntPtr.Zero, IntPtr.Zero) : 0; }
        }

        public int VisibleLines
        {
            get
            {
                int lh = FontHeight;
                return lh > 0 ? Math.Max(1, ClientSize.Height / lh) : 1;
            }
        }

        public void ScrollToLine(int line)
        {
            if (!IsHandleCreated) return;
            int delta = line - FirstVisibleLine;
            if (delta != 0) SendMessage(Handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)delta);
        }

        private void Raise()
        {
            var h = ViewChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Raise(); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); Raise(); }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_VSCROLL || m.Msg == WM_MOUSEWHEEL || m.Msg == WM_KEYDOWN || m.Msg == WM_LBUTTONDOWN)
                Raise();
        }
    }

    /// <summary>Scrollbar tipis bergaya modern, menggantikan scrollbar sistem.</summary>
    internal class SlimScrollBar : Control
    {
        private int _max = 1;
        private int _large = 1;
        private int _value;
        private bool _hover, _dragging;
        private int _dragOffset;

        public event EventHandler ValueChanged;

        public SlimScrollBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Width = 10;
            Cursor = Cursors.Default;
        }

        /// <summary>Jumlah total baris.</summary>
        public int Maximum
        {
            get { return _max; }
            set { _max = Math.Max(1, value); Invalidate(); }
        }

        /// <summary>Jumlah baris yang terlihat sekaligus.</summary>
        public int LargeChange
        {
            get { return _large; }
            set { _large = Math.Max(1, value); Invalidate(); }
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int max = Math.Max(0, _max - _large);
                int v = value < 0 ? 0 : (value > max ? max : value);
                if (v == _value) return;
                _value = v;
                Invalidate();
                var h = ValueChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public bool NeedsScroll { get { return _max > _large; } }

        private Rectangle Thumb()
        {
            if (!NeedsScroll) return Rectangle.Empty;
            int track = Height - 4;
            int h = Math.Max(28, (int)(track * (double)_large / _max));
            int span = track - h;
            int max = Math.Max(1, _max - _large);
            int y = 2 + (int)(span * (double)_value / max);
            return new Rectangle(2, y, Width - 4, h);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!NeedsScroll || e.Button != MouseButtons.Left) return;
            var thumb = Thumb();
            if (thumb.Contains(e.Location))
            {
                _dragging = true;
                _dragOffset = e.Y - thumb.Y;
            }
            else
            {
                // Klik di jalur: lompat satu layar.
                Value += e.Y < thumb.Y ? -_large : _large;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging) return;
            var thumb = Thumb();
            int track = Height - 4;
            int span = Math.Max(1, track - thumb.Height);
            int y = e.Y - _dragOffset - 2;
            Value = (int)Math.Round((double)y / span * Math.Max(1, _max - _large));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Card);
            if (!NeedsScroll) return;

            var thumb = Thumb();
            if (thumb.Width <= 0 || thumb.Height <= 0) return;

            Color c = _dragging ? Theme.Accent : (_hover ? Ui.Mix(Theme.Border, Theme.Accent, 0.6) : Theme.Border);
            using (var path = Ui.RoundedRect(thumb, thumb.Width / 2))
            using (var fill = new SolidBrush(c))
                g.FillPath(fill, path);
        }
    }
}
