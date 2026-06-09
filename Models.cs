using System;
using System.Collections.Generic;

namespace MonitorTEF
{
    public class MeioCaptura
    {
        public string    Codigo           { get; set; }
        public string    Nome             { get; set; }
        public DateTime? UltimaTransacao  { get; set; }

        public TimeSpan TempoOcioso =>
            UltimaTransacao.HasValue
                ? DateTime.Now - UltimaTransacao.Value
                : TimeSpan.MaxValue;

        public bool AlertaDisparado { get; set; } = false;

        // Tempo de alerta individual por meio (0 = usa o padrão global)
        public int TempoAlertaMinutos { get; set; } = 0;

        public int LimiteEfetivoMinutos =>
            TempoAlertaMinutos > 0 ? TempoAlertaMinutos : Config.TempoAlertaPadraoMinutos;

        // ── Estado de análise ──────────────────────────────────────────
        // Quando o operador clica "Em Análise", o popup muda de estado
        // e só reativa o alerta após SuprimidoAte passar.
        public bool     EmAnalise    { get; set; } = false;
        public DateTime SuprimidoAte { get; set; } = DateTime.MinValue;

        public bool AnaliseAtiva => EmAnalise && DateTime.Now < SuprimidoAte;

        public TimeSpan TempoRestanteAnalise =>
            AnaliseAtiva ? SuprimidoAte - DateTime.Now : TimeSpan.Zero;
    }

    public static class MeiosConhecidos
    {
        public static readonly Dictionary<string, string> Mapa =
            new Dictionary<string, string>
        {
            { "E", "CIELO"               },
            { "I", "AUTORIZADOR"         },
            { "V", "BANCO 24H"           },
            { "R", "REDE"                },
            { "A", "CENTRAL ATENDIMENTO" },
            { "S", "SAFRAPAY"            },
            { "Z", "FEPAS"               },
            { "O", "ELO"                 },
            { "F", "SIPAG"               },
            { "P", "SIPAG NOVA"          },
        };

        public static string NomeOuCodigo(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return "DESCONHECIDO";
            return Mapa.TryGetValue(codigo.ToUpper(), out var nome)
                ? nome : codigo.ToUpper();
        }
    }
}
