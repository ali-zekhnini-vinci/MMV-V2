using System.Diagnostics;
using System.Text;

namespace MMV.Maintenance.Tests;

/// <summary>
/// Exécution RÉELLE du binaire MMV.Maintenance : une cible non autorisée est refusée (code 5) AVANT toute
/// connexion. Les cibles sont injoignables (TEST-NET 192.0.2.0/24) ou ne sont jamais contactées : aucune
/// base n'est lue ni modifiée. La variable ConnectionStrings__Mmv surcharge tout user-secret local.
/// </summary>
public sealed class BootstrapCommandProcessTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);

    [Theory]
    [InlineData("Host=192.0.2.10;Port=5433;Database=mmv_v2_dev;Username=mmv_v2_dev;Password=x;Timeout=15")]
    [InlineData("Host=localhost;Port=5433;Database=mmv_v2_test;Username=mmv_v2_test;Password=x")]
    [InlineData("Host=localhost;Port=5432;Database=mmv_v2_dev;Username=mmv_v2_dev;Password=x")]
    [InlineData("Host=localhost;Port=5433;Database=postgres;Username=postgres;Password=x")]
    public async Task BootstrapAdmin_ForeignTarget_IsRefusedWithoutConnecting(string connectionString)
    {
        var stopwatch = Stopwatch.StartNew();

        var run = await RunAsync("bootstrap-admin", connectionString);

        Assert.Equal(5, run.ExitCode);
        Assert.Contains("aucune connexion n'a été ouverte", run.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("Base cible", run.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Password=x", run.StandardError + run.StandardOutput, StringComparison.Ordinal);
        // Une tentative de connexion vers 192.0.2.10 attendrait le délai de 15 s : le refus est immédiat.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Refus trop lent ({stopwatch.Elapsed}).");
    }

    [Fact]
    public async Task Status_ForeignTarget_StaysReadOnlyAndIsNotRefusedByPolicy()
    {
        // Port 1 sur la boucle locale : connexion refusée immédiatement (aucun serveur PostgreSQL contacté).
        var run = await RunAsync("status", "Host=127.0.0.1;Port=1;Database=mmv_v2_test;Username=mmv_v2_test;Password=x;Timeout=3");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("base injoignable", run.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("Refusé", run.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownCommand_IsUsageError()
    {
        var run = await RunAsync("migrate", "Host=localhost;Port=5433;Database=mmv_v2_dev;Username=mmv_v2_dev");

        Assert.Equal(1, run.ExitCode);
    }

    private sealed record ProcessRun(int ExitCode, string StandardOutput, string StandardError);

    private static async Task<ProcessRun> RunAsync(string command, string connectionString)
    {
        var assembly = Path.Combine(AppContext.BaseDirectory, "MMV.Maintenance.dll");
        Assert.True(File.Exists(assembly), $"Binaire introuvable : {assembly}");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add(assembly);
        startInfo.ArgumentList.Add(command);
        startInfo.Environment["ConnectionStrings__Mmv"] = connectionString;

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Processus non démarré.");
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(Deadline);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return new ProcessRun(process.ExitCode, await output, await error);
    }
}
