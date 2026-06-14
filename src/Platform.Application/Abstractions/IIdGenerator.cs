namespace Platform.Application.Abstractions;

/// <summary>Generates prefixed, URL-safe, collision-resistant identifiers (e.g. <c>proj_ab12…</c>).</summary>
public interface IIdGenerator
{
    string NewId(string prefix);
}
