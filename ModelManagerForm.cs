using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Suarakata
{
    /// <summary>Jendela pengelola model: unduh, jeda, lanjutkan, unduh via browser, impor, hapus.</summary>
    internal class ModelManagerForm : Form
    {
        private readonly string _modelsDir;
        private readonly List<ModelRow> _rows = new List<ModelRow>();
        private HeaderPanel _header;
        private Panel _list;
        private Label _lblDir;
        private RoundedButton _btnFolder, _btnClose;

        public ModelManagerForm(string modelsDir)
        {
            _modelsDir = modelsDir ?? ModelDownloader.DefaultModelsDir;
            DoubleBuffered = true;
            BuildUi();
            Theme.Changed += ApplyTheme;
            FormClosed += (s, e) => Theme.Changed -= ApplyTheme;
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        }

        /// <summary>Model yang ingin disorot saat jendela dibuka.</summary>
        public ModelDef Highlight { get; set; }

        private void BuildUi()
        {
            Text = "Kelola Model";
            Font = Ui.F(9.5f);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            StartPosition = FormStartPosition.CenterParent;
            // Tinggi dipas agar kelima model muat tanpa scrollbar sistem.
            ClientSize = new Size(760, 730);
            MinimumSize = new Size(700, 520);
            ShowInTaskbar = false;
            MaximizeBox = false;

            _header = new HeaderPanel
            {
                Dock = DockStyle.Top,
                Height = 74,
                Title = "Kelola Model",
                Subtitle = "Unduh, jeda, dan lanjutkan model Whisper kapan saja",
                Glyph = Ui.GlyphChip
            };
            _header.Width = ClientSize.Width;

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 62, BackColor = Theme.Card };
            // Lebar final sebelum kontrol berjangkar-kanan ditambahkan (Dock baru berlaku saat dipasang ke form).
            footer.Width = ClientSize.Width;
            footer.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Border, 1))
                    e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };

            _lblDir = new Label
            {
                Text = "Folder model: " + _modelsDir,
                AutoSize = false,
                ForeColor = Theme.Muted,
                Font = Ui.F(8.5f),
                Bounds = new Rectangle(20, 22, 380, 20),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                AutoEllipsis = true
            };

            _btnFolder = new RoundedButton
            {
                Text = "Buka folder",
                Glyph = Ui.GlyphFolder,
                Bounds = new Rectangle(ClientSize.Width - 260, 13, 130, 36),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Surface = Theme.Card
            };
            _btnFolder.Click += (s, e) =>
            {
                try
                {
                    Directory.CreateDirectory(_modelsDir);
                    Process.Start(new ProcessStartInfo(_modelsDir) { UseShellExecute = true });
                }
                catch (Exception ex) { Warn(ex.Message); }
            };

            _btnClose = new RoundedButton
            {
                Text = "Tutup",
                Primary = true,
                Bounds = new Rectangle(ClientSize.Width - 120, 13, 100, 36),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Surface = Theme.Card
            };
            _btnClose.Click += (s, e) => Close();

            footer.Controls.AddRange(new Control[] { _lblDir, _btnFolder, _btnClose });

            _list = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                AutoScroll = true,
                Padding = new Padding(16, 12, 16, 12)
            };
            _list.Size = new Size(ClientSize.Width, ClientSize.Height - _header.Height - footer.Height);

            int y = 12;
            foreach (var m in ModelCatalog.All)
            {
                var row = new ModelRow(m, _modelsDir)
                {
                    Bounds = new Rectangle(16, y, _list.ClientSize.Width - 52, 104),
                    Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
                };
                row.StateChanged += (s, e) => OnModelsChanged();
                _rows.Add(row);
                _list.Controls.Add(row);
                y += 114;
            }

            _list.HandleCreated += (s, e) => NativeTheme.Apply(_list);

            Controls.Add(_list);
            Controls.Add(footer);
            Controls.Add(_header);

            Shown += (s, e) =>
            {
                if (Highlight != null)
                {
                    foreach (var r in _rows)
                        if (r.Model == Highlight) { _list.ScrollControlIntoView(r); r.Flash(); }
                }
            };

            FormClosing += (s, e) =>
            {
                foreach (var r in _rows)
                {
                    if (r.IsDownloading)
                    {
                        bool jeda = ModernDialog.Confirm(this, "Unduhan sedang berjalan",
                            "Jeda semua unduhan lalu tutup jendela ini? Kemajuan unduhan tetap tersimpan " +
                            "dan bisa dilanjutkan kapan saja.",
                            "Jeda dan tutup", "Tetap di sini");
                        if (!jeda) { e.Cancel = true; return; }
                        foreach (var rr in _rows) rr.PauseIfRunning();
                        break;
                    }
                }
            };
        }

        /// <summary>Dipicu saat daftar model terpasang berubah.</summary>
        public event EventHandler ModelsChanged;

        private void OnModelsChanged()
        {
            var h = ModelsChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        private void ApplyTheme()
        {
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            _list.BackColor = Theme.Bg;
            _lblDir.ForeColor = Theme.Muted;
            foreach (Control c in Controls)
                if (c is Panel && c.Dock == DockStyle.Bottom) c.BackColor = Theme.Card;
            _btnFolder.Surface = Theme.Card;
            _btnClose.Surface = Theme.Card;
            foreach (var r in _rows) r.ApplyTheme();
            NativeTheme.Apply(_list);
            Invalidate(true);
        }

        private void Warn(string msg)
        {
            ModernDialog.Warn(this, "Perhatian", msg);
        }
    }

    /// <summary>Satu baris model di dalam pengelola model.</summary>
    internal class ModelRow : CardPanel
    {
        public ModelDef Model { get; private set; }

        private readonly string _modelsDir;
        private readonly Badge _badge;
        private readonly RoundedProgress _bar;
        private readonly Label _lblName, _lblMeta, _lblProgress;
        private readonly RoundedButton _btnMain, _btnSecondary, _btnMore;
        private readonly System.Windows.Forms.Timer _flashTimer;

        private CancellationTokenSource _cts;
        private bool _paused;
        private int _flashCount;

        public event EventHandler StateChanged;

        public bool IsDownloading { get { return _cts != null; } }

        public ModelRow(ModelDef model, string modelsDir)
        {
            Model = model;
            _modelsDir = modelsDir;
            Radius = 12;
            Shadow = false;
            BackColor = Theme.Bg;

            _lblName = new Label
            {
                Text = model.Display,
                Font = Ui.F(12f, FontStyle.Bold),
                ForeColor = Theme.Text,
                BackColor = Theme.Card,
                AutoSize = true,
                Location = new Point(18, 14)
            };

            _badge = new Badge
            {
                Bounds = new Rectangle(120, 17, 150, 22),
                Surface = Theme.Card
            };

            _lblMeta = new Label
            {
                Text = string.Format("{0}  |  Akurasi {1}  |  {2}", Ui.HumanBytes(model.ApproxBytes), model.Quality, model.Speed),
                Font = Ui.F(8.75f),
                ForeColor = Theme.Muted,
                BackColor = Theme.Card,
                AutoSize = true,
                Location = new Point(20, 44)
            };

            _bar = new RoundedProgress
            {
                Bounds = new Rectangle(20, 72, 300, 7),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                Visible = false
            };

            _lblProgress = new Label
            {
                Text = string.Empty,
                Font = Ui.F(8.25f),
                ForeColor = Theme.Muted,
                BackColor = Theme.Card,
                AutoSize = false,
                Bounds = new Rectangle(20, 82, 400, 16),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                Visible = false
            };

            _btnMore = new RoundedButton
            {
                Glyph = Ui.GlyphMore,
                GlyphSize = 13f,
                Bounds = new Rectangle(0, 0, 40, 34),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Surface = Theme.Card
            };
            _btnSecondary = new RoundedButton
            {
                Text = "Batal",
                Bounds = new Rectangle(0, 0, 86, 34),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Surface = Theme.Card,
                Visible = false
            };
            _btnMain = new RoundedButton
            {
                Text = "Unduh",
                Glyph = Ui.GlyphDownload,
                Primary = true,
                Bounds = new Rectangle(0, 0, 122, 34),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Surface = Theme.Card
            };

            _btnMain.Click += BtnMain_Click;
            _btnSecondary.Click += BtnSecondary_Click;

            _btnMore.Click += (s, e) => ShowMenu();

            Controls.AddRange(new Control[] { _lblName, _badge, _lblMeta, _bar, _lblProgress, _btnMain, _btnSecondary, _btnMore });

            _flashTimer = new System.Windows.Forms.Timer { Interval = 220 };
            _flashTimer.Tick += (s, e) =>
            {
                _flashCount++;
                Shadow = _flashCount % 2 == 1;
                Invalidate();
                if (_flashCount >= 6) { _flashTimer.Stop(); Shadow = false; Invalidate(); }
            };

            Resize += (s, e) => LayoutButtons();
            LayoutButtons();
            RefreshState();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _flashTimer.Dispose();
                if (_cts != null) { try { _cts.Cancel(); } catch { } _cts.Dispose(); _cts = null; }
            }
            base.Dispose(disposing);
        }

        public void Flash()
        {
            _flashCount = 0;
            _flashTimer.Start();
        }

        private void LayoutButtons()
        {
            int right = Width - 20;
            _btnMore.Left = right - _btnMore.Width;
            _btnMore.Top = 18;
            _btnMain.Left = _btnMore.Left - 10 - _btnMain.Width;
            _btnMain.Top = 18;
            _btnSecondary.Left = _btnMain.Left - 8 - _btnSecondary.Width;
            _btnSecondary.Top = 18;

            int barRight = _btnSecondary.Visible ? _btnSecondary.Left : _btnMain.Left;
            _bar.Width = Math.Max(80, barRight - 20 - 16);
            _lblProgress.Width = Math.Max(120, Width - 40);
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Bg;
            _lblName.ForeColor = Theme.Text;
            _lblName.BackColor = Theme.Card;
            _lblMeta.ForeColor = Theme.Muted;
            _lblMeta.BackColor = Theme.Card;
            _lblProgress.ForeColor = Theme.Muted;
            _lblProgress.BackColor = Theme.Card;
            _badge.Surface = Theme.Card;
            _btnMain.Surface = Theme.Card;
            _btnSecondary.Surface = Theme.Card;
            _btnMore.Surface = Theme.Card;
            Invalidate(true);
        }

        /// <summary>Menyegarkan tampilan sesuai status berkas di disk.</summary>
        public void RefreshState()
        {
            bool installed = ModelDownloader.IsInstalled(Model, _modelsDir);
            long partial = ModelDownloader.PartialBytes(Model, _modelsDir);

            if (IsDownloading)
            {
                _badge.Text = "Mengunduh";
                _badge.Accent = Theme.Accent;
                _btnMain.Text = "Jeda";
                _btnMain.Glyph = Ui.GlyphPause;
                _btnMain.Primary = false;
                _btnMain.Enabled = true;
                _btnSecondary.Text = "Batal";
                _btnSecondary.Visible = true;
                _bar.Visible = true;
                _lblProgress.Visible = true;
            }
            else if (installed)
            {
                _badge.Text = "Terpasang";
                _badge.Accent = Theme.Success;
                _btnMain.Text = "Terpasang";
                _btnMain.Glyph = Ui.GlyphCheck;
                _btnMain.Primary = false;
                _btnMain.Enabled = false;
                _btnSecondary.Visible = false;
                _bar.Visible = false;
                _lblProgress.Visible = true;
                _lblProgress.Text = "Ukuran di disk: " + Ui.HumanBytes(ModelDownloader.InstalledBytes(Model, _modelsDir));
            }
            else if (partial > 0)
            {
                _badge.Text = _paused ? "Dijeda" : "Belum selesai";
                _badge.Accent = Theme.Warn;
                _btnMain.Text = "Lanjutkan";
                _btnMain.Glyph = Ui.GlyphPlay;
                _btnMain.Primary = true;
                _btnMain.Enabled = true;
                _btnSecondary.Text = "Batal";
                _btnSecondary.Visible = true;
                _bar.Visible = true;
                _bar.Indeterminate = false;
                _bar.Value = Model.ApproxBytes > 0 ? (double)partial / Model.ApproxBytes : 0;
                _lblProgress.Visible = true;
                _lblProgress.Text = string.Format("Tertunda di {0} dari ~{1}  |  siap dilanjutkan",
                    Ui.HumanBytes(partial), Ui.HumanBytes(Model.ApproxBytes));
            }
            else
            {
                _badge.Text = "Belum diunduh";
                _badge.Accent = Theme.Muted;
                _btnMain.Text = "Unduh";
                _btnMain.Glyph = Ui.GlyphDownload;
                _btnMain.Primary = true;
                _btnMain.Enabled = true;
                _btnSecondary.Visible = false;
                _bar.Visible = false;
                _lblProgress.Visible = false;
            }

            LayoutButtons();
            Invalidate(true);

            var h = StateChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        private async void BtnMain_Click(object sender, EventArgs e)
        {
            if (IsDownloading) { PauseIfRunning(); return; }
            await StartDownloadAsync();
        }

        private void BtnSecondary_Click(object sender, EventArgs e)
        {
            // Batal: hentikan unduhan dan buang berkas .part
            if (IsDownloading)
            {
                _paused = false;
                try { _cts.Cancel(); } catch { }
                // Berkas .part dihapus setelah tugas berhenti (lihat StartDownloadAsync).
                _discardOnStop = true;
                return;
            }
            DeletePartial();
        }

        private bool _discardOnStop;

        public void PauseIfRunning()
        {
            if (!IsDownloading) return;
            _paused = true;
            _discardOnStop = false;
            try { _cts.Cancel(); } catch { }
        }

        private async Task StartDownloadAsync()
        {
            _cts = new CancellationTokenSource();
            _paused = false;
            _discardOnStop = false;
            _bar.Visible = true;
            _bar.Indeterminate = true;
            _lblProgress.Visible = true;
            _lblProgress.Text = "Menghubungi server...";
            RefreshState();

            var progress = new Progress<DownloadProgress>(p =>
            {
                if (_bar.Indeterminate) _bar.Indeterminate = false;
                _bar.Value = p.Fraction;
                _lblProgress.Text = string.Format("{0} dari {1}  ({2:0.0}%)  |  {3}  |  sisa {4}",
                    Ui.HumanBytes(p.Received), Ui.HumanBytes(p.Total), p.Fraction * 100,
                    Ui.HumanSpeed(p.BytesPerSecond), Ui.HumanEta(p.Eta));
            });

            try
            {
                await ModelDownloader.DownloadAsync(Model, _modelsDir, progress, _cts.Token);
                _lblProgress.Text = "Selesai diunduh.";
            }
            catch (OperationCanceledException)
            {
                if (_discardOnStop)
                {
                    try { ModelDownloader.DeletePartial(Model, _modelsDir); } catch { }
                }
            }
            catch (Exception ex)
            {
                ModernDialog.Error(FindForm(), "Gagal mengunduh model " + Model.Display,
                    ex.Message + " Kemajuan unduhan tersimpan, klik Lanjutkan untuk meneruskan.");
            }
            finally
            {
                if (_cts != null) { _cts.Dispose(); _cts = null; }
                _bar.Indeterminate = false;
                RefreshState();
            }
        }

        private void ShowMenu()
        {
            bool adaPart = ModelDownloader.PartialBytes(Model, _modelsDir) > 0;
            bool terpasang = ModelDownloader.IsInstalled(Model, _modelsDir);

            var entries = new List<MenuEntry>
            {
                new MenuEntry { Text = "Unduh lewat browser", Glyph = Ui.GlyphGlobe, Action = OpenInBrowser },
                new MenuEntry { Text = "Impor berkas model (.bin)", Glyph = Ui.GlyphImport, Action = ImportFile },
                new MenuEntry { Text = "Salin tautan unduhan", Glyph = Ui.GlyphCopy, Action = CopyUrl },
                MenuEntry.Sep(),
                new MenuEntry
                {
                    Text = "Hapus unduhan tertunda", Glyph = Ui.GlyphCancel,
                    Action = DeletePartial, Danger = true, Disabled = !adaPart
                },
                new MenuEntry
                {
                    Text = "Hapus model dari disk", Glyph = Ui.GlyphDelete,
                    Action = DeleteModel, Danger = true, Disabled = !terpasang
                }
            };

            PopupMenu.Show(_btnMore, entries);
        }

        private void CopyUrl()
        {
            try
            {
                Clipboard.SetText(Model.Url);
                ModernDialog.Success(FindForm(), "Tautan disalin",
                    "Tautan unduhan model " + Model.Display + " sudah disalin ke clipboard.");
            }
            catch (Exception ex)
            {
                ModernDialog.Error(FindForm(), "Gagal menyalin", ex.Message);
            }
        }

        private void OpenInBrowser()
        {
            try
            {
                ModelDownloader.OpenInBrowser(Model);
                ModernDialog.Info(FindForm(), "Unduh lewat browser",
                    "Tautan model dibuka di peramban. Simpan berkas " + Model.FileName + " di sana " +
                    "(unduhan peramban bisa dijeda dan dilanjutkan), lalu kembali ke jendela ini dan pilih " +
                    "Impor berkas model (.bin).");
            }
            catch (Exception ex)
            {
                ModernDialog.Error(FindForm(), "Gagal membuka peramban", ex.Message);
            }
        }

        private async void ImportFile()
        {
            string source;
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Pilih berkas model " + Model.FileName;
                dlg.Filter = "Model ggml|*.bin|Semua file|*.*";
                dlg.FileName = Model.FileName;
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                source = dlg.FileName;
            }

            var detected = ModelCatalog.ByFileName(source);
            if (detected != null && detected != Model)
            {
                bool lanjut = ModernDialog.Confirm(FindForm(), "Nama berkas berbeda",
                    "Berkas " + Path.GetFileName(source) + " terlihat seperti model " + detected.Display +
                    ", bukan " + Model.Display + ". Tetap impor sebagai " + Model.Display + "?",
                    "Tetap impor", "Batal", DialogKind.Warning);
                if (!lanjut) return;
            }

            _bar.Visible = true;
            _bar.Indeterminate = false;
            _lblProgress.Visible = true;
            _btnMain.Enabled = false;
            _btnMore.Enabled = false;

            var progress = new Progress<DownloadProgress>(p =>
            {
                _bar.Value = p.Fraction;
                _lblProgress.Text = string.Format("Menyalin {0} dari {1}  ({2:0.0}%)",
                    Ui.HumanBytes(p.Received), Ui.HumanBytes(p.Total), p.Fraction * 100);
            });

            try
            {
                await ModelDownloader.ImportAsync(Model, source, _modelsDir, progress, CancellationToken.None);
                _lblProgress.Text = "Berhasil diimpor.";
            }
            catch (Exception ex)
            {
                ModernDialog.Error(FindForm(), "Gagal mengimpor", ex.Message);
            }
            finally
            {
                _btnMore.Enabled = true;
                RefreshState();
            }
        }

        private void DeletePartial()
        {
            long partial = ModelDownloader.PartialBytes(Model, _modelsDir);
            if (partial <= 0)
            {
                ModernDialog.Info(FindForm(), "Tidak ada unduhan tertunda",
                    "Model " + Model.Display + " tidak punya berkas unduhan yang belum selesai.");
                return;
            }
            bool hapus = ModernDialog.Confirm(FindForm(), "Hapus unduhan tertunda",
                "Unduhan model " + Model.Display + " sebesar " + Ui.HumanBytes(partial) +
                " akan dibuang dan harus diulang dari awal.",
                "Hapus", "Batal", DialogKind.Warning);
            if (!hapus) return;
            try { ModelDownloader.DeletePartial(Model, _modelsDir); }
            catch (Exception ex) { ModernDialog.Error(FindForm(), "Gagal menghapus", ex.Message); }
            RefreshState();
        }

        private void DeleteModel()
        {
            if (!ModelDownloader.IsInstalled(Model, _modelsDir))
            {
                ModernDialog.Info(FindForm(), "Model belum terpasang",
                    "Model " + Model.Display + " belum ada di disk, jadi tidak ada yang perlu dihapus.");
                return;
            }
            bool hapus = ModernDialog.Confirm(FindForm(), "Hapus model dari disk",
                "Model " + Model.Display + " (" + Ui.HumanBytes(ModelDownloader.InstalledBytes(Model, _modelsDir)) +
                ") akan dihapus. Model harus diunduh ulang bila ingin dipakai lagi.",
                "Hapus model", "Batal", DialogKind.Warning);
            if (!hapus) return;
            try { ModelDownloader.DeleteModel(Model, _modelsDir); }
            catch (Exception ex) { ModernDialog.Error(FindForm(), "Gagal menghapus", ex.Message); }
            RefreshState();
        }
    }
}
