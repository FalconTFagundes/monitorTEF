using System;
using System.Drawing;
using System.Windows.Forms;

namespace MonitorTEF
{
    public class FormAlerta : Form
    {
        // ── dimensões ─────────────────────────────────────────────────────
        private const int LARGURA        = 300;
        private const int ALTURA_ALERTA  = 138;
        private const int ALTURA_ANALISE = 56;
        private const int MARGEM         = 12;
        private const int BARRA_W        = 4;
        private const int TIMEOUT_SEG    = 30;

        // ── paleta ────────────────────────────────────────────────────────
        private static readonly Color BgAlerta      = Color.FromArgb(26, 26, 34);
        private static readonly Color BarraAlerta   = Color.FromArgb(160, 55, 55);
        private static readonly Color CorNome       = Color.FromArgb(205, 205, 218);
        private static readonly Color CorTempo      = Color.FromArgb(185, 85, 85);
        private static readonly Color CorDetalhe    = Color.FromArgb(95, 100, 120);
        private static readonly Color BtnVerdeBg    = Color.FromArgb(28, 72, 48);
        private static readonly Color BtnVerdeBord  = Color.FromArgb(42, 105, 70);
        private static readonly Color BtnAmBg       = Color.FromArgb(76, 57, 12);
        private static readonly Color BtnAmBord     = Color.FromArgb(130, 100, 25);
        private static readonly Color CorContadorHi = Color.FromArgb(160, 55, 55);  // vermelho quando < 10s
        private static readonly Color CorContadorLo = Color.FromArgb(85, 90, 108);  // cinza normal
        private static readonly Color BgAnalise     = Color.FromArgb(24, 19, 5);
        private static readonly Color BarraAnalise  = Color.FromArgb(190, 140, 18);
        private static readonly Color CorAnalise    = Color.FromArgb(190, 140, 18);

        // ── estado ────────────────────────────────────────────────────────
        private readonly MeioCaptura _meio;
        public  int  SlotIndex  { get; set; }
        private bool _emAnalise = false;
        private bool _fechando  = false;
        private bool _clicado   = false;
        private int  _segundos  = TIMEOUT_SEG;

        // ── controles ─────────────────────────────────────────────────────
        private Panel  _barra;
        private Label  _lblNome;
        private Label  _lblTempo;
        private Label  _lblDetalhe;
        private Button _btnConfirmar;
        private Button _btnAnalise;
        private Label  _lblContador;     // "29s" no canto superior direito
        private Label  _lblAnaliseInfo;

        // ── timers ────────────────────────────────────────────────────────
        private Timer _timerTimeout;
        private Timer _timerAnalise;
        private Timer _timerContagem;

        // ── eventos ───────────────────────────────────────────────────────
        public event EventHandler ConfirmadoClick;
        public event EventHandler AnaliseClick;
        public event EventHandler SumidoAutomaticamente;

        // ─────────────────────────────────────────────────────────────────
        public FormAlerta(MeioCaptura meio, int slotIndex = 0)
        {
            _meio     = meio;
            SlotIndex = slotIndex;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition   = FormStartPosition.Manual;
            TopMost         = true;
            ShowInTaskbar   = false;
            Opacity         = 0;
            BackColor       = BgAlerta;

            ConstruirControles();
            AtualizarPosicao();
        }

        // ─────────────────────────────────────────────────────────────────
        //  POSICIONAMENTO
        // ─────────────────────────────────────────────────────────────────
        public void AtualizarPosicao()
        {
            int h = _emAnalise ? ALTURA_ANALISE : ALTURA_ALERTA;
            Size = new Size(LARGURA, h);
            var area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(
                area.Right  - LARGURA - MARGEM,
                area.Bottom - h - MARGEM - SlotIndex * (ALTURA_ALERTA + 6));
        }

        // ─────────────────────────────────────────────────────────────────
        //  CONTROLES
        // ─────────────────────────────────────────────────────────────────
        private void ConstruirControles()
        {
            // barra lateral colorida
            _barra = new Panel
            {
                BackColor = BarraAlerta,
                Dock      = DockStyle.Left,
                Width     = BARRA_W
            };

            // nome do meio
            _lblNome = new Label
            {
                Text      = _meio.Nome,
                Font      = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = CorNome,
                Location  = new Point(16, 11),
                Size      = new Size(220, 24),
                BackColor = Color.Transparent
            };

            // ── CONTADOR DE TEMPO no canto superior direito ───────────────
            // Mostra "30s", "29s", ..., "1s" — visível, sem ambiguidade
            _lblContador = new Label
            {
                Text      = $"{TIMEOUT_SEG}s",
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = CorContadorLo,
                Location  = new Point(LARGURA - 42, 11),
                Size      = new Size(36, 20),
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent
            };

            // tempo ocioso
            _lblTempo = new Label
            {
                Text      = "há  " + FormatarTempo(_meio.TempoOcioso),
                Font      = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = CorTempo,
                Location  = new Point(16, 37),
                Size      = new Size(272, 22),
                BackColor = Color.Transparent
            };

            // detalhe — média
            string media = _meio.MediaIntervaloMinutos > 0
                ? "média: " + FormatarMin(_meio.MediaIntervaloMinutos)
                : "";
            _lblDetalhe = new Label
            {
                Text      = media,
                Font      = new Font("Segoe UI", 7.5f),
                ForeColor = CorDetalhe,
                Location  = new Point(16, 61),
                Size      = new Size(272, 15),
                BackColor = Color.Transparent
            };

            // botão Confirmar
            _btnConfirmar = new Button
            {
                Text      = "Confirmar",
                Font      = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = BtnVerdeBg,
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(130, 26),
                Location  = new Point(16, 96),
                Cursor    = Cursors.Hand
            };
            _btnConfirmar.FlatAppearance.BorderColor = BtnVerdeBord;
            _btnConfirmar.FlatAppearance.BorderSize  = 1;
            _btnConfirmar.Click += BtnConfirmar_Click;

            // botão Em Analise
            _btnAnalise = new Button
            {
                Text      = "Em Analise",
                Font      = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = BtnAmBg,
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(130, 26),
                Location  = new Point(152, 96),
                Cursor    = Cursors.Hand
            };
            _btnAnalise.FlatAppearance.BorderColor = BtnAmBord;
            _btnAnalise.FlatAppearance.BorderSize  = 1;
            _btnAnalise.Click += BtnAnalise_Click;

            // label estado análise (oculto por padrão)
            _lblAnaliseInfo = new Label
            {
                Text      = "",
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = CorAnalise,
                Location  = new Point(16, 16),
                Size      = new Size(268, 22),
                BackColor = Color.Transparent,
                Visible   = false
            };

            Controls.AddRange(new Control[]
            {
                _barra,
                _lblNome, _lblContador,
                _lblTempo, _lblDetalhe,
                _btnConfirmar, _btnAnalise,
                _lblAnaliseInfo
            });
        }

        // ─────────────────────────────────────────────────────────────────
        //  FADE IN + inicia temporizador de 30s
        // ─────────────────────────────────────────────────────────────────
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            var fade = new Timer { Interval = 16 };
            fade.Tick += (s, ev) =>
            {
                if (Opacity < 0.82) Opacity += 0.06;
                else { Opacity = 0.82; fade.Stop(); }
            };
            fade.Start();

            // temporizador de 30s — atualiza o contador a cada 1 segundo
            _timerTimeout = new Timer { Interval = 1000 };
            _timerTimeout.Tick += (s, ev) =>
            {
                _segundos--;

                // atualiza o texto do contador
                if (_lblContador != null && !_lblContador.IsDisposed)
                {
                    _lblContador.Text      = $"{_segundos}s";
                    _lblContador.ForeColor = _segundos <= 10
                        ? CorContadorHi   // vermelho nos últimos 10s
                        : CorContadorLo;  // cinza discreto no resto
                }

                if (_segundos <= 0)
                {
                    _timerTimeout.Stop();
                    FecharSemClique();
                }
            };
            _timerTimeout.Start();
        }

        // fecha sem ação → dispara SumidoAutomaticamente
        private void FecharSemClique()
        {
            if (_fechando) return;
            _clicado = false;
            _timerTimeout?.Stop();
            FecharComFade();
        }

        // ─────────────────────────────────────────────────────────────────
        //  CONFIRMAR
        // ─────────────────────────────────────────────────────────────────
        private void BtnConfirmar_Click(object sender, EventArgs e)
        {
            if (_fechando) return;
            _clicado = true;
            _timerTimeout?.Stop();

            LogService.Registrar(
                _meio.Codigo, _meio.Nome, AcaoOperador.CONFIRMADO,
                _meio.TempoOcioso, _meio.ToleranciaEfetivaPercent,
                $"{_meio.PercentualMedia:F0}% da media");

            ConfirmadoClick?.Invoke(this, EventArgs.Empty);
            FecharComFade();
        }

        // ─────────────────────────────────────────────────────────────────
        //  EM ANÁLISE
        // ─────────────────────────────────────────────────────────────────
        private void BtnAnalise_Click(object sender, EventArgs e)
        {
            if (_emAnalise || _fechando) return;
            _emAnalise = true;
            _clicado   = true;
            _timerTimeout?.Stop();

            LogService.Registrar(
                _meio.Codigo, _meio.Nome, AcaoOperador.ANALISE,
                _meio.TempoOcioso, _meio.ToleranciaEfetivaPercent,
                $"Suprimido {Config.SupressaoAnaliseMinutos}min");

            AnaliseClick?.Invoke(this, EventArgs.Empty);
            MudarParaAnalise();
        }

        private void MudarParaAnalise()
        {
            BackColor        = BgAnalise;
            _barra.BackColor = BarraAnalise;

            _lblNome.Visible      = false;
            _lblContador.Visible  = false;
            _lblTempo.Visible     = false;
            _lblDetalhe.Visible   = false;
            _btnConfirmar.Visible = false;
            _btnAnalise.Visible   = false;

            _lblAnaliseInfo.Text    = $"{_meio.Nome}  —  em analise";
            _lblAnaliseInfo.Visible = true;

            AtualizarPosicao();

            // contagem regressiva da supressão
            _timerContagem = new Timer { Interval = 1000 };
            _timerContagem.Tick += (s, ev) =>
            {
                var r = _meio.TempoRestanteAnalise;
                if (r <= TimeSpan.Zero) { _timerContagem.Stop(); return; }
                _lblAnaliseInfo.Text = $"{_meio.Nome}  —  {r.Minutes:D2}:{r.Seconds:D2}";
            };
            _timerContagem.Start();

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
            _timerTimeout?.Stop();
            _timerAnalise?.Stop();
            _timerContagem?.Stop();

            var fade = new Timer { Interval = 16 };
            fade.Tick += (s, e) =>
            {
                if (Opacity > 0.06) Opacity -= 0.08;
                else
                {
                    fade.Stop();
                    if (!_clicado && !_emAnalise)
                        SumidoAutomaticamente?.Invoke(this, EventArgs.Empty);
                    Close();
                }
            };
            fade.Start();
        }

        // ─────────────────────────────────────────────────────────────────
        //  FORMATAÇÃO
        // ─────────────────────────────────────────────────────────────────
        private static string FormatarTempo(TimeSpan ts)
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
            if (min >= 1)  return $"{min:F1}min";
            return $"{(int)(min * 60)}s";
        }

        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000;
                return cp;
            }
        }
    }
}
