using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ActionCache.Common.Serialization;

/// <summary>
/// A serialization binder that restricts type resolution to assemblies already loaded
/// in the current AppDomain and explicitly blocks known deserialization gadget-chain namespaces.
/// </summary>
internal class SafeSerializationBinder : ISerializationBinder
{
    // Known prefixes used in published Newtonsoft.Json gadget chains.
    private static readonly string[] BlockedTypePrefixes =
    [
        "System.Windows",
        "System.Workflow",
        "System.Web.Security",
        "Microsoft.Exchange",
        "Microsoft.IdentityModel",
    ];

    /// <inheritdoc/>
    public Type BindToType(string? assemblyName, string typeName)
    {
        if (BlockedTypePrefixes.Any(prefix => typeName.StartsWith(prefix, StringComparison.Ordinal)))
            throw new JsonSerializationException(
                $"Type '{typeName}' is not permitted for deserialization.");

        // Only resolve against assemblies already present in the AppDomain — this prevents
        // an attacker from triggering the load of arbitrary assemblies via $type.
        var type = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => assembly.GetName().Name == assemblyName)
            .Select(assembly => assembly.GetType(typeName))
            .FirstOrDefault(resolved => resolved is not null);

        return type ?? throw new JsonSerializationException(
            $"Type '{typeName}' from assembly '{assemblyName}' could not be resolved.");
    }

    /// <inheritdoc/>
    public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
    {
        assemblyName = serializedType.Assembly.GetName().Name;
        typeName = serializedType.FullName;
    }
}
