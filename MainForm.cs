using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using Whisper.net.Ggml;

namespace Suarakata
{
    public class MainForm : Form
    {
        // Palet warna (light modern)
        private static readonly Color CAccent = Color.FromArgb(37, 99, 235);   // blue-600
        private static readonly Color CAccentHover = Color.FromArgb(29, 78, 216); // blue-700
        private static readonly Color CBg = Color.FromArgb(243, 244, 246);       // gray-100
        private static readonly Color CCard = Color.White;
        private static readonly Color CText = Color.FromArgb(31, 41, 55);        // gray-800
        private static readonly Color CMuted = Color.FromArgb(107, 114, 128);    // gray-500
        private static readonly Color CBorder = Color.FromArgb(226, 229, 234);   // gray-200
        private static readonly Color CHover = Color.FromArgb(243, 244, 246);

        private TextBox txtFile;
        private Button btnBrowse;
        private ComboBox cboModel;
        private ComboBox cboLang;
        private CheckBox chkTimestamps;
        private Button btnRun;
        private ProgressBar bar;
        private Label lblStatus;
        private TextBox txtOut;
        private Button btnSave;
        private Button btnCopy;

        private string _fullText = string.Empty;

        // Nilai bahasa sejajar dengan item cboLang
        private static readonly string[] LangCodes =
            { "auto", "id", "id", "en", "ms", "jw", "su", "ar", "zh" };

        // Tipe model sejajar dengan item cboModel
        private static readonly GgmlType[] ModelTypes =
            { GgmlType.Tiny, GgmlType.Base, GgmlType.Small, GgmlType.Medium, GgmlType.LargeV3 };

        public MainForm()
        {
            DoubleBuffered = true;
            BuildUi();
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        }

        private sealed class BufferedPanel : Panel
        {
            public BufferedPanel()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
            }
        }

        private void BuildUi()
        {
            Text = "Suarakata — Speech to Text";
            Font = new Font("Segoe UI", 9.5f);
            BackColor = CBg;
            ForeColor = CText;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(880, 700);
            MinimumSize = new Size(720, 560);

            // ---------- HEADER ----------
            var header = new BufferedPanel { Dock = DockStyle.Top, Height = 72, BackColor = CAccent };
            var icon = new Label
            {
                Text = "\uE720", // Segoe MDL2 Assets: Microphone
                Font = new Font("Segoe MDL2 Assets", 24f),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds = new Rectangle(20, 12, 46, 46)
            };
            var title = new Label
            {
                Text = "Suarakata",
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(74, 11)
            };
            var subtitle = new Label
            {
                Text = "Speech to Text • transkripsi suara jadi teks (offline)",
                Font = new Font("Segoe UI", 8.75f),
                ForeColor = Color.FromArgb(219, 234, 254),
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(76, 46)
            };
            header.Controls.AddRange(new Control[] { icon, title, subtitle });

            // ---------- FOOTER ----------
            var footer = new BufferedPanel { Dock = DockStyle.Bottom, Height = 64, BackColor = CCard };
            footer.Paint += (s, e) =>
            {
                using (var pen = new Pen(CBorder, 1))
                    e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };
            lblStatus = new Label { Text = "Siap.", ForeColor = CMuted, AutoSize = false, Bounds = new Rectangle(20, 11, 440, 20), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            bar = new ProgressBar { Bounds = new Rectangle(20, 36, 320, 6), Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25, Visible = false, Anchor = AnchorStyles.Top | AnchorStyles.Left };
            btnCopy = new Button { Text = "Salin", Bounds = new Rectangle(760, 14, 100, 36), Anchor = AnchorStyles.Top | AnchorStyles.Right, Enabled = false };
            btnSave = new Button { Text = "Simpan .txt", Bounds = new Rectangle(636, 14, 116, 36), Anchor = AnchorStyles.Top | AnchorStyles.Right, Enabled = false };
            StyleSecondary(btnCopy);
            StyleSecondary(btnSave);
            btnSave.Click += BtnSave_Click;
            btnCopy.Click += BtnCopy_Click;
            footer.Controls.AddRange(new Control[] { lblStatus, bar, btnSave, btnCopy });

            // ---------- CONTENT ----------
            var content = new BufferedPanel
            {
                BackColor = CBg,
                Bounds = new Rectangle(0, header.Height, ClientSize.Width, ClientSize.Height - header.Height - footer.Height),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            int cardW = content.Width - 36;

            // Kartu input
            var inputCard = new BufferedPanel
            {
                Bounds = new Rectangle(18, 14, cardW, 162),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = CBg
            };
            StyleCard(inputCard);

            var lblFile = new Label { Text = "FILE AUDIO", Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = CMuted, AutoSize = true, Location = new Point(18, 14) };
            txtFile = new TextBox { Bounds = new Rectangle(18, 36, inputCard.Width - 18 - 132 - 12, 26), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10f) };
            btnBrowse = new Button { Text = "Telusuri…", Bounds = new Rectangle(inputCard.Width - 18 - 132, 34, 132, 30), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            StyleSecondary(btnBrowse);
            btnBrowse.Click += BtnBrowse_Click;

            var lblModel = new Label { Text = "MODEL", Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = CMuted, AutoSize = true, Location = new Point(18, 82) };
            cboModel = new ComboBox { Bounds = new Rectangle(18, 102, 186, 26), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = CCard, Font = new Font("Segoe UI", 9.5f) };
            cboModel.Items.AddRange(new object[] { "Tiny (~75 MB)", "Base (~142 MB)", "Small (~466 MB)", "Medium (~1.5 GB)", "Large-v3 (~3 GB)" });
            cboModel.SelectedIndex = 2;

            var lblLang = new Label { Text = "BAHASA", Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = CMuted, AutoSize = true, Location = new Point(220, 82) };
            cboLang = new ComboBox { Bounds = new Rectangle(220, 102, 196, 26), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = CCard, Font = new Font("Segoe UI", 9.5f) };
            cboLang.Items.AddRange(new object[] { "Auto-deteksi", "Indonesia", "Banjar (via Indonesia)", "Inggris", "Melayu", "Jawa", "Sunda", "Arab", "Mandarin" });
            cboLang.SelectedIndex = 1;

            chkTimestamps = new CheckBox { Text = "Sertakan waktu", ForeColor = CText, AutoSize = true, Location = new Point(432, 104) };

            btnRun = new Button { Text = "▶   Transkripsi", Bounds = new Rectangle(inputCard.Width - 18 - 176, 92, 176, 46), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            StylePrimary(btnRun);
            btnRun.Click += BtnRun_Click;

            inputCard.Controls.AddRange(new Control[] { lblFile, txtFile, btnBrowse, lblModel, cboModel, lblLang, cboLang, chkTimestamps, btnRun });

            // Kartu hasil
            var outputCard = new BufferedPanel
            {
                Bounds = new Rectangle(18, 190, cardW, content.Height - 190 - 14),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = CBg
            };
            StyleCard(outputCard);

            var lblOut = new Label { Text = "HASIL TRANSKRIPSI", Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = CMuted, AutoSize = true, Location = new Point(18, 14) };
            txtOut = new TextBox
            {
                Bounds = new Rectangle(16, 38, outputCard.Width - 32, outputCard.Height - 38 - 16),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = true,
                BorderStyle = BorderStyle.None,
                BackColor = CCard,
                ForeColor = CText,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 11f)
            };
            outputCard.Controls.AddRange(new Control[] { lblOut, txtOut });

            content.Controls.Add(inputCard);
            content.Controls.Add(outputCard);

            Controls.Add(header);
            Controls.Add(footer);
            Controls.Add(content);

            // ---------- Drag & drop ----------
            AllowDrop = true;
            DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop += (s, e) =>
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0) txtFile.Text = files[0];
            };
        }

        // ===================== STYLING HELPERS =====================

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0 || r.Width <= 0 || r.Height <= 0) { path.AddRectangle(r); return path; }
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void StyleCard(Panel p)
        {
            // Panel transparan terhadap CBg; kartu putih membulat digambar via Paint (anti-alias).
            p.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, p.Width - 1, p.Height - 1);
                using (var path = RoundedRect(rect, 12))
                using (var fill = new SolidBrush(CCard))
                using (var pen = new Pen(CBorder, 1))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(pen, path);
                }
            };
        }

        private void StylePrimary(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = CAccent;
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.FlatAppearance.MouseOverBackColor = CAccentHover;
            b.FlatAppearance.MouseDownBackColor = CAccentHover;
            RoundControl(b, 8);
        }

        private void StyleSecondary(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = CCard;
            b.ForeColor = CText;
            b.Font = new Font("Segoe UI", 9.5f);
            b.Cursor = Cursors.Hand;
            b.FlatAppearance.BorderColor = CBorder;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = CHover;
            RoundControl(b, 8);
        }

        private static void RoundControl(Control c, int radius)
        {
            Action apply = () =>
            {
                if (c.Width <= 0 || c.Height <= 0) return;
                using (var path = RoundedRect(new Rectangle(0, 0, c.Width, c.Height), radius))
                    c.Region = new Region(path);
            };
            apply();
            c.Resize += (s, e) => apply();
        }

        // ===================== LOGIC =====================

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "File audio|*.ogg;*.opus;*.mp3;*.m4a;*.wav;*.aac;*.flac;*.webm;*.mp4|Semua file|*.*";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    txtFile.Text = dlg.FileName;
            }
        }

        private async void BtnRun_Click(object sender, EventArgs e)
        {
            string audio = txtFile.Text.Trim().Trim('"');
            if (string.IsNullOrEmpty(audio) || !File.Exists(audio))
            {
                MessageBox.Show(this, "Pilih file audio yang valid terlebih dahulu.", "Perhatian",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            GgmlType model = ModelTypes[cboModel.SelectedIndex];
            string lang = LangCodes[cboLang.SelectedIndex];
            bool timestamps = chkTimestamps.Checked;
            string modelsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");

            SetBusy(true);
            txtOut.Clear();
            _fullText = string.Empty;

            Action<string> status = s => SafeInvoke(() => lblStatus.Text = s);
            Action<string> onLine = l => SafeInvoke(() => txtOut.AppendText(l + Environment.NewLine));

            try
            {
                _fullText = await Transcriber.TranscribeAsync(
                    audio, model, lang, timestamps, modelsDir, status, onLine);

                SafeInvoke(() =>
                {
                    lblStatus.Text = "Selesai. " + txtOut.Lines.Length + " baris.";
                    btnSave.Enabled = _fullText.Length > 0;
                    btnCopy.Enabled = _fullText.Length > 0;
                });
            }
            catch (Exception ex)
            {
                SafeInvoke(() => lblStatus.Text = "Gagal.");
                MessageBox.Show(this, ex.Message, "Gagal transkripsi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "Teks|*.txt";
                string src = txtFile.Text.Trim().Trim('"');
                dlg.FileName = (string.IsNullOrEmpty(src) ? "transkrip" : Path.GetFileNameWithoutExtension(src)) + ".txt";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    File.WriteAllText(dlg.FileName, chkTimestamps.Checked ? txtOut.Text : _fullText, System.Text.Encoding.UTF8);
                    lblStatus.Text = "Tersimpan: " + dlg.FileName;
                }
            }
        }

        private void BtnCopy_Click(object sender, EventArgs e)
        {
            string text = chkTimestamps.Checked ? txtOut.Text : _fullText;
            if (!string.IsNullOrEmpty(text))
            {
                Clipboard.SetText(text);
                lblStatus.Text = "Teks disalin ke clipboard.";
            }
        }

        private void SetBusy(bool busy)
        {
            btnRun.Enabled = !busy;
            btnBrowse.Enabled = !busy;
            cboModel.Enabled = !busy;
            cboLang.Enabled = !busy;
            chkTimestamps.Enabled = !busy;
            bar.Visible = busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void SafeInvoke(Action action)
        {
            if (!IsHandleCreated) return;
            try
            {
                if (InvokeRequired) BeginInvoke(action);
                else action();
            }
            catch { /* form mungkin sedang ditutup */ }
        }
    }
}
