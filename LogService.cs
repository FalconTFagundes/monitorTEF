using System;
using System.IO;
using System.Text;

namespace MonitorTEF
{
    /// <summary>
    /// Grava ações dos operadores (CONFIRMADO / ANALISE) em CSV na pasta de rede.
    /// Thread-safe via lock estático.
    /// </summary>
    internal static class LogService
    {
        private static readonly object _lock = new object();

        private static string CaminhoCompleto =>
            Path.Combine(Config.PastaLogRede, Config.NomeArquivoLog);

        private static readonly string[] Cabecalho =
        {
            "Timestamp", "Operador", "Maquina", "Codigo", "Meio", "Acao",
            "TempoOcioso", "LimiteMinutos", "Observacao"
        };

        /// <summary>
        /// Registra uma ação do operador no CSV compartilhado em rede.
        /// </summary>
        public static void Registrar(
            string codigoMeio,
            string nomeMeio,
            AcaoOperador acao,
            TimeSpan tempoOcioso,
            int limiteMinutos,
            string obs = "")
        {
            try
            {
                lock (_lock)
                {
                    bool arquivoNovo = !File.Exists(CaminhoCompleto);

                    using (var sw = new StreamWriter(CaminhoCompleto,
                           append: true, encoding: new UTF8Encoding(true)))
                    {
                        // escreve cabeçalho só na primeira vez
                        if (arquivoNovo)
                            sw.WriteLine(string.Join(";", Cabecalho));

                        sw.WriteLine(string.Join(";",
                            DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                            Environment.UserName,
                            Environment.MachineName,
                            codigoMeio,
                            Escapar(nomeMeio),
                            acao.ToString(),
                            FormatarTempo(tempoOcioso),
                            limiteMinutos.ToString(),
                            Escapar(obs)
                        ));
                    }
                }
            }
            catch (Exception ex)
            {
                // falha silenciosa — não travar o operador por problema de rede
                System.Diagnostics.Debug.WriteLine($"[LogService] Erro ao gravar log: {ex.Message}");
            }
        }

        private static string Escapar(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            // envolve em aspas se contiver ponto-e-vírgula ou quebra de linha
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

    internal enum AcaoOperador
    {
        CONFIRMADO,
        ANALISE
    }
}
