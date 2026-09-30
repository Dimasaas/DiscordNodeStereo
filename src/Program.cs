// Ponto de entrada: instância única, fonte do .node embutido e início na bandeja (--tray).
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("DiscordNodeStereo")]
[assembly: AssemblyDescription("Atualizador de módulos para Discord")]
[assembly: AssemblyProduct("DiscordNodeStereo")]
[assembly: AssemblyCopyright("DiscordNodeStereo")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

namespace DiscordNodeStereo
{
    static class Program
    {
        const string MutexName = @"Local\DiscordNodeStereo";
        const string ShowEventName = @"Local\DiscordNodeStereo.Show";

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
                List<PatchFile> embedded = CreateEmbeddedPatch(out embeddedLength);
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

        // O build embute os dois arquivos do patch (discord_voice.node + index.js) compactados em gzip,
        // cada um com um .info de tamanho e SHA-256: a comparação de hora em hora não descompacta nada.
        static List<PatchFile> CreateEmbeddedPatch(out long totalLength)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            List<PatchFile> files = new List<PatchFile>();
            totalLength = 0;
            foreach (string name in Patcher.FileNames)
            {
                string resource = name;
                GzipNodeSource src = new GzipNodeSource(
                    delegate { return asm.GetManifestResourceStream(resource + ".gz"); }, name + " (embutido)");
                using (Stream s = asm.GetManifestResourceStream(name + ".info"))
                using (StreamReader r = new StreamReader(s))
                {
                    string[] info = r.ReadToEnd().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    long length = long.Parse(info[0].Trim());
                    src.SetKnownInfo(length, Hashing.FromHex(info[1]));
                    totalLength += length;
                }
                files.Add(new PatchFile(name, src));
            }
            return files;
        }
    }
}
