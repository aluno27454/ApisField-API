using System.Security.Cryptography;

namespace ApisField.Api.Data;

/// <summary>Hash e verificação de palavras-passe com PBKDF2-SHA256, só com a biblioteca standard do .NET.</summary>
/// <remarks>
/// Formato guardado: <c>{iterações}.{salt em Base64}.{hash em Base64}</c>.
/// As iterações ficam no próprio valor, para se poder aumentar <see cref="Iterations"/> no futuro
/// sem invalidar os hashes que já existem.
/// </remarks>
public static class PasswordHasher
{
    /// <summary>Iterações usadas nos hashes novos. Mais iterações tornam os ataques por força bruta mais lentos.</summary>
    private const int Iterations = 100_000;

    private const int SaltSize = 16;
    private const int HashSize = 32;

    /// <summary>Gera o hash de <paramref name="password"/> com um salt aleatório novo.</summary>
    /// <returns>O valor a guardar em <see cref="User.PasswordHash"/>.</returns>
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    /// <summary>Indica se <paramref name="password"/> corresponde ao hash guardado.</summary>
    /// <param name="password">Palavra-passe introduzida pelo utilizador.</param>
    /// <param name="stored">Valor guardado, gerado por <see cref="Hash"/>.</param>
    /// <returns><c>false</c> também quando <paramref name="stored"/> não tem o formato esperado.</returns>
    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations)) return false;

        var salt = Convert.FromBase64String(parts[1]);
        var expected = Convert.FromBase64String(parts[2]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

        // Comparação em tempo constante, para não revelar pelo tempo de resposta quantos bytes coincidem.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
