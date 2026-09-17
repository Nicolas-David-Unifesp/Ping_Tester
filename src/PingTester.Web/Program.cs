using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PingTester.Core.Ips;
using PingTester.Shell;
using PingTester.Web;

var builder = WebApplication.CreateBuilder(args);

// --- Dependency injection: wire the imperative shell -----------------------
// The executor runs the classic ping.exe / tracert.exe directly (same ICMP
// mechanism as the CMD prompt), which works where the Windows PowerShell 5.1
// Test-Connection cmdlet fails via WMI. An optional per-command timeout can be
// set via config "Ping:TimeoutSeconds".
var timeoutSeconds = builder.Configuration.GetValue<int?>("Ping:TimeoutSeconds") ?? 120;
builder.Services.AddSingleton<IPowerShellExecutor>(_ =>
    new PowerShellExecutor(timeout: TimeSpan.FromSeconds(timeoutSeconds)));
builder.Services.AddSingleton(sp =>
    new NetworkTestOrchestrator(sp.GetRequiredService<IPowerShellExecutor>(), maxParallelism: 4));

var app = builder.Build();

app.UseDefaultFiles();   // serve index.html at "/"
app.UseStaticFiles();    // serve wwwroot

// --- Endpoint: typed IPs ---------------------------------------------------
app.MapPost("/api/test", async (TestRequest request, NetworkTestOrchestrator orch, CancellationToken ct) =>
{
    var parsed = IpParser.Parse(request.Ips);
    return await ApiHandlers.RunAndBuildResponse(parsed, orch, ct);
});

// --- Endpoint: CSV upload --------------------------------------------------
app.MapPost("/api/test/upload", async (HttpRequest http, NetworkTestOrchestrator orch, CancellationToken ct) =>
{
    if (!http.HasFormContentType)
        return Results.BadRequest(new { message = "Envie um arquivo CSV via multipart/form-data." });

    var form = await http.ReadFormAsync(ct);
    var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
    if (file is null || file.Length == 0)
        return Results.BadRequest(new { message = "Nenhum arquivo enviado." });

    if (file.Length > ApiHandlers.MaxCsvBytes)
        return Results.BadRequest(new { message = "Arquivo CSV muito grande (máx. 1 MB)." });

    string csvText;
    using (var reader = new StreamReader(file.OpenReadStream()))
        csvText = await reader.ReadToEndAsync(ct);

    var parsed = CsvIpExtractor.Extract(csvText);
    return await ApiHandlers.RunAndBuildResponse(parsed, orch, ct);
})
.DisableAntiforgery();

app.Run();
