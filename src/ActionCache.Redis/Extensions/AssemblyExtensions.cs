using System.Collections.Concurrent;
using System.Reflection;

namespace ActionCache.Redis.Extensions;

/// <summary>
/// Extensions for working with Assemblies and embedded resources.
/// </summary>
internal static class AssemblyExtensions
{
    /// <summary>
    /// Assembly cache to store embedded resources. 
    /// </summary> 
    private static ConcurrentDictionary<string, string> _cache = new();

    /// <summary>
    /// Tries to retrieve the embedded resource as text from the Assembly.
    /// </summary>
    /// <param name="source">The Assembly to retrieve the resource from.</param>
    /// <param name="name">The name of the embedded resource.</param>
    /// <param name="resource">The text content of the resource if found.</param>
    /// <returns>True if the resource is successfully retrieved, false otherwise.</returns>
    internal static bool TryGetResourceAsText(this Assembly source, string name, out string resource)
    {
        if (_cache.TryGetValue(name, out resource!))
        {
            return true;
        }
        else if (source.TryReadResourceStream(name, out resource))
        {
            _cache.TryAdd(name, resource);
            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the name of the embedded resource that matches the specified file name.
    /// </summary>
    /// <param name="source">The Assembly to search for the resource.</param>
    /// <param name="fileName">The file name to match against.</param>
    /// <returns>The name of the matching embedded resource or an empty string if not found.</returns>
    internal static string GetResourceName(this Assembly source, string fileName)
    {
        var resourceName = source
            .GetManifestResourceNames()
            .FirstOrDefault(resource => resource.EndsWith(fileName));

        return resourceName ?? string.Empty;
    }

    /// <summary>
    /// Attempts to read an embedded resource stream from the specified assembly.
    /// </summary>
    /// <param name="source">The assembly containing the embedded resource.</param>
    /// <param name="name">The name of the resource to read.</param>
    /// <param name="resource">
    /// When this method returns, contains the content of the resource as a string if successful; otherwise, null.
    /// </param>
    /// <returns><c>true</c> if the resource was successfully read; otherwise, <c>false</c>.</returns>
    private static bool TryReadResourceStream(this Assembly source, string name, out string resource)
    {
        try
        {
            var resourceName = source.GetResourceName(name);
            if (!string.IsNullOrWhiteSpace(resourceName))
            {
                using var stream = source.GetManifestResourceStream(resourceName);
                using var reader = new StreamReader(stream!);
                resource = reader.ReadToEnd();
                return true;
            }
            else
            {
                resource = default!;
                return false;
            }
        }
        catch (Exception)
        {
            resource = default!;
            return false;
        }
    }
}