using ActionCache.Common.Enums;

namespace ActionCache.Common.Validation;

/// <summary>
/// One cache attribute found on an endpoint: what it does, and to which namespace.
/// </summary>
/// <param name="Type">The operation the attribute declares.</param>
/// <param name="Namespace">The namespace the attribute names, as written.</param>
public readonly record struct ActionCacheDeclaration(FilterType Type, string Namespace);
