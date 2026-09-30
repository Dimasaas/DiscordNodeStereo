// Configurações (VoxGuard.ini), log (VoxGuard.log) e "Iniciar com o Windows".
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Win32;

namespace VoxGuard
{
    public static class AppPaths
    {
        static string dataDir;

        public static string ExePath
        {
            get { return Assembly.GetExecutingAssembly().Location; }
        }

        // Portátil: guarda tudo ao lado do .exe. Se a pasta não deixar gravar, usa %APPDATA%\VoxGuard.
        // VOXGUARD_DATA_DIR serve para testes.
        public static string DataDir
        {
            get
            {
                if (dataDir != null)
                    return dataDir;
                string env = Environment.GetEnvironmentVariable("VOXGUARD_DATA_DIR");
                if (!string.IsNullOrEmpty(env))
                    dataDir = env;
                else if (CanWrite(Path.GetDirectoryName(ExePath)))
                    dataDir = Path.GetDirectoryName(ExePath);
                else
                    dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VoxGuard");
                Directory.CreateDirectory(dataDir);
                return dataDir;
            }
        }

        static bool CanWrite(string dir)
        {
            try
            {
                string probe = Path.Combine(dir, ".voxguard-write-test");
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public sealed class Settings
    {
        public int IntervalMinutes = 60;
        public bool Autostart = true;
        public string SourcePath = "";      // vazio = .node de 512 kbps embutido no .exe (o normal)
        public bool TrayHintShown;
        // Última versão vista de cada Discord (Discord, DiscordPTB, DiscordCanary), para avisar "versão nova".
        public readonly Dictionary<string, string> LastVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static string FilePath
        {
            get { return Path.Combine(AppPaths.DataDir, "VoxGuard.ini"); }
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            if (!File.Exists(FilePath))
                return s;
            foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0 || line.StartsWith(";") || line.StartsWith("["))
                    continue;
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                int n;
                switch (key)
                {
                    case "interval_minutes":
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n >= 1)
                            s.IntervalMinutes = n;
                        break;
                    case "autostart": s.Autostart = value == "1"; break;
                    case "source": s.SourcePath = value; break;
                    case "tray_hint_shown": s.TrayHintShown = value == "1"; break;
                    default:
                        if (key.StartsWith("last_version.") && value.Length > 0)
                            s.LastVersions[key.Substring("last_version.".Length)] = value;
                        break;
                }
            }
            return s;
        }

        public void Save()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("; Configurações do VoxGuard");
            sb.AppendLine("[VoxGuard]");
            sb.AppendLine("interval_minutes=" + IntervalMinutes.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("autostart=" + (Autostart ? "1" : "0"));
            sb.AppendLine("source=" + SourcePath);
            sb.AppendLine("tray_hint_shown=" + (TrayHintShown ? "1" : "0"));
            foreach (KeyValuePair<string, string> kv in LastVersions)
                sb.AppendLine("last_version." + kv.Key + "=" + kv.Value);
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
            if (File.Exists(FilePath))
                File.Delete(FilePath);
            File.Move(tmp, FilePath);
        }
    }

    public static class Log
    {
        const long MaxBytes = 256 * 1024;
        public static event Action<string> Written;

        static string FilePath
        {
            get { return Path.Combine(AppPaths.DataDir, "VoxGuard.log"); }
        }

        public static void Write(string message)
        {
            string line = DateTime.Now.ToString("dd/MM HH:mm:ss") + "  " + message;
            try
            {
                FileInfo fi = new FileInfo(FilePath);
                if (fi.Exists && fi.Length > MaxBytes)
                {
                    string rotated = FilePath + ".1";
                    if (File.Exists(rotated))
                        File.Delete(rotated);
                    File.Move(FilePath, rotated);
                }
                File.AppendAllText(FilePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception)
            {
                // log nunca pode derrubar o programa
            }
            Action<string> handler = Written;
            if (handler != null)
                handler(line);
        }

        public static string[] Tail(int count)
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new string[0];
                string[] all = File.ReadAllLines(FilePath, Encoding.UTF8);
                int start = Math.Max(0, all.Length - count);
                string[] tail = new string[all.Length - start];
                Array.Copy(all, start, tail, 0, tail.Length);
                return tail;
            }
            catch (Exception)
            {
                return new string[0];
            }
        }
    }

    public static class Autostart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "VoxGuard";

        // VOXGUARD_NO_AUTOSTART=1 impede mexer no registro (testes).
        static bool Disabled
        {
            get { return Environment.GetEnvironmentVariable("VOXGUARD_NO_AUTOSTART") == "1"; }
        }

        public static void Apply(bool enabled)
        {
            if (Disabled)
                return;
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (key == null)
                    return;
                if (enabled)
                    key.SetValue(ValueName, "\"" + AppPaths.ExePath + "\" --tray");
                else if (key.GetValue(ValueName) != null)
                    key.DeleteValue(ValueName);
            }
        }
    }
}
