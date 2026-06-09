using System;
using System.Drawing;
using System.Windows.Forms;

namespace MonitorTEF
{
    /// <summary>
    /// Popup de alerta que fica na tela até o operador agir.
    ///
    /// Estados visuais:
    ///   ALERTA    — fundo escuro, barra vermelha, dois botões de ação
    ///   EM ANÁLISE — fundo amarelo-escuro, barra amarela, contador regressivo
    ///
    /// Eventos públicos:
    ///   OnConfirmado → operador confirmou o alerta (fecha e loga)
    ///   OnAnalise    → operador colocou em análise (muda visual, loga, fecha após 5 min)
    /// </summary>
    public class FormAlerta : Form
    {
        // ── dimensões ─────────────────────────────────────────────────────
        private const int LARGURA_ALERTA  = 360;
        private const int ALTURA_ALERTA   = 140;
        private const int LARGURA_ANALISE = 320;
        private const int ALTURA_ANALISE  = 72;
        private const int MARGEM          = 12;

        // ── cores ─────────────────────────────────────────────────────────
        private static readonly Color CorAlertaBg    = Color.FromArgb(28, 28, 38);
        private static readonly Color CorAlertaBarra = Color.FromArgb(220, 60, 50);
        private static readonly Color CorAnaliseBg   = Color.FromArgb(50, 40, 10);
        private static readonly Color CorAnaliseBarra= Color.FromArgb(220, 170, 30);

        // ── estado ────────────────────────────────────────────────────────
        private readonly MeioCaptura _meio;
        private bool _emAnalise = false;
        private bool _fechando  = false;

        // ── controles ─────────────────────────────────────────────────────
        private Panel  _barra;
        private Label  _lblIcone;
        private Label  _lblTitulo;
        private Label  _lblMensagem;
        private Button _btnConfirmar;
        private Button _btnAnalise;
        private Label  _lblContagem;  // visível só no estado análise

        // ── timers ────────────────────────────────────────────────────────
        private Timer _timerAnalise;   // dispara reativação ao fim da supressão
        private Timer _timerContagem;  // atualiza contador regressivo (1s)

        // ── offset para empilhamento externo ──────────────────────────────
        private readonly int _offsetY;

        // ── eventos para FormPrincipal ────────────────────────────────────
        public event EventHandler ConfirmadoClick;
        public event EventHandler AnaliseClick;

        public FormAlerta(MeioCaptura meio, int offsetY = 0)
        {
            _meio    = meio;
            _offsetY = offsetY;

            InicializarJanela();
            ConstruirControles();
            Posicionar(LARGURA_ALERTA, ALTURA_ALERTA);
        }

        // ─────────────────────────────────────────────────────────────────
        //  INICIALIZAÇÃO
        // ─────────────────────────────────────────────────────────────────
        private void InicializarJanela()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition   = FormStartPosition.Manual;
            TopMost         = true;
            ShowInTaskbar   = false;
            Opacity         = 0;
            BackColor       = CorAlertaBg;
        }

        private void ConstruirControles()
        {
            // barra lateral colorida
            _barra = new Panel
            {
                BackColor = CorAlertaBarra,
                Dock      = DockStyle.Left,
                Width     = 6
            };

            // ícone
            _lblIcone = new Label
            {
                Text      = "\u26A0",
                Font      = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = CorAlertaBarra,
                Location  = new Point(14, 18),
                Size      = new Size(36, 36),
                BackColor = Color.Transparent
            };

            // título
            _lblTitulo = new Label
            {
                Text      = "MEIO SEM TRANSAÇÃO",
                Font      = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = CorAlertaBarra,
                Location  = new Point(54, 12),
                Size      = new Size(290, 18),
                BackColor = Color.Transparent
            };

            // mensagem com meio e tempo ocioso
            string horas   = (int)_meio.TempoOcioso.TotalHours > 0
                             ? $"{(int)_meio.TempoOcioso.TotalHours}h " : "";
            string minutos = $"{_meio.TempoOcioso.Minutes:D2}min";

            _lblMensagem = new Label
            {
                Text      = $"{_meio.Nome}\nsem transacionar há {horas}{minutos}\n" +
                             $"Limite configurado: {_meio.LimiteEfetivoMinutos} min",
                Font      = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(220, 220, 220),
                Location  = new Point(54, 32),
                Size      = new Size(292, 55),
                BackColor = Color.Transparent
            };

            // botão CONFIRMAR
            _btnConfirmar = new Button
            {
                Text      = "✔  CONFIRMAR",
                Font      = new Font("Segoe UI", 8, FontStyle.Bold),
                BackColor = Color.FromArgb(20, 100, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(142, 30),
                Location  = new Point(54, 100),
                Cursor    = Cursors.Hand
            };
            _btnConfirmar.FlatAppearance.BorderColor = Color.FromArgb(40, 150, 90);
            _btnConfirmar.Click += BtnConfirmar_Click;

            // botão EM ANÁLISE
            _btnAnalise = new Button
            {
                Text      = "🔍  EM ANÁLISE",
                Font      = new Font("Segoe UI", 8, FontStyle.Bold),
                BackColor = Color.FromArgb(110, 80, 10),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(142, 30),
                Location  = new Point(204, 100),
                Cursor    = Cursors.Hand
            };
            _btnAnalise.FlatAppearance.BorderColor = Color.FromArgb(180, 130, 20);
            _btnAnalise.Click += BtnAnalise_Click;

            // label de contagem regressiva (só visível no estado análise)
            _lblContagem = new Label
            {
                Text      = "",
                Font      = new Font("Segoe UI", 8, FontStyle.Italic),
                ForeColor = Color.FromArgb(220, 170, 30),
                Location  = new Point(54, 44),
                Size      = new Size(254, 18),
                BackColor = Color.Transparent,
                Visible   = false
            };

            // botão fechar (X) — fecha como confirmação silenciosa
            var btnX = new Label
            {
                Text      = "\u00D7",
                Font      = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 100, 110),
                Location  = new Point(LARGURA_ALERTA - 24, 4),
                Size      = new Size(20, 20),
                Cursor    = Cursors.Hand,
                BackColor = Color.Transparent
            };
            btnX.Click += (s, e) => FecharComFade();

            Controls.AddRange(new Control[]
            {
                _barra, _lblIcone, _lblTitulo, _lblMensagem,
                _btnConfirmar, _btnAnalise, _lblContagem, btnX
            });
        }

        // ─────────────────────────────────────────────────────────────────
        //  POSICIONAMENTO
        // ─────────────────────────────────────────────────────────────────
        private void Posicionar(int largura, int altura)
        {
            Size = new Size(largura, altura);
            var area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(
                area.Right  - largura - MARGEM,
                area.Bottom - altura  - MARGEM - _offsetY
            );
        }

        // ─────────────────────────────────────────────────────────────────
        //  FADE IN ao mostrar
        // ─────────────────────────────────────────────────────────────────
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            var fade = new Timer { Interval = 20 };
            fade.Tick += (s, ev) =>
            {
                if (Opacity < 0.94) Opacity += 0.08;
                else { Opacity = 0.94; fade.Stop(); }
            };
            fade.Start();
        }

        // ─────────────────────────────────────────────────────────────────
        //  AÇÃO: CONFIRMAR
        // ─────────────────────────────────────────────────────────────────
        private void BtnConfirmar_Click(object sender, EventArgs e)
        {
            // Loga a confirmação
            LogService.Registrar(
                _meio.Codigo,
                _meio.Nome,
                AcaoOperador.CONFIRMADO,
                _meio.TempoOcioso,
                _meio.LimiteEfetivoMinutos,
                obs: "Alerta visualizado e confirmado pelo operador"
            );

            ConfirmadoClick?.Invoke(this, EventArgs.Empty);
            FecharComFade();
        }

        // ─────────────────────────────────────────────────────────────────
        //  AÇÃO: EM ANÁLISE
        // ─────────────────────────────────────────────────────────────────
        private void BtnAnalise_Click(object sender, EventArgs e)
        {
            if (_emAnalise) return;
            _emAnalise = true;

            // Loga a ação
            LogService.Registrar(
                _meio.Codigo,
                _meio.Nome,
                AcaoOperador.ANALISE,
                _meio.TempoOcioso,
                _meio.LimiteEfetivoMinutos,
                obs: $"Suprimido por {Config.SupressaoAnaliseMinutos} min pelo operador"
            );

            // Notifica FormPrincipal para marcar o meio como EmAnalise
            AnaliseClick?.Invoke(this, EventArgs.Empty);

            // Muda visual para estado "análise"
            MudarParaEstadoAnalise();
        }

        // ─────────────────────────────────────────────────────────────────
        //  MUDAR VISUAL → ESTADO ANÁLISE
        //  popup encolhe, amarela e mostra contagem regressiva
        // ─────────────────────────────────────────────────────────────────
        private void MudarParaEstadoAnalise()
        {
            BackColor       = CorAnaliseBg;
            _barra.BackColor= CorAnaliseBarra;
            _lblIcone.Text  = "\uD83D\uDD0D"; // 🔍 via surrogates
            _lblIcone.Text  = "?";  // fallback texto simples
            _lblIcone.ForeColor = CorAnaliseBarra;
            _lblIcone.Text  = "🔍";

            _lblTitulo.Text      = "EM ANÁLISE";
            _lblTitulo.ForeColor = CorAnaliseBarra;

            _lblMensagem.Visible  = false;
            _btnConfirmar.Visible = false;
            _btnAnalise.Visible   = false;
            _lblContagem.Visible  = true;

            // encolhe o popup
            Posicionar(LARGURA_ANALISE, ALTURA_ANALISE);

            // Inicia contagem regressiva visual (atualiza a cada 1s)
            _timerContagem = new Timer { Interval = 1000 };
            _timerContagem.Tick += (s, e) =>
            {
                var restante = _meio.TempoRestanteAnalise;
                if (restante <= TimeSpan.Zero)
                {
                    _timerContagem.Stop();
                    return;
                }
                _lblContagem.Text =
                    $"{_meio.Nome} — volta em {restante.Minutes:D2}:{restante.Seconds:D2}";
            };
            _timerContagem.Start();

            // Timer que fecha o popup quando a supressão expira
            int msRestante = (int)_meio.TempoRestanteAnalise.TotalMilliseconds;
            if (msRestante <= 0) msRestante = Config.SupressaoAnaliseMinutos * 60 * 1000;

            _timerAnalise = new Timer { Interval = msRestante };
            _timerAnalise.Tick += (s, e) =>
            {
                _timerAnalise.Stop();
                FecharComFade();
            };
            _timerAnalise.Start();
        }

        // ─────────────────────────────────────────────────────────────────
        //  FADE OUT e fecha
        // ─────────────────────────────────────────────────────────────────
        private void FecharComFade()
        {
            if (_fechando) return;
            _fechando = true;
            _timerAnalise?.Stop();
            _timerContagem?.Stop();

            var fade = new Timer { Interval = 20 };
            fade.Tick += (s, e) =>
            {
                if (Opacity > 0.05) Opacity -= 0.09;
                else { fade.Stop(); Close(); }
            };
            fade.Start();
        }

        // impede roubo de foco
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
