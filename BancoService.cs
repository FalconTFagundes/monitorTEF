using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace MonitorTEF
{
    internal static class BancoService
    {
        private static string ConnectionString =>
            $"Server={Config.Servidor};" +
            $"Database={Config.Banco};" +
            $"User Id={Config.Usuario};" +
            $"Password={Config.Senha};" +
            "Connect Timeout=10;";

        /// <summary>
        /// Busca, para cada TIPO presente em LOG_TEF no período configurado:
        ///   - A última data/hora de transação
        ///   - O total de transações no período (para calcular a média)
        ///   - O total de desfazimentos (operação 0420) no período
        ///
        /// Esses dados permitem calcular a média dinâmica de intervalo:
        ///   mediaIntervalo = periodoMinutos / totalTransacoes
        /// </summary>
        public static List<MeioCaptura> ConsultarUltimasTransacoes(int periodoHoras)
        {
            var resultado = new List<MeioCaptura>();

            // DATA_LIMITE: início do período de histórico
            // GETDATE() - N horas
            const string sql = @"
                SELECT
                    TIPO,
                    MAX(DATA)                           AS ULTIMA_TRANSACAO,
                    COUNT(*)                            AS TOTAL_TRANSACOES,
                    SUM(CASE WHEN OPERACAO = '0420'
                             THEN 1 ELSE 0 END)         AS TOTAL_DESFAZIMENTOS
                FROM LOG_TEF WITH (NOLOCK)
                WHERE TIPO     IS NOT NULL
                  AND TIPO     <> ''
                  AND DATA     >= DATEADD(HOUR, @periodoHoras * -1, GETDATE())
                GROUP BY TIPO
                ORDER BY TIPO";

            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@periodoHoras", periodoHoras);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var codigo = reader["TIPO"]?.ToString()?.Trim() ?? "";

                            DateTime? ultima = null;
                            if (!reader.IsDBNull(reader.GetOrdinal("ULTIMA_TRANSACAO")))
                                ultima = Convert.ToDateTime(reader["ULTIMA_TRANSACAO"]);

                            int totalTx   = reader.IsDBNull(reader.GetOrdinal("TOTAL_TRANSACOES"))
                                            ? 0 : Convert.ToInt32(reader["TOTAL_TRANSACOES"]);
                            int totalDesf = reader.IsDBNull(reader.GetOrdinal("TOTAL_DESFAZIMENTOS"))
                                            ? 0 : Convert.ToInt32(reader["TOTAL_DESFAZIMENTOS"]);

                            resultado.Add(new MeioCaptura
                            {
                                Codigo             = codigo,
                                Nome               = MeiosConhecidos.NomeOuCodigo(codigo),
                                UltimaTransacao    = ultima,
                                TotalTransacoes    = totalTx,
                                TotalDesfazimentos = totalDesf,
                                AlertaDisparado    = false
                            });
                        }
                    }
                }
            }

            return resultado;
        }

        public static bool TestarConexao(out string erro)
        {
            erro = "";
            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                    conn.Open();
                return true;
            }
            catch (Exception ex)
            {
                erro = ex.Message;
                return false;
            }
        }
    }
}
