using System;
using System.IO;
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
            if (args.Length >= 1)
            {
                try
                {
                    return RunCliAsync(args).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    try { File.WriteAllText("stt_error.log", ex.ToString()); } catch { }
                    Console.Error.WriteLine("ERROR: " + ex.Message);
                    return 1;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }

        private static async Task<int> RunCliAsync(string[] args)
        {
            string audio = args[0];
            if (!File.Exists(audio))
            {
                Console.Error.WriteLine("File audio tidak ditemukan: " + audio);
                return 2;
            }

            var model = Transcriber.ParseModel(args.Length > 1 ? args[1] : "small");
            string lang = args.Length > 2 ? args[2] : "id";
            string outPath = args.Length > 3 ? args[3] : audio + ".stt.txt";
            string modelsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");

            Action<string> status = s => Console.Error.WriteLine("[status] " + s);
            Action<string> onLine = l => Console.Out.WriteLine(l);

            string full = await Transcriber.TranscribeAsync(
                audio, model, lang, includeTimestamps: false,
                modelsDir: modelsDir, status: status, onLine: onLine);

            File.WriteAllText(outPath, full, System.Text.Encoding.UTF8);
            Console.Error.WriteLine("[status] Tersimpan: " + outPath);
            return 0;
        }
    }
}
