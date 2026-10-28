using System.Security.Cryptography;

namespace LinkShortener.Application.Links;

public sealed class LinkCodeGenerator : ILinkCodeGenerator
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789";
    private const int CodeLength = 7;

    public string Generate()
    {
        return string.Create(
            CodeLength,
            Alphabet,
            static (code, alphabet) =>
            {
                for (var index = 0; index < code.Length; index++)
                {
                    code[index] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
                }
            });
    }
}
