// Aviso mostrado depois de substituir o .node: o Discord precisa reiniciar para carregar o módulo novo.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DiscordNodeStereo
{
    sealed class RestartDialog : Form
    {
        readonly List<DiscordVariant> variants;
        readonly List<DiscordVariant> killed = new List<DiscordVariant>();
        readonly Label lblMessage;
        readonly Button btnKill;

        public RestartDialog(List<CheckResult> replaced)
        {
            variants = replaced.Select(r => r.Variant).ToList();
            SuspendLayout();
            Text = "DiscordNodeStereo · módulo substituído";
            Icon = AppIcon.Full();
            Font = new Font("Segoe UI", 9F);
            BackColor = Theme.Background;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;

            HeaderPanel strip = new HeaderPanel();
            strip.SetBounds(0, 0, 460, 6);
            Controls.Add(strip);

            PictureBox logo = new PictureBox();
            logo.SetBounds(22, 26, 56, 56);
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.Image = AppIcon.Logo(128);
            Controls.Add(logo);

            Label title = new Label();
            title.SetBounds(94, 24, 344, 28);
            title.Font = new Font("Segoe UI Semibold", 13F);
            title.ForeColor = Theme.Text;
            title.Text = "Arquivos .node substituídos";
            Controls.Add(title);

            List<string> names = replaced.Select(r => r.Variant.Display + " " + r.Target.App.Version).ToList();
            lblMessage = new Label();
            lblMessage.SetBounds(95, 56, 344, 76);
            lblMessage.ForeColor = Theme.Muted;
            lblMessage.Text = "O módulo de voz de 512 kbps foi colocado no " + JoinNames(names) +
                              ".\nReinicie o Discord para ele carregar o arquivo novo.";
            Controls.Add(lblMessage);

            btnKill = Theme.MakeButton("Fechar Discord", false);
            btnKill.SetBounds(22, 144, 134, 36);
            btnKill.Click += OnKill;
            Controls.Add(btnKill);

            Button btnOpen = Theme.MakeButton("Abrir Discord", true);
            btnOpen.SetBounds(164, 144, 150, 36);
            btnOpen.Click += OnOpen;
            Controls.Add(btnOpen);

            Button btnLater = Theme.MakeButton("Depois", false);
            btnLater.SetBounds(322, 144, 116, 36);
            btnLater.Click += delegate { Close(); };
            Controls.Add(btnLater);

            // Enter/Esc = "Depois": ninguém fecha o Discord sem querer apertando Enter.
            AcceptButton = btnLater;
            CancelButton = btnLater;

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(460, 200);
            ResumeLayout(false);
            PerformLayout();
        }

        static string JoinNames(List<string> names)
        {
            if (names.Count == 1)
                return names[0];
            return string.Join(", ", names.Take(names.Count - 1)) + " e no " + names[names.Count - 1];
        }

        List<DiscordVariant> Running()
        {
            return variants.Where(DiscordProcess.IsRunning).ToList();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (Running().Count == 0)
            {
                btnKill.Enabled = false;
                lblMessage.Text += "\nO Discord está fechado: é só abrir.";
            }
            ActiveControl = null;
        }

        void OnKill(object sender, EventArgs e)
        {
            foreach (DiscordVariant v in Running())
            {
                DiscordProcess.Kill(v);
                killed.Add(v);
                Log.Write(v.Display + " fechado pelo aviso (taskkill).");
            }
            btnKill.Enabled = false;
            lblMessage.Text = "Discord fechado. Clique em \"Abrir Discord\" para ele voltar já com o módulo novo.";
        }

        // Com o Discord aberto, "abrir" não recarrega o módulo: fecha antes (reinicia).
        // Abre de volta os que estavam abertos; se nenhum estava, abre o primeiro da lista.
        void OnOpen(object sender, EventArgs e)
        {
            List<DiscordVariant> toOpen = new List<DiscordVariant>(killed);
            foreach (DiscordVariant v in Running())
            {
                DiscordProcess.Kill(v);
                Log.Write(v.Display + " fechado para reiniciar.");
                toOpen.Add(v);
            }
            if (toOpen.Count == 0)
                toOpen.Add(variants[0]);
            foreach (DiscordVariant v in toOpen.Distinct())
            {
                try
                {
                    DiscordProcess.Open(v);
                    Log.Write("Abrindo o " + v.Display + " com o módulo novo…");
                }
                catch (Exception ex)
                {
                    Log.Write("Não consegui abrir o " + v.Display + ": " + ex.Message);
                }
            }
            Close();
        }
    }
}
