using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MonitorTEF
{
    public class FormAlerta : Form
    {
        // ── dimensões ─────────────────────────────────────────────────────
        private const int LARGURA        = 360;
        private const int ALTURA_ALERTA  = 180;
        private const int ALTURA_ANALISE = 88;
        private const int MARGEM         = 14;
        private const int BARRA_W        = 5;

        // ── paleta ALERTA ─────────────────────────────────────────────────
        private static readonly Color BgAlerta      = Color.FromArgb(22, 22, 32);
        private static readonly Color BarraAlerta   = Color.FromArgb(220, 55, 55);
        private static readonly Color TextoPrimario = Color.FromArgb(240, 240, 245);
        private static readonly Color TextoSecund   = Color.FromArgb(160, 165, 185);
        private static readonly Color BtnVerdeBg    = Color.FromArgb(25, 110, 65);
        private static readonly Color BtnVerdeBord  = Color.FromArgb(40, 160, 95);
        private static readonly Color BtnAmBg       = Color.FromArgb(120, 85, 10);
        private static readonly Color BtnAmBord     = Color.FromArgb(200, 145, 20);

        // ── paleta ANÁLISE ────────────────────────────────────────────────
        private static readonly Color BgAnalise     = Color.FromArgb(28, 22, 8);
        private static readonly Color BarraAnalise  = Color.FromArgb(215, 160, 25);
        private static readonly Color TextoAnalise  = Color.FromArgb(215, 160, 25);

        // ── estado ────────────────────────────────────────────────────────
        private readonly MeioCaptura _meio;
        private readonly int         _offsetY;
        private bool _emAnalise = false;
        private bool _fechando  = false;

        // ── controles ─────────────────────────────────────────────────────
        private Panel  _barra;
        private Label  _lblNome;
        private Label  _lblTempo;
        private Label  _lblDetalhe;
        private Button _btnConfirmar;
        private Button _btnAnalise;
        private Label  _lblAnaliseInfo;

        private Timer _timerAnalise;
        private Timer _timerContagem;

        public event EventHandler ConfirmadoClick;
        public event EventHandler AnaliseClick;

        // ─────────────────────────────────────────────────────────────────
        public FormAlerta(MeioCaptura meio, int offsetY = 0)
        {
            _meio    = meio;
            _offsetY = offsetY;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition   = FormStartPosition.Manual;
            TopMost         = true;
            ShowInTaskbar   = false;
            Opacity         = 0;
            BackColor       = BgAlerta;

            ConstruirControles();
            Posicionar(LARGURA, ALTURA_ALERTA);
        }

        // ─────────────────────────────────────────────────────────────────
        //  CONSTRUÇÃO DOS CONTROLES
        // ─────────────────────────────────────────────────────────────────
        private void ConstruirControles()
        {
            // barra lateral
            _barra = new Panel
            {
                BackColor = BarraAlerta,
                Dock      = DockStyle.Left,
                Width     = BARRA_W
            };

            // ── NOME DO MEIO (grande, destaque máximo) ────────────────────
            _lblNome = new Label
            {
                Text      = _meio.Nome,
                Font      = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = TextoPrimario,
                Location  = new Point(20, 18),
                Size      = new Size(310, 30),
                BackColor = Color.Transparent
            };

            // ── TEMPO OCIOSO (número grande, cor de alerta) ───────────────
            string tempoStr = FormatarTempoOcioso(_meio.TempoOcioso);
            _lblTempo = new Label
            {
                Text      = $"Sem transacionar há  {tempoStr}",
                Font      = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = BarraAlerta,
                Location  = new Point(20, 52),
                Size      = new Size(320, 26),
                BackColor = Color.Transparent
            };

            // ── DETALHE DISCRETO (uma linha, média) ───────────────────────
            string mediaStr = _meio.MediaIntervaloMinutos > 0
                ? $"Média histórica: {FormatarMin(_meio.MediaIntervaloMinutos)}"
                : "";

            _lblDetalhe = new Label
            {
                Text      = mediaStr,
                Font      = new Font("Segoe UI", 8.5f),
                ForeColor = TextoSecund,
                Location  = new Point(20, 82),
                Size      = new Size(320, 18),
                BackColor = Color.Transparent
            };

            // ── BOTÃO CONFIRMAR ───────────────────────────────────────────
            _btnConfirmar = new Button
            {
                Text      = "✔   Confirmar",
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = BtnVerdeBg,
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(152, 36),
                Location  = new Point(20, 118),
                Cursor    = Cursors.Hand
            };
            _btnConfirmar.FlatAppearance.BorderColor = BtnVerdeBord;
            _btnConfirmar.FlatAppearance.BorderSize  = 1;
            _btnConfirmar.Click += BtnConfirmar_Click;

            // ── BOTÃO EM ANÁLISE ──────────────────────────────────────────
            _btnAnalise = new Button
            {
                Text      = "🔍   Em Análise",
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = BtnAmBg,
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(152, 36),
                Location  = new Point(182, 118),
                Cursor    = Cursors.Hand
            };
            _btnAnalise.FlatAppearance.BorderColor = BtnAmBord;
            _btnAnalise.FlatAppearance.BorderSize  = 1;
            _btnAnalise.Click += BtnAnalise_Click;

            // ── LABEL ESTADO ANÁLISE (oculto por padrão) ──────────────────
            _lblAnaliseInfo = new Label
            {
                Text      = "",
                Font      = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = TextoAnalise,
                Location  = new Point(20, 28),
                Size      = new Size(316, 24),
                BackColor = Color.Transparent,
                Visible   = false
            };

            // ── X de fechar (discreto) ────────────────────────────────────
            var btnX = new Label
            {
                Text      = "×",
                Font      = new Font("Segoe UI", 12),
                ForeColor = Color.FromArgb(80, 85, 100),
                Location  = new Point(LARGURA - 22, 5),
                Size      = new Size(18, 18),
                Cursor    = Cursors.Hand,
                BackColor = Color.Transparent
            };
            btnX.Click += (s, e) => FecharComFade();

            Controls.AddRange(new Control[]
            {
                _barra,
                _lblNome, _lblTempo, _lblDetalhe,
                _btnConfirmar, _btnAnalise,
                _lblAnaliseInfo,
                btnX
            });
        }

        // ─────────────────────────────────────────────────────────────────
        //  POSICIONAMENTO (canto inferior direito, empilhado)
        // ─────────────────────────────────────────────────────────────────
        private void Posicionar(int largura, int altura)
        {
            Size = new Size(largura, altura);
            var area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(
                area.Right  - largura - MARGEM,
                area.Bottom - altura  - MARGEM - _offsetY);
        }

        // ─────────────────────────────────────────────────────────────────
        //  FADE IN ao mostrar
        // ─────────────────────────────────────────────────────────────────
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            var fade = new Timer { Interval = 16 };
            fade.Tick += (s, ev) =>
            {
                if (Opacity < 0.95) Opacity += 0.07;
                else { Opacity = 0.95; fade.Stop(); }
            };
            fade.Start();
        }

        // ─────────────────────────────────────────────────────────────────
        //  AÇÃO: CONFIRMAR
        // ─────────────────────────────────────────────────────────────────
        private void BtnConfirmar_Click(object sender, EventArgs e)
        {
            LogService.Registrar(
                _meio.Codigo, _meio.Nome, AcaoOperador.CONFIRMADO,
                _meio.TempoOcioso, _meio.ToleranciaEfetivaPercent,
                $"{_meio.PercentualMedia:F0}% da média");

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

            LogService.Registrar(
                _meio.Codigo, _meio.Nome, AcaoOperador.ANALISE,
                _meio.TempoOcioso, _meio.ToleranciaEfetivaPercent,
                $"Suprimido {Config.SupressaoAnaliseMinutos}min");

            AnaliseClick?.Invoke(this, EventArgs.Empty);
            MudarParaAnalise();
        }

        // ─────────────────────────────────────────────────────────────────
        //  TRANSIÇÃO PARA ESTADO ANÁLISE
        // ─────────────────────────────────────────────────────────────────
        private void MudarParaAnalise()
        {
            // muda cores
            BackColor        = BgAnalise;
            _barra.BackColor = BarraAnalise;

            // esconde tudo menos o label de contagem
            _lblNome.Visible      = false;
            _lblTempo.Visible     = false;
            _lblDetalhe.Visible   = false;
            _btnConfirmar.Visible = false;
            _btnAnalise.Visible   = false;

            // mostra label compacto
            _lblAnaliseInfo.Text    = $"🔍  {_meio.Nome}  —  em análise";
            _lblAnaliseInfo.Visible = true;

            // encolhe
            Posicionar(LARGURA, ALTURA_ANALISE);

            // contagem regressiva (atualiza a cada 1s)
            _timerContagem = new Timer { Interval = 1000 };
            _timerContagem.Tick += (s, ev) =>
            {
                var r = _meio.TempoRestanteAnalise;
                if (r <= TimeSpan.Zero) { _timerContagem.Stop(); return; }
                _lblAnaliseInfo.Text =
                    $"🔍  {_meio.Nome}  —  volta em {r.Minutes:D2}:{r.Seconds:D2}";
            };
            _timerContagem.Start();

            // fecha automaticamente quando a supressão expirar
            int ms = Math.Max(500, (int)_meio.TempoRestanteAnalise.TotalMilliseconds);
            _timerAnalise = new Timer { Interval = ms };
            _timerAnalise.Tick += (s, ev) => { _timerAnalise.Stop(); FecharComFade(); };
            _timerAnalise.Start();
        }

        // ─────────────────────────────────────────────────────────────────
        //  FADE OUT
        // ─────────────────────────────────────────────────────────────────
        private void FecharComFade()
        {
            if (_fechando) return;
            _fechando = true;
            _timerAnalise?.Stop();
            _timerContagem?.Stop();

            var fade = new Timer { Interval = 16 };
            fade.Tick += (s, e) =>
            {
                if (Opacity > 0.06) Opacity -= 0.08;
                else { fade.Stop(); Close(); }
            };
            fade.Start();
        }

        // ─────────────────────────────────────────────────────────────────
        //  FORMATAÇÃO
        // ─────────────────────────────────────────────────────────────────
        private static string FormatarTempoOcioso(TimeSpan ts)
        {
            if (ts == TimeSpan.MaxValue) return "—";
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes:D2}min";
            if (ts.TotalMinutes >= 1)
                return $"{(int)ts.TotalMinutes}min {ts.Seconds:D2}s";
            return $"{ts.Seconds}s";
        }

        private static string FormatarMin(double min)
        {
            if (min <= 0) return "—";
            if (min >= 60) return $"{(int)(min / 60)}h {(int)(min % 60):D2}min";
            if (min >= 1)  return $"{min:F1} min";
            return $"{(int)(min * 60)}s";
        }

        // sem roubar foco
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
