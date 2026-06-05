using System;
using System.Collections.Generic;

namespace MonitorTEF
{
    public class MeioCaptura
    {
        public string Codigo { get; set; }
        public string Nome { get; set; }
        public DateTime? UltimaTransacao { get; set; }

        public TimeSpan TempoOcioso =>
            UltimaTransacao.HasValue
                ? DateTime.Now - UltimaTransacao.Value
                : TimeSpan.MaxValue;

        public bool AlertaDisparado { get; set; } = false;

        // Tempo de alerta individual por meio (usa o padrão se for 0)
        public int TempoAlertaMinutos { get; set; } = 0;

        public int LimiteEfetivoMinutos =>
            TempoAlertaMinutos > 0 ? TempoAlertaMinutos : Config.TempoAlertaPadraoMinutos;
    }

    public static class MeiosConhecidos
    {
        public static readonly Dictionary<string, string> Mapa = new Dictionary<string, string>
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
            return Mapa.TryGetValue(codigo.ToUpper(), out var nome) ? nome : codigo.ToUpper();
        }
    }
}
