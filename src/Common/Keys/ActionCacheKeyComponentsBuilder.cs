using System.Collections.Specialized;
using System.Web;
using ActionCache.Common.Extensions.Internal;
using Microsoft.AspNetCore.Routing;

namespace ActionCache.Common.Keys;

/// <summary>
/// Builds an <see cref="ActionCacheKeyComponents"/> object by parsing and decoding a query string value.
/// This class is used to generate cache key components, including route values and action arguments.
/// </summary>
public class ActionCacheKeyComponentsBuilder
{
    /// <summary>
    /// A collection of name-value pairs extracted from the query string after decryption.
    /// </summary>
    protected readonly NameValueCollection NameValues;

    /// <summary>
    /// A generator used to handle key decoding during the construction of cache key components.
    /// </summary>
    protected readonly KeyEncoder KeyEncoder = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionCacheKeyComponentsBuilder"/> class.
    /// Decodes the given value and parses it into a <see cref="NameValueCollection"/>.
    /// </summary>
    /// <param name="value">The encoded string value that contains the query string to parse.</param>
    public ActionCacheKeyComponentsBuilder(string value)
    {
        NameValues = HttpUtility.ParseQueryString(KeyEncoder.Decode(value));
    }

    /// <summary>
    /// Builds an <see cref="ActionCacheKeyComponents"/> object by extracting and deserializing route values and action arguments 
    /// from the parsed query string.
    /// </summary>
    /// <returns>A new <see cref="ActionCacheKeyComponents"/> object populated with route values and action arguments.</returns>
    public ActionCacheKeyComponents Build() =>
        new ActionCacheKeyComponents 
        { 
            RouteValues     = NameValues.ParseValueAsJson<RouteValueDictionary>(ActionCacheKeyComponents.RouteValuesKey), 
            ActionArguments = NameValues.ParseValueAsJson<Dictionary<string, object?>>(ActionCacheKeyComponents.ActionArgumentsKey)
        };
}