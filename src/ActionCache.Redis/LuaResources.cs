namespace ActionCache.Redis;

/// <summary>
/// Provides constant file names for Lua scripts used in the application.
/// </summary>
internal static class LuaResources
{
    /// <summary>
    /// The filename for the Lua script responsible for removing an entry.
    /// </summary>
    internal const string Remove = $"{nameof(Remove)}.lua";

    /// <summary>
    /// The filename for the Lua script responsible for removing a namespace.
    /// </summary>
    internal const string RemoveNamespace = $"{nameof(RemoveNamespace)}.lua";

    /// <summary>
    /// The filename for the Lua script responsible for setting a hash entry.
    /// </summary>
    internal const string SetHash = $"{nameof(SetHash)}.lua";
}