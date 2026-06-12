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

        // ── timers ────────────────────────────────────────────────────────
        private Timer    _timerPolling;
        private Timer    _timerClock;
        private DateTime _proximaVerificacao;

        // ── estado ────────────────────────────────────────────────────────
        private List<MeioCaptura> _meios          = new List<MeioCaptura>();
        private int _periodoHoras    = Config.PeriodoHistoricoHoras;
        private int _toleranciaPerc  = Config.ToleranciaGlobalPercent;
        private int _intervaloSeg    = Config.IntervaloVerificacaoSegundos;

        // usuário logado
        private string _nomeOperador     = "";
        private string _primeiroNome     = "";
        private string _codigoOperador   = "";

        // ── popups ativos por código de meio ──────────────────────────────
        // slots 0..MAX_POPUPS-1: cada slot ocupa uma posição fixa na pilha
        // PopupsAtivos: codigo → popup; Slots: slot → codigo (null = livre)
        private readonly Dictionary<string, FormAlerta> _popupsAtivos =
            new Dictionary<string, FormAlerta>();
        private readonly string[] _slots = new string[4]; // MAX_POPUPS = 4
        private const int MAX_POPUPS = 4;

        // meios que sumiram automaticamente e aguardam reexibição em 5s
        private readonly Dictionary<string, Timer> _reexibicaoPendente =
            new Dictionary<string, Timer>();

        private int ObterSlotLivre()
        {
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] == null) return i;
            return -1;
        }

        private void LiberarSlot(string codigo)
        {
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] == codigo) { _slots[i] = null; break; }
            // reposicionar popups restantes para preencher lacunas
            ReempilharPopups();
        }

        private void ReempilharPopups()
        {
            // compacta slots: sem lacunas, do 0 para cima
            int prox = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null) continue;
                if (i != prox)
                {
                    _slots[prox] = _slots[i];
                    _slots[i]    = null;
                    if (_popupsAtivos.TryGetValue(_slots[prox], out var p))
                    {
                        p.SlotIndex = prox;
                        p.AtualizarPosicao();
                    }
                }
                prox++;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        public FormPrincipal(string nomeCompleto, string primeiroNome, string codigo)
        {
            _nomeOperador   = nomeCompleto;
            _primeiroNome   = primeiroNome;
            _codigoOperador = codigo;

            // propaga para o LogService — todos os logs usam o nome logado
            LogService.OperadorAtual = nomeCompleto;

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

            // nome do operador logado (canto direito do topo)
            var lblOperador = new Label
            {
                Text      = $"● {_primeiroNome}",
                Font      = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = Color.FromArgb(120, 200, 120),
                AutoSize  = true,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right,
                Location  = new Point(10, 14),
                BackColor = Color.Transparent
            };
            // posição final definida após resize
            _painelTopo.Controls.Add(lblOperador);
            _painelTopo.Resize += (s, e) =>
            {
                lblOperador.Location = new Point(
                    _painelTopo.Width - lblOperador.Width - 170, 14);
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

            // ── rodapé de status (somente leitura — config vem do servidor) ──
            _painelRodape = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 34,
                BackColor = Color.FromArgb(235, 238, 245),
                Padding   = new Padding(14, 0, 14, 0)
            };

            var lblDica = new Label
            {
                Text      = $"Duplo clique → tolerância individual por meio  |  {Config.Servidor}/{Config.Banco}",
                ForeColor = Color.Gray,
                Font      = new Font("Segoe UI", 8),
                AutoSize  = true,
                Location  = new Point(14, 10)
            };

            _painelRodape.Controls.Add(lblDica);
            Controls.AddRange(new Control[] { _grid, _painelTopo, _painelRodape });
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

                // ── Calcula métricas dinâmicas para cada meio ─────────────────────
                foreach (var m in meiosAtualizados)
                    m.CalcularMetricas(_periodoHoras);

                // ── Merge com o estado anterior ──────────────────────────────────
                // Regra central: AlertaDisparado só é FALSE quando o meio
                // ENTRA em Crítico pela primeira vez (UltimaTransacao mudou
                // para pior, ou meio era Normal/Atenção e virou Crítico).
                // Se já estava Crítico com a mesma UltimaTransacao → mesmo
                // evento, não dispara de novo.
                foreach (var novo in meiosAtualizados)
                {
                    var anterior = _meios.Find(m => m.Codigo == novo.Codigo);

                    if (anterior == null)
                    {
                        // Meio novo (primeira execução ou novo tipo no banco).
                        // Se já está crítico na primeira leitura, marca como
                        // já disparado — não mostra popup para estado pré-existente.
                        if (novo.Status == StatusMeio.Critico)
                            novo.AlertaDisparado = true;
                        continue;
                    }

                    // Preserva configuração individual do operador
                    novo.ToleranciaIndividualPercent = anterior.ToleranciaIndividualPercent;

                    // Preserva estado de análise
                    if (anterior.AnaliseAtiva)
                    {
                        novo.EmAnalise       = true;
                        novo.SuprimidoAte    = anterior.SuprimidoAte;
                        novo.AlertaDisparado = true;
                        continue;
                    }

                    if (novo.Status == StatusMeio.Critico)
                    {
                        bool mesmaUltimaTx = novo.UltimaTransacao == anterior.UltimaTransacao;
                        bool jaEraCritico  = anterior.Status == StatusMeio.Critico;

                        if (jaEraCritico && mesmaUltimaTx)
                        {
                            // MESMO evento, MESMO status, MESMA última tx
                            // → não é novidade, não dispara popup
                            novo.AlertaDisparado = anterior.AlertaDisparado;

                            // Cancela reexibição pendente se o popup ainda estava
                            // aguardando os 5s (situação não mudou, sem necessidade)
                            // — deixa correr, o handler já verifica Status na hora
                        }
                        else if (jaEraCritico && !mesmaUltimaTx)
                        {
                            // O meio transacionou e voltou a ficar crítico
                            // → nova ocorrência, DISPARA novo alerta
                            // Cancela qualquer reexibição pendente para não duplicar
                            CancelarReexibicao(novo.Codigo);
                            novo.AlertaDisparado = false;
                        }
                        else
                        {
                            // Mudou de Normal/Atenção/SemDados para Crítico
                            // → DISPARA alerta
                            CancelarReexibicao(novo.Codigo);
                            novo.AlertaDisparado = false;
                        }
                    }
                    else
                    {
                        // Saiu do crítico → reseta e cancela pendências
                        CancelarReexibicao(novo.Codigo);
                        novo.AlertaDisparado = false;
                    }
                }

                // RC já está na lista retornada pelo BancoService

                _meios              = meiosAtualizados;
                _proximaVerificacao = DateTime.Now.AddSeconds(_intervaloSeg);

                AtualizarGrid();
                DispararAlertas();

                int criticos = _meios.FindAll(m => m.Status == StatusMeio.Critico).Count;
                _lblStatus.Text =
                    $"Última atualização: {DateTime.Now:HH:mm:ss}  |  " +
                    $"{_meios.Count} meio(s)  |  " +
                    $"Período: {_periodoHoras}h  |  Tolerância: {_toleranciaPerc}%  |  " +
                    (criticos > 0 ? $"⚠ {criticos} em alerta" : "✔ Tudo normal");
            }
            catch (System.Data.SqlClient.SqlException ex)
            {
                _lblStatus.Text = $"⚠ Erro SQL: {ex.Message}";
                MessageBox.Show($"Erro ao consultar o banco de dados:\n\n{ex.Message}",
                    "Erro de Conexão", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"Erro: {ex.Message}";
                MessageBox.Show($"Erro inesperado:\n\n{ex.Message}",
                    "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        private void CancelarReexibicao(string codigo)
        {
            if (_reexibicaoPendente.TryGetValue(codigo, out var t))
            {
                t.Stop();
                _reexibicaoPendente.Remove(codigo);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  MEIO SINTÉTICO: REDECOMPRAS
        //  Agrega todos os meios com [ RC ] no nome.
        //  Regra de alerta:
        //    - Se TODOS os meios RC estiverem sem transacionar (Crítico ou SemDados)
        //      → RC fica Crítico (problema no grupo inteiro)
        //    - Se pelo menos um estiver OK ou Atenção
        //      → RC fica OK (algum canal RC ainda está respondendo)
        // ─────────────────────────────────────────────────────────────────
        private MeioCaptura CriarMeioRedeCompras(List<MeioCaptura> meios)
        {
            var membros = meios.FindAll(m => m.Nome.Contains("[ RC ]"));
            if (membros.Count == 0) return null;

            // preserva estado anterior do RC sintético
            var anterior = _meios.Find(m => m.Codigo == "RC");

            // última transação = a mais recente entre todos os membros RC
            DateTime? ultimaRC = null;
            foreach (var mb in membros)
            {
                if (!mb.UltimaTransacao.HasValue) continue;
                if (ultimaRC == null || mb.UltimaTransacao.Value > ultimaRC.Value)
                    ultimaRC = mb.UltimaTransacao;
            }

            // total de transações e desfazimentos somados
            int totalTx   = 0;
            int totalDesf = 0;
            foreach (var mb in membros) { totalTx += mb.TotalTransacoes; totalDesf += mb.TotalDesfazimentos; }

            // status: Crítico só se TODOS estiverem críticos ou sem dados
            bool todosProblema = membros.TrueForAll(
                mb => mb.Status == StatusMeio.Critico || mb.Status == StatusMeio.SemDados);

            var rc = new MeioCaptura
            {
                Codigo             = "RC",
                Nome               = "REDECOMPRAS",
                UltimaTransacao    = ultimaRC,
                TotalTransacoes    = totalTx,
                TotalDesfazimentos = totalDesf,
            };

            // métricas calculadas localmente para o RC sintético do FormPrincipal
            // (backup — RC calculado pelo FormPrincipal caso não venha do banco)
            double periodoMin = _periodoHoras * 60.0;
            double mediaRcLocal = totalTx > 0 ? periodoMin / totalTx : 0;
            double tempoRcLocal = rc.UltimaTransacao.HasValue
                ? (DateTime.Now - rc.UltimaTransacao.Value).TotalMinutes : 0;
            double pctRcLocal   = mediaRcLocal > 0 ? tempoRcLocal / mediaRcLocal * 100 : 0;
            rc.DefinirMetricasExternas(mediaRcLocal, tempoRcLocal, pctRcLocal);

            // sobrescreve status pela regra de negócio RC
            if (todosProblema)
            {
                // força Crítico via propriedade pública (CalcularMetricas pode ter dito OK
                // se a última tx agregada for recente)
                // — usamos o campo público StatusForcado para isso
                rc.ForcarStatus(StatusMeio.Critico);
            }
            else
            {
                rc.ForcarStatus(StatusMeio.Ok);
            }

            // preserva AlertaDisparado e estado de análise
            if (anterior != null)
            {
                rc.ToleranciaIndividualPercent = anterior.ToleranciaIndividualPercent;
                if (anterior.AnaliseAtiva)
                {
                    rc.EmAnalise    = true;
                    rc.SuprimidoAte = anterior.SuprimidoAte;
                    rc.AlertaDisparado = true;
                }
                else
                {
                    rc.AlertaDisparado = anterior.AlertaDisparado;
                    if (rc.Status != StatusMeio.Critico && !rc.AnaliseAtiva)
                        rc.AlertaDisparado = false;
                }
            }

            return rc;
        }

        private void DispararAlertas()
        {
            foreach (var m in _meios)
            {
                if (m.Status != StatusMeio.Critico)      continue;
                if (m.AlertaDisparado)                   continue;
                if (m.AnaliseAtiva)                      continue;
                if (_popupsAtivos.ContainsKey(m.Codigo)) continue;

                int slot = ObterSlotLivre();
                if (slot < 0) break; // todos os slots ocupados

                m.AlertaDisparado = true;
                _slots[slot]      = m.Codigo;

                var meioLocal = m;
                var popup     = new FormAlerta(meioLocal, slot);

                popup.ConfirmadoClick += (s, e) =>
                {
                    meioLocal.AlertaDisparado = false;
                    meioLocal.EmAnalise       = false;
                    _popupsAtivos.Remove(meioLocal.Codigo);
                    LiberarSlot(meioLocal.Codigo);
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
                        LiberarSlot(meioLocal.Codigo);
                        AtualizarGrid();
                        ExecutarVerificacao();
                    };
                    timerReat.Start();
                };

                popup.SumidoAutomaticamente += (s, e) =>
                {
                    // sumiu sem clique → reagenda para reexibir em 5s
                    // AlertaDisparado permanece true; Timer reseta e dispara Show novamente
                    var timerReex = new Timer { Interval = 5000 };
                    _reexibicaoPendente[meioLocal.Codigo] = timerReex;
                    timerReex.Tick += (ts, te) =>
                    {
                        timerReex.Stop();
                        _reexibicaoPendente.Remove(meioLocal.Codigo);
                        // só reexibe se o meio ainda estiver crítico e sem análise
                        var mAtual = _meios.Find(x => x.Codigo == meioLocal.Codigo);
                        if (mAtual != null
                            && mAtual.Status == StatusMeio.Critico
                            && !mAtual.AnaliseAtiva
                            && !_popupsAtivos.ContainsKey(meioLocal.Codigo))
                        {
                            mAtual.AlertaDisparado = false; // permite redisparar
                            DispararAlertas();
                        }
                    };
                    timerReex.Start();
                };

                popup.FormClosed += (s, e) =>
                {
                    _popupsAtivos.Remove(meioLocal.Codigo);
                    LiberarSlot(meioLocal.Codigo);
                };

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
                    // Recalcula só o status com base nas métricas já existentes
                    // (as métricas de média/tempo não mudam, só a tolerância mudou)
                    if (meio.PercentualMedia > meio.ToleranciaEfetivaPercent)
                        meio.ForcarStatus(StatusMeio.Critico);
                    else if (meio.PercentualMedia > 100)
                        meio.ForcarStatus(StatusMeio.Atencao);
                    else
                        meio.ForcarStatus(StatusMeio.Ok);
                    AtualizarGrid();
                }
            }
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
