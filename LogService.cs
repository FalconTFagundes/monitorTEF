using System;
using System.IO;
using System.Text;

namespace MonitorTEF
{
    internal static class LogService
    {
        private static readonly object _lock = new object();

        /// <summary>
        /// Nome do operador logado. Definido pelo FormPrincipal após login.
        /// Usado nos logs em vez do Environment.UserName.
        /// </summary>
        public static string OperadorAtual { get; set; } = Environment.UserName;

        private static string CaminhoCompleto =>
            Path.Combine(Config.PastaLogRede, Config.NomeArquivoLog);

        private static readonly string[] Cabecalho =
        {
            "Timestamp", "Operador", "Maquina", "Codigo", "Meio", "Acao",
            "TempoOcioso", "PercentualMedia", "ToleranciaPercent", "Observacao"
        };

        public static void Registrar(
            string       codigoMeio,
            string       nomeMeio,
            AcaoOperador acao,
            TimeSpan     tempoOcioso,
            int          toleranciaPercent,
            string       obs = "")
        {
            try
            {
                lock (_lock)
                {
                    bool novo = !File.Exists(CaminhoCompleto);
                    using (var sw = new StreamWriter(CaminhoCompleto,
                           append: true, encoding: new UTF8Encoding(true)))
                    {
                        if (novo) sw.WriteLine(string.Join(";", Cabecalho));

                        sw.WriteLine(string.Join(";",
                            DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                            OperadorAtual,
                            Environment.MachineName,
                            codigoMeio,
                            Escapar(nomeMeio),
                            acao.ToString(),
                            FormatarTempo(tempoOcioso),
                            toleranciaPercent.ToString() + "%",
                            Escapar(obs)
                        ));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LogService] {ex.Message}");
            }
        }

        private static string Escapar(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains(";") || s.Contains("\n") || s.Contains("\""))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        private static string FormatarTempo(TimeSpan ts)
        {
            if (ts == TimeSpan.MaxValue) return "—";
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes:D2}min";
            return $"{ts.Minutes}min {ts.Seconds:D2}s";
        }
    }

    internal enum AcaoOperador { CONFIRMADO, ANALISE }
}
