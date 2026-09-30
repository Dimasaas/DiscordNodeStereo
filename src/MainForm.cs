// Janela principal + ícone da bandeja + timer de verificação.
// Verifica sozinho o Discord, o PTB e o Canary (os que estiverem instalados).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DiscordNodeStereo
{
    sealed class MainForm : Form
    {
        static readonly int[] IntervalOptions = { 5, 15, 30, 60, 120, 360, 720, 1440 };

        // Uma linha por Discord (normal, PTB, Canary) no card de cima.
        sealed class VariantRow
        {
            public DiscordVariant Variant;
            public StatusDot Dot;
            public LinkLabel Name;
            public Label Version;
            public Label State;
            public string Folder;
        }

        readonly Settings settings;
        readonly NodeSource embedded;
        readonly long embeddedLength;
        readonly NodeSource source;

        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer firstCheck = new System.Windows.Forms.Timer();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly ToolTip tips = new ToolTip();
        readonly SynchronizationContext ui;
        readonly List<VariantRow> rows = new List<VariantRow>();
        readonly List<DiscordVariant> closedByUs = new List<DiscordVariant>();
        bool allowVisible;
        bool exiting;
        bool checking;
        bool dialogOpen;
        bool loadingUi;
        string lastProblem = "";
        DateTime nextCheck;

        readonly Font fTitle = new Font("Segoe UI Semibold", 17F);
        readonly Font fSubtitle = new Font("Segoe UI", 9.5F);
        readonly Font fSection = new Font("Segoe UI Semibold", 7.5F);
        readonly Font fBold = new Font("Segoe UI Semibold", 9F);
        readonly Font fStatus = new Font("Segoe UI Semibold", 11.5F);
        readonly Font fMono = new Font("Consolas", 8.25F);

        Label lblStatus, lblStatusDetail, lblSource;
        ComboBox cmbInterval;
        CheckBox chkAutostart;
        Button btnCheck, btnKill, btnOpen;
        TextBox txtLog;
        StatusDot dot;

        public MainForm(bool startHidden, NodeSource embeddedSource, long embeddedSize)
        {
            allowVisible = !startHidden;
            embedded = embeddedSource;
            embeddedLength = embeddedSize;
            settings = Settings.Load();
            source = string.IsNullOrEmpty(settings.SourcePath) ? embedded : new FileNodeSource(settings.SourcePath);

            BuildUi();
            ui = SynchronizationContext.Current;
            SetupTray();
            LoadLogTail();
            Log.Written += OnLogWritten;

            try
            {
                Autostart.Apply(settings.Autostart);
            }
            catch (Exception ex)
            {
                Log.Write("Não consegui configurar o início com o Windows: " + ex.Message);
            }
            timer.Tick += delegate { RunCheck(false); };
            RestartTimer();
            firstCheck.Interval = 400;
            firstCheck.Tick += delegate { firstCheck.Stop(); RunCheck(false); };
            firstCheck.Start();
            Log.Write("DiscordNodeStereo iniciado" + (startHidden ? " na bandeja" : "") + ".");
        }

        // ---------------- interface ----------------

        void BuildUi()
        {
            SuspendLayout();
            loadingUi = true;
            Text = "DiscordNodeStereo";
            Icon = AppIcon.Full();
            Font = new Font("Segoe UI", 9F);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            // Cabeçalho: logo no canto esquerdo + nome
            HeaderPanel header = Place(this, new HeaderPanel(), 0, 0, 480, 88);
            PictureBox logo = Place(header, new PictureBox(), 20, 18, 52, 52);
            logo.BackColor = Color.Transparent;
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.Image = AppIcon.Logo(128);
            MakeLabel(header, "DiscordNodeStereo", 82, 14, 312, 36, fTitle, Color.White);
            MakeLabel(header, "Mic estéreo a 512 kbps no Discord, sempre.", 85, 50, 310, 20, fSubtitle, Color.FromArgb(235, 232, 255));
            Label ver = MakeLabel(header, "v" + Assembly.GetExecutingAssembly().GetName().Version.ToString(2), 400, 16, 62, 18,
                                  fSection, Color.FromArgb(225, 225, 255));
            ver.TextAlign = ContentAlignment.TopRight;

            // Card: os Discords deste usuário
            CardPanel discord = Place(this, new CardPanel(), 16, 102, 448, 150);
            MakeLabel(discord, "DISCORDS DESTE USUÁRIO", 16, 10, 260, 16, fSection, Theme.Muted);
            MakeLabel(discord, "Usuário do Windows", 16, 34, 140, 20, null, Theme.Muted);
            Label user = MakeLabel(discord, Environment.UserName, 160, 34, 272, 20, fBold, Theme.Text);
            tips.SetToolTip(user, DiscordLocator.LocalAppData());
            for (int i = 0; i < DiscordVariant.All.Length; i++)
                rows.Add(MakeVariantRow(discord, DiscordVariant.All[i], 64 + i * 28));

            // Card: status geral
            CardPanel status = Place(this, new CardPanel(), 16, 264, 448, 72);
            dot = Place(status, new StatusDot(), 16, 17, 18, 18);
            lblStatus = MakeLabel(status, "Aguardando a primeira verificação…", 42, 12, 392, 26, fStatus, Theme.Text);
            lblStatusDetail = MakeLabel(status, "", 43, 40, 392, 20, null, Theme.Muted);

            // Card: configurações
            CardPanel config = Place(this, new CardPanel(), 16, 348, 448, 118);
            MakeLabel(config, "CONFIGURAÇÕES", 16, 10, 200, 16, fSection, Theme.Muted);
            MakeLabel(config, "Verificar a cada", 16, 36, 140, 20, null, Theme.Muted);
            cmbInterval = Place(config, new ComboBox(), 160, 32, 150, 24);
            cmbInterval.DropDownStyle = ComboBoxStyle.DropDownList;
            FillIntervals();
            cmbInterval.SelectedIndexChanged += OnIntervalChanged;
            chkAutostart = Place(config, new CheckBox(), 16, 62, 420, 22);
            chkAutostart.Text = "Iniciar com o Windows (fica quietinho na bandeja)";
            chkAutostart.Checked = settings.Autostart;
            chkAutostart.CheckedChanged += OnAutostartChanged;
            MakeLabel(config, "Arquivo .node", 16, 90, 140, 20, null, Theme.Muted);
            lblSource = MakeLabel(config, "", 160, 90, 276, 20, fBold, Theme.Text);
            UpdateSourceLabel();

            // Botões
            btnCheck = Place(this, Theme.MakeButton("Verificar agora", true), 16, 478, 160, 36);
            btnCheck.Click += delegate { RunCheck(true); };
            btnKill = Place(this, Theme.MakeButton("Fechar Discord", false), 186, 478, 134, 36);
            btnKill.Click += delegate { KillDiscords(); };
            tips.SetToolTip(btnKill, "Fecha (taskkill) todos os Discords abertos");
            btnOpen = Place(this, Theme.MakeButton("Abrir Discord", false), 330, 478, 134, 36);
            btnOpen.Click += delegate { OpenDiscords(); };

            // Card: atividade
            CardPanel activity = Place(this, new CardPanel(), 16, 526, 448, 104);
            MakeLabel(activity, "ATIVIDADE", 16, 10, 200, 16, fSection, Theme.Muted);
            txtLog = Place(activity, new TextBox(), 16, 30, 420, 66);
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.BorderStyle = BorderStyle.None;
            txtLog.BackColor = Theme.Card;
            txtLog.ForeColor = Theme.Muted;
            txtLog.Font = fMono;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.WordWrap = true;

            MakeLabel(this, "Ao fechar, ele continua rodando na bandeja (perto do relógio).", 16, 638, 448, 18,
                      null, Theme.Muted);

            foreach (VariantRow row in rows)
                ShowVariantInfo(row, DiscordLocator.Resolve(DiscordLocator.RootFor(row.Variant)), null);

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(480, 664);
            loadingUi = false;
            ResumeLayout(false);
            PerformLayout();
        }

        VariantRow MakeVariantRow(Control parent, DiscordVariant v, int y)
        {
            VariantRow row = new VariantRow { Variant = v };
            row.Dot = Place(parent, new StatusDot(), 16, y + 2, 15, 15);
            row.Name = Place(parent, new LinkLabel(), 38, y, 118, 20);
            row.Name.Text = v.Display;
            row.Name.Font = fBold;
            StyleLink(row.Name);
            row.Name.LinkClicked += delegate { OpenFolder(row); };
            row.Version = MakeLabel(parent, "", 160, y, 180, 20, null, Theme.Text);
            row.State = MakeLabel(parent, "", 336, y, 96, 20, fBold, Theme.Muted);
            row.State.TextAlign = ContentAlignment.TopRight;
            return row;
        }

        static T Place<T>(Control parent, T c, int x, int y, int w, int h) where T : Control
        {
            c.Location = new Point(x, y);
            c.Size = new Size(w, h);
            parent.Controls.Add(c);
            return c;
        }

        Label MakeLabel(Control parent, string text, int x, int y, int w, int h, Font font, Color color)
        {
            Label l = new Label();
            l.Text = text;
            if (font != null)
                l.Font = font;
            l.ForeColor = color;
            l.BackColor = Color.Transparent;
            l.AutoEllipsis = true;
            return Place(parent, l, x, y, w, h);
        }

        static void StyleLink(LinkLabel l)
        {
            l.LinkColor = Theme.Text;
            l.ActiveLinkColor = Theme.AccentDown;
            l.VisitedLinkColor = Theme.Text;
            l.LinkBehavior = LinkBehavior.HoverUnderline;
            l.BackColor = Color.Transparent;
        }

        void FillIntervals()
        {
            cmbInterval.Items.Clear();
            int selected = -1;
            for (int i = 0; i < IntervalOptions.Length; i++)
            {
                cmbInterval.Items.Add(DescribeInterval(IntervalOptions[i]));
                if (IntervalOptions[i] == settings.IntervalMinutes)
                    selected = i;
            }
            if (selected < 0)   // valor personalizado editado no .ini
            {
                cmbInterval.Items.Add(DescribeInterval(settings.IntervalMinutes));
                selected = cmbInterval.Items.Count - 1;
            }
            cmbInterval.SelectedIndex = selected;
        }

        static string DescribeInterval(int minutes)
        {
            if (minutes % 60 == 0)
            {
                int h = minutes / 60;
                return h == 1 ? "1 hora" : h + " horas";
            }
            return minutes == 1 ? "1 minuto" : minutes + " minutos";
        }

        // O .node é sempre o de 512 kbps (mic estéreo) embutido no .exe.
        // "source=" no DiscordNodeStereo.ini existe só como escape para testes.
        void UpdateSourceLabel()
        {
            if (source == embedded)
                lblSource.Text = "512 kbps estéreo  ·  embutido (" + FormatSize(embeddedLength) + ")";
            else
                lblSource.Text = Path.GetFileName(source.Description) + "  ·  " + source.Description;
        }

        static string FormatSize(long bytes)
        {
            return (bytes / 1048576.0).ToString("0.0", CultureInfo.GetCultureInfo("pt-BR")) + " MB";
        }

        void ShowVariantInfo(VariantRow row, VoiceTarget t, CheckResult r)
        {
            row.Folder = t.TargetDir ?? (t.App != null ? t.App.FullPath : null);
            tips.SetToolTip(row.Name, t.App != null ? "Abrir " + (row.Folder ?? t.Root) : t.Root);
            if (t.App == null)
            {
                row.Version.Text = "—";
                row.Version.ForeColor = Theme.Muted;
            }
            else
            {
                row.Version.Text = t.App.Name + (t.Module != null ? "  ·  " + t.Module.Name : "");
                row.Version.ForeColor = Theme.Text;
            }

            Color color;
            string state;
            if (r == null)
            {
                color = Theme.Border;
                state = t.App == null ? "não instalado" : "…";
            }
            else
            {
                switch (r.Status)
                {
                    case CheckStatus.UpToDate: color = Theme.Ok; state = "atualizado"; break;
                    case CheckStatus.Replaced: color = Theme.Warn; state = "substituído"; break;
                    case CheckStatus.DiscordNotFound: color = Theme.Border; state = "não instalado"; break;
                    case CheckStatus.ModuleNotFound: color = Theme.Warn; state = "sem módulo"; break;
                    default: color = Theme.Bad; state = "erro"; break;
                }
                tips.SetToolTip(row.State, r.Error ?? "");
            }
            row.Dot.DotColor = color;
            row.State.Text = state;
            row.State.ForeColor = color == Theme.Border ? Theme.Muted : color;
        }

        void SetStatus(Color color, string title, string detail)
        {
            dot.DotColor = color;
            lblStatus.Text = title;
            lblStatus.ForeColor = color == Theme.Muted ? Theme.Text : color;
            lblStatusDetail.Text = detail;
            SetTrayText("DiscordNodeStereo · " + title);
        }

        void LoadLogTail()
        {
            txtLog.Lines = Log.Tail(60);
            ScrollLogToEnd();
        }

        void OnLogWritten(string line)
        {
            if (ui != null && SynchronizationContext.Current != ui)
            {
                ui.Post(delegate { OnLogWritten(line); }, null);
                return;
            }
            if (txtLog.Lines.Length > 200)
                LoadLogTail();
            txtLog.AppendText((txtLog.TextLength > 0 ? Environment.NewLine : "") + line);
            ScrollLogToEnd();
        }

        void ScrollLogToEnd()
        {
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
        }

        // ---------------- bandeja ----------------

        void SetupTray()
        {
            tray.Icon = AppIcon.Sized(SystemInformation.SmallIconSize);
            tray.Text = "DiscordNodeStereo";
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripItem open = menu.Items.Add("Abrir DiscordNodeStereo", null, delegate { ShowFromTray(); });
            open.Font = new Font(menu.Font, FontStyle.Bold);
            menu.Items.Add("Verificar agora", null, delegate { RunCheck(true); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Sair", null, delegate { ExitApp(); });
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowFromTray(); };
            tray.BalloonTipClicked += delegate { ShowFromTray(); };
            tray.Visible = true;
        }

        void SetTrayText(string text)
        {
            tray.Text = text.Length > 63 ? text.Substring(0, 62) + "…" : text;   // limite do Windows
        }

        void Balloon(string title, string text, ToolTipIcon icon)
        {
            tray.ShowBalloonTip(6000, title, text, icon);
        }

        // Chamado pelo Program quando alguém abre o .exe de novo com o DiscordNodeStereo já rodando.
        public void RequestShow()
        {
            if (ui != null)
                ui.Post(delegate { ShowFromTray(); }, null);
        }

        public void ShowFromTray()
        {
            allowVisible = true;
            Show();
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            Activate();
        }

        protected override void SetVisibleCore(bool value)
        {
            if (!allowVisible)
            {
                value = false;
                if (!IsHandleCreated)
                    CreateHandle();
            }
            base.SetVisibleCore(value);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exiting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                if (!settings.TrayHintShown)
                {
                    Balloon("O DiscordNodeStereo continua rodando", "Ele fica aqui na bandeja e verifica os Discords " +
                            "a cada " + DescribeInterval(settings.IntervalMinutes) + ". Clique no ícone para abrir.", ToolTipIcon.Info);
                    settings.TrayHintShown = true;
                    SaveSettings();
                }
                TrimMemory();
                return;
            }
            base.OnFormClosing(e);
        }

        void ExitApp()
        {
            exiting = true;
            Log.Write("DiscordNodeStereo encerrado.");
            tray.Visible = false;
            tray.Dispose();
            Application.Exit();
        }

        // ---------------- verificação ----------------

        void RestartTimer()
        {
            timer.Stop();
            timer.Interval = settings.IntervalMinutes * 60 * 1000;
            nextCheck = DateTime.Now.AddMinutes(settings.IntervalMinutes);
            timer.Start();
        }

        async void RunCheck(bool manual)
        {
            if (checking)
                return;
            checking = true;
            btnCheck.Enabled = false;
            btnCheck.Text = "Verificando…";
            SetStatus(Theme.Muted, "Verificando…", "Procurando a versão mais nova de cada Discord.");

            NodeSource src = source;
            Dictionary<string, string> last = new Dictionary<string, string>(settings.LastVersions);
            List<CheckResult> results;
            try
            {
                results = await Task.Run(delegate { return Patcher.CheckAll(src, last); });
            }
            catch (Exception e)
            {
                results = null;
                Log.Write("Falha na verificação: " + e.Message);
                SetStatus(Theme.Bad, "Falha na verificação", e.Message);
            }

            checking = false;
            btnCheck.Enabled = true;
            btnCheck.Text = "Verificar agora";
            RestartTimer();
            if (results != null)
                HandleResults(results, manual);
            TrimMemory();
        }

        void HandleResults(List<CheckResult> results, bool manual)
        {
            List<CheckResult> installed = new List<CheckResult>();
            List<CheckResult> replaced = new List<CheckResult>();
            List<CheckResult> updated = new List<CheckResult>();
            List<CheckResult> noModule = new List<CheckResult>();
            List<CheckResult> errors = new List<CheckResult>();
            bool settingsChanged = false;

            foreach (CheckResult r in results)
            {
                ShowVariantInfo(rows.First(row => row.Variant == r.Variant), r.Target, r);
                if (r.Status == CheckStatus.DiscordNotFound)
                    continue;
                installed.Add(r);
                string version = r.Target.App.Version;
                string name = r.Variant.Display + " " + version;
                string previous;
                if (!settings.LastVersions.TryGetValue(r.Variant.Id, out previous) || previous != version)
                {
                    settings.LastVersions[r.Variant.Id] = version;
                    settingsChanged = true;
                }
                switch (r.Status)
                {
                    case CheckStatus.UpToDate:
                        updated.Add(r);
                        if (r.NewVersion)
                            Log.Write("Nova versão do " + name + ": o módulo já estava certo.");
                        break;
                    case CheckStatus.Replaced:
                        replaced.Add(r);
                        Log.Write((r.NewVersion ? "Nova versão do " + name + "! " : name + ": ") +
                                  "substituído " + r.Target.Module.Name + "\\" + DiscordLocator.FileName);
                        break;
                    case CheckStatus.ModuleNotFound:
                        noModule.Add(r);
                        break;
                    default:
                        errors.Add(r);
                        break;
                }
            }
            if (settingsChanged)
                SaveSettings();

            string when = "Última verificação às " + DateTime.Now.ToString("HH:mm") + "  ·  próxima às " + nextCheck.ToString("HH:mm");
            if (replaced.Count > 0)
            {
                SetStatus(Theme.Warn, "Módulo substituído — reinicie o Discord", when);
                lastProblem = "";
                ShowRestartDialog(replaced);
            }
            else if (errors.Count > 0)
            {
                CheckResult e = errors[0];
                string detail = e.Status == CheckStatus.SourceMissing ? "Arquivo de origem não existe: " + source.Description : e.Error;
                Problem(manual, "Falha no " + e.Variant.Display, detail);
            }
            else if (installed.Count == 0)
            {
                Problem(manual, "Nenhum Discord encontrado", "Procurei Discord, PTB e Canary em " + DiscordLocator.LocalAppData());
            }
            else if (updated.Count == 0)
            {
                Problem(manual, "Módulo de voz ainda não baixado",
                        "Abra o Discord e entre num canal de voz para ele baixar o módulo.");
            }
            else
            {
                SetStatus(Theme.Ok, "Módulo de voz atualizado", when);
                lastProblem = "";
                if (manual || installed.Any(r => r.NewVersion))
                    Log.Write("Tudo certo: " + string.Join(", ", updated.Select(r => r.Variant.Display + " " + r.Target.App.Version)) + ".");
                if (manual && !Visible)
                    Balloon("Tudo certo", "O módulo de voz já está atualizado.", ToolTipIcon.Info);
            }
            foreach (CheckResult r in noModule)
                if (manual)
                    Log.Write(r.Variant.Display + ": ainda sem módulo de voz (entre num canal de voz uma vez).");
        }

        // Com a janela escondida, avisa pelo balão só quando o problema é novo (nada de balão a cada hora).
        void Problem(bool manual, string title, string detail)
        {
            SetStatus(Theme.Bad, title, detail);
            if (manual || lastProblem != title)
                Log.Write("Problema: " + title + " — " + detail);
            if (!Visible && (manual || lastProblem != title))
                Balloon(title, detail, ToolTipIcon.Warning);
            lastProblem = title;
        }

        void ShowRestartDialog(List<CheckResult> replaced)
        {
            if (dialogOpen)
                return;
            dialogOpen = true;
            try
            {
                using (RestartDialog d = new RestartDialog(replaced))
                    d.ShowDialog(Visible ? this : null);
            }
            finally
            {
                dialogOpen = false;
            }
        }

        // ---------------- ações ----------------

        void OnIntervalChanged(object sender, EventArgs e)
        {
            if (loadingUi)
                return;
            int i = cmbInterval.SelectedIndex;
            settings.IntervalMinutes = i < IntervalOptions.Length ? IntervalOptions[i] : settings.IntervalMinutes;
            SaveSettings();
            RestartTimer();
            Log.Write("Intervalo alterado para " + DescribeInterval(settings.IntervalMinutes) + ".");
            int at = lblStatusDetail.Text.IndexOf("próxima às");
            if (at >= 0)
                lblStatusDetail.Text = lblStatusDetail.Text.Substring(0, at) + "próxima às " + nextCheck.ToString("HH:mm");
        }

        void OnAutostartChanged(object sender, EventArgs e)
        {
            if (loadingUi)
                return;
            settings.Autostart = chkAutostart.Checked;
            SaveSettings();
            try
            {
                Autostart.Apply(settings.Autostart);
                Log.Write(settings.Autostart ? "Vai iniciar junto com o Windows." : "Não vai mais iniciar com o Windows.");
            }
            catch (Exception ex)
            {
                Log.Write("Não consegui mudar o início automático: " + ex.Message);
            }
        }

        void OpenFolder(VariantRow row)
        {
            string dir = row.Folder ?? DiscordLocator.RootFor(row.Variant);
            if (Directory.Exists(dir))
                System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
        }

        void KillDiscords()
        {
            List<DiscordVariant> running = DiscordVariant.All.Where(DiscordProcess.IsRunning).ToList();
            if (running.Count == 0)
            {
                Log.Write("Nenhum Discord está aberto.");
                return;
            }
            foreach (DiscordVariant v in running)
            {
                DiscordProcess.Kill(v);
                if (!closedByUs.Contains(v))
                    closedByUs.Add(v);
                Log.Write(v.Display + " fechado (taskkill).");
            }
        }

        // Reabre os que o DiscordNodeStereo fechou; se não fechou nenhum, abre o primeiro instalado.
        void OpenDiscords()
        {
            List<DiscordVariant> targets = closedByUs.Count > 0
                ? new List<DiscordVariant>(closedByUs)
                : DiscordVariant.All.Where(DiscordLocator.IsInstalled).Take(1).ToList();
            closedByUs.Clear();
            if (targets.Count == 0)
            {
                Log.Write("Nenhum Discord instalado para abrir.");
                return;
            }
            foreach (DiscordVariant v in targets)
            {
                try
                {
                    DiscordProcess.Open(v);
                    Log.Write("Abrindo o " + v.Display + "…");
                }
                catch (Exception ex)
                {
                    Log.Write("Não consegui abrir o " + v.Display + ": " + ex.Message);
                }
            }
        }

        void SaveSettings()
        {
            try
            {
                settings.Save();
            }
            catch (Exception ex)
            {
                Log.Write("Não consegui salvar as configurações: " + ex.Message);
            }
        }

        // ---------------- memória ----------------

        [DllImport("kernel32.dll")]
        static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr min, IntPtr max);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        // Devolve ao Windows a memória usada só durante a verificação.
        static void TrimMemory()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            SetProcessWorkingSetSize(GetCurrentProcess(), new IntPtr(-1), new IntPtr(-1));
        }
    }
}
