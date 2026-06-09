using System;
using System.Collections.Generic;

namespace MonitorTEF
{
    // ═══════════════════════════════════════════════════════════════════
    //  MeioCaptura
    //  O limite de alerta NÃO é fixo em minutos.
    //  É calculado dinamicamente assim (igual ao monitor HTML):
    //
    //    mediaIntervalo (min) = periodoMinutos / qtdTransacoes
    //    percentual           = tempoDesdeUltima / mediaIntervalo * 100
    //    alerta               = percentual > ToleranciaPercent
    //
    //  Isso faz com que cada meio tenha seu próprio "ritmo":
    //  um meio que transaciona a cada 1 min é alertado em ~1.5 min,
    //  um meio que transaciona a cada 40 min é alertado em ~60 min.
    // ═══════════════════════════════════════════════════════════════════
    public class MeioCaptura
    {
        // ── Identificação ─────────────────────────────────────────────
        public string    Codigo  { get; set; }
        public string    Nome    { get; set; }

        // ── Dados brutos do banco ─────────────────────────────────────
        public DateTime? UltimaTransacao   { get; set; }
        public int       TotalTransacoes   { get; set; }   // no período configurado
        public int       TotalDesfazimentos{ get; set; }   // operação 0420 no período

        // ── Métricas calculadas (preenchidas por CalcularMetricas) ─────
        public double MediaIntervaloMinutos { get; private set; }  // período / total
        public double TempoOciosoMinutos    { get; private set; }  // agora - última tx
        public double PercentualMedia       { get; private set; }  // ocioso / média * 100
        public StatusMeio Status            { get; private set; }  = StatusMeio.SemDados;

        // ── Comportamento de alerta ───────────────────────────────────
        public bool     AlertaDisparado { get; set; } = false;
        public bool     EmAnalise       { get; set; } = false;
        public DateTime SuprimidoAte    { get; set; } = DateTime.MinValue;

        public bool     AnaliseAtiva         => EmAnalise && DateTime.Now < SuprimidoAte;
        public TimeSpan TempoRestanteAnalise => AnaliseAtiva
            ? SuprimidoAte - DateTime.Now : TimeSpan.Zero;

        // ── Limite individual opcional (0 = usa tolerância global) ────
        // Continua existindo para casos onde o operador quer forçar
        // um limite mínimo independente do histórico.
        // Se > 0, é usado como ToleranciaPercent para esse meio.
        public int ToleranciaIndividualPercent { get; set; } = 0;

        public int ToleranciaEfetivaPercent =>
            ToleranciaIndividualPercent > 0
                ? ToleranciaIndividualPercent
                : Config.ToleranciaGlobalPercent;

        // ── Cálculo dinâmico ──────────────────────────────────────────
        /// <summary>
        /// Recalcula todas as métricas com base nos dados atuais.
        /// Deve ser chamado após atualizar UltimaTransacao e TotalTransacoes.
        /// </summary>
        public void CalcularMetricas(int periodoHoras)
        {
            var agora = DateTime.Now;

            if (!UltimaTransacao.HasValue || TotalTransacoes == 0)
            {
                MediaIntervaloMinutos = 0;
                TempoOciosoMinutos    = 0;
                PercentualMedia       = 0;
                Status                = StatusMeio.SemDados;
                return;
            }

            double periodoMinutos  = periodoHoras * 60.0;
            MediaIntervaloMinutos  = periodoMinutos / TotalTransacoes;
            TempoOciosoMinutos     = (agora - UltimaTransacao.Value).TotalMinutes;

            if (MediaIntervaloMinutos > 0)
            {
                PercentualMedia = (TempoOciosoMinutos / MediaIntervaloMinutos) * 100.0;

                if (PercentualMedia > ToleranciaEfetivaPercent)
                    Status = StatusMeio.Critico;
                else if (PercentualMedia > 100)
                    Status = StatusMeio.Atencao;
                else
                    Status = StatusMeio.Ok;
            }
            else if (TotalTransacoes == 1)
            {
                PercentualMedia = 50;
                Status          = StatusMeio.Atencao;
            }
            else
            {
                Status = StatusMeio.Ok;
            }
        }

        // Compatibilidade com código que usava TempoOcioso como TimeSpan
        public TimeSpan TempoOcioso =>
            UltimaTransacao.HasValue
                ? DateTime.Now - UltimaTransacao.Value
                : TimeSpan.MaxValue;
    }

    // ─────────────────────────────────────────────────────────────────
    public enum StatusMeio { Ok, Atencao, Critico, SemDados }

    // ─────────────────────────────────────────────────────────────────
    public static class MeiosConhecidos
    {
        public static readonly Dictionary<string, string> Mapa =
            new Dictionary<string, string>
        {
            { "A", "Central de Atendimento" },
            { "B", "App Celular"            },
            { "C", "BigCash"                },
            { "E", "Cielo - [ RC ]"         },
            { "F", "Sipag - [ RC ]"         },
            { "I", "Autorizador"            },
            { "L", "Logpay"                 },
            { "O", "Elo"                    },
            { "R", "Rede - [ RC ]"          },
            { "S", "SafraPay - [ RC ]"      },
            { "V", "Banco 24H"              },
            { "W", "Web Site"               },
            { "Z", "Sitef / FEPAS"          },
            { "P", "Sipag Nova - [ RC ]"    },
            { "H", "HG Pay"                 },
        };

        public static string NomeOuCodigo(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return "DESCONHECIDO";
            return Mapa.TryGetValue(codigo.ToUpper(), out var nome)
                ? nome : codigo.ToUpper();
        }
    }
}
