using MMV.Application.Identity;
using MMV.Domain.Identity;

namespace MMV.Api.Tests.Identity;

/// <summary>Règles de connexion et de session (hachage Identity réel, store non persistant).</summary>
public sealed class AuthServiceTests
{
    private const string Password = "Mot-de-passe-de-test-1";
    private static readonly IPasswordHashing Hashing = RealHashing.Create();

    private readonly InMemoryIdentityStore _store = new();
    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 10, 16, 8, 0, 0, TimeSpan.Zero));
    private readonly AuthOptions _options = new();
    private readonly AuthService _auth;

    public AuthServiceTests()
    {
        _auth = new AuthService(_store, Hashing, _options, _clock);
    }

    [Fact]
    public void PasswordIsStoredAsIdentityV3HashNeverInClear()
    {
        var user = _store.AddUser(Hashing, "alice", Password);

        Assert.DoesNotContain(Password, user.PasswordHash, StringComparison.Ordinal);
        // Format V3 d'ASP.NET Core Identity : premier octet 0x01 (base64 « AQAAAA… »).
        Assert.StartsWith("AQAAAA", user.PasswordHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidCredentials_OpenSessionWithOpaqueTokenAndHashedStorage()
    {
        _store.AddUser(Hashing, "alice", Password, Permissions.SystemAdmin);

        var result = await _auth.LoginAsync("  ALICE ", Password, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(43, result.AccessToken.Length);
        var session = Assert.Single(_store.Sessions);
        Assert.Equal(SessionTokens.TryHash(result.AccessToken), session.TokenHash);
        Assert.Equal(_clock.Now.AddHours(8), session.ExpiresAtUtc);
        Assert.Equal([Permissions.SystemAdmin], result.Session.User.Permissions);
    }

    [Fact]
    public async Task UnknownUserWrongPasswordAndDisabledAccount_AreIndistinguishable()
    {
        var disabled = _store.AddUser(Hashing, "bob", Password);
        disabled.Deactivate();
        _store.AddUser(Hashing, "alice", Password);

        Assert.Null(await _auth.LoginAsync("inconnu", Password, CancellationToken.None));
        Assert.Null(await _auth.LoginAsync("alice", "mauvais", CancellationToken.None));
        Assert.Null(await _auth.LoginAsync("bob", Password, CancellationToken.None));
        Assert.Empty(_store.Sessions);
    }

    [Fact]
    public async Task RepeatedFailures_LockAccountTemporarily_EvenForCorrectPassword()
    {
        var alice = _store.AddUser(Hashing, "alice", Password);

        for (var i = 0; i < _options.MaxFailedAttempts; i++)
        {
            Assert.Null(await _auth.LoginAsync("alice", "mauvais", CancellationToken.None));
        }

        Assert.True(alice.IsLockedOut(_clock.Now));
        Assert.Null(await _auth.LoginAsync("alice", Password, CancellationToken.None));

        _clock.Advance(_options.LockoutDuration + TimeSpan.FromSeconds(1));
        Assert.NotNull(await _auth.LoginAsync("alice", Password, CancellationToken.None));
        Assert.Equal(0, alice.FailedLoginCount);
    }

    [Fact]
    public async Task Session_IsRefusedAfterIdleTimeout_AndRevoked()
    {
        _store.AddUser(Hashing, "alice", Password);
        var login = (await _auth.LoginAsync("alice", Password, CancellationToken.None))!;

        _clock.Advance(_options.IdleTimeout - TimeSpan.FromMinutes(1));
        Assert.NotNull(await _auth.AuthenticateAsync(login.AccessToken, CancellationToken.None));

        _clock.Advance(_options.IdleTimeout + TimeSpan.FromSeconds(1));
        Assert.Null(await _auth.AuthenticateAsync(login.AccessToken, CancellationToken.None));
        Assert.Equal(SessionRevocationReasons.IdleTimeout, _store.Sessions[0].RevocationReason);
    }

    [Fact]
    public async Task Session_IsRefusedAfterAbsoluteLifetime_EvenWhenActive()
    {
        _store.AddUser(Hashing, "alice", Password);
        var login = (await _auth.LoginAsync("alice", Password, CancellationToken.None))!;

        for (var elapsed = TimeSpan.Zero; elapsed < _options.AbsoluteLifetime; elapsed += TimeSpan.FromMinutes(20))
        {
            _clock.Advance(TimeSpan.FromMinutes(20));
            if (_clock.Now < login.Session.ExpiresAtUtc)
            {
                Assert.NotNull(await _auth.AuthenticateAsync(login.AccessToken, CancellationToken.None));
            }
        }

        Assert.Null(await _auth.AuthenticateAsync(login.AccessToken, CancellationToken.None));
    }

    [Fact]
    public async Task Logout_RevokesSession()
    {
        _store.AddUser(Hashing, "alice", Password);
        var login = (await _auth.LoginAsync("alice", Password, CancellationToken.None))!;

        await _auth.LogoutAsync(login.Session.SessionId, CancellationToken.None);

        Assert.Null(await _auth.AuthenticateAsync(login.AccessToken, CancellationToken.None));
        Assert.Equal(SessionRevocationReasons.Logout, _store.Sessions[0].RevocationReason);
    }

    [Fact]
    public async Task Deactivation_RevokesExistingSessionsOnNextRequest()
    {
        var alice = _store.AddUser(Hashing, "alice", Password);
        var login = (await _auth.LoginAsync("alice", Password, CancellationToken.None))!;

        alice.Deactivate();

        Assert.Null(await _auth.AuthenticateAsync(login.AccessToken, CancellationToken.None));
        Assert.Equal(SessionRevocationReasons.AccountDisabled, _store.Sessions[0].RevocationReason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("trop-court")]
    [InlineData("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!")]
    public async Task MalformedTokens_AreRefusedWithoutLookup(string token)
    {
        Assert.Null(await _auth.AuthenticateAsync(token, CancellationToken.None));
    }

    [Fact]
    public void Tokens_AreUniqueAndUrlSafe()
    {
        var tokens = Enumerable.Range(0, 200).Select(_ => SessionTokens.NewToken()).ToHashSet();

        Assert.Equal(200, tokens.Count);
        Assert.All(tokens, t => Assert.Matches("^[A-Za-z0-9_-]{43}$", t));
    }
}
