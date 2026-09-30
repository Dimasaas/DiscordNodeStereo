// Núcleo do DiscordNodeStereo: acha a versão mais nova do Discord e troca o discord_voice.node.
// Sem dependência de interface, para poder ser testado em tests/CoreTests.cs.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace DiscordNodeStereo
{
    public static class VersionUtil
    {
        // "1.0.10000" > "1.0.9999": compara número a número, não como texto.
        public static int Compare(string a, string b)
        {
            string[] pa = a.Split('.');
            string[] pb = b.Split('.');
            int n = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < n; i++)
            {
                long x = i < pa.Length ? long.Parse(pa[i]) : 0;
                long y = i < pb.Length ? long.Parse(pb[i]) : 0;
                if (x != y)
                    return x < y ? -1 : 1;
            }
            return 0;
        }
    }

    public sealed class VersionedDir
    {
        public string Name;
        public string Version;
        public string FullPath;
    }

    public sealed class DiscordVariant
    {
        public string Id;          // nome da pasta em %LOCALAPPDATA% (e do .exe)
        public string Display;

        public static readonly DiscordVariant[] All =
        {
            new DiscordVariant { Id = "Discord", Display = "Discord" },
            new DiscordVariant { Id = "DiscordPTB", Display = "Discord PTB" },
            new DiscordVariant { Id = "DiscordCanary", Display = "Discord Canary" },
        };

        public static DiscordVariant ById(string id)
        {
            foreach (DiscordVariant v in All)
                if (string.Equals(v.Id, id, StringComparison.OrdinalIgnoreCase))
                    return v;
            return All[0];
        }
    }

    public sealed class VoiceTarget
    {
        public string Root;
        public VersionedDir App;       // app-1.0.9259
        public VersionedDir Module;    // discord_voice-1
        public string TargetDir;       // ...\discord_voice-1\discord_voice
        public string TargetFile;      // ...\discord_voice.node
    }

    public static class DiscordLocator
    {
        public const string ModuleName = "discord_voice";
        public const string FileName = "discord_voice.node";

        // C:\Users\<usuário>\AppData\Local  (cada usuário do Windows tem o seu)
        public static string LocalAppData()
        {
            string env = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (!string.IsNullOrEmpty(env))
                return env;
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        public static string RootFor(DiscordVariant variant)
        {
            return Path.Combine(LocalAppData(), variant.Id);
        }

        public static bool IsInstalled(DiscordVariant variant)
        {
            return FindHighest(RootFor(variant), "app-") != null;
        }

        // Entre as pastas "<prefixo><número>", devolve a de maior número.
        public static VersionedDir FindHighest(string parent, string prefix)
        {
            if (!Directory.Exists(parent))
                return null;
            Regex re = new Regex("^" + Regex.Escape(prefix) + @"(\d+(?:\.\d+)*)$");
            VersionedDir best = null;
            foreach (string dir in Directory.GetDirectories(parent))
            {
                string name = Path.GetFileName(dir);
                Match m = re.Match(name);
                if (!m.Success)
                    continue;
                if (best == null || VersionUtil.Compare(m.Groups[1].Value, best.Version) > 0)
                    best = new VersionedDir { Name = name, Version = m.Groups[1].Value, FullPath = dir };
            }
            return best;
        }

        public static VoiceTarget Resolve(string root)
        {
            VoiceTarget t = new VoiceTarget { Root = root };
            t.App = FindHighest(root, "app-");
            if (t.App == null)
                return t;
            t.Module = FindHighest(Path.Combine(t.App.FullPath, "modules"), ModuleName + "-");
            if (t.Module == null)
                return t;
            t.TargetDir = Path.Combine(t.Module.FullPath, ModuleName);
            t.TargetFile = Path.Combine(t.TargetDir, FileName);
            return t;
        }
    }

    // De onde vem o .node bom: embutido no .exe (gzip) ou um arquivo escolhido pelo usuário.
    public abstract class NodeSource
    {
        long length = -1;
        byte[] hash;
        string stamp;

        public abstract string Description { get; }
        public abstract bool Exists { get; }
        public abstract Stream Open();
        protected abstract string Stamp();

        public long Length { get { EnsureInfo(); return length; } }
        public byte[] Hash { get { EnsureInfo(); return hash; } }

        // Para a fonte embutida o build já grava tamanho e hash, então não precisa descompactar.
        public void SetKnownInfo(long knownLength, byte[] knownHash)
        {
            length = knownLength;
            hash = knownHash;
            stamp = Stamp();
        }

        void EnsureInfo()
        {
            string current = Stamp();
            if (hash != null && current == stamp)
                return;
            using (Stream s = Open())
            {
                long total;
                hash = Hashing.Sha256(s, out total);
                length = total;
            }
            stamp = current;
        }
    }

    public sealed class FileNodeSource : NodeSource
    {
        readonly string path;

        public FileNodeSource(string path)
        {
            this.path = path;
        }

        public string FilePath { get { return path; } }
        public override string Description { get { return path; } }
        public override bool Exists { get { return File.Exists(path); } }

        public override Stream Open()
        {
            return File.OpenRead(path);
        }

        protected override string Stamp()
        {
            FileInfo fi = new FileInfo(path);
            return fi.Exists ? fi.Length + "|" + fi.LastWriteTimeUtc.Ticks : "missing";
        }
    }

    public sealed class GzipNodeSource : NodeSource
    {
        readonly Func<Stream> opener;
        readonly string description;

        public GzipNodeSource(Func<Stream> opener, string description)
        {
            this.opener = opener;
            this.description = description;
        }

        public override string Description { get { return description; } }
        public override bool Exists { get { return true; } }

        public override Stream Open()
        {
            return new GZipStream(opener(), CompressionMode.Decompress);
        }

        protected override string Stamp()
        {
            return "embedded";
        }
    }

    public static class Hashing
    {
        public static byte[] Sha256(Stream s, out long total)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] buf = new byte[81920];
                total = 0;
                int n;
                while ((n = s.Read(buf, 0, buf.Length)) > 0)
                {
                    sha.TransformBlock(buf, 0, n, null, 0);
                    total += n;
                }
                sha.TransformFinalBlock(buf, 0, 0);
                return sha.Hash;
            }
        }

        public static byte[] Sha256File(string path)
        {
            long ignored;
            using (FileStream fs = File.OpenRead(path))
                return Sha256(fs, out ignored);
        }

        public static bool Equal(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    return false;
            return true;
        }

        public static byte[] FromHex(string hex)
        {
            hex = hex.Trim();
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }
    }

    public enum CheckStatus { UpToDate, Replaced, DiscordNotFound, ModuleNotFound, SourceMissing, Failed, Restored, NothingToRestore }

    public sealed class CheckResult
    {
        public DiscordVariant Variant;
        public CheckStatus Status;
        public VoiceTarget Target;
        public bool NewVersion;   // a pasta app-* mudou desde a última verificação
        public string Error;
        public DateTime Time;
        public List<string> ReplacedFiles = new List<string>();
    }

    // Um arquivo do patch e o nome que ele tem na pasta final do Discord.
    public sealed class PatchFile
    {
        public string Name;         // "discord_voice.node" ou "index.js"
        public NodeSource Source;

        public PatchFile(string name, NodeSource source)
        {
            Name = name;
            Source = source;
        }
    }

    public static class Patcher
    {
        public const string BackupSuffix = ".original";
        const string OldMarker = ".old-";
        const string NewSuffix = ".new";

        // O patch de 512 kbps são dois arquivos que só funcionam juntos:
        // discord_voice.node + index.js. Os dois vão soltos na pasta ...\discord_voice.
        public static readonly string[] FileNames = { DiscordLocator.FileName, "index.js" };

        public static CheckResult Check(string root, IList<PatchFile> patch, string lastVersion)
        {
            CheckResult r = new CheckResult { Time = DateTime.Now };
            r.Target = DiscordLocator.Resolve(root);
            if (r.Target.App == null)
            {
                r.Status = CheckStatus.DiscordNotFound;
                return r;
            }
            r.NewVersion = !string.IsNullOrEmpty(lastVersion) && lastVersion != r.Target.App.Version;
            if (r.Target.Module == null || !Directory.Exists(r.Target.TargetDir))
            {
                r.Status = CheckStatus.ModuleNotFound;
                return r;
            }
            foreach (PatchFile f in patch)
            {
                if (!f.Source.Exists)
                {
                    r.Status = CheckStatus.SourceMissing;
                    r.Error = f.Source.Description;
                    return r;
                }
            }
            try
            {
                CleanupOld(r.Target.TargetDir);
                List<PatchFile> changed = new List<PatchFile>();
                foreach (PatchFile f in patch)
                    if (!SameContent(f.Source, Path.Combine(r.Target.TargetDir, f.Name)))
                        changed.Add(f);
                if (changed.Count == 0)
                {
                    r.Status = CheckStatus.UpToDate;
                    return r;
                }
                ReplaceAll(changed, r.Target.TargetDir);
                foreach (PatchFile f in changed)
                    r.ReplacedFiles.Add(f.Name);
                r.Status = CheckStatus.Replaced;
            }
            catch (Exception e)
            {
                r.Status = CheckStatus.Failed;
                r.Error = e.Message;
            }
            return r;
        }

        // Verifica Discord, PTB e Canary de uma vez; os que não estão instalados voltam como DiscordNotFound.
        public static List<CheckResult> CheckAll(IList<PatchFile> patch, IDictionary<string, string> lastVersions)
        {
            List<CheckResult> results = new List<CheckResult>();
            foreach (DiscordVariant v in DiscordVariant.All)
            {
                string last;
                lastVersions.TryGetValue(v.Id, out last);
                CheckResult r = Check(DiscordLocator.RootFor(v), patch, last);
                r.Variant = v;
                results.Add(r);
            }
            return results;
        }

        // Desfazer: volta os arquivos do Discord a partir dos backups .original (os dois juntos).
        public static CheckResult Restore(string root)
        {
            CheckResult r = new CheckResult { Time = DateTime.Now };
            r.Target = DiscordLocator.Resolve(root);
            if (r.Target.App == null)
            {
                r.Status = CheckStatus.DiscordNotFound;
                return r;
            }
            if (r.Target.Module == null || !Directory.Exists(r.Target.TargetDir))
            {
                r.Status = CheckStatus.ModuleNotFound;
                return r;
            }
            try
            {
                CleanupOld(r.Target.TargetDir);
                List<PatchFile> back = new List<PatchFile>();
                foreach (string name in FileNames)
                {
                    string dst = Path.Combine(r.Target.TargetDir, name);
                    FileNodeSource original = new FileNodeSource(dst + BackupSuffix);
                    if (original.Exists && !SameContent(original, dst))
                        back.Add(new PatchFile(name, original));
                }
                if (back.Count == 0)
                {
                    r.Status = CheckStatus.NothingToRestore;
                    return r;
                }
                ReplaceAll(back, r.Target.TargetDir);
                foreach (PatchFile f in back)
                    r.ReplacedFiles.Add(f.Name);
                r.Status = CheckStatus.Restored;
            }
            catch (Exception e)
            {
                r.Status = CheckStatus.Failed;
                r.Error = e.Message;
            }
            return r;
        }

        public static List<CheckResult> RestoreAll()
        {
            List<CheckResult> results = new List<CheckResult>();
            foreach (DiscordVariant v in DiscordVariant.All)
            {
                CheckResult r = Restore(DiscordLocator.RootFor(v));
                r.Variant = v;
                results.Add(r);
            }
            return results;
        }

        public static bool SameContent(NodeSource source, string file)
        {
            FileInfo fi = new FileInfo(file);
            if (!fi.Exists || fi.Length != source.Length)
                return false;
            return Hashing.Equal(Hashing.Sha256File(file), source.Hash);
        }

        // Troca todos os arquivos juntos: meio patch (só o .node ou só o index.js) quebra o Discord.
        // 1) guarda o original (.original) e escreve cada arquivo novo como .new;
        // 2) só então troca; se alguma troca falhar, desfaz as que já foram feitas.
        // Com o Discord aberto o .node fica travado para escrita, mas o Windows deixa renomear:
        // o atual vira .old-*, o novo entra no lugar e o Discord carrega ele ao reiniciar.
        public static void ReplaceAll(IList<PatchFile> files, string dir)
        {
            foreach (PatchFile f in files)
            {
                string dst = Path.Combine(dir, f.Name);
                string backup = dst + BackupSuffix;
                if (File.Exists(dst) && !File.Exists(backup))
                    File.Copy(dst, backup);
                using (Stream input = f.Source.Open())
                using (FileStream output = new FileStream(dst + NewSuffix, FileMode.Create, FileAccess.Write, FileShare.None))
                    input.CopyTo(output);
            }

            List<KeyValuePair<string, string>> swapped = new List<KeyValuePair<string, string>>();  // (destino, .old ou null)
            try
            {
                foreach (PatchFile f in files)
                {
                    string dst = Path.Combine(dir, f.Name);
                    string old = null;
                    if (File.Exists(dst))
                    {
                        old = dst + OldMarker + DateTime.Now.Ticks;
                        File.Move(dst, old);
                    }
                    try
                    {
                        File.Move(dst + NewSuffix, dst);
                    }
                    catch
                    {
                        if (old != null)
                            File.Move(old, dst);
                        throw;
                    }
                    swapped.Add(new KeyValuePair<string, string>(dst, old));
                }
            }
            catch
            {
                for (int i = swapped.Count - 1; i >= 0; i--)
                {
                    string dst = swapped[i].Key;
                    string old = swapped[i].Value;
                    TryDelete(dst);
                    if (old != null && !File.Exists(dst))
                        File.Move(old, dst);
                }
                foreach (PatchFile f in files)
                    TryDelete(Path.Combine(dir, f.Name) + NewSuffix);
                throw;
            }
            foreach (KeyValuePair<string, string> s in swapped)
                if (s.Value != null)
                    TryDelete(s.Value);
        }

        // Apaga sobras de trocas anteriores (só consegue quando o Discord já soltou o arquivo).
        public static void CleanupOld(string dir)
        {
            foreach (string f in Directory.GetFiles(dir))
            {
                string name = Path.GetFileName(f);
                if (name.Contains(OldMarker) || name.EndsWith(NewSuffix, StringComparison.OrdinalIgnoreCase))
                    TryDelete(f);
            }
        }

        static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    public static class DiscordProcess
    {
        public static bool IsRunning(DiscordVariant v)
        {
            Process[] ps = Process.GetProcessesByName(v.Id);
            foreach (Process p in ps)
                p.Dispose();
            return ps.Length > 0;
        }

        public static void Kill(DiscordVariant v)
        {
            ProcessStartInfo psi = new ProcessStartInfo("taskkill", "/F /T /IM " + v.Id + ".exe");
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            using (Process p = Process.Start(psi))
                p.WaitForExit(10000);
        }

        public static void Open(DiscordVariant v)
        {
            string root = DiscordLocator.RootFor(v);
            string update = Path.Combine(root, "Update.exe");
            if (File.Exists(update))
            {
                Process.Start(new ProcessStartInfo(update, "--processStart " + v.Id + ".exe") { UseShellExecute = false });
                return;
            }
            VersionedDir app = DiscordLocator.FindHighest(root, "app-");
            if (app != null)
                Process.Start(Path.Combine(app.FullPath, v.Id + ".exe"));
        }
    }
}
