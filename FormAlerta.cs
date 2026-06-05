using System;
using System.Drawing;
using System.Windows.Forms;

namespace MonitorTEF
{
    public class FormAlerta : Form
    {
        private const int LARGURA  = 340;
        private const int ALTURA   = 100;
        private const int MARGEM   = 12;
        private const int DURACAO_MS = 6000;   // some sozinho após 6 s
        private const int FADE_STEP = 15;

        private Timer _timerFade;
        private Timer _timerVida;
        private Label _lblTitulo;
        private Label _lblMensagem;
        private bool _fechando = false;

        public FormAlerta(string nomeMeio, TimeSpan tempoOcioso)
        {
            // ── janela sem borda, sempre no topo ──────────────────────────
            FormBorderStyle = FormBorderStyle.None;
            StartPosition   = FormStartPosition.Manual;
            TopMost         = true;
            ShowInTaskbar   = false;
            Opacity         = 0;
            Size            = new Size(LARGURA, ALTURA);
            BackColor       = Color.FromArgb(30, 30, 30);

            // posição: canto inferior direito
            var area  = Screen.PrimaryScreen.WorkingArea;
            Location  = new Point(area.Right - LARGURA - MARGEM,
                                  area.Bottom - ALTURA - MARGEM);

            // ── barra colorida lateral de alerta ─────────────────────────
            var barra = new Panel
            {
                BackColor = Color.FromArgb(230, 80, 60),
                Dock      = DockStyle.Left,
                Width     = 6
            };

            // ── ícone ⚠ ─────────────────────────────────────────────────
            var lblIcone = new Label
            {
                Text      = "\u26A0",
                Font      = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.FromArgb(230, 80, 60),
                Location  = new Point(14, 18),
                Size      = new Size(36, 36),
                BackColor = Color.Transparent
            };

            // ── título ───────────────────────────────────────────────────
            _lblTitulo = new Label
            {
                Text      = "MEIO SEM TRANSAÇÃO",
                Font      = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = Color.FromArgb(230, 80, 60),
                Location  = new Point(54, 14),
                Size      = new Size(270, 18),
                BackColor = Color.Transparent
            };

            // ── mensagem ─────────────────────────────────────────────────
            string horas   = (int)tempoOcioso.TotalHours > 0
                             ? $"{(int)tempoOcioso.TotalHours}h " : "";
            string minutos = $"{tempoOcioso.Minutes:D2}min";

            _lblMensagem = new Label
            {
                Text      = $"{nomeMeio}\nsem transacionar há {horas}{minutos}",
                Font      = new Font("Segoe UI", 9),
                ForeColor = Color.WhiteSmoke,
                Location  = new Point(54, 34),
                Size      = new Size(272, 50),
                BackColor = Color.Transparent
            };

            // ── botão fechar ─────────────────────────────────────────────
            var btnFechar = new Label
            {
                Text      = "\u00D7",
                Font      = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.Gray,
                Location  = new Point(LARGURA - 24, 4),
                Size      = new Size(20, 20),
                Cursor    = Cursors.Hand,
                BackColor = Color.Transparent
            };
            btnFechar.Click += (s, e) => FecharComFade();

            Controls.AddRange(new Control[] { barra, lblIcone, _lblTitulo, _lblMensagem, btnFechar });

            // clique em qualquer lugar fecha
            Click += (s, e) => FecharComFade();
            _lblMensagem.Click += (s, e) => FecharComFade();
            _lblTitulo.Click   += (s, e) => FecharComFade();

            // ── timers ───────────────────────────────────────────────────
            _timerVida = new Timer { Interval = DURACAO_MS };
            _timerVida.Tick += (s, e) => { _timerVida.Stop(); FecharComFade(); };
            _timerVida.Start();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // fade in
            _timerFade = new Timer { Interval = 20 };
            _timerFade.Tick += (s, ev) =>
            {
                if (Opacity < 0.93) Opacity += 0.07;
                else { Opacity = 0.93; _timerFade.Stop(); }
            };
            _timerFade.Start();
        }

        private void FecharComFade()
        {
            if (_fechando) return;
            _fechando = true;
            _timerVida.Stop();

            var fade = new Timer { Interval = 20 };
            fade.Tick += (s, e) =>
            {
                if (Opacity > 0.05) Opacity -= 0.08;
                else { fade.Stop(); Close(); }
            };
            fade.Start();
        }

        // impede que o popup roube o foco da janela ativa
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }
    }
}
