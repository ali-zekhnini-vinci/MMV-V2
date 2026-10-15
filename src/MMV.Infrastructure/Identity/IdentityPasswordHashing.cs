using Microsoft.AspNetCore.Identity;
using MMV.Application.Identity;
using MMV.Domain.Identity;

namespace MMV.Infrastructure.Identity;

/// <summary>
/// Adaptateur de <see cref="PasswordHasher{TUser}"/> d'ASP.NET Core Identity (format V3 :
/// PBKDF2-HMAC-SHA512, sel aléatoire, nombre d'itérations du framework). Aucune cryptographie maison.
/// </summary>
internal sealed class IdentityPasswordHashing : IPasswordHashing
{
    private static readonly UserAccount DummyUser = new(Guid.Empty, "dummy", "dummy", DateTimeOffset.UnixEpoch);

    private readonly PasswordHasher<UserAccount> _hasher = new();
    private readonly Lazy<string> _dummyHash;

    public IdentityPasswordHashing()
    {
        // Hachage d'un secret aléatoire, calculé une fois : sert uniquement à égaliser le temps de réponse.
        _dummyHash = new Lazy<string>(() => _hasher.HashPassword(DummyUser, SessionTokens.NewToken()));
    }

    public string Hash(UserAccount user, string password) => _hasher.HashPassword(user, password);

    public PasswordCheck Verify(UserAccount user, string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(user, passwordHash, password) switch
        {
            PasswordVerificationResult.Success => PasswordCheck.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
            _ => PasswordCheck.Failed,
        };

    public void VerifyAgainstDummy(string password) =>
        _hasher.VerifyHashedPassword(DummyUser, _dummyHash.Value, password);
}
