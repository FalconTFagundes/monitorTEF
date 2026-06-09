# MonitorTEF — BigCard v2

Monitor de ociosidade de meios de captura TEF com **limite dinâmico por histórico**.

---

## Como funciona o limite dinâmico

O alerta **não usa um tempo fixo em minutos**. O sistema calcula automaticamente
o intervalo médio de cada meio a partir do histórico real de transações:

```
médiaIntervalo = periodoHoras × 60 / totalTransações
percentual     = tempoOcioso / médiaIntervalo × 100
alerta         = percentual > tolerância%  (padrão: 150%)
```

**Exemplos com tolerância 150%:**

| Meio       | Transações (24h) | Média       | Alerta se ocioso > |
|------------|-----------------|-------------|-------------------|
| Sitef      | 1440            | 1 min       | ~1,5 min          |
| Banco 24H  | 36              | 40 min      | ~60 min           |
| Web Site   | 6               | 4 horas     | ~6 horas          |

Cada meio se adapta ao seu próprio ritmo sem nenhuma configuração manual.

---

## Pré-requisitos

- Visual Studio 2022+
- .NET Framework 4.8
- Acesso à rede do SQL Server da BigCard
- Acesso UNC à pasta `\\192.168.2.57\Interno\rafa` (para log)

---

## Configuração antes de compilar

Edite **`Config.cs`**:

```csharp
public const string Servidor = "SEU_SERVIDOR";
public const string Banco    = "SEU_BANCO";
public const string Usuario  = "SEU_USUARIO";
public const string Senha    = "SUA_SENHA";

// Parâmetros de monitoramento (ajustáveis também em runtime):
public const int PeriodoHistoricoHoras    = 24;   // horas de histórico para calcular média
public const int ToleranciaGlobalPercent  = 150;  // % acima da média para alertar
public const int IntervaloVerificacaoSegundos = 60;
public const int SupressaoAnaliseMinutos  = 5;
```

---

## Como compilar

1. Abrir `MonitorTEF.csproj` no Visual Studio
2. `Ctrl+Shift+B` → Build Solution
3. Executável em `bin\Release\net48\MonitorTEF.exe`

---

## Funcionalidades

| Recurso | Descrição |
|---|---|
| Limite dinâmico | Calculado a partir do histórico real de cada meio |
| Grid ordenado | Críticos → Atenção → Sem dados → OK |
| Colunas informativas | Média, % da Média, Tx/Desfazimentos no período |
| Popup de alerta | Fica na tela até o operador confirmar ou colocar em análise |
| CONFIRMAR | Loga a confirmação no CSV de rede |
| EM ANÁLISE | Suprime o alerta por 5 min, popup muda visual com contagem |
| Log CSV em rede | `\\192.168.2.57\Interno\rafa\monitor_tef_log.csv` |
| Tolerância individual | Duplo clique no grid → tolerância própria para o meio |
| Parâmetros em runtime | Período, tolerância global e polling ajustáveis sem recompilar |

---

## Meios de captura mapeados

| Código | Nome |
|---|---|
| A | Central de Atendimento |
| B | App Celular |
| C | BigCash |
| E | Cielo - [RC] |
| F | Sipag - [RC] |
| I | Autorizador |
| L | Logpay |
| O | Elo |
| R | Rede - [RC] |
| S | SafraPay - [RC] |
| V | Banco 24H |
| W | Web Site |
| Z | Sitef / FEPAS |
| P | Sipag Nova - [RC] |
| H | HG Pay |

Novos meios: adicionar em `Models.cs` → `MeiosConhecidos.Mapa`.
