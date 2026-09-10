using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;

namespace Suarakata
{
    /// <summary>
    /// Inti pipeline speech-to-text: memastikan model tersedia, konversi audio ke WAV 16k mono
    /// via ffmpeg, lalu transkripsi memakai Whisper.net (whisper.cpp).
    /// </summary>
    internal static class Transcriber
    {
        public static GgmlType ParseModel(string s)
        {
            var def = ModelCatalog.ByKey(s);
            if (def != null) return def.Type;
            switch ((s ?? "small").Trim().ToLowerInvariant())
            {
                case "large":
                case "largev3": return GgmlType.LargeV3;
                default: return GgmlType.Small;
            }
        }

        public static string ModelFileName(GgmlType type)
        {
            return ModelCatalog.ByType(type).FileName;
        }

        /// <summary>Mengembalikan lokasi model bila sudah terpasang; jika belum, melempar ModelMissingException.</summary>
        public static string ResolveModelPath(GgmlType type, string modelsDir)
        {
            var def = ModelCatalog.ByType(type);
            string path = ModelDownloader.ExistingPath(def, modelsDir);
            if (path == null) throw new ModelMissingException(def);
            return path;
        }

        public static string FindFfmpeg()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, "ffmpeg.exe"),
                Path.Combine(baseDir, "ffmpeg", "ffmpeg.exe"),
                Path.Combine(baseDir, "ffmpeg", "bin", "ffmpeg.exe"),
            };
            foreach (var c in candidates)
                if (File.Exists(c)) return c;

            try
            {
                var psi = new ProcessStartInfo("where", "ffmpeg")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    string outp = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    foreach (var line in outp.Split('\n'))
                    {
                        string f = line.Trim();
                        if (!string.IsNullOrEmpty(f) && File.Exists(f)) return f;
                    }
                }
            }
            catch { /* abaikan */ }

            return null;
        }

        public static string ConvertToWav(string inputPath, string ffmpegPath)
        {
            string outWav = Path.Combine(Path.GetTempPath(), "stt_" + Guid.NewGuid().ToString("N") + ".wav");
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = string.Format(
                    "-y -hide_banner -loglevel error -i \"{0}\" -vn -ac 1 -ar 16000 -c:a pcm_s16le \"{1}\"",
                    inputPath, outWav),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            using (var p = Process.Start(psi))
            {
                string err = p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0 || !File.Exists(outWav))
                    throw new Exception("Konversi audio gagal (ffmpeg). " + err);
            }
            return outWav;
        }

        /// <summary>Perkiraan durasi WAV 16 kHz mono 16-bit dari ukuran berkas.</summary>
        private static TimeSpan WavDuration(string wavPath)
        {
            try
            {
                long bytes = new FileInfo(wavPath).Length - 44;
                if (bytes <= 0) return TimeSpan.Zero;
                return TimeSpan.FromSeconds(bytes / (16000.0 * 2.0));
            }
            catch { return TimeSpan.Zero; }
        }

        public static async Task<string> TranscribeAsync(
            string audioPath, GgmlType modelType, string language, bool includeTimestamps,
            string modelsDir, Action<string> status, Action<string> onLine,
            Action<double> onProgress = null, CancellationToken ct = default(CancellationToken))
        {
            if (!File.Exists(audioPath))
                throw new FileNotFoundException("File audio tidak ditemukan.", audioPath);

            string modelPath = ResolveModelPath(modelType, modelsDir);

            string ffmpeg = FindFfmpeg();
            if (ffmpeg == null)
                throw new FileNotFoundException(
                    "ffmpeg tidak ditemukan. Letakkan ffmpeg.exe di folder aplikasi (atau sub-folder \\ffmpeg\\), atau tambahkan ke PATH.");

            if (status != null) status("Mengonversi audio ke 16 kHz mono...");
            string wav = await Task.Run(() => ConvertToWav(audioPath, ffmpeg), ct).ConfigureAwait(false);

            try
            {
                ct.ThrowIfCancellationRequested();
                var duration = WavDuration(wav);

                if (status != null) status("Memuat model " + ModelCatalog.ByType(modelType).Display + "...");
                using (var factory = WhisperFactory.FromPath(modelPath))
                {
                    var builder = factory.CreateBuilder();
                    builder = (string.IsNullOrEmpty(language) || language == "auto")
                        ? builder.WithLanguage("auto")
                        : builder.WithLanguage(language);

                    using (var processor = builder.Build())
                    using (var fileStream = File.OpenRead(wav))
                    {
                        if (status != null) status("Mentranskripsi audio...");
                        var sb = new StringBuilder();
                        await foreach (var segment in processor.ProcessAsync(fileStream, ct).ConfigureAwait(false))
                        {
                            string text = (segment.Text ?? string.Empty).Trim();
                            string line = includeTimestamps
                                ? string.Format("[{0:hh\\:mm\\:ss} -> {1:hh\\:mm\\:ss}] {2}", segment.Start, segment.End, text)
                                : text;

                            if (onLine != null) onLine(line);

                            if (onProgress != null && duration.TotalSeconds > 0.5)
                                onProgress(Math.Min(1.0, segment.End.TotalSeconds / duration.TotalSeconds));

                            if (sb.Length > 0) sb.Append(' ');
                            sb.Append(text);
                        }
                        if (onProgress != null) onProgress(1.0);
                        return sb.ToString().Trim();
                    }
                }
            }
            finally
            {
                try { File.Delete(wav); } catch { /* abaikan */ }
            }
        }
    }
}
