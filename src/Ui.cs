// Tema visual, ícone e painéis usados pelas janelas do DiscordNodeStereo.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace DiscordNodeStereo
{
    static class Theme
    {
        public static readonly Color Background = Color.FromArgb(246, 247, 251);
        public static readonly Color Card = Color.White;
        public static readonly Color Border = Color.FromArgb(226, 228, 238);
        public static readonly Color Text = Color.FromArgb(28, 27, 46);
        public static readonly Color Muted = Color.FromArgb(112, 116, 138);
        public static readonly Color Accent = Color.FromArgb(109, 40, 217);
        public static readonly Color AccentHover = Color.FromArgb(124, 58, 237);
        public static readonly Color AccentDown = Color.FromArgb(91, 33, 182);
        public static readonly Color Cyan = Color.FromArgb(8, 145, 178);
        public static readonly Color Ok = Color.FromArgb(22, 163, 74);
        public static readonly Color Warn = Color.FromArgb(217, 119, 6);
        public static readonly Color Bad = Color.FromArgb(220, 38, 38);

        public static Button MakeButton(string text, bool primary)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            if (primary)
            {
                b.BackColor = Accent;
                b.ForeColor = Color.White;
                b.FlatAppearance.BorderSize = 0;
                b.FlatAppearance.MouseOverBackColor = AccentHover;
                b.FlatAppearance.MouseDownBackColor = AccentDown;
                b.Font = new Font("Segoe UI Semibold", 9.5F);
            }
            else
            {
                b.BackColor = Color.White;
                b.ForeColor = Text;
                b.FlatAppearance.BorderColor = Border;
                b.FlatAppearance.MouseOverBackColor = Color.FromArgb(243, 240, 255);
                b.FlatAppearance.MouseDownBackColor = Color.FromArgb(233, 228, 255);
            }
            return b;
        }
    }

    static class AppIcon
    {
        static Stream Resource()
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream("icon.ico");
        }

        public static Icon Full()
        {
            using (Stream s = Resource())
                return new Icon(s);
        }

        public static Icon Sized(Size size)
        {
            using (Stream s = Resource())
                return new Icon(s, size);
        }

        public static Bitmap Logo(int size)
        {
            using (Icon i = Sized(new Size(size, size)))
                return i.ToBitmap();
        }
    }

    // Faixa do topo com degradê roxo -> ciano.
    sealed class HeaderPanel : Panel
    {
        public HeaderPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0)
                return;
            using (LinearGradientBrush b = new LinearGradientBrush(ClientRectangle, Theme.Accent, Theme.Cyan, 20F))
                e.Graphics.FillRectangle(b, ClientRectangle);
        }
    }

    sealed class CardPanel : Panel
    {
        public CardPanel()
        {
            BackColor = Theme.Card;
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Theme.Border))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    sealed class StatusDot : Control
    {
        Color color = Theme.Muted;

        public StatusDot()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Theme.Card;
        }

        public Color DotColor
        {
            get { return color; }
            set { color = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int d = Math.Min(Width, Height) - 1;
            using (SolidBrush halo = new SolidBrush(Color.FromArgb(50, color)))
                e.Graphics.FillEllipse(halo, 0, 0, d, d);
            int inner = d / 2;
            using (SolidBrush b = new SolidBrush(color))
                e.Graphics.FillEllipse(b, (d - inner) / 2F, (d - inner) / 2F, inner, inner);
        }
    }
}
