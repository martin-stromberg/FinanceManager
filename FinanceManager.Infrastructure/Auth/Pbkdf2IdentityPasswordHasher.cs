using System;
using FinanceManager.Domain.Users;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;

namespace FinanceManager.Infrastructure.Auth;

/// <summary>
/// Helper service for password hashing and verification used by infrastructure components.
/// Provides a simple API to create and verify password hashes independent of ASP.NET Identity.
/// </summary>
public interface IPasswordHashingService
{
    /// <summary>
    /// Computes a salted hash for the provided plain-text password.
    /// </summary>
    /// <param name="password">The plain-text password to hash. Must not be <c>null</c>.</param>
    /// <returns>A string representation of the generated password hash including algorithm and salt metadata.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="password"/> is <c>null</c>.</exception>
    string Hash(string password);

    /// <summary>
    /// Verifies whether the provided plain-text password matches the stored hash.
    /// </summary>
    /// <param name="providedPassword">The plain-text password provided by the user.</param>
    /// <param name="storedHash">The stored password hash to verify against (as produced by <see cref="Hash"/>).</param>
    /// <returns><c>true</c> when the password matches the stored hash; otherwise <c>false</c>.</returns>
    bool Verify(string providedPassword, string storedHash);
}

/// <summary>
/// PBKDF2-based password hasher that implements both <see cref="IPasswordHasher{User}"/> for ASP.NET Identity
/// and <see cref="IPasswordHashingService"/> for internal usage. Uses HMACSHA256 with a configurable iteration count.
/// </summary>
public sealed class Pbkdf2IdentityPasswordHasher : IPasswordHasher<User>, IPasswordHashingService
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    /// <summary>
    /// Computes a salted PBKDF2 hash for the specified password.
    /// The returned string encodes algorithm, iteration count, salt and derived key and can be stored as the password hash.
    /// </summary>
    /// <param name="password">The plain-text password to hash. Must not be <c>null</c>.</param>
    /// <returns>A formatted hash string containing algorithm, iterations, salt and key.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="password"/> is <c>null</c>.</exception>
    public string Hash(string password)
    {
        if (password is null) throw new ArgumentNullException(nameof(password));
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, Iterations, KeySize);
        return $"pbkdf2|{Iterations}|{Convert.ToBase64String(salt)}|{Convert.ToBase64String(key)}";
    }

    /// <summary>
    /// Verifies a provided password against the stored hash string.
    /// </summary>
    /// <param name="providedPassword">The plain-text password provided for verification.</param>
    /// <param name="storedHash">The stored hash string created by <see cref="Hash"/>.</param>
    /// <returns><c>true</c> when the provided password matches the stored hash; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// The method performs robust validation of the stored format and returns <c>false</c> for any malformed
    /// input or parsing errors instead of throwing, to avoid leaking hash format details to callers.
    /// </remarks>
    public bool Verify(string providedPassword, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(providedPassword))
            return false;

        var parts = storedHash.Split('|');
        if (parts.Length != 4 || parts[0] != "pbkdf2")
            return false;

        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var key = Convert.FromBase64String(parts[3]);
            var attempted = KeyDerivation.Pbkdf2(providedPassword, salt, KeyDerivationPrf.HMACSHA256, iterations, key.Length);
            return CryptographicOperations.FixedTimeEquals(key, attempted);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Identity-compatible wrapper that computes a password hash for the provided user/password pair.
    /// Delegates to <see cref="Hash"/>.
    /// </summary>
    /// <param name="user">The user for which the password is hashed. Argument is unused by this implementation but kept to match the contract.</param>
    /// <param name="password">The plain-text password to hash.</param>
    /// <returns>The computed password hash string.</returns>
    public string HashPassword(User user, string password) => Hash(password);

    /// <summary>
    /// Identity-compatible verification method. Checks the provided password against the stored hashed password.
    /// </summary>
    /// <param name="user">The user being validated. Not used by this implementation.</param>
    /// <param name="hashedPassword">The stored password hash.</param>
    /// <param name="providedPassword">The plain-text password provided for verification.</param>
    /// <returns>A <see cref="PasswordVerificationResult"/> indicating success or failure.</returns>
    public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword)
        => Verify(providedPassword, hashedPassword) ? PasswordVerificationResult.Success : PasswordVerificationResult.Failed;
}
