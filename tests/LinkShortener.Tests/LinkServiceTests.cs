using System.Net;
using LinkShortener.Application.Links;

namespace LinkShortener.Tests;

public sealed class LinkServiceTests
{
    [Fact]
    public async Task CreateAsyncRetriesWhenGeneratedCodeIsAlreadyInUse()
    {
        var repository = new StubLinkRepository(false, true);
        var generator = new StubLinkCodeGenerator("AAAAAAA", "BBBBBBB");
        var service = CreateService(repository, generator);

        var result = await service.CreateAsync("https://example.com");

        Assert.True(result.IsSuccess);
        Assert.Equal("BBBBBBB", result.Link?.Code);
        Assert.Equal(2, repository.Attempts);
        Assert.Equal(2, generator.Attempts);
    }

    [Fact]
    public async Task CreateAsyncReturnsUnavailableWhenAllGeneratedCodesAreInUse()
    {
        var repository = new StubLinkRepository(false, false, false, false, false);
        var generator = new StubLinkCodeGenerator(
            "AAAAAAA",
            "BBBBBBB",
            "CCCCCCC",
            "DDDDDDD",
            "EEEEEEE");
        var service = CreateService(repository, generator);

        var result = await service.CreateAsync("https://example.com");

        Assert.False(result.IsSuccess);
        Assert.Equal(503, result.ErrorStatusCode);
        Assert.Equal(5, repository.Attempts);
    }

    [Theory]
    [InlineData("ABCD")]
    [InlineData("ABC123XYZ987654321AB")]
    public async Task CreateAsyncAcceptsValidCustomCode(string customCode)
    {
        var repository = new StubLinkRepository(true);
        var generator = new StubLinkCodeGenerator();
        var service = CreateService(repository, generator);

        var result = await service.CreateAsync("https://example.com", customCode);

        Assert.True(result.IsSuccess);
        Assert.Equal(customCode, result.Link?.Code);
        Assert.Equal(1, repository.Attempts);
        Assert.Equal(0, generator.Attempts);
    }

    [Theory]
    [InlineData("ABC")]
    [InlineData("ABC0")]
    [InlineData("abcD")]
    [InlineData("AB C")]
    [InlineData("AB-C")]
    [InlineData("ABC123XYZ987654321ABC")]
    public async Task CreateAsyncRejectsInvalidCustomCode(string customCode)
    {
        var repository = new StubLinkRepository();
        var generator = new StubLinkCodeGenerator();
        var service = CreateService(repository, generator);

        var result = await service.CreateAsync("https://example.com", customCode);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.ErrorStatusCode);
        Assert.Equal(0, repository.Attempts);
        Assert.Equal(0, generator.Attempts);
    }

    [Fact]
    public async Task CreateAsyncReturnsConflictWhenCustomCodeIsAlreadyInUse()
    {
        var repository = new StubLinkRepository(false);
        var generator = new StubLinkCodeGenerator();
        var service = CreateService(repository, generator);

        var result = await service.CreateAsync("https://example.com", "MYCODE");

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.ErrorStatusCode);
        Assert.Equal(1, repository.Attempts);
        Assert.Equal(0, generator.Attempts);
    }

    [Fact]
    public async Task RedirectAsyncUsesCacheForRepeatedAccessesAndStillCountsEveryAccess()
    {
        var link = new Link(
            "CACHE99",
            "https://example.com/cached",
            DateTime.UtcNow,
            null);
        var repository = new StubLinkRepository(link);
        var cache = new StubLinkCache();
        var service = CreateService(repository, new StubLinkCodeGenerator(), cache);

        var firstResult = await service.RedirectAsync("CACHE99", CancellationToken.None);
        var secondResult = await service.RedirectAsync("CACHE99", CancellationToken.None);

        Assert.Equal("https://example.com/cached", firstResult.TargetUrl);
        Assert.Equal("https://example.com/cached", secondResult.TargetUrl);
        Assert.Equal(1, repository.ReadAttempts);
        Assert.Equal(2, repository.AccessCountIncrements);
        Assert.Equal(1, cache.SetAttempts);
    }

    private static LinkService CreateService(
        ILinkRepository repository,
        ILinkCodeGenerator codeGenerator,
        ILinkCache? cache = null)
    {
        var validator = new TargetUrlValidator(new PublicHostResolver());
        return new LinkService(
            repository,
            cache ?? new StubLinkCache(),
            codeGenerator,
            validator,
            new StubManagementKeyGenerator());
    }

    private sealed class StubLinkRepository : ILinkRepository
    {
        private readonly Queue<bool> _addResults;
        private readonly Link? _link;

        public StubLinkRepository(params bool[] addResults)
        {
            _addResults = new Queue<bool>(addResults);
        }

        public StubLinkRepository(Link link)
        {
            _addResults = new Queue<bool>();
            _link = link;
        }

        public int Attempts { get; private set; }

        public int ReadAttempts { get; private set; }

        public int AccessCountIncrements { get; private set; }

        public Task<bool> TryAddAsync(Link link, CancellationToken cancellationToken)
        {
            Attempts++;
            return Task.FromResult(_addResults.Dequeue());
        }

        public Task<Link?> GetByCodeAsync(string code, CancellationToken cancellationToken)
        {
            ReadAttempts++;
            return Task.FromResult(_link?.Code == code ? _link : null);
        }

        public Task<bool> IncrementAccessCountAsync(string code, CancellationToken cancellationToken)
        {
            AccessCountIncrements++;
            return Task.FromResult(true);
        }
    }

    private sealed class StubLinkCache : ILinkCache
    {
        private readonly Dictionary<string, Link> _links = new(StringComparer.Ordinal);

        public int SetAttempts { get; private set; }

        public Link? Get(string code)
        {
            return _links.GetValueOrDefault(code);
        }

        public void Set(Link link)
        {
            SetAttempts++;
            _links[link.Code] = link;
        }
    }

    private sealed class StubLinkCodeGenerator(params string[] codes) : ILinkCodeGenerator
    {
        private readonly Queue<string> _codes = new(codes);

        public int Attempts { get; private set; }

        public string Generate()
        {
            Attempts++;
            return _codes.Dequeue();
        }
    }

    private sealed class StubManagementKeyGenerator : IManagementKeyGenerator
    {
        public string Generate()
        {
            return "test-management-key";
        }
    }

    private sealed class PublicHostResolver : ILinkHostResolver
    {
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            return Task.FromResult(new[] { IPAddress.Parse("93.184.216.34") });
        }
    }
}
