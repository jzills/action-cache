using System.Web;
using ActionCache.Utilities;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;

namespace ActionCache.Common.Extensions.Internal;

/// <summary>
/// Provides extension methods for the <see cref="Namespace"/> class to handle route templates
/// and route values.
/// </summary>
internal static class NamespaceExtensions
{
    /// <summary>
    /// Determines if the <see cref="Namespace"/> instance contains route template parameters.
    /// </summary>
    /// <param name="source">The <see cref="Namespace"/> instance to check.</param>
    /// <returns><c>true</c> if the namespace contains route template parameters; otherwise, <c>false</c>.</returns>
    internal static bool ContainsRouteTemplateParameters(this Namespace source) =>
        RoutePatternFactory.Parse(source)?.Parameters?.Any() ?? false;

    /// <summary>
    /// Attaches route values to the <see cref="Namespace"/> instance by binding route template 
    /// parameters to the provided route values.
    /// </summary>
    /// <param name="source">The <see cref="Namespace"/> instance to attach route values to.</param>
    /// <param name="routeValues">The <see cref="RouteValueDictionary"/> containing the route values.</param>
    /// <param name="templateBinderFactory">The factory to create a template binder for binding route values.</param>
    internal static void AttachRouteValues(this Namespace source, 
        RouteValueDictionary routeValues, 
        TemplateBinderFactory templateBinderFactory
    )
    {
        if (source.ContainsRouteTemplateParameters())
        {
            var templateBinder = templateBinderFactory.Create(
                TemplateParser.Parse(source.Value), 
                new RouteValueDictionary()
            );

            var boundValues = templateBinder.BindValues(routeValues);
            if (!string.IsNullOrEmpty(boundValues))
            {
                // Strip any query-string suffix produced by the binder.
                var queryIndex = boundValues.IndexOf('?');
                if (queryIndex >= 0)
                {
                    boundValues = boundValues[..queryIndex];
                }

                // Strip the leading path separator.
                boundValues = boundValues.TrimStart('/');

                // Re-escape the fully-decoded value so delimiters embedded in
                // user-supplied route values (notably ':') cannot alter the
                // namespace's structural separators. Caching and eviction both
                // resolve the namespace through this same path, keeping the
                // escaped form consistent.
                source.ValueWithRouteTemplateParameters =
                    Uri.EscapeDataString(HttpUtility.UrlDecode(boundValues) ?? string.Empty);
            }
        }
    }
}