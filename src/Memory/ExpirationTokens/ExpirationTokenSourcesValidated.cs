namespace ActionCache.Memory;

/// <summary>
/// Represents a class that provides validated expirationToken sources.
/// </summary>
public class ExpirationTokenSourcesValidated : IExpirationTokenSources
{
    /// <summary>
    /// An implementation of <see cref="IExpirationTokenSources"/>. 
    /// </summary>
    protected readonly IExpirationTokenSources Next;

    /// <summary>
    /// Initializes a new instance of the ExpirationTokenSourcesValidated class with the specified IExpirationTokenSources.
    /// </summary>
    /// <param name="next">The next IExpirationTokenSources.</param>
    public ExpirationTokenSourcesValidated(IExpirationTokenSources next) => Next = next;

    /// <summary>
    /// Tries to get or add a CancellationTokenSource for the specified key.
    /// </summary>
    /// <param name="key">The key to try to get or add.</param>
    /// <param name="cancellationTokenSource">When this method returns, contains the CancellationTokenSource associated with the key.</param>
    /// <returns>True if the CancellationTokenSource was successfully retrieved or added; otherwise, false.</returns>
    public bool TryGetOrAdd(string key, out CancellationTokenSource cancellationTokenSource)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            return Next.TryGetOrAdd(key, out cancellationTokenSource);
        }
        else
        {
            cancellationTokenSource = default!;
            return false;
        }
    }

    /// <inheritdoc/>
    public void Reset(string key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            Next.Reset(key);
        }
    }
}
