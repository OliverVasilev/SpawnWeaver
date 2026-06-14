namespace Platform.Contracts.Http;

/// <summary>Response for a successful storage write.</summary>
public sealed record StorageSavedResponse(string Key, DateTimeOffset UpdatedAtUtc);

/// <summary>Response for reading a stored value.</summary>
public sealed record StorageValueResponse(string Key, string Value, DateTimeOffset UpdatedAtUtc);

/// <summary>Response listing a player's stored keys.</summary>
public sealed record StorageKeysResponse(IReadOnlyList<string> Keys);
