// Ponto de entrada: instância única, fonte do .node embutido e início na bandeja (--tray).
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("VoxGuard")]
[assembly: AssemblyDescription("Atualizador de módulos para Discord")]
[assembly: AssemblyProduct("VoxGuard")]
[assembly: AssemblyCopyright("VoxGuard")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace VoxGuard
{
    static class Program
    {
        const string MutexName = @"Local\VoxGuard";
        const string ShowEventName = @"Local\VoxGuard.Show";

        [STAThread]
        static void Main(string[] args)
        {
            bool startHidden = Array.IndexOf(args, "--tray") >= 0;
            bool created;
            using (Mutex mutex = new Mutex(true, MutexName, out created))
            {
                if (!created)
                {
                    // Já está rodando: pede para a instância aberta mostrar a janela.
                    try
                    {
                        using (EventWaitHandle ev = EventWaitHandle.OpenExisting(ShowEventName))
                            ev.Set();
                    }
                    catch (Exception)
                    {
                    }
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
                {
                    Log.Write("Erro inesperado: " + e.Exception);
                };

                long embeddedLength;
                NodeSource embedded = CreateEmbeddedSource(out embeddedLength);
                using (EventWaitHandle showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName))
                {
                    MainForm form = new MainForm(startHidden, embedded, embeddedLength);
                    RegisteredWaitHandle wait = ThreadPool.RegisterWaitForSingleObject(
                        showEvent, delegate { form.RequestShow(); }, null, -1, false);
                    Application.Run(form);
                    wait.Unregister(null);
                }
            }
        }

        // O build embute o .node compactado (gzip) + um .info com tamanho e SHA-256,
        // assim a comparação de hora em hora não precisa descompactar nada.
        static NodeSource CreateEmbeddedSource(out long length)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            GzipNodeSource src = new GzipNodeSource(
                delegate { return asm.GetManifestResourceStream("discord_voice.node.gz"); }, "embutido");
            using (Stream s = asm.GetManifestResourceStream("discord_voice.node.info"))
            using (StreamReader r = new StreamReader(s))
            {
                string[] info = r.ReadToEnd().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                length = long.Parse(info[0].Trim());
                src.SetKnownInfo(length, Hashing.FromHex(info[1]));
            }
            return src;
        }
    }
}
