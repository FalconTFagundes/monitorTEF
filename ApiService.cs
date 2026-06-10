using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;

namespace MonitorTEF
{
    /// <summary>
    /// Substitui o BancoService: busca dados via HTTP do servidor Python
    /// em vez de conectar diretamente ao SQL Server.
    ///
    /// O endpoint GET /api/transacoes retorna linhas individuais:
    ///   [{ "modo":"Z", "emissao":"02/06/2026", "hora":"10:07", "operacao":"0200" }, ...]
    ///
    /// Este serviço agrega localmente (igual ao monitor.html):
    ///   - última transação por modo
    ///   - total de transações por modo
    ///   - total de desfazimentos (operacao == "0420")
    /// </summary>
    internal static class ApiService
    {
        // ── parser JSON mínimo sem dependência externa ────────────────────
        // Usa apenas System.Runtime.Serialization presente no .NET 4.8
        // para deserializar a lista de objetos simples.

        private static string Get(string caminho, int timeoutMs = 0)
        {
            if (timeoutMs == 0) timeoutMs = Config.HttpTimeoutSegundos * 1000;
            var req = (HttpWebRequest)WebRequest.Create(Config.UrlServidor + caminho);
            req.Method  = "GET";
            req.Timeout = timeoutMs;
            req.Accept  = "application/json";

            using (var resp   = (HttpWebResponse)req.GetResponse())
            using (var stream = resp.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        // ─────────────────────────────────────────────────────────────────
        //  PING — verifica se o servidor está no ar
        // ─────────────────────────────────────────────────────────────────
        public static bool ServidorAtivo(out string erro)
        {
            erro = "";
            try
            {
                var json = Get("/api/ping", 4000);
                // {"ok": true, ...}
                erro = "";
                return json.Contains("\"ok\": true") || json.Contains("\"ok\":true");
            }
            catch (WebException ex) when (ex.Status == WebExceptionStatus.ConnectFailure)
            {
                erro = $"Servidor offline em {Config.UrlServidor}";
                return false;
            }
            catch (WebException ex) when (ex.Status == WebExceptionStatus.Timeout)
            {
                erro = "Servidor não respondeu (timeout)";
                return false;
            }
            catch (Exception ex)
            {
                erro = ex.Message;
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  CONSULTAR TRANSAÇÕES — agrega da mesma forma que o monitor.html
        // ─────────────────────────────────────────────────────────────────
        public static List<MeioCaptura> ConsultarUltimasTransacoes(int periodoHoras)
        {
            var json = Get($"/api/transacoes?periodo={periodoHoras}");

            // Agrega linhas por modo
            var ultimaTx    = new Dictionary<string, DateTime>();
            var totalTx     = new Dictionary<string, int>();
            var totalDesf   = new Dictionary<string, int>();

            ParseLinhas(json, (modo, emissao, hora, operacao) =>
            {
                if (string.IsNullOrWhiteSpace(modo)) return;

                // monta DateTime combinando emissao (dd/MM/yyyy) + hora (HH:mm)
                DateTime? dt = ParseDataHora(emissao, hora);

                if (!totalTx.ContainsKey(modo))
                {
                    totalTx[modo]   = 0;
                    totalDesf[modo] = 0;
                }

                totalTx[modo]++;

                if (operacao == "0420")
                    totalDesf[modo]++;

                if (dt.HasValue)
                {
                    if (!ultimaTx.ContainsKey(modo) || dt.Value > ultimaTx[modo])
                        ultimaTx[modo] = dt.Value;
                }
            });

            // Monta lista de MeioCaptura
            var resultado = new List<MeioCaptura>();
            foreach (var modo in totalTx.Keys)
            {
                resultado.Add(new MeioCaptura
                {
                    Codigo             = modo,
                    Nome               = MeiosConhecidos.NomeOuCodigo(modo),
                    UltimaTransacao    = ultimaTx.ContainsKey(modo) ? ultimaTx[modo] : (DateTime?)null,
                    TotalTransacoes    = totalTx[modo],
                    TotalDesfazimentos = totalDesf[modo],
                    AlertaDisparado    = false
                });
            }

            resultado.Sort((a, b) => string.Compare(a.Codigo, b.Codigo));
            return resultado;
        }

        // ─────────────────────────────────────────────────────────────────
        //  PARSER JSON MANUAL
        //  Evita dependência de Newtonsoft ou System.Text.Json (não incluso
        //  no .NET 4.8 por padrão). Parseia o array de objetos simples
        //  extraindo apenas os quatro campos que precisamos.
        // ─────────────────────────────────────────────────────────────────
        private static void ParseLinhas(
            string json,
            Action<string, string, string, string> onLinha)
        {
            // Estratégia: encontra cada objeto {...} e extrai campos por chave
            int pos = 0;
            while (pos < json.Length)
            {
                int ini = json.IndexOf('{', pos);
                if (ini < 0) break;
                int fim = json.IndexOf('}', ini);
                if (fim < 0) break;

                string obj = json.Substring(ini, fim - ini + 1);

                string modo      = ExtrairCampo(obj, "modo");
                string emissao   = ExtrairCampo(obj, "emissao");
                string hora      = ExtrairCampo(obj, "hora");
                string operacao  = ExtrairCampo(obj, "operacao");

                onLinha(modo, emissao, hora, operacao);
                pos = fim + 1;
            }
        }

        /// <summary>Extrai o valor de uma chave string num objeto JSON simples.</summary>
        private static string ExtrairCampo(string obj, string chave)
        {
            // Procura por "chave": "valor"  ou  "chave":null
            string busca = $"\"{chave}\"";
            int idx = obj.IndexOf(busca, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return "";

            int colon = obj.IndexOf(':', idx + busca.Length);
            if (colon < 0) return "";

            // Avança espaços
            int start = colon + 1;
            while (start < obj.Length && obj[start] == ' ') start++;

            if (start >= obj.Length) return "";

            if (obj[start] == '"')
            {
                // valor string
                int end = obj.IndexOf('"', start + 1);
                if (end < 0) return "";
                return obj.Substring(start + 1, end - start - 1);
            }

            if (obj.Substring(start, Math.Min(4, obj.Length - start)) == "null")
                return "";

            // valor não-string (número, bool): vai até vírgula ou }
            int endVal = start;
            while (endVal < obj.Length && obj[endVal] != ',' && obj[endVal] != '}')
                endVal++;
            return obj.Substring(start, endVal - start).Trim();
        }

        /// <summary>
        /// Converte emissao "dd/MM/yyyy" + hora "HH:mm" em DateTime.
        /// Aceita também hora com segundos "HH:mm:ss".
        /// </summary>
        private static DateTime? ParseDataHora(string emissao, string hora)
        {
            if (string.IsNullOrWhiteSpace(emissao)) return null;
            try
            {
                var partes = emissao.Split('/');
                if (partes.Length != 3) return null;
                int dia = int.Parse(partes[0]);
                int mes = int.Parse(partes[1]);
                int ano = int.Parse(partes[2]);

                int hh = 0, mm = 0, ss = 0;
                if (!string.IsNullOrWhiteSpace(hora))
                {
                    var hp = hora.Split(':');
                    hh = hp.Length > 0 ? int.Parse(hp[0]) : 0;
                    mm = hp.Length > 1 ? int.Parse(hp[1]) : 0;
                    ss = hp.Length > 2 ? int.Parse(hp[2]) : 0;
                }

                return new DateTime(ano, mes, dia, hh, mm, ss);
            }
            catch
            {
                return null;
            }
        }
    }
}
