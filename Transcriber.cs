using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;

namespace Suarakata
{
    /// <summary>
    /// Inti pipeline speech-to-text: unduh model ggml, konversi audio ke WAV 16k mono via ffmpeg,
    /// lalu transkripsi memakai Whisper.net (whisper.cpp).
    /// </summary>
    internal static class Transcriber
    {
        public static GgmlType ParseModel(string s)
        {
            switch ((s ?? "small").Trim().ToLowerInvariant())
            {
                case "tiny": return GgmlType.Tiny;
                case "base": return GgmlType.Base;
                case "small": return GgmlType.Small;
                case "medium": return GgmlType.Medium;
                case "large":
                case "largev3":
                case "large-v3": return GgmlType.LargeV3;
                default: return GgmlType.Small;
            }
        }

        public static string ModelFileName(GgmlType type)
        {
            switch (type)
            {
                case GgmlType.Tiny: return "ggml-tiny.bin";
                case GgmlType.Base: return "ggml-base.bin";
                case GgmlType.Small: return "ggml-small.bin";
                case GgmlType.Medium: return "ggml-medium.bin";
                case GgmlType.LargeV3: return "ggml-large-v3.bin";
                default: return "ggml-" + type.ToString().ToLowerInvariant() + ".bin";
            }
        }

        public static async Task<string> EnsureModelAsync(GgmlType type, string modelsDir, Action<string> status)
        {
            Directory.CreateDirectory(modelsDir);
            string path = Path.Combine(modelsDir, ModelFileName(type));
            if (File.Exists(path) && new FileInfo(path).Length > 1_000_000)
                return path;

            status?.Invoke("Mengunduh model " + type + " ...");
            string tmp = path + ".download";
            using (var src = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(type).ConfigureAwait(false))
            using (var dst = File.Create(tmp))
            {
                byte[] buffer = new byte[131072];
                long total = 0;
                int read;
                while ((read = await src.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                {
                    dst.Write(buffer, 0, read);
                    total += read;
                    status?.Invoke(string.Format("Mengunduh model {0}: {1:0.0} MB", type, total / 1048576.0));
                }
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
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

            // Cari di PATH
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

        public static async Task<string> TranscribeAsync(
            string audioPath, GgmlType modelType, string language, bool includeTimestamps,
            string modelsDir, Action<string> status, Action<string> onLine)
        {
            if (!File.Exists(audioPath))
                throw new FileNotFoundException("File audio tidak ditemukan.", audioPath);

            string modelPath = await EnsureModelAsync(modelType, modelsDir, status).ConfigureAwait(false);

            string ffmpeg = FindFfmpeg();
            if (ffmpeg == null)
                throw new FileNotFoundException(
                    "ffmpeg tidak ditemukan. Letakkan ffmpeg.exe di folder aplikasi (atau sub-folder \\ffmpeg\\), atau tambahkan ke PATH.");

            status?.Invoke("Mengonversi audio ke 16kHz mono...");
            string wav = ConvertToWav(audioPath, ffmpeg);

            try
            {
                status?.Invoke("Memuat model & menranskripsi (mohon tunggu)...");
                using (var factory = WhisperFactory.FromPath(modelPath))
                {
                    var builder = factory.CreateBuilder();
                    builder = (string.IsNullOrEmpty(language) || language == "auto")
                        ? builder.WithLanguage("auto")
                        : builder.WithLanguage(language);

                    using (var processor = builder.Build())
                    using (var fileStream = File.OpenRead(wav))
                    {
                        var sb = new StringBuilder();
                        await foreach (var segment in processor.ProcessAsync(fileStream).ConfigureAwait(false))
                        {
                            string text = (segment.Text ?? string.Empty).Trim();
                            string line = includeTimestamps
                                ? string.Format("[{0:hh\\:mm\\:ss} -> {1:hh\\:mm\\:ss}] {2}", segment.Start, segment.End, text)
                                : text;

                            onLine?.Invoke(line);

                            if (sb.Length > 0) sb.Append(' ');
                            sb.Append(text);
                        }
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
