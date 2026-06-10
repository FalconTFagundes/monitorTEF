using System;
using System.Drawing;
using System.Windows.Forms;

namespace MonitorTEF
{
    public class FormAlerta : Form
    {
        // ── dimensões ─────────────────────────────────────────────────────
        private const int LARGURA        = 300;
        private const int ALTURA_ALERTA  = 130;
        private const int ALTURA_ANALISE = 56;
        private const int MARGEM         = 12;
        private const int BARRA_W        = 4;

        // ── paleta ALERTA ─────────────────────────────────────────────────
        private static readonly Color BgAlerta     = Color.FromArgb(22, 22, 32);
        private static readonly Color BarraAlerta  = Color.FromArgb(210, 50, 50);
        private static readonly Color CorNome      = Color.FromArgb(240, 240, 245);
        private static readonly Color CorTempo     = Color.FromArgb(210, 50, 50);
        private static readonly Color CorDetalhe   = Color.FromArgb(130, 135, 155);
        private static readonly Color BtnVerdeBg   = Color.FromArgb(22, 100, 58);
        private static readonly Color BtnVerdeBord = Color.FromArgb(35, 150, 85);
        private static readonly Color BtnAmBg      = Color.FromArgb(110, 78, 8);
        private static readonly Color BtnAmBord    = Color.FromArgb(190, 138, 18);

        // ── paleta ANÁLISE ────────────────────────────────────────────────
        private static readonly Color BgAnalise    = Color.FromArgb(25, 20, 6);
        private static readonly Color BarraAnalise = Color.FromArgb(200, 150, 20);
        private static readonly Color CorAnalise   = Color.FromArgb(200, 150, 20);

        // ── estado ────────────────────────────────────────────────────────
        private readonly MeioCaptura _meio;
        public  int SlotIndex { get; set; }   // slot de empilhamento (0=mais abaixo)

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
        //  POSICIONAMENTO — recalculável quando um slot muda
        // ─────────────────────────────────────────────────────────────────
        public void AtualizarPosicao()
        {
            int altura = _emAnalise ? ALTURA_ANALISE : ALTURA_ALERTA;
            Size = new Size(LARGURA, altura);
            var area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(
                area.Right  - LARGURA - MARGEM,
                area.Bottom - altura  - MARGEM - SlotIndex * (ALTURA_ALERTA + 6));
        }

        // ─────────────────────────────────────────────────────────────────
        //  CONTROLES
        // ─────────────────────────────────────────────────────────────────
        private void ConstruirControles()
        {
            _barra = new Panel
            {
                BackColor = BarraAlerta,
                Dock      = DockStyle.Left,
                Width     = BARRA_W
            };

            // nome do meio — destaque máximo
            _lblNome = new Label
            {
                Text      = _meio.Nome,
                Font      = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = CorNome,
                Location  = new Point(16, 12),
                Size      = new Size(262, 24),
                BackColor = Color.Transparent
            };

            // tempo ocioso — segunda info mais importante
            _lblTempo = new Label
            {
                Text      = "há  " + FormatarTempo(_meio.TempoOcioso),
                Font      = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = CorTempo,
                Location  = new Point(16, 38),
                Size      = new Size(262, 22),
                BackColor = Color.Transparent
            };

            // detalhe discreto — só a média, em cinza pequeno
            string media = _meio.MediaIntervaloMinutos > 0
                ? $"média do meio: {FormatarMin(_meio.MediaIntervaloMinutos)}"
                : "";
            _lblDetalhe = new Label
            {
                Text      = media,
                Font      = new Font("Segoe UI", 7.5f),
                ForeColor = CorDetalhe,
                Location  = new Point(16, 62),
                Size      = new Size(262, 16),
                BackColor = Color.Transparent
            };

            // botões — altura reduzida, sem ícone emoji para evitar rendering quebrado
            _btnConfirmar = new Button
            {
                Text      = "Confirmar",
                Font      = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = BtnVerdeBg,
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(130, 28),
                Location  = new Point(16, 88),
                Cursor    = Cursors.Hand
            };
            _btnConfirmar.FlatAppearance.BorderColor = BtnVerdeBord;
            _btnConfirmar.FlatAppearance.BorderSize  = 1;
            _btnConfirmar.Click += BtnConfirmar_Click;

            _btnAnalise = new Button
            {
                Text      = "Em Analise",
                Font      = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = BtnAmBg,
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(130, 28),
                Location  = new Point(152, 88),
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
                Size      = new Size(270, 22),
                BackColor = Color.Transparent,
                Visible   = false
            };

            // X discreto
            var btnX = new Label
            {
                Text      = "×",
                Font      = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(70, 75, 90),
                Location  = new Point(LARGURA - 18, 4),
                Size      = new Size(16, 16),
                Cursor    = Cursors.Hand,
                BackColor = Color.Transparent
            };
            btnX.Click += (s, e) => FecharComFade();

            Controls.AddRange(new Control[]
            {
                _barra,
                _lblNome, _lblTempo, _lblDetalhe,
                _btnConfirmar, _btnAnalise,
                _lblAnaliseInfo, btnX
            });
        }

        // ─────────────────────────────────────────────────────────────────
        //  FADE IN
        // ─────────────────────────────────────────────────────────────────
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            var fade = new Timer { Interval = 16 };
            fade.Tick += (s, ev) =>
            {
                if (Opacity < 0.94) Opacity += 0.07;
                else { Opacity = 0.94; fade.Stop(); }
            };
            fade.Start();
        }

        // ─────────────────────────────────────────────────────────────────
        //  CONFIRMAR
        // ─────────────────────────────────────────────────────────────────
        private void BtnConfirmar_Click(object sender, EventArgs e)
        {
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
            if (_emAnalise) return;
            _emAnalise = true;

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
            _lblTempo.Visible     = false;
            _lblDetalhe.Visible   = false;
            _btnConfirmar.Visible = false;
            _btnAnalise.Visible   = false;

            _lblAnaliseInfo.Text    = $"{_meio.Nome}  —  em analise";
            _lblAnaliseInfo.Visible = true;

            // encolhe e reposiciona
            AtualizarPosicao();

            // contagem regressiva
            _timerContagem = new Timer { Interval = 1000 };
            _timerContagem.Tick += (s, ev) =>
            {
                var r = _meio.TempoRestanteAnalise;
                if (r <= TimeSpan.Zero) { _timerContagem.Stop(); return; }
                _lblAnaliseInfo.Text =
                    $"{_meio.Nome}  —  {r.Minutes:D2}:{r.Seconds:D2}";
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
