using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net.Ggml;

namespace Suarakata
{
    /// <summary>Definisi satu model Whisper (ggml).</summary>
    internal sealed class ModelDef
    {
        public string Key;
        public string Display;
        public string FileName;
        public GgmlType Type;
        public long ApproxBytes;
        public string Quality;
        public string Speed;

        public string Url
        {
            get { return ModelCatalog.BaseUrl + FileName; }
        }
    }

    internal static class ModelCatalog
    {
        public const string BaseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

        public static readonly ModelDef[] All = new[]
        {
            new ModelDef { Key = "tiny",     Display = "Tiny",     FileName = "ggml-tiny.bin",     Type = GgmlType.Tiny,    ApproxBytes = 77_691_713L,    Quality = "Rendah",   Speed = "Sangat cepat" },
            new ModelDef { Key = "base",     Display = "Base",     FileName = "ggml-base.bin",     Type = GgmlType.Base,    ApproxBytes = 147_951_465L,   Quality = "Sedang-",  Speed = "Cepat" },
            new ModelDef { Key = "small",    Display = "Small",    FileName = "ggml-small.bin",    Type = GgmlType.Small,   ApproxBytes = 487_601_967L,   Quality = "Sedang+",  Speed = "Menengah" },
            new ModelDef { Key = "medium",   Display = "Medium",   FileName = "ggml-medium.bin",   Type = GgmlType.Medium,  ApproxBytes = 1_533_763_059L, Quality = "Tinggi",   Speed = "Lambat" },
            new ModelDef { Key = "large-v3", Display = "Large-v3", FileName = "ggml-large-v3.bin", Type = GgmlType.LargeV3, ApproxBytes = 3_095_033_483L, Quality = "Terbaik",  Speed = "Paling lambat" },
        };

        public static ModelDef ByType(GgmlType type)
        {
            foreach (var m in All) if (m.Type == type) return m;
            return All[2];
        }

        public static ModelDef ByKey(string key)
        {
            if (key != null)
                foreach (var m in All)
                    if (string.Equals(m.Key, key.Trim(), StringComparison.OrdinalIgnoreCase)) return m;
            return null;
        }

        public static ModelDef ByFileName(string fileName)
        {
            if (fileName != null)
                foreach (var m in All)
                    if (string.Equals(m.FileName, Path.GetFileName(fileName), StringComparison.OrdinalIgnoreCase)) return m;
            return null;
        }
    }

    internal sealed class DownloadProgress
    {
        public long Received;
        public long Total;
        public double BytesPerSecond;
        public TimeSpan Eta;

        public double Fraction
        {
            get { return Total > 0 ? (double)Received / Total : 0.0; }
        }
    }

    /// <summary>Dilempar bila model yang diminta belum terpasang.</summary>
    internal sealed class ModelMissingException : Exception
    {
        public ModelDef Model { get; private set; }

        public ModelMissingException(ModelDef model)
            : base("Model " + model.Display + " belum diunduh. Buka \"Kelola Model\" untuk mengunduhnya.")
        {
            Model = model;
        }
    }

    /// <summary>
    /// Pengunduh model dengan dukungan jeda dan lanjut (HTTP Range).
    /// Berkas sementara berakhiran .part; menjeda unduhan hanya menghentikan aliran,
    /// berkas .part tetap disimpan sehingga bisa dilanjutkan kapan saja.
    /// </summary>
    internal static class ModelDownloader
    {
        private static readonly HttpClient Http;

        static ModelDownloader()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch { /* runtime lama */ }

            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.None
            };
            Http = new HttpClient(handler);
            Http.Timeout = Timeout.InfiniteTimeSpan;
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("Suarakata/1.2 (+https://github.com/Cyserrex/Suarakata)");
        }

        private static string _defaultModelsDir;

        /// <summary>Folder models di samping executable (dipakai saat aplikasi berjalan portable).</summary>
        public static string AppModelsDir
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models"); }
        }

        /// <summary>
        /// Folder penyimpanan model. Dipakai folder di samping executable bila bisa ditulis
        /// (mode portable); bila aplikasi terpasang di Program Files, folder itu hanya bisa
        /// dibaca sehingga model disimpan di %LocalAppData%\Suarakata\models.
        /// </summary>
        public static string DefaultModelsDir
        {
            get
            {
                if (_defaultModelsDir != null) return _defaultModelsDir;
                _defaultModelsDir = IsWritable(AppModelsDir)
                    ? AppModelsDir
                    : Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Suarakata", "models");
                return _defaultModelsDir;
            }
        }

        private static bool IsWritable(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".tulis-uji");
                File.WriteAllText(probe, "x");
                File.Delete(probe);
                return true;
            }
            catch { return false; }
        }

        /// <summary>Lokasi tujuan penulisan berkas model.</summary>
        public static string PathOf(ModelDef m, string modelsDir = null)
        {
            return Path.Combine(modelsDir ?? DefaultModelsDir, m.FileName);
        }

        /// <summary>
        /// Lokasi model yang benar-benar ada, atau null. Folder di samping executable ikut
        /// diperiksa agar model yang disalin manual ke folder aplikasi tetap terpakai.
        /// </summary>
        public static string ExistingPath(ModelDef m, string modelsDir = null)
        {
            string primary = PathOf(m, modelsDir);
            if (IsCompleteFile(primary, m)) return primary;

            string alt = Path.Combine(AppModelsDir, m.FileName);
            if (!string.Equals(alt, primary, StringComparison.OrdinalIgnoreCase) && IsCompleteFile(alt, m))
                return alt;

            return null;
        }

        private static bool IsCompleteFile(string path, ModelDef m)
        {
            try
            {
                var fi = new FileInfo(path);
                // Ambang aman: minimal 60% dari ukuran perkiraan, mencegah berkas korup dianggap valid.
                return fi.Exists && fi.Length > (long)(m.ApproxBytes * 0.6);
            }
            catch { return false; }
        }

        public static string PartPathOf(ModelDef m, string modelsDir = null)
        {
            return PathOf(m, modelsDir) + ".part";
        }

        public static bool IsInstalled(ModelDef m, string modelsDir = null)
        {
            return ExistingPath(m, modelsDir) != null;
        }

        public static long InstalledBytes(ModelDef m, string modelsDir = null)
        {
            try
            {
                string p = ExistingPath(m, modelsDir);
                return p == null ? 0L : new FileInfo(p).Length;
            }
            catch { return 0L; }
        }

        public static long PartialBytes(ModelDef m, string modelsDir = null)
        {
            try
            {
                var fi = new FileInfo(PartPathOf(m, modelsDir));
                return fi.Exists ? fi.Length : 0L;
            }
            catch { return 0L; }
        }

        public static void DeleteModel(ModelDef m, string modelsDir = null)
        {
            string p = ExistingPath(m, modelsDir);
            if (p != null && File.Exists(p)) File.Delete(p);
        }

        public static void DeletePartial(ModelDef m, string modelsDir = null)
        {
            string p = PartPathOf(m, modelsDir);
            if (File.Exists(p)) File.Delete(p);
        }

        /// <summary>
        /// Mengunduh model. Bila ada berkas .part, unduhan dilanjutkan dari byte terakhir
        /// memakai header Range. Batalkan lewat <paramref name="ct"/> untuk menjeda;
        /// berkas .part tidak dihapus sehingga bisa dilanjutkan.
        /// </summary>
        public static async Task DownloadAsync(
            ModelDef model, string modelsDir, IProgress<DownloadProgress> progress, CancellationToken ct)
        {
            if (model == null) throw new ArgumentNullException("model");
            modelsDir = modelsDir ?? DefaultModelsDir;
            Directory.CreateDirectory(modelsDir);

            string finalPath = PathOf(model, modelsDir);
            string partPath = PartPathOf(model, modelsDir);

            long existing = 0;
            try { if (File.Exists(partPath)) existing = new FileInfo(partPath).Length; }
            catch { existing = 0; }

            using (var request = new HttpRequestMessage(HttpMethod.Get, model.Url))
            {
                if (existing > 0)
                    request.Headers.Range = new RangeHeaderValue(existing, null);

                using (var response = await Http.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
                {
                    if (existing > 0 && response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                    {
                        // .part sudah selesai atau lebih besar dari sumber: coba selesaikan.
                        FinishPart(partPath, finalPath, model, existing);
                        Report(progress, existing, existing, 0, TimeSpan.Zero);
                        return;
                    }

                    response.EnsureSuccessStatusCode();

                    // Server mengabaikan Range: mulai lagi dari nol.
                    if (existing > 0 && response.StatusCode != HttpStatusCode.PartialContent)
                        existing = 0;

                    long total = existing + (response.Content.Headers.ContentLength ?? 0L);
                    if (total <= existing) total = model.ApproxBytes;

                    using (var netStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var file = new FileStream(partPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read,
                                                     81920, FileOptions.SequentialScan))
                    {
                        file.SetLength(existing);
                        file.Seek(existing, SeekOrigin.Begin);

                        var buffer = new byte[131072];
                        long received = existing;
                        var started = DateTime.UtcNow;
                        long baseline = existing;
                        var lastReport = DateTime.UtcNow;
                        double speed = 0;

                        int read;
                        while ((read = await netStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                        {
                            await file.WriteAsync(buffer, 0, read, ct).ConfigureAwait(false);
                            received += read;

                            var now = DateTime.UtcNow;
                            if ((now - lastReport).TotalMilliseconds >= 200)
                            {
                                double elapsed = (now - started).TotalSeconds;
                                if (elapsed > 0.001) speed = (received - baseline) / elapsed;
                                var eta = speed > 1 && total > received
                                    ? TimeSpan.FromSeconds((total - received) / speed)
                                    : TimeSpan.Zero;
                                Report(progress, received, total, speed, eta);
                                lastReport = now;
                            }
                        }

                        await file.FlushAsync(ct).ConfigureAwait(false);
                        file.Dispose();

                        if (total > 0 && received < total)
                            throw new IOException(string.Format(
                                "Unduhan terputus pada {0} dari {1}. Klik Lanjutkan untuk meneruskan.",
                                Ui.HumanBytes(received), Ui.HumanBytes(total)));

                        FinishPart(partPath, finalPath, model, received);
                        Report(progress, received, total <= 0 ? received : total, speed, TimeSpan.Zero);
                    }
                }
            }
        }

        private static void FinishPart(string partPath, string finalPath, ModelDef model, long size)
        {
            if (!File.Exists(partPath)) return;
            if (size < (long)(model.ApproxBytes * 0.6))
                throw new IOException("Berkas hasil unduhan tidak lengkap (" + Ui.HumanBytes(size) + ").");
            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(partPath, finalPath);
        }

        private static void Report(IProgress<DownloadProgress> progress, long received, long total, double speed, TimeSpan eta)
        {
            if (progress == null) return;
            progress.Report(new DownloadProgress
            {
                Received = received,
                Total = total,
                BytesPerSecond = speed,
                Eta = eta
            });
        }

        /// <summary>Menyalin berkas model yang diunduh manual (mis. lewat browser) ke folder models.</summary>
        public static async Task ImportAsync(
            ModelDef model, string sourcePath, string modelsDir, IProgress<DownloadProgress> progress, CancellationToken ct)
        {
            modelsDir = modelsDir ?? DefaultModelsDir;
            Directory.CreateDirectory(modelsDir);

            string finalPath = PathOf(model, modelsDir);
            string tempPath = finalPath + ".import";

            var srcInfo = new FileInfo(sourcePath);
            if (!srcInfo.Exists) throw new FileNotFoundException("Berkas tidak ditemukan.", sourcePath);
            if (srcInfo.Length < (long)(model.ApproxBytes * 0.6))
                throw new IOException(string.Format(
                    "Ukuran berkas ({0}) jauh lebih kecil dari model {1} (~{2}). Pastikan berkas yang dipilih benar dan unduhannya selesai.",
                    Ui.HumanBytes(srcInfo.Length), model.Display, Ui.HumanBytes(model.ApproxBytes)));

            if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(finalPath), StringComparison.OrdinalIgnoreCase))
                return;

            using (var src = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
            using (var dst = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[262144];
                long copied = 0;
                var started = DateTime.UtcNow;
                var lastReport = DateTime.UtcNow;
                int read;
                while ((read = await src.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                {
                    await dst.WriteAsync(buffer, 0, read, ct).ConfigureAwait(false);
                    copied += read;
                    var now = DateTime.UtcNow;
                    if ((now - lastReport).TotalMilliseconds >= 200)
                    {
                        double elapsed = (now - started).TotalSeconds;
                        double speed = elapsed > 0.001 ? copied / elapsed : 0;
                        Report(progress, copied, srcInfo.Length, speed,
                            speed > 1 ? TimeSpan.FromSeconds((srcInfo.Length - copied) / speed) : TimeSpan.Zero);
                        lastReport = now;
                    }
                }
            }

            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(tempPath, finalPath);
            DeletePartial(model, modelsDir);
            Report(progress, srcInfo.Length, srcInfo.Length, 0, TimeSpan.Zero);
        }

        /// <summary>Membuka URL model di peramban bawaan sistem.</summary>
        public static void OpenInBrowser(ModelDef model)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(model.Url)
            {
                UseShellExecute = true
            });
        }

        public static IEnumerable<ModelDef> Models { get { return ModelCatalog.All; } }
    }
}
