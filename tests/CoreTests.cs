// Testes do núcleo (src/Core.cs). Rodar com:  .\build.ps1 -Test
using System;
using System.Collections.Generic;
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
        Run("troca .node e index.js juntos, com backup dos dois", ReplacesBothFiles);
        Run("não mexe quando os dois já estão certos", UpToDateIsNoOp);
        Run("só o index.js errado: troca o index.js", ReplacesIndexJsAlone);
        Run("nova versão usa o módulo mais novo", NewVersionUsesHighestModule);
        Run("troca mesmo com o .node travado", ReplacesLockedFile);
        Run("se uma troca falha, desfaz a outra (nada de meio patch)", RollsBackHalfPatch);
        Run("fonte gzip embutida tem o hash certo", GzipSourceMatchesFile);
        Run("erros: sem Discord, sem módulo, sem arquivo do patch", ReportsProblems);
        Run("verifica Discord, PTB e Canary de uma vez", ChecksAllVariants);
        Run("desfazer volta o .node e o index.js originais", UndoRestoresOriginals);
        Run("desfazer sem patch instalado não mexe em nada", UndoWithoutPatchIsNoOp);
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

    static string Read(string dir, string name)
    {
        return File.ReadAllText(Path.Combine(dir, name));
    }

    // Cria ...\app-X\modules\discord_voice-N\discord_voice com discord_voice.node + index.js e devolve a pasta.
    static string MakeApp(string root, string version, int module, string node, string js)
    {
        string dir = Path.Combine(root, "app-" + version, "modules", "discord_voice-" + module, "discord_voice");
        Write(Path.Combine(dir, "discord_voice.node"), node);
        Write(Path.Combine(dir, "index.js"), js);
        return dir;
    }

    // O patch de teste: os mesmos dois arquivos do de verdade.
    static List<PatchFile> Patch(string node, string js)
    {
        string dir = Path.Combine(tmp, "patch");
        Write(Path.Combine(dir, "discord_voice.node"), node);
        Write(Path.Combine(dir, "index.js"), js);
        List<PatchFile> files = new List<PatchFile>();
        foreach (string name in Patcher.FileNames)
            files.Add(new PatchFile(name, new FileNodeSource(Path.Combine(dir, name))));
        return files;
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

    static void ReplacesBothFiles()
    {
        string root = Path.Combine(tmp, "Discord");
        string dir = MakeApp(root, "1.0.9259", 1, "NODE-SET", "JS-SET");
        CheckResult r = Patcher.Check(root, Patch("NODE-512", "JS-512"), "");
        Assert(r.Status == CheckStatus.Replaced, "status " + r.Status + " " + r.Error);
        Assert(r.ReplacedFiles.Count == 2, "esperava 2 arquivos trocados, veio " + r.ReplacedFiles.Count);
        Assert(Read(dir, "discord_voice.node") == "NODE-512" && Read(dir, "index.js") == "JS-512", "conteúdo não foi trocado");
        Assert(Read(dir, "discord_voice.node.original") == "NODE-SET", "backup do .node errado");
        Assert(Read(dir, "index.js.original") == "JS-SET", "backup do index.js errado");
        Assert(Directory.GetFiles(dir).Length == 4, "sobrou lixo na pasta");
    }

    static void UpToDateIsNoOp()
    {
        string root = Path.Combine(tmp, "Discord");
        string dir = MakeApp(root, "1.0.9259", 1, "NODE-512", "JS-512");
        CheckResult r = Patcher.Check(root, Patch("NODE-512", "JS-512"), "1.0.9259");
        Assert(r.Status == CheckStatus.UpToDate && !r.NewVersion, "status " + r.Status);
        Assert(Directory.GetFiles(dir).Length == 2, "não deveria criar backup");
    }

    // O bug de 29/09: .node de 512 kbps com o index.js novo do Discord. Tem que trocar o index.js.
    static void ReplacesIndexJsAlone()
    {
        string root = Path.Combine(tmp, "Discord");
        string dir = MakeApp(root, "1.0.9259", 1, "NODE-512", "JS-SET");
        CheckResult r = Patcher.Check(root, Patch("NODE-512", "JS-512"), "");
        Assert(r.Status == CheckStatus.Replaced, "status " + r.Status);
        Assert(r.ReplacedFiles.Count == 1 && r.ReplacedFiles[0] == "index.js", "deveria trocar só o index.js");
        Assert(Read(dir, "index.js") == "JS-512", "index.js não foi trocado");
    }

    static void NewVersionUsesHighestModule()
    {
        string root = Path.Combine(tmp, "Discord");
        MakeApp(root, "1.0.9259", 1, "NODE", "JS");
        MakeApp(root, "1.0.9300", 1, "NODE", "JS");
        string newest = MakeApp(root, "1.0.9300", 2, "NODE", "JS");
        CheckResult r = Patcher.Check(root, Patch("NODE-512", "JS-512"), "1.0.9259");
        Assert(r.NewVersion, "deveria detectar versão nova");
        Assert(r.Target.Module.Name == "discord_voice-2", "módulo errado: " + r.Target.Module.Name);
        Assert(Read(newest, "discord_voice.node") == "NODE-512" && Read(newest, "index.js") == "JS-512",
               "não trocou o módulo mais novo");
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr LoadLibraryW(string path);

    [DllImport("kernel32.dll")]
    static extern bool FreeLibrary(IntPtr module);

    // Simula o Discord aberto: uma DLL carregada não pode ser sobrescrita, só renomeada.
    static void ReplacesLockedFile()
    {
        string root = Path.Combine(tmp, "Discord");
        string dir = MakeApp(root, "1.0.9259", 1, "x", "JS-SET");
        string node = Path.Combine(dir, "discord_voice.node");
        File.Copy(Path.Combine(Environment.SystemDirectory, "version.dll"), node, true);
        IntPtr h = LoadLibraryW(node);
        Assert(h != IntPtr.Zero, "não consegui carregar a DLL de teste");
        bool writeBlocked = false;
        try { File.WriteAllText(node, "x"); } catch (IOException) { writeBlocked = true; } catch (UnauthorizedAccessException) { writeBlocked = true; }
        Assert(writeBlocked, "o arquivo deveria estar travado");

        CheckResult r = Patcher.Check(root, Patch("NODE-512", "JS-512"), "");
        Assert(r.Status == CheckStatus.Replaced, "status " + r.Status + " " + r.Error);
        Assert(Read(dir, "discord_voice.node") == "NODE-512" && Read(dir, "index.js") == "JS-512", "conteúdo não foi trocado");
        Assert(Directory.GetFiles(dir, "*.old-*").Length == 1, "esperava o .old travado");

        FreeLibrary(h);
        Patcher.Check(root, Patch("NODE-512", "JS-512"), "");
        Assert(Directory.GetFiles(dir, "*.old-*").Length == 0, ".old deveria ser limpo depois de solto");
    }

    // Se o index.js não puder ser trocado, o .node também não pode ficar trocado.
    static void RollsBackHalfPatch()
    {
        string root = Path.Combine(tmp, "Discord");
        string dir = MakeApp(root, "1.0.9259", 1, "NODE-SET", "JS-SET");
        CheckResult r;
        // aberto sem permitir renomear: a troca do index.js (o segundo arquivo) falha
        using (new FileStream(Path.Combine(dir, "index.js"), FileMode.Open, FileAccess.Read, FileShare.Read))
            r = Patcher.Check(root, Patch("NODE-512", "JS-512"), "");
        Assert(r.Status == CheckStatus.Failed, "deveria falhar, veio " + r.Status);
        Assert(Read(dir, "discord_voice.node") == "NODE-SET", "o .node deveria voltar ao original");
        Assert(Read(dir, "index.js") == "JS-SET", "o index.js deveria continuar o original");
        Assert(Directory.GetFiles(dir, "*.new").Length == 0 && Directory.GetFiles(dir, "*.old-*").Length == 0,
               "sobrou arquivo temporário");

        CheckResult again = Patcher.Check(root, Patch("NODE-512", "JS-512"), "");
        Assert(again.Status == CheckStatus.Replaced, "depois de solto deveria trocar, veio " + again.Status);
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
        string dir = MakeApp(root, "1.0.1", 1, "ORIGINAL", "JS");
        List<PatchFile> patch = new List<PatchFile> { new PatchFile("discord_voice.node", src) };
        Assert(Patcher.Check(root, patch, "").Status == CheckStatus.Replaced, "não substituiu com gzip");
        Assert(Read(dir, "discord_voice.node") == File.ReadAllText(file), "conteúdo descompactado errado");
    }

    static void ReportsProblems()
    {
        string root = Path.Combine(tmp, "Discord");
        List<PatchFile> patch = Patch("P", "J");
        Assert(Patcher.Check(root, patch, "").Status == CheckStatus.DiscordNotFound, "sem Discord");
        Directory.CreateDirectory(Path.Combine(root, "app-1.0.1", "modules"));
        Assert(Patcher.Check(root, patch, "").Status == CheckStatus.ModuleNotFound, "sem módulo");
        string dir = MakeApp(root, "1.0.1", 1, "ORIGINAL", "JS");
        File.Delete(Path.Combine(tmp, "patch", "index.js"));
        CheckResult r = Patcher.Check(root, patch, "");
        Assert(r.Status == CheckStatus.SourceMissing && r.Error.EndsWith("index.js"), "sem index.js: " + r.Status);
        Assert(Read(dir, "discord_voice.node") == "ORIGINAL", "não pode trocar só o .node");
    }

    static void UndoRestoresOriginals()
    {
        string root = Path.Combine(tmp, "Discord");
        string dir = MakeApp(root, "1.0.9259", 1, "NODE-SET", "JS-SET");
        Assert(Patcher.Check(root, Patch("NODE-512", "JS-512"), "").Status == CheckStatus.Replaced, "instalar");
        CheckResult r = Patcher.Restore(root);
        Assert(r.Status == CheckStatus.Restored && r.ReplacedFiles.Count == 2, "status " + r.Status + " " + r.Error);
        Assert(Read(dir, "discord_voice.node") == "NODE-SET" && Read(dir, "index.js") == "JS-SET", "não voltou o original");
        Assert(Directory.GetFiles(dir, "*.new").Length == 0 && Directory.GetFiles(dir, "*.old-*").Length == 0, "sobrou lixo");
        Assert(Patcher.Restore(root).Status == CheckStatus.NothingToRestore, "segunda vez não tem o que desfazer");
    }

    static void UndoWithoutPatchIsNoOp()
    {
        string root = Path.Combine(tmp, "Discord");
        string dir = MakeApp(root, "1.0.9259", 1, "NODE-SET", "JS-SET");
        CheckResult r = Patcher.Restore(root);
        Assert(r.Status == CheckStatus.NothingToRestore, "status " + r.Status);
        Assert(Directory.GetFiles(dir).Length == 2, "não pode criar nem apagar arquivos");
        Assert(Patcher.Restore(Path.Combine(tmp, "NaoExiste")).Status == CheckStatus.DiscordNotFound, "sem Discord");
    }

    static void ChecksAllVariants()
    {
        string fakeLocal = Path.Combine(tmp, "Local");
        string previous = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        Environment.SetEnvironmentVariable("LOCALAPPDATA", fakeLocal);   // só neste processo de teste
        try
        {
            string stable = MakeApp(Path.Combine(fakeLocal, "Discord"), "1.0.9259", 1, "NODE-SET", "JS-SET");
            string canary = MakeApp(Path.Combine(fakeLocal, "DiscordCanary"), "1.0.1026", 1, "NODE-512", "JS-512");
            Dictionary<string, string> last = new Dictionary<string, string>();
            last["Discord"] = "1.0.9000";
            List<CheckResult> results = Patcher.CheckAll(Patch("NODE-512", "JS-512"), last);
            Assert(results.Count == 3, "esperava 3 resultados");
            Assert(results[0].Variant.Id == "Discord" && results[0].Status == CheckStatus.Replaced && results[0].NewVersion,
                   "Discord: " + results[0].Status);
            Assert(results[1].Variant.Id == "DiscordPTB" && results[1].Status == CheckStatus.DiscordNotFound,
                   "PTB: " + results[1].Status);
            Assert(results[2].Variant.Id == "DiscordCanary" && results[2].Status == CheckStatus.UpToDate,
                   "Canary: " + results[2].Status);
            Assert(Read(stable, "index.js") == "JS-512" && Read(canary, "discord_voice.node") == "NODE-512", "conteúdo");
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", previous);
        }
    }
}
