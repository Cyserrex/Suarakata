using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Whisper.net.Ggml;

namespace Suarakata
{
    public class MainForm : Form
    {
        private HeaderPanel _header;
        private RoundedButton _btnTheme, _btnModels;
        private Panel _content, _footer;
        private CardPanel _inputCard, _outputCard;
        private DropZone _drop;
        private ModernDropdown _cboModel, _cboLang;
        private Label _lblModel, _lblLang, _lblOut, _lblCount, _lblStatus, _lblSource;
        private Badge _badgeModel, _badgeStatus;
        private ThemedCheckBox _chkTimestamps;
        private RoundedButton _btnRun, _btnCancel, _btnSave, _btnCopy;
        private RoundedProgress _bar;
        private TextView _txtOut;
        private SlimScrollBar _scroll;

        private string _fullText = string.Empty;
        private CancellationTokenSource _cts;
        // Preferensi baru boleh disimpan setelah UI selesai dibangun, supaya event
        // SelectedIndexChanged saat konstruksi tidak menimpa pilihan tersimpan.
        private bool _loaded;

        private static readonly string[] LangCodes =
            { "auto", "id", "id", "en", "ms", "jw", "su", "ar", "zh" };

        private static string ModelsDir
        {
            get { return ModelDownloader.DefaultModelsDir; }
        }

        public MainForm()
        {
            DoubleBuffered = true;
            BuildUi();
            Theme.Changed += ApplyTheme;
            FormClosed += (s, e) => Theme.Changed -= ApplyTheme;
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        }

        // ===================== UI =====================

        private void BuildUi()
        {
            Text = "Suarakata - Speech to Text";
            Font = Ui.F(9.5f);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(980, 740);
            MinimumSize = new Size(860, 640);
            AllowDrop = true;

            BuildHeader();
            BuildFooter();
            BuildContent();

            Controls.Add(_content);
            Controls.Add(_footer);
            Controls.Add(_header);

            DragEnter += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            };
            DragDrop += (s, e) =>
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0) _drop.SelectedFile = files[0];
            };

            RestorePreferences();
            _loaded = true;
            _txtOut.HandleCreated += (s, e) => SyncScrollBar();
            UpdateModelBadge();
            UpdateCounter();
        }

        private void BuildHeader()
        {
            _header = new HeaderPanel
            {
                Dock = DockStyle.Top,
                Height = 78,
                Title = "Suarakata",
                Subtitle = "Ubah rekaman suara menjadi teks, sepenuhnya offline",
                Glyph = Ui.GlyphMic
            };
            // Lebar final ditetapkan sebelum tombol ditambahkan, agar jangkar kanan
            // dihitung dari ukuran sebenarnya (Dock baru berlaku saat panel dipasang ke form).
            _header.Width = ClientSize.Width;

            _btnModels = new RoundedButton
            {
                Text = "Kelola Model",
                Glyph = Ui.GlyphChip,
                Ghost = true,
                Bounds = new Rectangle(ClientSize.Width - 210, 21, 148, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnModels.Click += (s, e) => OpenModelManager(null);

            _btnTheme = new RoundedButton
            {
                Glyph = Theme.IsDark ? Ui.GlyphSun : Ui.GlyphMoon,
                GlyphSize = 13f,
                Ghost = true,
                Bounds = new Rectangle(ClientSize.Width - 54, 21, 40, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnTheme.Click += (s, e) => Theme.Toggle();

            _header.Controls.AddRange(new Control[] { _btnModels, _btnTheme });
            _header.Resize += (s, e) => UpdateHeaderSurfaces();
            UpdateHeaderSurfaces();
        }

        private void UpdateHeaderSurfaces()
        {
            // Tombol di header digambar di atas gradien: samakan warna latarnya.
            _btnModels.Surface = _header.SurfaceAt(_btnModels.Left + _btnModels.Width / 2);
            _btnTheme.Surface = _header.SurfaceAt(_btnTheme.Left + _btnTheme.Width / 2);
            _btnModels.Invalidate();
            _btnTheme.Invalidate();
        }

        private void BuildFooter()
        {
            _footer = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = Theme.Card };
            _footer.Width = ClientSize.Width;
            _footer.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Border, 1))
                    e.Graphics.DrawLine(pen, 0, 0, _footer.Width, 0);
            };

            _badgeStatus = new Badge
            {
                Text = "Siap",
                Accent = Theme.Success,
                Bounds = new Rectangle(20, 14, 78, 22),
                Surface = Theme.Card
            };

            _lblStatus = new Label
            {
                Text = "Pilih file audio lalu klik Transkripsi.",
                ForeColor = Theme.Muted,
                BackColor = Theme.Card,
                AutoSize = false,
                Bounds = new Rectangle(108, 15, 500, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true
            };

            _bar = new RoundedProgress
            {
                Bounds = new Rectangle(20, 46, ClientSize.Width - 160, 7),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Visible = false
            };

            _btnCancel = new RoundedButton
            {
                Text = "Batalkan",
                Glyph = Ui.GlyphStop,
                Danger = true,
                Bounds = new Rectangle(ClientSize.Width - 130, 16, 110, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Surface = Theme.Card,
                Visible = false
            };
            _btnCancel.Click += (s, e) =>
            {
                if (_cts != null)
                {
                    try { _cts.Cancel(); } catch { }
                    SetStatus("Membatalkan...", Theme.Warn, "Batal");
                }
            };

            _footer.Controls.AddRange(new Control[] { _badgeStatus, _lblStatus, _bar, _btnCancel });
        }

        private void BuildContent()
        {
            _content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                Padding = new Padding(0)
            };
            _content.Size = new Size(ClientSize.Width, ClientSize.Height - _header.Height - _footer.Height);

            int cardW = ClientSize.Width - 36;

            // ---------- Kartu sumber ----------
            _inputCard = new CardPanel
            {
                Bounds = new Rectangle(18, 16, cardW, 300),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _lblSource = SectionLabel("SUMBER AUDIO", new Point(20, 16));

            _drop = new DropZone
            {
                Bounds = new Rectangle(18, 40, _inputCard.Width - 36, 122),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _drop.FileChanged += (s, e) =>
            {
                _btnRun.Enabled = !string.IsNullOrEmpty(_drop.SelectedFile);
                SetStatus(string.IsNullOrEmpty(_drop.SelectedFile)
                    ? "Pilih file audio lalu klik Transkripsi."
                    : "Siap mentranskripsi " + Path.GetFileName(_drop.SelectedFile) + ".",
                    Theme.Success, "Siap");
            };

            _lblModel = SectionLabel("MODEL", new Point(20, 178));
            _cboModel = new ModernDropdown { Bounds = new Rectangle(18, 198, 212, 34) };
            foreach (var m in ModelCatalog.All)
                _cboModel.Items.Add(string.Format("{0}  ({1})", m.Display, Ui.HumanBytes(m.ApproxBytes)));
            _cboModel.SelectedIndex = 2;
            _cboModel.SelectedIndexChanged += (s, e) =>
            {
                if (_loaded) Settings.Set("model", SelectedModel().Key);
                UpdateModelBadge();
            };

            _badgeModel = new Badge
            {
                Bounds = new Rectangle(238, 202, 138, 24),
                Surface = Theme.Card,
                Cursor = Cursors.Hand
            };
            _badgeModel.Click += (s, e) => OpenModelManager(SelectedModel());

            _lblLang = SectionLabel("BAHASA", new Point(384, 178));
            _cboLang = new ModernDropdown { Bounds = new Rectangle(382, 198, 176, 34) };
            foreach (var l in new[]
            {
                "Auto-deteksi", "Indonesia", "Banjar (via Indonesia)", "Inggris",
                "Melayu", "Jawa", "Sunda", "Arab", "Mandarin"
            })
                _cboLang.Items.Add(l);
            _cboLang.SelectedIndex = 1;
            _cboLang.SelectedIndexChanged += (s, e) => { if (_loaded) Settings.Set("lang", _cboLang.SelectedIndex); };

            _chkTimestamps = new ThemedCheckBox
            {
                Text = "Sertakan waktu",
                Bounds = new Rectangle(570, 198, 162, 34),
                ForeColor = Theme.Text,
                BackColor = Theme.Card
            };
            _chkTimestamps.CheckedChanged += (s, e) =>
            {
                if (_loaded) Settings.Set("timestamps", _chkTimestamps.Checked);
                UpdateCounter();
            };

            _btnRun = new RoundedButton
            {
                Text = "Transkripsi",
                Glyph = Ui.GlyphPlay,
                GlyphSize = 12f,
                Primary = true,
                Font = Ui.F(11f, FontStyle.Bold),
                Bounds = new Rectangle(_inputCard.Width - 18 - 186, 190, 186, 50),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Surface = Theme.Card,
                Enabled = false
            };
            _btnRun.Click += BtnRun_Click;

            var hint = new Label
            {
                Text = "Model diunduh sekali dan tersimpan di folder models. Unduhan bisa dijeda lalu dilanjutkan.",
                Font = Ui.F(8.5f),
                ForeColor = Theme.Muted,
                BackColor = Theme.Card,
                AutoSize = false,
                Bounds = new Rectangle(20, 254, _inputCard.Width - 40, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true,
                Tag = "muted"
            };

            _inputCard.Controls.AddRange(new Control[]
            {
                _lblSource, _drop, _lblModel, _cboModel, _badgeModel,
                _lblLang, _cboLang, _chkTimestamps, _btnRun, hint
            });

            // ---------- Kartu hasil ----------
            _outputCard = new CardPanel
            {
                Bounds = new Rectangle(18, 330, cardW, _content.ClientSize.Height - 330 - 16),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            _lblOut = SectionLabel("HASIL TRANSKRIPSI", new Point(20, 18));

            _lblCount = new Label
            {
                Text = "0 kata",
                Font = Ui.F(8.5f),
                ForeColor = Theme.Muted,
                BackColor = Theme.Card,
                AutoSize = true,
                Location = new Point(160, 18),
                Tag = "muted"
            };

            _btnCopy = new RoundedButton
            {
                Text = "Salin",
                Glyph = Ui.GlyphCopy,
                Bounds = new Rectangle(_outputCard.Width - 18 - 96, 12, 96, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Surface = Theme.Card,
                Enabled = false
            };
            _btnCopy.Click += BtnCopy_Click;

            _btnSave = new RoundedButton
            {
                Text = "Simpan .txt",
                Glyph = Ui.GlyphSave,
                Bounds = new Rectangle(_outputCard.Width - 18 - 96 - 8 - 122, 12, 122, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Surface = Theme.Card,
                Enabled = false
            };
            _btnSave.Click += BtnSave_Click;

            _txtOut = new TextView
            {
                Bounds = new Rectangle(18, 56, _outputCard.Width - 36 - 16, _outputCard.Height - 56 - 18),
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = Ui.F(11f)
            };
            _txtOut.TextChanged += (s, e) => UpdateCounter();

            _scroll = new SlimScrollBar
            {
                Bounds = new Rectangle(_outputCard.Width - 18 - 10, 58, 10, _outputCard.Height - 58 - 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right
            };
            _scroll.ValueChanged += (s, e) => _txtOut.ScrollToLine(_scroll.Value);
            _txtOut.ViewChanged += (s, e) => SyncScrollBar();

            _outputCard.Controls.AddRange(new Control[] { _lblOut, _lblCount, _btnSave, _btnCopy, _txtOut, _scroll });

            _content.Controls.Add(_inputCard);
            _content.Controls.Add(_outputCard);
        }

        /// <summary>Menyelaraskan scrollbar tipis dengan posisi gulir kotak hasil.</summary>
        private void SyncScrollBar()
        {
            if (_scroll == null || _txtOut == null) return;
            _scroll.Maximum = Math.Max(1, _txtOut.TotalLines);
            _scroll.LargeChange = _txtOut.VisibleLines;
            _scroll.Value = _txtOut.FirstVisibleLine;
            _scroll.Visible = _scroll.NeedsScroll;
        }

        private Label SectionLabel(string text, Point location)
        {
            return new Label
            {
                Text = text,
                Font = Ui.F(8f, FontStyle.Bold),
                ForeColor = Theme.Muted,
                BackColor = Theme.Card,
                AutoSize = true,
                Location = location,
                Tag = "muted"
            };
        }

        // ===================== TEMA & STATUS =====================

        private void ApplyTheme()
        {
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            _content.BackColor = Theme.Bg;
            _inputCard.BackColor = Theme.Bg;
            _outputCard.BackColor = Theme.Bg;
            _footer.BackColor = Theme.Card;
            _btnTheme.Glyph = Theme.IsDark ? Ui.GlyphSun : Ui.GlyphMoon;

            RecolorChildren(_inputCard);
            RecolorChildren(_outputCard);
            RecolorChildren(_footer);

            _cboModel.Invalidate();
            _cboLang.Invalidate();
            _txtOut.BackColor = Theme.Card;
            _txtOut.ForeColor = Theme.Text;
            _chkTimestamps.BackColor = Theme.Card;
            _chkTimestamps.ForeColor = Theme.Text;

            UpdateHeaderSurfaces();
            UpdateModelBadge();
            Invalidate(true);
        }

        private static void RecolorChildren(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                var lbl = c as Label;
                if (lbl != null)
                {
                    lbl.BackColor = Theme.Card;
                    lbl.ForeColor = (lbl.Tag as string) == "muted" ? Theme.Muted : Theme.Text;
                    continue;
                }
                var btn = c as RoundedButton;
                if (btn != null && !btn.Ghost) { btn.Surface = Theme.Card; btn.Invalidate(); continue; }
                var badge = c as Badge;
                if (badge != null) { badge.Surface = Theme.Card; badge.Invalidate(); }
            }
        }

        private void SetStatus(string text, Color accent, string badge)
        {
            SafeInvoke(() =>
            {
                _lblStatus.Text = text;
                _badgeStatus.Text = badge;
                _badgeStatus.Accent = accent;
                _badgeStatus.Width = TextRenderer.MeasureText(badge, _badgeStatus.Font).Width + 34;
                _lblStatus.Left = _badgeStatus.Right + 12;
                _badgeStatus.Invalidate();
            });
        }

        private ModelDef SelectedModel()
        {
            int i = _cboModel.SelectedIndex;
            if (i < 0 || i >= ModelCatalog.All.Length) i = 2;
            return ModelCatalog.All[i];
        }

        private void UpdateModelBadge()
        {
            var m = SelectedModel();
            bool installed = ModelDownloader.IsInstalled(m, ModelsDir);
            long partial = ModelDownloader.PartialBytes(m, ModelsDir);

            if (installed)
            {
                _badgeModel.Text = "Model siap";
                _badgeModel.Accent = Theme.Success;
            }
            else if (partial > 0)
            {
                _badgeModel.Text = "Unduhan tertunda";
                _badgeModel.Accent = Theme.Warn;
            }
            else
            {
                _badgeModel.Text = "Perlu diunduh";
                _badgeModel.Accent = Theme.Danger;
            }
            _badgeModel.Invalidate();
        }

        private void UpdateCounter()
        {
            string text = _chkTimestamps.Checked ? _txtOut.Text : _fullText;
            int words = 0;
            if (!string.IsNullOrWhiteSpace(text))
                words = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
            _lblCount.Text = string.Format("{0} kata  |  {1} karakter", words, text == null ? 0 : text.Length);
            _lblCount.Left = _lblOut.Right + 14;
        }

        private void RestorePreferences()
        {
            var saved = ModelCatalog.ByKey(Settings.Get("model", "small"));
            if (saved != null)
            {
                for (int i = 0; i < ModelCatalog.All.Length; i++)
                    if (ModelCatalog.All[i] == saved) _cboModel.SelectedIndex = i;
            }
            int lang = Settings.GetInt("lang", 1);
            if (lang >= 0 && lang < _cboLang.Items.Count) _cboLang.SelectedIndex = lang;
            _chkTimestamps.Checked = Settings.GetBool("timestamps", false);
        }

        // ===================== AKSI =====================

        private void OpenModelManager(ModelDef highlight)
        {
            using (var form = new ModelManagerForm(ModelsDir) { Highlight = highlight })
            {
                form.ModelsChanged += (s, e) => UpdateModelBadge();
                form.ShowDialog(this);
            }
            UpdateModelBadge();
        }

        private async void BtnRun_Click(object sender, EventArgs e)
        {
            string audio = (_drop.SelectedFile ?? string.Empty).Trim().Trim('"');
            if (string.IsNullOrEmpty(audio) || !File.Exists(audio))
            {
                ModernDialog.Warn(this, "File audio belum dipilih",
                    "Seret sebuah file audio ke jendela ini atau klik area sumber audio untuk memilihnya.");
                return;
            }

            var model = SelectedModel();
            if (!ModelDownloader.IsInstalled(model, ModelsDir))
            {
                bool buka = ModernDialog.Confirm(this, "Model belum tersedia",
                    "Model " + model.Display + " berukuran sekitar " + Ui.HumanBytes(model.ApproxBytes) +
                    " dan belum terpasang. Buka Kelola Model untuk mengunduhnya sekarang?",
                    "Buka Kelola Model", "Nanti");
                if (buka) OpenModelManager(model);
                if (!ModelDownloader.IsInstalled(model, ModelsDir)) return;
            }

            string lang = LangCodes[_cboLang.SelectedIndex];
            bool timestamps = _chkTimestamps.Checked;

            _cts = new CancellationTokenSource();
            SetBusy(true);
            _txtOut.Clear();
            _fullText = string.Empty;
            _bar.Value = 0;
            _bar.Indeterminate = true;

            Action<string> status = s => SetStatus(s, Theme.Accent, "Proses");
            Action<string> onLine = l => SafeInvoke(() =>
            {
                _txtOut.AppendText(l + Environment.NewLine);
                SyncScrollBar();
            });
            Action<double> onProgress = p => SafeInvoke(() =>
            {
                if (_bar.Indeterminate) _bar.Indeterminate = false;
                _bar.Value = p;
            });

            try
            {
                _fullText = await Transcriber.TranscribeAsync(
                    audio, model.Type, lang, timestamps, ModelsDir, status, onLine, onProgress, _cts.Token);

                SafeInvoke(() =>
                {
                    _bar.Indeterminate = false;
                    _bar.Value = 1;
                    SetStatus("Selesai. " + _txtOut.Lines.Length + " baris dihasilkan.", Theme.Success, "Selesai");
                    _btnSave.Enabled = _fullText.Length > 0;
                    _btnCopy.Enabled = _fullText.Length > 0;
                    UpdateCounter();
                });
            }
            catch (OperationCanceledException)
            {
                SetStatus("Transkripsi dibatalkan.", Theme.Warn, "Batal");
                SafeInvoke(() =>
                {
                    _btnSave.Enabled = _txtOut.TextLength > 0;
                    _btnCopy.Enabled = _txtOut.TextLength > 0;
                });
            }
            catch (ModelMissingException ex)
            {
                SetStatus("Model belum tersedia.", Theme.Danger, "Gagal");
                if (ModernDialog.Confirm(this, "Model belum tersedia", ex.Message,
                        "Buka Kelola Model", "Nanti"))
                    OpenModelManager(ex.Model);
            }
            catch (Exception ex)
            {
                SetStatus("Gagal: " + ex.Message, Theme.Danger, "Gagal");
                ModernDialog.Error(this, "Gagal transkripsi", ex.Message);
            }
            finally
            {
                if (_cts != null) { _cts.Dispose(); _cts = null; }
                SetBusy(false);
                UpdateModelBadge();
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "Teks|*.txt";
                string src = (_drop.SelectedFile ?? string.Empty).Trim().Trim('"');
                dlg.FileName = (string.IsNullOrEmpty(src) ? "transkrip" : Path.GetFileNameWithoutExtension(src)) + ".txt";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    File.WriteAllText(dlg.FileName,
                        _chkTimestamps.Checked ? _txtOut.Text : _fullText,
                        new System.Text.UTF8Encoding(true));
                    SetStatus("Tersimpan: " + dlg.FileName, Theme.Success, "Tersimpan");
                }
            }
        }

        private void BtnCopy_Click(object sender, EventArgs e)
        {
            string text = _chkTimestamps.Checked ? _txtOut.Text : _fullText;
            if (!string.IsNullOrEmpty(text))
            {
                Clipboard.SetText(text);
                SetStatus("Teks disalin ke clipboard.", Theme.Success, "Disalin");
            }
        }

        private void SetBusy(bool busy)
        {
            _btnRun.Enabled = !busy && !string.IsNullOrEmpty(_drop.SelectedFile);
            _drop.Enabled = !busy;
            _cboModel.Enabled = !busy;
            _cboLang.Enabled = !busy;
            _chkTimestamps.Enabled = !busy;
            _btnModels.Enabled = !busy;
            _bar.Visible = busy;
            _btnCancel.Visible = busy;
            Cursor = busy ? Cursors.AppStarting : Cursors.Default;
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
