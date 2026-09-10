using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Suarakata
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // Mode CLI: Suarakata.exe "audio.ogg" [model] [bahasa] [output.txt]
            //           Suarakata.exe --download <model>   (unduh model, bisa dilanjutkan)
            //           Suarakata.exe --models             (daftar model & statusnya)
            if (args.Length >= 1)
            {
                try
                {
                    return RunCliAsync(args).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    Console.Error.WriteLine("[status] Dibatalkan. Kemajuan unduhan tersimpan.");
                    return 3;
                }
                catch (Exception ex)
                {
                    try { File.WriteAllText("stt_error.log", ex.ToString()); } catch { }
                    Console.Error.WriteLine("ERROR: " + ex.Message);
                    return 1;
                }
            }

            NativeTheme.Init();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }

        private static async Task<int> RunCliAsync(string[] args)
        {
            string modelsDir = ModelDownloader.DefaultModelsDir;
            string first = args[0];

            if (first.Equals("--models", StringComparison.OrdinalIgnoreCase) ||
                first.Equals("-l", StringComparison.OrdinalIgnoreCase))
            {
                ListModels(modelsDir);
                return 0;
            }

            if (first.Equals("--download", StringComparison.OrdinalIgnoreCase) ||
                first.Equals("-d", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("Pemakaian: Suarakata.exe --download <tiny|base|small|medium|large-v3>");
                    return 2;
                }
                var def = ModelCatalog.ByKey(args[1]);
                if (def == null)
                {
                    Console.Error.WriteLine("Model tidak dikenal: " + args[1]);
                    return 2;
                }
                await DownloadWithConsoleProgressAsync(def, modelsDir).ConfigureAwait(false);
                return 0;
            }

            string audio = first;
            if (!File.Exists(audio))
            {
                Console.Error.WriteLine("File audio tidak ditemukan: " + audio);
                return 2;
            }

            var model = Transcriber.ParseModel(args.Length > 1 ? args[1] : "small");
            string lang = args.Length > 2 ? args[2] : "id";
            string outPath = args.Length > 3 ? args[3] : audio + ".stt.txt";

            var modelDef = ModelCatalog.ByType(model);
            if (!ModelDownloader.IsInstalled(modelDef, modelsDir))
                await DownloadWithConsoleProgressAsync(modelDef, modelsDir).ConfigureAwait(false);

            Action<string> status = s => Console.Error.WriteLine("[status] " + s);
            Action<string> onLine = l => Console.Out.WriteLine(l);

            var mulai = DateTime.UtcNow;
            Action<double> onProgress = p =>
            {
                var berjalan = DateTime.UtcNow - mulai;
                if (p <= 0.02)
                {
                    Console.Error.Write(string.Format("\r[proses] {0:0}% ({1} berjalan)      ",
                        p * 100, Ui.Clock(berjalan)));
                    return;
                }
                var total = TimeSpan.FromSeconds(berjalan.TotalSeconds / p);
                var sisa = total - berjalan;
                if (sisa < TimeSpan.Zero) sisa = TimeSpan.Zero;
                Console.Error.Write(string.Format("\r[proses] {0:0}% | {1} berjalan | {2}      ",
                    p * 100, Ui.Clock(berjalan), Ui.SisaWaktu(sisa)));
            };

            string full = await Transcriber.TranscribeAsync(
                audio, model, lang, includeTimestamps: false,
                modelsDir: modelsDir, status: status, onLine: onLine,
                onProgress: onProgress).ConfigureAwait(false);

            Console.Error.WriteLine();
            Console.Error.WriteLine(string.Format("[status] Selesai dalam {0}.",
                Ui.HumanEta(DateTime.UtcNow - mulai)));

            File.WriteAllText(outPath, full, new System.Text.UTF8Encoding(true));
            Console.Error.WriteLine("[status] Tersimpan: " + outPath);
            return 0;
        }

        private static void ListModels(string modelsDir)
        {
            Console.Out.WriteLine("Folder model: " + modelsDir);
            Console.Out.WriteLine();
            foreach (var m in ModelCatalog.All)
            {
                string state;
                if (ModelDownloader.IsInstalled(m, modelsDir))
                    state = "terpasang (" + Ui.HumanBytes(ModelDownloader.InstalledBytes(m, modelsDir)) + ")";
                else
                {
                    long partial = ModelDownloader.PartialBytes(m, modelsDir);
                    state = partial > 0
                        ? "tertunda di " + Ui.HumanBytes(partial) + ", bisa dilanjutkan"
                        : "belum diunduh";
                }
                Console.Out.WriteLine(string.Format("{0,-10} ~{1,-10} {2}", m.Key, Ui.HumanBytes(m.ApproxBytes), state));
            }
        }

        private static async Task DownloadWithConsoleProgressAsync(ModelDef def, string modelsDir)
        {
            long resumeFrom = ModelDownloader.PartialBytes(def, modelsDir);
            Console.Error.WriteLine(resumeFrom > 0
                ? string.Format("[status] Melanjutkan unduhan model {0} dari {1}...", def.Display, Ui.HumanBytes(resumeFrom))
                : string.Format("[status] Mengunduh model {0} (~{1})...", def.Display, Ui.HumanBytes(def.ApproxBytes)));

            using (var cts = new CancellationTokenSource())
            {
                ConsoleCancelEventHandler onBreak = (s, e) =>
                {
                    e.Cancel = true;   // jangan matikan proses, biarkan .part tersimpan rapi
                    cts.Cancel();
                };
                Console.CancelKeyPress += onBreak;

                var progress = new Progress<DownloadProgress>(p =>
                    Console.Error.Write(string.Format("\r[unduh] {0} / {1} ({2:0.0}%) {3} sisa {4}   ",
                        Ui.HumanBytes(p.Received), Ui.HumanBytes(p.Total), p.Fraction * 100,
                        Ui.HumanSpeed(p.BytesPerSecond), Ui.HumanEta(p.Eta))));

                try
                {
                    await ModelDownloader.DownloadAsync(def, modelsDir, progress, cts.Token).ConfigureAwait(false);
                    Console.Error.WriteLine();
                    Console.Error.WriteLine("[status] Model " + def.Display + " siap.");
                }
                finally
                {
                    Console.CancelKeyPress -= onBreak;
                }
            }
        }
    }
}
