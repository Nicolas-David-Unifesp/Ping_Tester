using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using PingTester.Core.Ips;
using PingTester.Shell;

namespace PingTester.Web;

/// <summary>Shared request-handling logic for the test endpoints.</summary>
public static class ApiHandlers
{
    public const int MaxCsvBytes = 1_000_000; // 1 MB
    public const int MaxHosts = 256;

    public static async Task<IResult> RunAndBuildResponse(
        IpParseResult parsed, NetworkTestOrchestrator orch, CancellationToken ct)
    {
        var invalid = ResultMapper.ToInvalid(parsed.Errors);

        if (parsed.ValidHosts.Count == 0)
        {
            return Results.Ok(new TestResponse(Array.Empty<HostResultDto>(), invalid));
        }

        // Guard against too many hosts in a single request.
        var hosts = parsed.ValidHosts.Take(MaxHosts).ToList();

        var reports = await orch.RunAsync(hosts, ct);
        var results = reports.Select(ResultMapper.ToDto).ToList();

        return Results.Ok(new TestResponse(results, invalid));
    }
}
