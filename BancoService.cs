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
        /// Retorna a última data/hora de transação de cada TIPO presente em LOG_TEF.
        /// </summary>
        public static List<MeioCaptura> ConsultarUltimasTransacoes()
        {
            var resultado = new List<MeioCaptura>();

            const string sql = @"
                SELECT
                    TIPO,
                    MAX(DATA) AS ULTIMA_TRANSACAO
                FROM LOG_TEF WITH (NOLOCK)
                WHERE TIPO IS NOT NULL
                  AND TIPO <> ''
                GROUP BY TIPO
                ORDER BY TIPO";

            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var codigo = reader["TIPO"]?.ToString()?.Trim() ?? "";
                        DateTime? ultima = null;

                        if (!reader.IsDBNull(1))
                            ultima = Convert.ToDateTime(reader["ULTIMA_TRANSACAO"]);

                        resultado.Add(new MeioCaptura
                        {
                            Codigo           = codigo,
                            Nome             = MeiosConhecidos.NomeOuCodigo(codigo),
                            UltimaTransacao  = ultima,
                            AlertaDisparado  = false
                        });
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
