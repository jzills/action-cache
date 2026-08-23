using ActionCache.Common.Concurrency;
using Microsoft.Data.SqlClient;

namespace ActionCache.SqlServer.Concurrency.Locks;

/// <summary>
/// Represents a lock backed by a SQL Server session-level application lock (sp_getapplock).
/// The open <see cref="SqlConnection"/> is held until <see cref="SqlServerCacheLocker.ReleaseLockAsync"/>
/// calls sp_releaseapplock and disposes it.
/// </summary>
public class SqlServerCacheLock : CacheLock
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerCacheLock"/> class.
    /// </summary>
    /// <param name="resource">The resource identifier passed to sp_getapplock.</param>
    /// <param name="lockTimeout">Maximum time to wait for acquisition.</param>
    public SqlServerCacheLock(string resource, TimeSpan lockTimeout) : base(resource)
    {
        Timeout = lockTimeout;
    }

    /// <summary>
    /// The open SQL connection that holds the session-level application lock.
    /// Kept alive until <see cref="SqlServerCacheLocker.ReleaseLockAsync"/> disposes it.
    /// </summary>
    internal SqlConnection? Connection { get; set; }
}
