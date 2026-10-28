namespace LinkShortener.Application.Links;

public interface ILinkCache
{
    Link? Get(string code);

    void Set(Link link);
}
