using LinkShortener.Application.Links;

namespace LinkShortener.Tests;

public sealed class LinkCodeGeneratorTests
{
    [Fact]
    public void GenerateReturnsSevenCharactersFromAllowedAlphabet()
    {
        var generator = new LinkCodeGenerator();

        for (var attempt = 0; attempt < 100; attempt++)
        {
            var code = generator.Generate();

            Assert.Equal(7, code.Length);
            Assert.All(code, character =>
                Assert.Contains(character, "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789"));
        }
    }
}
