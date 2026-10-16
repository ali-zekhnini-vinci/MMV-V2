using MMV.Application.Identity;
using MMV.Domain.Identity;

namespace MMV.Api.Tests.Identity;

public sealed class BootstrapServiceTests
{
    private const string Password = "Mot-de-passe-bootstrap-1";
    private static readonly IPasswordHashing Hashing = RealHashing.Create();

    private readonly InMemoryIdentityStore _store = new();
    private readonly BootstrapService _bootstrap;

    public BootstrapServiceTests()
    {
        _bootstrap = new BootstrapService(_store, Hashing, new ManualClock(DateTimeOffset.UnixEpoch.AddYears(56)));
    }

    [Fact]
    public async Task FirstBootstrap_CreatesSingleSystemAdmin()
    {
        var result = await _bootstrap.CreateInitialAdministratorAsync("admin.local", "Administrateur", Password, CancellationToken.None);

        Assert.Equal(BootstrapStatus.Created, result.Status);
        var admin = Assert.Single(_store.Users);
        Assert.True(admin.HasPermission(Permissions.SystemAdmin));
        Assert.Equal(PasswordCheck.Success, Hashing.Verify(admin, admin.PasswordHash, Password));
        Assert.Equal(admin.Id, _store.Bootstrap!.AdministratorId);
    }

    [Fact]
    public async Task SecondBootstrap_IsRefused_AndCreatesNothing()
    {
        await _bootstrap.CreateInitialAdministratorAsync("admin.local", "Administrateur", Password, CancellationToken.None);

        var second = await _bootstrap.CreateInitialAdministratorAsync("autre", "Autre", Password, CancellationToken.None);

        Assert.Equal(BootstrapStatus.AlreadyCompleted, second.Status);
        Assert.Single(_store.Users);
    }

    [Theory]
    [InlineData("ab", "Nom", Password)]
    [InlineData("avec espace", "Nom", Password)]
    [InlineData("élodie", "Nom", Password)]
    [InlineData("admin", "", Password)]
    [InlineData("admin", "Nom", "court")]
    public async Task InvalidInput_IsRejectedWithoutCreation(string userName, string displayName, string password)
    {
        var result = await _bootstrap.CreateInitialAdministratorAsync(userName, displayName, password, CancellationToken.None);

        Assert.Equal(BootstrapStatus.Invalid, result.Status);
        Assert.NotEmpty(result.Errors);
        Assert.Empty(_store.Users);
        Assert.Null(_store.Bootstrap);
    }

    [Fact]
    public async Task ErrorMessages_NeverEchoThePassword()
    {
        var result = await _bootstrap.CreateInitialAdministratorAsync("admin", "Nom", "secret-x", CancellationToken.None);

        Assert.All(result.Errors, e => Assert.DoesNotContain("secret-x", e, StringComparison.Ordinal));
    }
}
