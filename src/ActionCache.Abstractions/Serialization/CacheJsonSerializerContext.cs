using System.Text.Json.Serialization;
using ActionCache.Common.Responses;

namespace ActionCache.Common.Serialization;

/// <summary>
/// Source-generated serialization metadata for the types ActionCache stores itself,
/// so the hot path needs no reflection and stays trim- and AOT-friendly.
/// </summary>
[JsonSerializable(typeof(CachedResponse))]
[JsonSerializable(typeof(CachedRequest))]
internal partial class CacheJsonSerializerContext : JsonSerializerContext
{
}
