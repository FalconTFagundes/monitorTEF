using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MonitorTEF
{
    public class FormPrincipal : Form
    {
        // ── controles ─────────────────────────────────────────────────────
        private Panel         _painelTopo;
        private Label         _lblTitulo;
        private Label         _lblStatus;
        private Label         _lblProxima;
        private Button        _btnAtualizar;
        private DataGridView  _grid;
        private Panel         _painelRodape;

        // config global
        private NumericUpDown _nudPeriodoHoras;
        private NumericUpDown _nudTolerancia;
        private NumericUpDown _nudIntervaloSeg;
        private Button        _btnSalvarConfig;

        // ── timers ────────────────────────────────────────────────────────
        private Timer    _timerPolling;
        private Timer    _timerClock;
        private DateTime _proximaVerificacao;

        // ── estado ────────────────────────────────────────────────────────
        private List<MeioCaptura> _meios          = new List<MeioCaptura>();
        private int _periodoHoras    = Config.PeriodoHistoricoHoras;
        private int _toleranciaPerc  = Config.ToleranciaGlobalPercent;
        private int _intervaloSeg    = Config.IntervaloVerificacaoSegundos;

        // ── popups ativos por código de meio ──────────────────────────────
        private readonly Dictionary<string, FormAlerta> _popupsAtivos =
            new Dictionary<string, FormAlerta>();
        private const int MAX_POPUPS = 4;

        // ─────────────────────────────────────────────────────────────────
        public FormPrincipal()
        {
            ConstruirInterface();
            IniciarPolling();
            ExecutarVerificacao();
        }

        // ─────────────────────────────────────────────────────────────────
        //  INTERFACE
        // ─────────────────────────────────────────────────────────────────
        private void ConstruirInterface()
        {
            Text          = "Monitor TEF — BigCard";
            Size          = new Size(940, 600);
            MinimumSize   = new Size(720, 480);
            StartPosition = FormStartPosition.CenterScreen;
            Font          = new Font("Segoe UI", 9);
            BackColor     = Color.FromArgb(245, 245, 245);

            // ── topo ─────────────────────────────────────────────────────
            _painelTopo = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 64,
                BackColor = Color.FromArgb(20, 60, 120),
                Padding   = new Padding(14, 0, 14, 0)
            };

            _lblTitulo = new Label
            {
                Text      = "Monitor de Meios de Captura TEF",
                Font      = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = true,
                Location  = new Point(14, 12)
            };

            _lblStatus = new Label
            {
                Text      = "Aguardando primeira verificação...",
                Font      = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(180, 210, 255),
                AutoSize  = true,
                Location  = new Point(16, 40)
            };

            _lblProxima = new Label
            {
                Text      = "",
                Font      = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(180, 210, 255),
                AutoSize  = true,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };

            _btnAtualizar = new Button
            {
                Text      = "⟳  Atualizar Agora",
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(40, 100, 190),
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(148, 30),
                Anchor    = AnchorStyles.Top | AnchorStyles.Right,
                Cursor    = Cursors.Hand
            };
            _btnAtualizar.FlatAppearance.BorderColor = Color.FromArgb(80, 140, 230);
            _btnAtualizar.Click += (s, e) => ExecutarVerificacao();

            _painelTopo.Controls.AddRange(new Control[]
                { _lblTitulo, _lblStatus, _lblProxima, _btnAtualizar });
            _painelTopo.Resize += (s, e) => PosicionarControlesTopo();
            PosicionarControlesTopo();

            // ── grid ─────────────────────────────────────────────────────
            _grid = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                ReadOnly              = true,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible     = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor       = Color.White,
                BorderStyle           = BorderStyle.None,
                CellBorderStyle       = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor             = Color.FromArgb(220, 220, 220),
                Font                  = new Font("Segoe UI", 9),
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeight   = 36
            };
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(20, 60, 120);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _grid.ColumnHeadersDefaultCellStyle.Font      = new Font("Segoe UI", 9, FontStyle.Bold);
            _grid.ColumnHeadersDefaultCellStyle.Padding   = new Padding(6, 0, 0, 0);
            _grid.EnableHeadersVisualStyles                = false;
            _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 255);
            _grid.DefaultCellStyle.Padding                   = new Padding(6, 4, 6, 4);
            _grid.RowTemplate.Height                         = 32;

            // colunas — agora com Média, % da Média e Transações em vez de "Limite fixo"
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCodigo",   HeaderText = "Cód.",             FillWeight = 5  });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colMeio",     HeaderText = "Meio de Captura",  FillWeight = 20 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUltima",   HeaderText = "Última Transação", FillWeight = 17 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOcioso",   HeaderText = "Ocioso há",        FillWeight = 12 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colMedia",    HeaderText = "Média Intervalo",  FillWeight = 12 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPercent",  HeaderText = "% da Média",       FillWeight = 10 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTxDesf",   HeaderText = "Tx / Desfaz.",     FillWeight = 11 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSituacao", HeaderText = "Situação",         FillWeight = 13 });

            _grid.CellDoubleClick += Grid_CellDoubleClick;

            // ── rodapé ───────────────────────────────────────────────────
            _painelRodape = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 56,
                BackColor = Color.FromArgb(235, 238, 245),
                Padding   = new Padding(14, 0, 14, 0)
            };

            // Período histórico
            AdicionarLabel(_painelRodape, "Período histórico (h):", 14, 18);
            _nudPeriodoHoras = AdicionarNud(_painelRodape, 1, 168, _periodoHoras, 56, 135, 15);

            // Tolerância
            AdicionarLabel(_painelRodape, "Tolerância (%):", 208, 18);
            _nudTolerancia   = AdicionarNud(_painelRodape, 100, 500, _toleranciaPerc, 68, 308, 15);

            // Intervalo de polling
            AdicionarLabel(_painelRodape, "Verificar a cada (seg):", 394, 18);
            _nudIntervaloSeg = AdicionarNud(_painelRodape, 10, 3600, _intervaloSeg, 68, 554, 15);

            // Botão Aplicar
            _btnSalvarConfig = new Button
            {
                Text      = "Aplicar",
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(20, 60, 120),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(80, 26),
                Location  = new Point(636, 15),
                Cursor    = Cursors.Hand
            };
            _btnSalvarConfig.FlatAppearance.BorderSize = 0;
            _btnSalvarConfig.Click += BtnSalvarConfig_Click;

            var lblDica = new Label
            {
                Text      = "Duplo clique → tolerância individual por meio",
                ForeColor = Color.Gray,
                Font      = new Font("Segoe UI", 8),
                AutoSize  = true,
                Location  = new Point(730, 20)
            };

            _painelRodape.Controls.AddRange(new Control[] { _btnSalvarConfig, lblDica });

            Controls.AddRange(new Control[] { _grid, _painelTopo, _painelRodape });
        }

        private static Label AdicionarLabel(Panel p, string texto, int x, int y)
        {
            var l = new Label { Text = texto, AutoSize = true, Location = new Point(x, y) };
            p.Controls.Add(l);
            return l;
        }

        private static NumericUpDown AdicionarNud(Panel p, int min, int max, int val, int w, int x, int y)
        {
            var n = new NumericUpDown { Minimum = min, Maximum = max, Value = val, Width = w, Location = new Point(x, y) };
            p.Controls.Add(n);
            return n;
        }

        private void PosicionarControlesTopo()
        {
            _btnAtualizar.Location = new Point(_painelTopo.Width - _btnAtualizar.Width - 14, 17);
            _lblProxima.Location   = new Point(_painelTopo.Width - _lblProxima.Width - 175, 44);
        }

        // ─────────────────────────────────────────────────────────────────
        //  POLLING
        // ─────────────────────────────────────────────────────────────────
        private void IniciarPolling()
        {
            _timerPolling          = new Timer();
            _timerPolling.Interval = _intervaloSeg * 1000;
            _timerPolling.Tick    += (s, e) => ExecutarVerificacao();
            _timerPolling.Start();

            _timerClock = new Timer { Interval = 1000 };
            _timerClock.Tick += (s, e) =>
            {
                if (_proximaVerificacao > DateTime.Now)
                {
                    var f = _proximaVerificacao - DateTime.Now;
                    _lblProxima.Text = $"Próxima verificação em {f.Minutes:D2}:{f.Seconds:D2}";
                }
            };
            _timerClock.Start();
        }

        // ─────────────────────────────────────────────────────────────────
        //  VERIFICAÇÃO PRINCIPAL
        // ─────────────────────────────────────────────────────────────────
        private void ExecutarVerificacao()
        {
            _btnAtualizar.Enabled = false;
            _lblStatus.Text       = "Consultando banco...";

            try
            {
                var meiosAtualizados = BancoService.ConsultarUltimasTransacoes(_periodoHoras);

                foreach (var novo in meiosAtualizados)
                {
                    var anterior = _meios.Find(m => m.Codigo == novo.Codigo);
                    if (anterior == null) continue;

                    // preserva tolerância individual configurada pelo operador
                    novo.ToleranciaIndividualPercent = anterior.ToleranciaIndividualPercent;

                    if (anterior.AnaliseAtiva)
                    {
                        novo.EmAnalise       = true;
                        novo.SuprimidoAte    = anterior.SuprimidoAte;
                        novo.AlertaDisparado = true;
                    }
                    else
                    {
                        // reseta alerta se o meio voltou a transacionar
                        novo.AlertaDisparado = anterior.AlertaDisparado
                            && novo.UltimaTransacao == anterior.UltimaTransacao;
                    }
                }

                // Calcula as métricas dinâmicas para cada meio
                foreach (var m in meiosAtualizados)
                    m.CalcularMetricas(_periodoHoras);

                _meios              = meiosAtualizados;
                _proximaVerificacao = DateTime.Now.AddSeconds(_intervaloSeg);

                AtualizarGrid();
                DispararAlertas();

                int criticos = _meios.FindAll(m => m.Status == StatusMeio.Critico).Count;
                _lblStatus.Text =
                    $"Última atualização: {DateTime.Now:HH:mm:ss}  |  " +
                    $"{_meios.Count} meio(s)  |  " +
                    (criticos > 0 ? $"⚠ {criticos} em alerta" : "Tudo normal");
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"Erro: {ex.Message}";
                MessageBox.Show($"Erro ao consultar o banco:\n\n{ex.Message}",
                    "Erro de Conexão", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnAtualizar.Enabled = true;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  GRID
        // ─────────────────────────────────────────────────────────────────
        private void AtualizarGrid()
        {
            _grid.Rows.Clear();

            // ordena: Crítico → Atenção → SemDados → Ok
            var ordenados = new List<MeioCaptura>(_meios);
            ordenados.Sort((a, b) =>
            {
                int prioA = PrioridadeStatus(a.Status);
                int prioB = PrioridadeStatus(b.Status);
                return prioA.CompareTo(prioB);
            });

            foreach (var m in ordenados)
            {
                string ultimaTx   = m.UltimaTransacao.HasValue
                    ? m.UltimaTransacao.Value.ToString("dd/MM/yyyy HH:mm:ss") : "—";
                string ocioso     = m.UltimaTransacao.HasValue
                    ? FormatarTempo(m.TempoOcioso) : "Sem registro";
                string media      = m.MediaIntervaloMinutos > 0
                    ? FormatarMinutos(m.MediaIntervaloMinutos) : "—";
                string percentual = m.PercentualMedia > 0
                    ? $"{m.PercentualMedia:F0}%" : "—";
                string txDesf     = m.TotalTransacoes > 0
                    ? $"{m.TotalTransacoes} / {m.TotalDesfazimentos}" : "—";

                string situacao;
                if (m.AnaliseAtiva)
                {
                    var r = m.TempoRestanteAnalise;
                    situacao = $"🔍 EM ANÁLISE ({r.Minutes:D2}:{r.Seconds:D2})";
                }
                else
                {
                    switch (m.Status)
                    {
                        case StatusMeio.Critico:  situacao = "⚠ CRÍTICO";        break;
                        case StatusMeio.Atencao:  situacao = "◉ ATENÇÃO";         break;
                        case StatusMeio.SemDados: situacao = "— Sem dados";       break;
                        default:                  situacao = "✔ Normal";           break;
                    }
                }

                // tolerância efetiva para exibir na coluna % da Média como tooltip
                int tolEfetiva = m.ToleranciaEfetivaPercent;

                int rowIdx = _grid.Rows.Add(
                    m.Codigo, m.Nome, ultimaTx, ocioso,
                    media, percentual, txDesf, situacao);

                var row = _grid.Rows[rowIdx];
                row.Tag = m.Codigo; // para recuperar o meio no duplo clique

                // tooltip na coluna % mostrando o limite efetivo
                row.Cells["colPercent"].ToolTipText =
                    $"Limite de alerta: {tolEfetiva}% da média" +
                    (m.ToleranciaIndividualPercent > 0 ? " (individual)" : " (global)");

                // cores
                if (m.AnaliseAtiva)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(60, 50, 10);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(220, 170, 30);
                    row.DefaultCellStyle.Font      = new Font("Segoe UI", 9, FontStyle.Italic);
                }
                else if (m.Status == StatusMeio.Critico)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 232, 232);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(160, 20, 20);
                    row.DefaultCellStyle.Font      = new Font("Segoe UI", 9, FontStyle.Bold);
                }
                else if (m.Status == StatusMeio.Atencao)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 248, 220);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(130, 90, 0);
                }
                else if (m.Status == StatusMeio.SemDados)
                {
                    row.DefaultCellStyle.ForeColor = Color.Gray;
                }
            }
        }

        private static int PrioridadeStatus(StatusMeio s)
        {
            switch (s)
            {
                case StatusMeio.Critico:  return 0;
                case StatusMeio.Atencao:  return 1;
                case StatusMeio.SemDados: return 2;
                default:                  return 3;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  ALERTAS
        // ─────────────────────────────────────────────────────────────────
        private void DispararAlertas()
        {
            foreach (var m in _meios)
            {
                if (m.Status != StatusMeio.Critico)    continue;
                if (m.AlertaDisparado)                 continue;
                if (m.AnaliseAtiva)                    continue;
                if (_popupsAtivos.Count >= MAX_POPUPS) break;
                if (_popupsAtivos.ContainsKey(m.Codigo)) continue;

                m.AlertaDisparado = true;
                int offsetY = _popupsAtivos.Count * 112;

                var meioLocal = m;
                var popup     = new FormAlerta(meioLocal, offsetY);

                popup.ConfirmadoClick += (s, e) =>
                {
                    meioLocal.AlertaDisparado = false;
                    meioLocal.EmAnalise       = false;
                    _popupsAtivos.Remove(meioLocal.Codigo);
                    AtualizarGrid();
                };

                popup.AnaliseClick += (s, e) =>
                {
                    meioLocal.EmAnalise    = true;
                    meioLocal.SuprimidoAte = DateTime.Now.AddMinutes(
                        Config.SupressaoAnaliseMinutos);
                    AtualizarGrid();

                    var timerReat = new Timer
                        { Interval = Config.SupressaoAnaliseMinutos * 60 * 1000 };
                    timerReat.Tick += (ts, te) =>
                    {
                        timerReat.Stop();
                        meioLocal.EmAnalise       = false;
                        meioLocal.AlertaDisparado = false;
                        _popupsAtivos.Remove(meioLocal.Codigo);
                        AtualizarGrid();
                        ExecutarVerificacao();
                    };
                    timerReat.Start();
                };

                popup.FormClosed += (s, e) =>
                    _popupsAtivos.Remove(meioLocal.Codigo);

                _popupsAtivos[m.Codigo] = popup;
                popup.Show();
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  DUPLO CLIQUE — tolerância individual por meio
        // ─────────────────────────────────────────────────────────────────
        private void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            // recupera o meio pelo código guardado em Tag
            string codigo = _grid.Rows[e.RowIndex].Tag as string;
            if (codigo == null) return;

            var meio = _meios.Find(m => m.Codigo == codigo);
            if (meio == null) return;

            using (var dlg = new FormConfigurarMeio(meio))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    meio.ToleranciaIndividualPercent = dlg.ToleranciaPercent;
                    meio.AlertaDisparado             = false;
                    meio.CalcularMetricas(_periodoHoras); // recalcula com nova tolerância
                    AtualizarGrid();
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  APLICAR CONFIGURAÇÕES GLOBAIS
        // ─────────────────────────────────────────────────────────────────
        private void BtnSalvarConfig_Click(object sender, EventArgs e)
        {
            _periodoHoras   = (int)_nudPeriodoHoras.Value;
            _toleranciaPerc = (int)_nudTolerancia.Value;
            _intervaloSeg   = (int)_nudIntervaloSeg.Value;

            _timerPolling.Interval = _intervaloSeg * 1000;

            // reseta alertas e recalcula com novos parâmetros
            foreach (var m in _meios)
            {
                if (m.ToleranciaIndividualPercent == 0) // só os que usam global
                    m.AlertaDisparado = false;
            }

            // força verificação imediata com novo período
            ExecutarVerificacao();

            MessageBox.Show(
                $"Configurações aplicadas!\n\n" +
                $"Período: {_periodoHoras}h  |  " +
                $"Tolerância: {_toleranciaPerc}%  |  " +
                $"Polling: {_intervaloSeg}s",
                "Monitor TEF", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ─────────────────────────────────────────────────────────────────
        //  UTILITÁRIOS
        // ─────────────────────────────────────────────────────────────────
        private static string FormatarTempo(TimeSpan ts)
        {
            if (ts == TimeSpan.MaxValue) return "—";
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes:D2}min";
            return $"{ts.Minutes}min {ts.Seconds:D2}s";
        }

        private static string FormatarMinutos(double minutos)
        {
            if (minutos <= 0) return "—";
            if (minutos >= 60)
                return $"{(int)(minutos / 60)}h {(int)(minutos % 60):D2}min";
            if (minutos >= 1)
                return $"{minutos:F1}min";
            return $"{(int)(minutos * 60)}s";
        }
    }
}
