# Ping Tester

Aplicação web em **C# / ASP.NET Core** para executar **ping** e **tracert**
automatizados sobre uma lista de IPs, usando os executáveis clássicos do
Windows **`ping.exe`** e **`tracert.exe`** (mesmo mecanismo ICMP do Prompt de
Comando). O tracert é limitado a **15 saltos** (`tracert -d -h 15`).

> **Por que não os cmdlets?** A ideia inicial usava `Test-Connection` /
> `Test-NetConnection`. Porém, no **Windows PowerShell 5.1**, o `Test-Connection`
> usa WMI e falhava no ambiente-alvo ("Erro devido à falta de recursos"), mesmo
> com o ping do CMD funcionando. Os executáveis clássicos usam ICMP direto e
> funcionam de forma confiável em qualquer versão do Windows.

Os IPs podem ser **digitados** (um por linha, ou separados por vírgula/ponto e
vírgula) ou enviados em um **arquivo CSV**.

## Arquitetura — Functional Core / Imperative Shell

A regra central: **o núcleo puro decide e interpreta; o shell executa.**

```
┌──────────────────────── IMPERATIVE SHELL (impuro) ────────────────────────┐
│  ASP.NET (PingTester.Web)  →  PowerShellExecutor  →  ping.exe / tracert.exe│
│  NetworkTestOrchestrator ── sequencia chamadas puras em volta do efeito ── │
│                                                                            │
│   ┌──────────────────── FUNCTIONAL CORE (puro) ────────────────────────┐  │
│   │ PingTester.Core:                                                    │  │
│   │  • IpParser / CsvIpExtractor  — texto → IPs válidos + erros         │  │
│   │  • PowerShellCommandBuilder   — monta o comando como DADOS          │  │
│   │  • PingOutputParser / TracertOutputParser — texto bruto → resultado │  │
│   │  Sem rede, sem processo, sem I/O → 100% testável offline.           │  │
│   └─────────────────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────────────┘
```

Fluxo por host: **core monta o spec (`ping -n 4 <ip>` / `tracert -d -h 15 <ip>`)
→ shell executa e captura o TEXTO → core parseia o texto (pt-BR) → resultado de
domínio**.

### Projetos

| Projeto | Papel |
|---------|-------|
| `src/PingTester.Core`  | Functional Core — funções puras, sem I/O. |
| `src/PingTester.Shell` | Imperative Shell — executa o PowerShell e orquestra. |
| `src/PingTester.Web`   | API ASP.NET Core + frontend HTML (`wwwroot`). |
| `tests/PingTester.Core.Tests` | Testes (TDD) do núcleo e da orquestração. |

## Como rodar (no Windows)

Pré-requisitos: **.NET SDK 8/9** e **Windows** (o app usa `ping.exe` e
`tracert.exe`, que já vêm no Windows).

```powershell
dotnet run --project src/PingTester.Web
```

Abra o endereço mostrado no console (ex.: `http://localhost:5000`).

O tempo limite por comando (padrão 120s) pode ser ajustado via configuração:

```powershell
# via variável de ambiente (ex.: 60 segundos)
$env:Ping__TimeoutSeconds = "60"
dotnet run --project src/PingTester.Web
```

## Endpoints da API

| Método | Rota | Corpo | Descrição |
|--------|------|-------|-----------|
| `POST` | `/api/test` | `{"ips": "8.8.8.8\n1.1.1.1"}` | Testa IPs digitados. |
| `POST` | `/api/test/upload` | `multipart/form-data`, campo `file` | Testa IPs de um CSV. |
| `GET`  | `/api/monitor` | `?refresh=true` (opcional) | Aba de monitoramento: ping (só) da lista fixa, em paralelo, com cache curto. |
| `GET`  | `/api/monitor/trace` | `?ip=8.8.8.8` | Tracert sob demanda (máx. 8 saltos) de um host. |

## Aba de Monitoramento

Uma segunda aba monitora uma **lista fixa de hosts** definida em um CSV no
servidor. Ao abrir a aba, o app executa **apenas ping** de todos os hosts em
paralelo (rápido, adequado a ~70 IPs) e mostra verde/vermelho. O **tracert**
(limitado a **8 saltos**) roda **sob demanda**, ao clicar em "Ver rota".

Configuração (todas opcionais, via `appsettings.json` ou variáveis de ambiente):

| Chave | Padrão | Descrição |
|-------|--------|-----------|
| `Monitor:CsvPath` | `monitored-hosts.csv` (ao lado do app) | Arquivo com os hosts. |
| `Monitor:CacheSeconds` | `30` | Duração do cache do resultado do ping. |
| `Monitor:Parallelism` | `20` | Pings simultâneos. |
| `Monitor:TraceHops` | `8` | Máximo de saltos do tracert sob demanda. |

Formato do `monitored-hosts.csv` (mesmas regras do upload de CSV — IP em
qualquer coluna, cabeçalho e células não-IP ignorados, `;` ou `,`):

```
ip;descricao
127.0.0.1;Loopback local
10.113.96.148;Servidor A
```

> O botão **Atualizar** ignora o cache e refaz a verredura na hora.

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
- No shell, cada opção, flag e o IP são passados como **argumentos de processo
  distintos** (`ProcessStartInfo.ArgumentList`), nunca concatenados numa string
  de shell — então um valor não consegue injetar comando. Há ainda uma
  verificação extra de caracteres (defesa em profundidade).
- A saída de `ping`/`tracert` é lida com a **code page OEM** do Windows
  (via `Console.OutputEncoding`), para os acentos do português virem corretos.
- Limite de **256 hosts** por requisição e CSV de no máximo **1 MB**.
