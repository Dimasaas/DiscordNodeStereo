// Testes do núcleo (src/Core.cs). Rodar com:  .\build.ps1 -Test
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using DiscordNodeStereo;

static class CoreTests
{
    static int failures;
    static string tmp;

    static int Main()
    {
        Run("versões comparadas como número", VersionsAreNumeric);
        Run("pega a pasta app-* de maior número", PicksHighestFolders);
        Run("substitui e guarda backup .original", ReplacesAndBacksUp);
        Run("não mexe quando já está certo", UpToDateIsNoOp);
        Run("nova versão usa o módulo mais novo", NewVersionUsesHighestModule);
        Run("substitui mesmo com o arquivo travado", ReplacesLockedFile);
        Run("fonte gzip embutida tem o hash certo", GzipSourceMatchesFile);
        Run("erros: sem Discord, sem módulo, sem origem", ReportsProblems);
        Run("verifica Discord, PTB e Canary de uma vez", ChecksAllVariants);
        Console.WriteLine(failures == 0 ? "\nTodos os testes passaram." : "\n" + failures + " teste(s) falharam.");
        return failures == 0 ? 0 : 1;
    }

    static void Run(string name, Action test)
    {
        tmp = Path.Combine(Path.GetTempPath(), "discordnodestereo-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            test();
            Console.WriteLine("  ok    " + name);
        }
        catch (Exception e)
        {
            failures++;
            Console.WriteLine("  FALHA " + name + "\n        " + e.Message);
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch (Exception) { }
        }
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    static string Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, content, Encoding.ASCII);
        return path;
    }

    static string MakeApp(string root, string version, int module, string content)
    {
        return Write(Path.Combine(root, "app-" + version, "modules", "discord_voice-" + module, "discord_voice",
                                  "discord_voice.node"), content);
    }

    static FileNodeSource Source(string content)
    {
        return new FileNodeSource(Write(Path.Combine(tmp, "patch", "discord_voice.node"), content));
    }

    // ---------------------------------------------------------------

    static void VersionsAreNumeric()
    {
        Assert(VersionUtil.Compare("1.0.10000", "1.0.9999") > 0, "1.0.10000 deveria ser maior");
        Assert(VersionUtil.Compare("1.0.9259", "1.0.9259") == 0, "iguais");
        Assert(VersionUtil.Compare("1.0", "1.0.1") < 0, "1.0 < 1.0.1");
    }

    static void PicksHighestFolders()
    {
        string root = Path.Combine(tmp, "Discord");
        foreach (string v in new[] { "1.0.9999", "1.0.10000", "1.0.999" })
            Directory.CreateDirectory(Path.Combine(root, "app-" + v));
        Directory.CreateDirectory(Path.Combine(root, "app-lixo"));
        Directory.CreateDirectory(Path.Combine(root, "packages"));
        VersionedDir best = DiscordLocator.FindHighest(root, "app-");
        Assert(best != null && best.Name == "app-1.0.10000", "esperava app-1.0.10000, veio " + (best == null ? "null" : best.Name));
    }

    static void ReplacesAndBacksUp()
    {
        string root = Path.Combine(tmp, "Discord");
        string dst = MakeApp(root, "1.0.9259", 1, "ORIGINAL");
        CheckResult r = Patcher.Check(root, Source("PATCHED"), "");
        Assert(r.Status == CheckStatus.Replaced, "status " + r.Status + " " + r.Error);
        Assert(File.ReadAllText(dst) == "PATCHED", "conteúdo não foi trocado");
        Assert(File.ReadAllText(dst + ".original") == "ORIGINAL", "backup .original errado");
        Assert(Directory.GetFiles(Path.GetDirectoryName(dst)).Length == 2, "sobrou lixo na pasta");
    }

    static void UpToDateIsNoOp()
    {
        string root = Path.Combine(tmp, "Discord");
        string dst = MakeApp(root, "1.0.9259", 1, "PATCHED");
        CheckResult r = Patcher.Check(root, Source("PATCHED"), "1.0.9259");
        Assert(r.Status == CheckStatus.UpToDate && !r.NewVersion, "status " + r.Status);
        Assert(!File.Exists(dst + ".original"), "não deveria criar backup");
    }

    static void NewVersionUsesHighestModule()
    {
        string root = Path.Combine(tmp, "Discord");
        MakeApp(root, "1.0.9259", 1, "ORIGINAL");
        MakeApp(root, "1.0.9300", 1, "ORIGINAL");
        string newest = MakeApp(root, "1.0.9300", 2, "ORIGINAL");
        CheckResult r = Patcher.Check(root, Source("PATCHED"), "1.0.9259");
        Assert(r.NewVersion, "deveria detectar versão nova");
        Assert(r.Target.Module.Name == "discord_voice-2", "módulo errado: " + r.Target.Module.Name);
        Assert(File.ReadAllText(newest) == "PATCHED", "não trocou o módulo mais novo");
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr LoadLibraryW(string path);

    [DllImport("kernel32.dll")]
    static extern bool FreeLibrary(IntPtr module);

    // Simula o Discord aberto: uma DLL carregada não pode ser sobrescrita, só renomeada.
    static void ReplacesLockedFile()
    {
        string root = Path.Combine(tmp, "Discord");
        string dst = MakeApp(root, "1.0.9259", 1, "x");
        File.Copy(Path.Combine(Environment.SystemDirectory, "version.dll"), dst, true);
        IntPtr h = LoadLibraryW(dst);
        Assert(h != IntPtr.Zero, "não consegui carregar a DLL de teste");
        bool writeBlocked = false;
        try { File.WriteAllText(dst, "x"); } catch (IOException) { writeBlocked = true; } catch (UnauthorizedAccessException) { writeBlocked = true; }
        Assert(writeBlocked, "o arquivo deveria estar travado");

        CheckResult r = Patcher.Check(root, Source("PATCHED"), "");
        Assert(r.Status == CheckStatus.Replaced, "status " + r.Status + " " + r.Error);
        Assert(File.ReadAllText(dst) == "PATCHED", "conteúdo não foi trocado");
        string dir = Path.GetDirectoryName(dst);
        Assert(Directory.GetFiles(dir, "*.old-*").Length == 1, "esperava o .old travado");

        FreeLibrary(h);
        Patcher.Check(root, Source("PATCHED"), "");
        Assert(Directory.GetFiles(dir, "*.old-*").Length == 0, ".old deveria ser limpo depois de solto");
    }

    static void GzipSourceMatchesFile()
    {
        string file = Write(Path.Combine(tmp, "big.node"), new string('A', 300000) + "fim");
        string gz = Path.Combine(tmp, "big.node.gz");
        using (FileStream input = File.OpenRead(file))
        using (FileStream output = File.Create(gz))
        using (GZipStream zip = new GZipStream(output, CompressionMode.Compress))
            input.CopyTo(zip);
        GzipNodeSource src = new GzipNodeSource(delegate { return File.OpenRead(gz); }, "teste");
        Assert(src.Length == new FileInfo(file).Length, "tamanho diferente");
        Assert(Hashing.Equal(src.Hash, Hashing.Sha256File(file)), "hash diferente");

        string root = Path.Combine(tmp, "Discord");
        string dst = MakeApp(root, "1.0.1", 1, "ORIGINAL");
        Assert(Patcher.Check(root, src, "").Status == CheckStatus.Replaced, "não substituiu com gzip");
        Assert(File.ReadAllText(dst) == File.ReadAllText(file), "conteúdo descompactado errado");
    }

    static void ChecksAllVariants()
    {
        string fakeLocal = Path.Combine(tmp, "Local");
        string previous = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        Environment.SetEnvironmentVariable("LOCALAPPDATA", fakeLocal);   // só neste processo de teste
        try
        {
            string stable = MakeApp(Path.Combine(fakeLocal, "Discord"), "1.0.9259", 1, "ORIGINAL");
            string canary = MakeApp(Path.Combine(fakeLocal, "DiscordCanary"), "1.0.1026", 1, "PATCHED");
            var last = new System.Collections.Generic.Dictionary<string, string>();
            last["Discord"] = "1.0.9000";
            var results = Patcher.CheckAll(Source("PATCHED"), last);
            Assert(results.Count == 3, "esperava 3 resultados");
            Assert(results[0].Variant.Id == "Discord" && results[0].Status == CheckStatus.Replaced && results[0].NewVersion,
                   "Discord: " + results[0].Status);
            Assert(results[1].Variant.Id == "DiscordPTB" && results[1].Status == CheckStatus.DiscordNotFound,
                   "PTB: " + results[1].Status);
            Assert(results[2].Variant.Id == "DiscordCanary" && results[2].Status == CheckStatus.UpToDate,
                   "Canary: " + results[2].Status);
            Assert(File.ReadAllText(stable) == "PATCHED" && File.ReadAllText(canary) == "PATCHED", "conteúdo");
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", previous);
        }
    }

    static void ReportsProblems()
    {
        string root = Path.Combine(tmp, "Discord");
        Assert(Patcher.Check(root, Source("P"), "").Status == CheckStatus.DiscordNotFound, "sem Discord");
        Directory.CreateDirectory(Path.Combine(root, "app-1.0.1", "modules"));
        Assert(Patcher.Check(root, Source("P"), "").Status == CheckStatus.ModuleNotFound, "sem módulo");
        MakeApp(root, "1.0.1", 1, "ORIGINAL");
        Assert(Patcher.Check(root, new FileNodeSource(Path.Combine(tmp, "nao-existe.node")), "").Status == CheckStatus.SourceMissing,
               "sem origem");
    }
}
