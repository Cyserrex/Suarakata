using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Whisper.net.Ggml;

namespace Suarakata
{
    public class MainForm : Form
    {
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
            { "auto", "id", "en", "ms", "jw", "su", "ar", "zh" };

        // Tipe model sejajar dengan item cboModel
        private static readonly GgmlType[] ModelTypes =
            { GgmlType.Tiny, GgmlType.Base, GgmlType.Small, GgmlType.Medium, GgmlType.LargeV3 };

        public MainForm()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "Suarakata — Speech to Text (Whisper)";
            Font = new Font("Segoe UI", 9f);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(800, 620);
            MinimumSize = new Size(680, 520);

            var lblFile = new Label { Text = "File audio (.ogg .opus .mp3 .m4a .wav ...):", Left = 12, Top = 12, Width = 400, AutoSize = true };
            txtFile = new TextBox { Left = 12, Top = 32, Width = 640, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            btnBrowse = new Button { Text = "Telusuri...", Left = 660, Top = 30, Width = 128, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnBrowse.Click += BtnBrowse_Click;

            var lblModel = new Label { Text = "Model:", Left = 12, Top = 70, Width = 45, AutoSize = true };
            cboModel = new ComboBox { Left = 60, Top = 66, Width = 175, DropDownStyle = ComboBoxStyle.DropDownList };
            cboModel.Items.AddRange(new object[]
            {
                "Tiny (~75 MB)",
                "Base (~142 MB)",
                "Small (~466 MB)",
                "Medium (~1.5 GB)",
                "Large-v3 (~3 GB)"
            });
            cboModel.SelectedIndex = 2; // Small

            var lblLang = new Label { Text = "Bahasa:", Left = 250, Top = 70, Width = 50, AutoSize = true };
            cboLang = new ComboBox { Left = 305, Top = 66, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            cboLang.Items.AddRange(new object[]
            {
                "Auto-deteksi",
                "Indonesia",
                "Inggris",
                "Melayu",
                "Jawa",
                "Sunda",
                "Arab",
                "Mandarin"
            });
            cboLang.SelectedIndex = 1; // Indonesia

            chkTimestamps = new CheckBox { Text = "Sertakan waktu", Left = 470, Top = 68, Width = 130, AutoSize = true };

            btnRun = new Button { Text = "Transkripsi", Left = 660, Top = 64, Width = 128, Height = 30, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnRun.Font = new Font(Font, FontStyle.Bold);
            btnRun.Click += BtnRun_Click;

            bar = new ProgressBar { Left = 12, Top = 104, Width = 776, Height = 14, Style = ProgressBarStyle.Marquee, Visible = false, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            lblStatus = new Label { Text = "Siap.", Left = 12, Top = 124, Width = 776, AutoSize = false, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

            txtOut = new TextBox
            {
                Left = 12,
                Top = 150,
                Width = 776,
                Height = 418,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = true,
                ReadOnly = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 10f)
            };

            btnSave = new Button { Text = "Simpan .txt", Left = 12, Top = 580, Width = 120, Height = 28, Enabled = false, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            btnSave.Click += BtnSave_Click;
            btnCopy = new Button { Text = "Salin", Left = 140, Top = 580, Width = 100, Height = 28, Enabled = false, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            btnCopy.Click += BtnCopy_Click;

            Controls.AddRange(new Control[]
            {
                lblFile, txtFile, btnBrowse,
                lblModel, cboModel, lblLang, cboLang, chkTimestamps, btnRun,
                bar, lblStatus, txtOut, btnSave, btnCopy
            });

            AllowDrop = true;
            DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop += (s, e) =>
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0) txtFile.Text = files[0];
            };
        }

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
