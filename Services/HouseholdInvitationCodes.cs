using System.Security.Cryptography;
using System.Text;

namespace otw.fings.api.management.Services;

internal static class HouseholdInvitationCodes
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Generate()
    {
        Span<char> characters = stackalloc char[8];
        for (var index = 0; index < characters.Length; index++)
        {
            characters[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return $"FINGS-{new string(characters[..4])}-{new string(characters[4..])}";
    }

    public static string Normalize(string code) => code.Trim().ToUpperInvariant();

    public static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(code))));
}
