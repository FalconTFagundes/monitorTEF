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
        /// Busca, para cada TIPO presente em LOG_TEF no período:
        ///   - última data/hora de transação
        ///   - total de transações (para calcular a média dinâmica)
        ///   - total de desfazimentos (operação 0420)
        ///
        /// Inclui também os meios da tabela MEIOS_CAPTURA que não
        /// tiveram transações no período — aparecem como SemDados.
        /// </summary>
        public static List<MeioCaptura> ConsultarUltimasTransacoes(int periodoHoras)
        {
            var resultado = new List<MeioCaptura>();

            const string sql = @"
                -- Meios com transações no período
                SELECT
                    l.TIPO                                          AS CODIGO,
                    ISNULL(m.DESCRICAO, l.TIPO)                    AS NOME,
                    MAX(CAST(l.EMISSAO AS DATE)
                        + CAST(l.HORA  AS TIME))                   AS ULTIMA_TRANSACAO,
                    COUNT(*)                                        AS TOTAL_TX,
                    SUM(CASE WHEN l.OPERACAO = '0420' THEN 1
                              ELSE 0 END)                          AS TOTAL_DESF
                FROM LOG_TEF l WITH (NOLOCK)
                LEFT JOIN MEIOS_CAPTURA m WITH (NOLOCK)
                    ON m.TIPO = l.TIPO AND m.ATIVO = 1
                WHERE l.TIPO IS NOT NULL
                  AND l.TIPO <> ''
                  AND CAST(l.EMISSAO AS DATE)
                      + CAST(l.HORA AS TIME)
                      >= DATEADD(HOUR, -@periodo, GETDATE())
                GROUP BY l.TIPO, m.DESCRICAO

                UNION ALL

                -- Meios cadastrados mas sem transações no período
                SELECT
                    m.TIPO,
                    m.DESCRICAO,
                    NULL,
                    0,
                    0
                FROM MEIOS_CAPTURA m WITH (NOLOCK)
                WHERE m.ATIVO = 1
                  AND NOT EXISTS (
                      SELECT 1 FROM LOG_TEF l WITH (NOLOCK)
                      WHERE l.TIPO = m.TIPO
                        AND CAST(l.EMISSAO AS DATE)
                            + CAST(l.HORA AS TIME)
                            >= DATEADD(HOUR, -@periodo, GETDATE())
                  )
                ORDER BY CODIGO";

            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@periodo", periodoHoras);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var codigo = reader["CODIGO"]?.ToString()?.Trim() ?? "";
                            if (string.IsNullOrEmpty(codigo)) continue;

                            // Nome: vem da tabela; fallback para dicionário local
                            string nomeDb = reader["NOME"]?.ToString()?.Trim() ?? "";
                            string nome   = !string.IsNullOrEmpty(nomeDb)
                                ? nomeDb
                                : MeiosConhecidos.NomeOuCodigo(codigo);

                            DateTime? ultima = null;
                            if (!reader.IsDBNull(reader.GetOrdinal("ULTIMA_TRANSACAO")))
                                ultima = Convert.ToDateTime(reader["ULTIMA_TRANSACAO"]);

                            int totalTx   = reader.IsDBNull(reader.GetOrdinal("TOTAL_TX"))
                                ? 0 : Convert.ToInt32(reader["TOTAL_TX"]);
                            int totalDesf = reader.IsDBNull(reader.GetOrdinal("TOTAL_DESF"))
                                ? 0 : Convert.ToInt32(reader["TOTAL_DESF"]);

                            resultado.Add(new MeioCaptura
                            {
                                Codigo             = codigo,
                                Nome               = nome,
                                UltimaTransacao    = ultima,
                                TotalTransacoes    = totalTx,
                                TotalDesfazimentos = totalDesf,
                            });
                        }
                    }
                }
            }

            return resultado;
        }

        // ─────────────────────────────────────────────────────────────────
        //  AUTENTICAÇÃO
        //  Mesmo algoritmo do servidor Python:
        //    encode: chr(50 + 2*i + int(d[5-i]))  para i in range(6)
        //    decode: ord(c[i]) - 50 - 2*i          para i in range(6)
        // ─────────────────────────────────────────────────────────────────
        private static string DecodeSenha(string cifrado)
        {
            if (cifrado == null || cifrado.Length < 6) return "";
            try
            {
                var digits = new int[6];
                for (int i = 0; i < 6; i++)
                    digits[i] = cifrado[i] - 50 - 2 * i;

                // reversed
                var result = new char[6];
                for (int i = 0; i < 6; i++)
                    result[i] = (char)('0' + digits[5 - i]);

                return new string(result);
            }
            catch { return ""; }
        }

        /// <summary>
        /// Autentica o operador pelo CODIGO e SENHA.
        /// Retorna true e preenche nomeCompleto se OK.
        /// </summary>
        public static bool AutenticarUsuario(
            string codigo, string senhaDigitada, out string nomeCompleto)
        {
            nomeCompleto = "";
            const string sql =
                "SELECT NOME, SENHA FROM USUARIOS " +
                "WHERE CODIGO = @cod";

            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@cod", codigo);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read()) return false;

                        string nome        = r["NOME"]?.ToString()?.Trim() ?? "";
                        string senhaCifrada= r["SENHA"]?.ToString()?.Trim() ?? "";
                        string senhaReal   = DecodeSenha(senhaCifrada);

                        if (senhaDigitada.Trim() != senhaReal) return false;

                        // Capitaliza cada palavra do nome
                        var partes = nome.Split(
                            new[]{' '}, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < partes.Length; i++)
                            partes[i] = char.ToUpper(partes[i][0]) +
                                        partes[i].Substring(1).ToLower();
                        nomeCompleto = string.Join(" ", partes);
                        return true;
                    }
                }
            }
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
