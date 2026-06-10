using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace MonitorTEF
{
    /// <summary>
    /// Busca dados diretamente do servidor Python do monitor central.
    /// Replica EXATAMENTE a lógica de processData() do monitor.html:
    ///   - /api/meios       → dicionário de meios (fonte de verdade dos nomes)
    ///   - /api/transacoes  → linhas brutas agregadas e calculadas aqui
    ///
    /// O C# não conecta ao banco SQL — toda a fonte de dados é o servidor central.
    /// </summary>
    internal static class ApiService
    {
        // ─────────────────────────────────────────────────────────────────
        //  HTTP
        // ─────────────────────────────────────────────────────────────────
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
        //  PING
        // ─────────────────────────────────────────────────────────────────
        public static bool ServidorAtivo(out string erro)
        {
            erro = "";
            try
            {
                var json = Get("/api/ping", 4000);
                return json.Contains("\"ok\": true") || json.Contains("\"ok\":true");
            }
            catch (WebException ex) when (
                ex.Status == WebExceptionStatus.ConnectFailure ||
                ex.Status == WebExceptionStatus.Timeout)
            {
                erro = $"Servidor offline em {Config.UrlServidor}";
                return false;
            }
            catch (Exception ex) { erro = ex.Message; return false; }
        }

        // ─────────────────────────────────────────────────────────────────
        //  MEIOS DE CAPTURA — vem do servidor, não do dicionário local
        // ─────────────────────────────────────────────────────────────────
        private static Dictionary<string, string> BuscarMeios()
        {
            try
            {
                var json = Get("/api/meios");
                // retorna: {"Z": "Sitef / FEPAS", "E": "Cielo - [ RC ]", ...}
                return ParseDicionario(json);
            }
            catch
            {
                // fallback: dicionário local se o endpoint falhar
                return new Dictionary<string, string>(MeiosConhecidos.Mapa);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  CONSULTA PRINCIPAL — replica processData() do monitor.html
        // ─────────────────────────────────────────────────────────────────
        public static List<MeioCaptura> ConsultarUltimasTransacoes(int periodoHoras)
        {
            // 1. Busca meios do servidor (nomes e lista completa)
            var meios = BuscarMeios();

            // 2. Busca transações brutas
            var json = Get($"/api/transacoes?periodo={periodoHoras}");

            // 3. Agrupa por modo (igual ao forEach do monitor.html)
            var agora        = DateTime.Now;
            var porModo      = new Dictionary<string, List<TxLinha>>();
            var txRC         = new List<TxLinha>();
            var meiosRC      = new HashSet<string>();

            foreach (var kv in meios)
                if (kv.Value.Contains("[ RC ]"))
                    meiosRC.Add(kv.Key);

            ParseTransacoes(json, tx =>
            {
                if (string.IsNullOrWhiteSpace(tx.Modo)) return;

                if (!porModo.ContainsKey(tx.Modo))
                    porModo[tx.Modo] = new List<TxLinha>();
                porModo[tx.Modo].Add(tx);

                if (meiosRC.Contains(tx.Modo))
                    txRC.Add(tx);
            });

            double periodoMinutos = periodoHoras * 60.0;

            // 4. Calcula métricas por modo — mesma fórmula do monitor.html
            var resultado = new List<MeioCaptura>();

            foreach (var kv in porModo)
            {
                string modo  = kv.Key;
                var    trans = kv.Value;
                trans.Sort((a, b) => a.DataHora.CompareTo(b.DataHora));

                var    ultima           = trans[trans.Count - 1];
                double mediaIntervalo   = periodoMinutos / trans.Count;
                double tempoDesdeUltima = (agora - ultima.DataHora).TotalMinutes;
                double percentual       = 0;
                int    desfazimentos    = 0;

                foreach (var t in trans)
                    if (t.Operacao == "0420") desfazimentos++;

                StatusMeio status = StatusMeio.SemDados;

                if (mediaIntervalo > 0)
                {
                    percentual = (tempoDesdeUltima / mediaIntervalo) * 100.0;

                    if (percentual > Config.ToleranciaGlobalPercent)
                        status = StatusMeio.Critico;
                    else if (percentual > 100)
                        status = StatusMeio.Atencao;
                    else
                        status = StatusMeio.Ok;
                }
                else if (trans.Count == 1)
                {
                    percentual = 50;
                    status     = StatusMeio.Atencao;
                }

                // Nome: vem do servidor; fallback para código se não mapeado
                string nome = meios.ContainsKey(modo) ? meios[modo] : modo;

                var mc = new MeioCaptura
                {
                    Codigo             = modo,
                    Nome               = nome,
                    UltimaTransacao    = ultima.DataHora,
                    TotalTransacoes    = trans.Count,
                    TotalDesfazimentos = desfazimentos,
                };
                mc.ForcarStatus(status);
                mc.DefinirMetricasExternas(mediaIntervalo, tempoDesdeUltima, percentual);

                resultado.Add(mc);
            }

            // 5. Card REDECOMPRAS agregado — igual ao monitor.html
            if (txRC.Count > 0)
            {
                txRC.Sort((a, b) => a.DataHora.CompareTo(b.DataHora));

                var    ultimaRC           = txRC[txRC.Count - 1];
                double mediaRC            = periodoMinutos / txRC.Count;
                double tempoRC            = (agora - ultimaRC.DataHora).TotalMinutes;
                double percentualRC       = mediaRC > 0 ? (tempoRC / mediaRC) * 100.0 : 0;
                int    desfazRC           = 0;

                foreach (var t in txRC)
                    if (t.Operacao == "0420") desfazRC++;

                StatusMeio statusRC = StatusMeio.Ok;
                if (percentualRC > Config.ToleranciaGlobalPercent)
                    statusRC = StatusMeio.Critico;
                else if (percentualRC > 100)
                    statusRC = StatusMeio.Atencao;

                var rc = new MeioCaptura
                {
                    Codigo             = "RC",
                    Nome               = "REDECOMPRAS",
                    UltimaTransacao    = ultimaRC.DataHora,
                    TotalTransacoes    = txRC.Count,
                    TotalDesfazimentos = desfazRC,
                };
                rc.ForcarStatus(statusRC);
                rc.DefinirMetricasExternas(mediaRC, tempoRC, percentualRC);
                resultado.Add(rc);
            }

            resultado.Sort((a, b) => string.Compare(a.Codigo, b.Codigo,
                StringComparison.Ordinal));
            return resultado;
        }

        // ─────────────────────────────────────────────────────────────────
        //  ESTRUTURA INTERNA DE LINHA
        // ─────────────────────────────────────────────────────────────────
        private class TxLinha
        {
            public string   Modo      { get; set; }
            public string   Operacao  { get; set; }
            public DateTime DataHora  { get; set; }
        }

        // ─────────────────────────────────────────────────────────────────
        //  PARSERS JSON MANUAIS (sem Newtonsoft, só .NET 4.8 built-in)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Parseia {"chave":"valor", ...}</summary>
        private static Dictionary<string, string> ParseDicionario(string json)
        {
            var result = new Dictionary<string, string>();
            int pos    = 0;
            while (pos < json.Length)
            {
                // encontra "chave"
                int q1 = json.IndexOf('"', pos);          if (q1 < 0) break;
                int q2 = json.IndexOf('"', q1 + 1);       if (q2 < 0) break;
                string chave = json.Substring(q1 + 1, q2 - q1 - 1);

                // : depois da chave
                int colon = json.IndexOf(':', q2 + 1);    if (colon < 0) break;

                // "valor"
                int q3 = json.IndexOf('"', colon + 1);    if (q3 < 0) break;
                int q4 = json.IndexOf('"', q3 + 1);       if (q4 < 0) break;
                string valor = json.Substring(q3 + 1, q4 - q3 - 1);

                if (!string.IsNullOrEmpty(chave))
                    result[chave] = valor;

                pos = q4 + 1;
            }
            return result;
        }

        /// <summary>Parseia [{...}, {...}, ...] chamando onLinha para cada objeto.</summary>
        private static void ParseTransacoes(string json, Action<TxLinha> onLinha)
        {
            int pos = 0;
            while (pos < json.Length)
            {
                int ini = json.IndexOf('{', pos); if (ini < 0) break;
                int fim = json.IndexOf('}', ini); if (fim < 0) break;

                string obj = json.Substring(ini, fim - ini + 1);

                string modo     = ExtrairCampo(obj, "modo");
                string emissao  = ExtrairCampo(obj, "emissao");
                string hora     = ExtrairCampo(obj, "hora");
                string operacao = ExtrairCampo(obj, "operacao");

                var dt = ParseDataHora(emissao, hora);
                if (dt.HasValue)
                {
                    onLinha(new TxLinha
                    {
                        Modo     = modo,
                        Operacao = operacao,
                        DataHora = dt.Value
                    });
                }

                pos = fim + 1;
            }
        }

        private static string ExtrairCampo(string obj, string chave)
        {
            string busca = $"\"{chave}\"";
            int idx = obj.IndexOf(busca, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return "";

            int colon = obj.IndexOf(':', idx + busca.Length);
            if (colon < 0) return "";

            int start = colon + 1;
            while (start < obj.Length && obj[start] == ' ') start++;
            if (start >= obj.Length) return "";

            if (obj[start] == '"')
            {
                int end = obj.IndexOf('"', start + 1);
                return end < 0 ? "" : obj.Substring(start + 1, end - start - 1);
            }

            if (start + 4 <= obj.Length &&
                obj.Substring(start, 4) == "null") return "";

            int endVal = start;
            while (endVal < obj.Length &&
                   obj[endVal] != ',' && obj[endVal] != '}') endVal++;
            return obj.Substring(start, endVal - start).Trim();
        }

        private static DateTime? ParseDataHora(string emissao, string hora)
        {
            if (string.IsNullOrWhiteSpace(emissao)) return null;
            try
            {
                var p  = emissao.Split('/');
                if (p.Length != 3) return null;
                int dia = int.Parse(p[0]);
                int mes = int.Parse(p[1]);
                int ano = int.Parse(p[2]);

                int hh = 0, mm = 0, ss = 0;
                if (!string.IsNullOrWhiteSpace(hora))
                {
                    var hp = hora.Split(':');
                    if (hp.Length > 0) hh = int.Parse(hp[0]);
                    if (hp.Length > 1) mm = int.Parse(hp[1]);
                    if (hp.Length > 2) ss = int.Parse(hp[2]);
                }
                return new DateTime(ano, mes, dia, hh, mm, ss);
            }
            catch { return null; }
        }
    }
}
