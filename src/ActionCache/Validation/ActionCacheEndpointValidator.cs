using ActionCache.Attributes;
using ActionCache.Common.Enums;
using ActionCache.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace ActionCache.Common.Validation;

/// <summary>
/// Validates every endpoint's cache attributes when the host starts.
/// </summary>
/// <remarks>
/// <para>
/// Runs over <see cref="EndpointDataSource"/>, which covers controller actions and Minimal API
/// endpoints alike: MVC projects an action's attributes into endpoint metadata, so one pass
/// sees both hosting models.
/// </para>
/// <para>
/// An <see cref="IStartupFilter"/> rather than an <see cref="Microsoft.Extensions.Hosting.IHostedService"/>,
/// because endpoints do not exist until the request pipeline is built. A hosted service
/// registered from <c>AddActionCache</c> starts before the web host's own and sees an empty
/// endpoint collection, so it would validate nothing and pass. Running after <c>next(app)</c>
/// puts this after every Map call, and throwing here still aborts startup.
/// </para>
/// </remarks>
internal sealed class ActionCacheEndpointValidator : IStartupFilter
{
    private readonly EndpointDataSource _endpointDataSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionCacheEndpointValidator"/> class.
    /// </summary>
    /// <param name="endpointDataSource">The application's endpoints.</param>
    public ActionCacheEndpointValidator(EndpointDataSource endpointDataSource) =>
        _endpointDataSource = endpointDataSource;

    /// <summary>
    /// Builds the pipeline, then validates every endpoint it registered.
    /// </summary>
    /// <param name="next">The next configuration step in the startup chain.</param>
    /// <returns>A configuration action that validates once the pipeline is built.</returns>
    /// <exception cref="ConflictingCacheAttributesException">One or more endpoints conflict.</exception>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        builder =>
        {
            next(builder);
            Validate();
        };

    /// <summary>
    /// Checks every endpoint's cache attributes, collecting all conflicts before throwing.
    /// </summary>
    /// <exception cref="ConflictingCacheAttributesException">One or more endpoints conflict.</exception>
    private void Validate()
    {
        var conflicts = new List<string>();

        foreach (var endpoint in _endpointDataSource.Endpoints)
        {
            var declarations = GetDeclarations(endpoint);
            if (declarations.Count == 0)
            {
                continue;
            }

            // Reported for every offending endpoint rather than the first, so an author fixes
            // them in one pass instead of restarting to discover the next one.
            if (ActionCacheDeclarationConflict.Detect(declarations) is { } conflict)
            {
                conflicts.Add($"{Describe(endpoint)} {conflict}");
            }
        }

        if (conflicts.Count > 0)
        {
            throw new ConflictingCacheAttributesException(conflicts);
        }
    }

    /// <summary>
    /// Collects the cache attributes declared on one endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint to inspect.</param>
    /// <returns>One declaration per distinct attribute.</returns>
    private static List<ActionCacheDeclaration> GetDeclarations(Endpoint endpoint)
    {
        // MVC adds each action attribute to endpoint metadata twice -- the same instance, from
        // the action descriptor and from the attribute collection -- so counting without
        // de-duplicating would report every cached controller action as declaring [ActionCache]
        // twice. Reference identity is the right key: two attributes an author actually wrote
        // are two objects, however alike their namespaces.
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var declarations = new List<ActionCacheDeclaration>();

        void Collect<TAttribute>(FilterType type) where TAttribute : Attribute
        {
            foreach (var attribute in endpoint.Metadata.GetOrderedMetadata<TAttribute>())
            {
                if (seen.Add(attribute))
                {
                    declarations.Add(new ActionCacheDeclaration(type, GetNamespace(attribute)));
                }
            }
        }

        Collect<ActionCacheAttribute>(FilterType.Add);
        Collect<ActionCacheEvictionAttribute>(FilterType.Evict);
        Collect<ActionCacheRefreshAttribute>(FilterType.Refresh);

        return declarations;
    }

    /// <summary>
    /// Reads the namespace an attribute names.
    /// </summary>
    /// <param name="attribute">The cache attribute.</param>
    /// <returns>The namespace as written.</returns>
    private static string GetNamespace(Attribute attribute) => attribute switch
    {
        ActionCacheAttribute cache            => cache.Namespace,
        ActionCacheEvictionAttribute eviction => eviction.Namespace,
        ActionCacheRefreshAttribute refresh   => refresh.Namespace,
        _                                     => string.Empty
    };

    /// <summary>
    /// Names an endpoint in a way an author can locate.
    /// </summary>
    /// <param name="endpoint">The endpoint to describe.</param>
    /// <returns>The route pattern where there is one, otherwise the display name.</returns>
    private static string Describe(Endpoint endpoint) =>
        endpoint is RouteEndpoint route
            ? $"\"{route.RoutePattern.RawText}\" ({endpoint.DisplayName})"
            : $"\"{endpoint.DisplayName}\"";
}
