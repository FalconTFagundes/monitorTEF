# MonitorTEF — BigCard

Monitor de ociosidade de meios de captura TEF via SQL Server.

---

## Pré-requisitos

- Visual Studio 2022 (ou superior)
- .NET Framework 4.8
- Acesso à rede do SQL Server da BigCard

---

## Como configurar antes de compilar

Abra o arquivo **`Config.cs`** e preencha:

```csharp
public const string Servidor = "SEU_SERVIDOR";   // IP ou nome\instancia
public const string Banco    = "SEU_BANCO";
public const string Usuario  = "SEU_USUARIO";
public const string Senha    = "SUA_SENHA";
```

Opcionalmente ajuste os valores padrão:

```csharp
public const int IntervaloVerificacaoSegundos = 60;   // polling a cada 60s
public const int TempoAlertaPadraoMinutos     = 30;   // alerta após 30min ocioso
```

---

## Como compilar

1. Abrir `MonitorTEF.csproj` no Visual Studio
2. Menu **Build → Build Solution** (ou `Ctrl+Shift+B`)
3. O `.exe` ficará em `bin\Release\net48\MonitorTEF.exe`

---

## Funcionalidades

| Recurso | Descrição |
|---|---|
| Painel principal | Lista todos os meios com última transação, tempo ocioso e situação |
| Popup de alerta | Aparece discretamente no canto inferior direito sem roubar o foco |
| Alerta global | Configurável em minutos pelo painel inferior |
| Alerta individual | Duplo clique em qualquer meio para definir um limite próprio |
| Intervalo de polling | Configurável em segundos pelo painel inferior |
| Reset automático | Alerta se reinicia quando o meio volta a transacionar |

---

## Meios de captura mapeados

| Código | Nome |
|---|---|
| E | CIELO |
| I | AUTORIZADOR |
| V | BANCO 24H |
| R | REDE |
| A | CENTRAL DE ATENDIMENTO |
| S | SAFRAPAY |
| Z | FEPAS |
| O | ELO |
| F | SIPAG |
| P | SIPAG NOVA |

Novos tipos vindos de outras tabelas podem ser adicionados em **`Models.cs`**
no dicionário `MeiosConhecidos.Mapa`.
