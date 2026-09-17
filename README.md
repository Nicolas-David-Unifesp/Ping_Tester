# Ping Tester

Aplicação web em **C# / ASP.NET Core** para executar **ping** e **tracert**
automatizados sobre uma lista de IPs, usando os **cmdlets do PowerShell**
(`Test-Connection` e `Test-NetConnection -TraceRoute`).

Os IPs podem ser **digitados** (um por linha, ou separados por vírgula/ponto e
vírgula) ou enviados em um **arquivo CSV**.

## Arquitetura — Functional Core / Imperative Shell

A regra central: **o núcleo puro decide e interpreta; o shell executa.**

```
┌──────────────────────── IMPERATIVE SHELL (impuro) ────────────────────────┐
│  ASP.NET (PingTester.Web)  →  PowerShellExecutor  →  powershell / pwsh     │
│  NetworkTestOrchestrator ── sequencia chamadas puras em volta do efeito ── │
│                                                                            │
│   ┌──────────────────── FUNCTIONAL CORE (puro) ────────────────────────┐  │
│   │ PingTester.Core:                                                    │  │
│   │  • IpParser / CsvIpExtractor  — texto → IPs válidos + erros         │  │
│   │  • PowerShellCommandBuilder   — monta o comando como DADOS          │  │
│   │  • PingOutputParser / TracertOutputParser — JSON bruto → resultado  │  │
│   │  Sem rede, sem processo, sem I/O → 100% testável offline.           │  │
│   └─────────────────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────────────┘
```

Fluxo por host: **core monta o spec → shell executa e captura JSON
(`ConvertTo-Json`) → core parseia o JSON → resultado de domínio**.

### Projetos

| Projeto | Papel |
|---------|-------|
| `src/PingTester.Core`  | Functional Core — funções puras, sem I/O. |
| `src/PingTester.Shell` | Imperative Shell — executa o PowerShell e orquestra. |
| `src/PingTester.Web`   | API ASP.NET Core + frontend HTML (`wwwroot`). |
| `tests/PingTester.Core.Tests` | Testes (TDD) do núcleo e da orquestração. |

## Como rodar (no Windows)

Pré-requisitos: **.NET SDK 8/9** e **PowerShell** (o Windows já vem com
`powershell`; para PowerShell 7 use `pwsh`).

```powershell
dotnet run --project src/PingTester.Web
```

Abra o endereço mostrado no console (ex.: `http://localhost:5000`).

Para usar o PowerShell 7 (`pwsh`) em vez do Windows PowerShell, configure:

```powershell
# via variável de ambiente
$env:PowerShell__Exe = "pwsh"
dotnet run --project src/PingTester.Web
```

## Endpoints da API

| Método | Rota | Corpo | Descrição |
|--------|------|-------|-----------|
| `POST` | `/api/test` | `{"ips": "8.8.8.8\n1.1.1.1"}` | Testa IPs digitados. |
| `POST` | `/api/test/upload` | `multipart/form-data`, campo `file` | Testa IPs de um CSV. |

Resposta:

```json
{
  "results": [{
    "target": "8.8.8.8",
    "pingReachable": true,
    "packetsSent": 4, "packetsReceived": 4, "packetsLost": 0,
    "lossPercentage": 0, "averageLatencyMs": 10.5,
    "traceDestinationReached": true,
    "hops": [{ "number": 1, "address": "192.168.0.1" }],
    "error": null
  }],
  "invalid": [{ "value": "xyz", "reason": "Não é um endereço IP válido." }]
}
```

## Testes

Os testes rodam **offline** (sem rede, sem PowerShell):

```bash
dotnet run --project tests/PingTester.Core.Tests
```

> **Nota sobre o runner de testes.** O projeto de testes usa um pequeno runner
> próprio (`TestFramework.cs` + `Runner.cs`) em vez do xUnit, porque foi
> desenvolvido em um ambiente sem acesso ao NuGet. Os asserts já imitam a API
> do xUnit (`Assert.Equal`, `Assert.True`, `Assert.Throws`, ...), então a
> migração para xUnit no Windows é mecânica:
>
> 1. `dotnet add tests/PingTester.Core.Tests package xunit`
>    e `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`.
> 2. Trocar o atributo `[Test]` por `[Fact]`.
> 3. Remover `TestFramework.cs` e `Runner.cs` (o xUnit fornece o runner e os
>    asserts com os mesmos nomes).
> 4. Rodar com `dotnet test`.

## Segurança

- Todo IP é validado no **núcleo puro** (`IPAddress.TryParse`, whitelist de
  formato) **antes** de qualquer execução.
- No shell, os valores vão para o PowerShell via **splatting de hashtable**
  (bind como dado, não como script), com escape de aspas e uma verificação
  extra de caracteres (defesa em profundidade).
- Limite de **256 hosts** por requisição e CSV de no máximo **1 MB**.
