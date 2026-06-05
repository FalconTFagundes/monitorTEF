using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MonitorTEF
{
    public class FormPrincipal : Form
    {
        // ── controles principais ──────────────────────────────────────────
        private Panel       _painelTopo;
        private Label       _lblTitulo;
        private Label       _lblStatus;
        private Label       _lblProxima;
        private Button      _btnAtualizar;
        private DataGridView _grid;
        private Panel       _painelRodape;
        private Label       _lblAlertaConfig;
        private NumericUpDown _nudAlertaMinutos;
        private Label       _lblIntervaloConfig;
        private NumericUpDown _nudIntervaloSegundos;
        private Button      _btnSalvarConfig;

        // ── timer de polling ──────────────────────────────────────────────
        private Timer _timerPolling;
        private DateTime _proximaVerificacao;

        // ── estado ────────────────────────────────────────────────────────
        private List<MeioCaptura> _meios = new List<MeioCaptura>();
        private int _alertaMinutos      = Config.TempoAlertaPadraoMinutos;
        private int _intervaloSegundos  = Config.IntervaloVerificacaoSegundos;

        // ── alerta de empilhamento (máx 4 popups simultâneos) ────────────
        private int _popupsAtivos = 0;
        private const int MAX_POPUPS = 4;

        public FormPrincipal()
        {
            ConstruirInterface();
            IniciarPolling();
            ExecutarVerificacao(); // verifica imediatamente ao abrir
        }

        // ─────────────────────────────────────────────────────────────────
        //  INTERFACE
        // ─────────────────────────────────────────────────────────────────
        private void ConstruirInterface()
        {
            Text            = "Monitor TEF — BigCard";
            Size            = new Size(780, 560);
            MinimumSize     = new Size(640, 460);
            StartPosition   = FormStartPosition.CenterScreen;
            Font            = new Font("Segoe UI", 9);
            BackColor       = Color.FromArgb(245, 245, 245);

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
                Size      = new Size(140, 30),
                Anchor    = AnchorStyles.Top | AnchorStyles.Right,
                Cursor    = Cursors.Hand
            };
            _btnAtualizar.FlatAppearance.BorderColor = Color.FromArgb(80, 140, 230);
            _btnAtualizar.Click += (s, e) => ExecutarVerificacao();

            _painelTopo.Controls.AddRange(new Control[] { _lblTitulo, _lblStatus, _lblProxima, _btnAtualizar });
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
            _grid.ColumnHeadersDefaultCellStyle.BackColor   = Color.FromArgb(20, 60, 120);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor   = Color.White;
            _grid.ColumnHeadersDefaultCellStyle.Font        = new Font("Segoe UI", 9, FontStyle.Bold);
            _grid.ColumnHeadersDefaultCellStyle.Padding     = new Padding(6, 0, 0, 0);
            _grid.EnableHeadersVisualStyles               = false;
            _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 255);
            _grid.DefaultCellStyle.Padding                  = new Padding(6, 4, 6, 4);
            _grid.RowTemplate.Height                        = 32;

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCodigo",    HeaderText = "Cód.",              FillWeight = 6  });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colMeio",      HeaderText = "Meio de Captura",   FillWeight = 25 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUltima",    HeaderText = "Última Transação",  FillWeight = 20 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOcioso",    HeaderText = "Tempo Ocioso",      FillWeight = 15 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colLimite",    HeaderText = "Limite Alerta",     FillWeight = 13 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSituacao",  HeaderText = "Situação",          FillWeight = 15 });

            // alerta personalizado por meio via duplo clique
            _grid.CellDoubleClick += Grid_CellDoubleClick;

            // ── rodapé de configuração ────────────────────────────────────
            _painelRodape = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 54,
                BackColor = Color.FromArgb(235, 238, 245),
                Padding   = new Padding(14, 0, 14, 0)
            };

            _lblAlertaConfig = new Label
            {
                Text     = "Alerta padrão (min):",
                AutoSize = true,
                Location = new Point(14, 18)
            };

            _nudAlertaMinutos = new NumericUpDown
            {
                Minimum  = 1,
                Maximum  = 1440,
                Value    = _alertaMinutos,
                Width    = 64,
                Location = new Point(134, 15)
            };

            _lblIntervaloConfig = new Label
            {
                Text     = "Verificar a cada (seg):",
                AutoSize = true,
                Location = new Point(218, 18)
            };

            _nudIntervaloSegundos = new NumericUpDown
            {
                Minimum  = 10,
                Maximum  = 3600,
                Value    = _intervaloSegundos,
                Width    = 70,
                Location = new Point(358, 15)
            };

            _btnSalvarConfig = new Button
            {
                Text      = "Aplicar",
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(20, 60, 120),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(80, 26),
                Location  = new Point(440, 13),
                Cursor    = Cursors.Hand
            };
            _btnSalvarConfig.FlatAppearance.BorderSize = 0;
            _btnSalvarConfig.Click += BtnSalvarConfig_Click;

            var lblDica = new Label
            {
                Text      = "Dica: duplo clique em um meio para definir limite individual.",
                ForeColor = Color.Gray,
                Font      = new Font("Segoe UI", 8),
                AutoSize  = true,
                Location  = new Point(540, 19)
            };

            _painelRodape.Controls.AddRange(new Control[]
            {
                _lblAlertaConfig, _nudAlertaMinutos,
                _lblIntervaloConfig, _nudIntervaloSegundos,
                _btnSalvarConfig, lblDica
            });

            // ── montar form ───────────────────────────────────────────────
            Controls.AddRange(new Control[] { _grid, _painelTopo, _painelRodape });
        }

        private void PosicionarControlesTopo()
        {
            _btnAtualizar.Location = new Point(_painelTopo.Width - _btnAtualizar.Width - 14, 17);
            _lblProxima.Location   = new Point(_painelTopo.Width - _lblProxima.Width - 170, 44);
        }

        // ─────────────────────────────────────────────────────────────────
        //  POLLING
        // ─────────────────────────────────────────────────────────────────
        private void IniciarPolling()
        {
            _timerPolling          = new Timer();
            _timerPolling.Interval = _intervaloSegundos * 1000;
            _timerPolling.Tick    += (s, e) => ExecutarVerificacao();
            _timerPolling.Start();

            // timer de 1s para atualizar contador de próxima verificação
            var timerClock = new Timer { Interval = 1000 };
            timerClock.Tick += (s, e) =>
            {
                if (_proximaVerificacao > DateTime.Now)
                {
                    var falta = _proximaVerificacao - DateTime.Now;
                    _lblProxima.Text = $"Próxima verificação em {falta.Minutes:D2}:{falta.Seconds:D2}";
                }
            };
            timerClock.Start();
        }

        private void ExecutarVerificacao()
        {
            _btnAtualizar.Enabled = false;
            _lblStatus.Text       = "Consultando banco...";

            try
            {
                var meiosAtualizados = BancoService.ConsultarUltimasTransacoes();

                // preservar alertas já disparados e limites individuais
                foreach (var novo in meiosAtualizados)
                {
                    var anterior = _meios.Find(m => m.Codigo == novo.Codigo);
                    if (anterior != null)
                    {
                        novo.TempoAlertaMinutos = anterior.TempoAlertaMinutos;
                        // reseta alerta se voltou a transacionar
                        novo.AlertaDisparado = anterior.AlertaDisparado
                            && novo.UltimaTransacao == anterior.UltimaTransacao;
                    }
                }

                _meios = meiosAtualizados;
                _proximaVerificacao = DateTime.Now.AddSeconds(_intervaloSegundos);

                AtualizarGrid();
                DispararAlertas();

                _lblStatus.Text = $"Última atualização: {DateTime.Now:HH:mm:ss}  |  {_meios.Count} meio(s) monitorado(s)";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"Erro na consulta: {ex.Message}";
                MessageBox.Show($"Não foi possível consultar o banco de dados:\n\n{ex.Message}",
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

            foreach (var m in _meios)
            {
                string tempoOcioso = m.UltimaTransacao.HasValue
                    ? FormatarTempo(m.TempoOcioso)
                    : "Sem registro";

                string ultimaTx = m.UltimaTransacao.HasValue
                    ? m.UltimaTransacao.Value.ToString("dd/MM/yyyy HH:mm:ss")
                    : "—";

                bool emAlerta = m.UltimaTransacao.HasValue
                    && m.TempoOcioso.TotalMinutes >= m.LimiteEfetivoMinutos;

                string situacao = !m.UltimaTransacao.HasValue  ? "Sem dados"
                                : emAlerta                     ? "⚠ EM ALERTA"
                                                               : "✔ Normal";

                int rowIdx = _grid.Rows.Add(
                    m.Codigo,
                    m.Nome,
                    ultimaTx,
                    tempoOcioso,
                    $"{m.LimiteEfetivoMinutos} min",
                    situacao
                );

                var row = _grid.Rows[rowIdx];

                if (emAlerta)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 235, 235);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(180, 30, 30);
                    row.DefaultCellStyle.Font      = new Font("Segoe UI", 9, FontStyle.Bold);
                }
                else if (!m.UltimaTransacao.HasValue)
                {
                    row.DefaultCellStyle.ForeColor = Color.Gray;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  ALERTAS
        // ─────────────────────────────────────────────────────────────────
        private void DispararAlertas()
        {
            foreach (var m in _meios)
            {
                if (!m.UltimaTransacao.HasValue) continue;
                if (m.AlertaDisparado) continue;
                if (m.TempoOcioso.TotalMinutes < m.LimiteEfetivoMinutos) continue;
                if (_popupsAtivos >= MAX_POPUPS) break;

                m.AlertaDisparado = true;
                _popupsAtivos++;

                // offset para empilhar popups
                int offset = (_popupsAtivos - 1) * 108;

                var popup = new FormAlerta(m.Nome, m.TempoOcioso);

                // sobe cada popup acima do anterior
                var area = Screen.PrimaryScreen.WorkingArea;
                popup.Location = new Point(
                    popup.Location.X,
                    popup.Location.Y - offset
                );

                popup.FormClosed += (s, e) => _popupsAtivos = Math.Max(0, _popupsAtivos - 1);
                popup.Show();
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  CONFIGURAÇÃO INDIVIDUAL POR MEIO (duplo clique)
        // ─────────────────────────────────────────────────────────────────
        private void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _meios.Count) return;
            var meio = _meios[e.RowIndex];

            using (var dlg = new FormConfigurarMeio(meio))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    meio.TempoAlertaMinutos = dlg.TempoAlertaMinutos;
                    meio.AlertaDisparado    = false; // reseta para poder alertar com novo limite
                    AtualizarGrid();
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  APLICAR CONFIGURAÇÕES GLOBAIS
        // ─────────────────────────────────────────────────────────────────
        private void BtnSalvarConfig_Click(object sender, EventArgs e)
        {
            _alertaMinutos     = (int)_nudAlertaMinutos.Value;
            _intervaloSegundos = (int)_nudIntervaloSegundos.Value;

            _timerPolling.Interval = _intervaloSegundos * 1000;

            // reseta alertas para re-avaliar com novo limite
            foreach (var m in _meios)
            {
                if (m.TempoAlertaMinutos == 0)   // só os que usam o padrão
                    m.AlertaDisparado = false;
            }

            AtualizarGrid();
            MessageBox.Show("Configurações aplicadas!", "Monitor TEF",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
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
    }
}
