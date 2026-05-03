using ActionCache.Common.Concurrency;
using ActionCache.SqlServer.Concurrency.Locks;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ActionCache.SqlServer.Concurrency;

/// <summary>
/// A cache locker that uses SQL Server session-level application locks (sp_getapplock /
/// sp_releaseapplock) for true atomic, cross-process mutual exclusion.
/// </summary>
public class SqlServerCacheLocker : CacheLockerBase<SqlServerCacheLock>
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerCacheLocker"/> class.
    /// </summary>
    /// <param name="connectionString">Connection string used to open a dedicated lock connection.</param>
    /// <param name="lockDuration">Duration hint stored on acquired locks.</param>
    /// <param name="lockTimeout">Maximum time sp_getapplock will wait before returning -1.</param>
    public SqlServerCacheLocker(string connectionString, TimeSpan lockDuration, TimeSpan lockTimeout)
        : base(lockDuration, lockTimeout)
    {
        _connectionString = connectionString;
    }

    /// <inheritdoc/>
    public override Task<SqlServerCacheLock> TryAcquireLockAsync(string resource) =>
        AcquireLockAsync(resource, timeoutMs: 0);

    /// <inheritdoc/>
    public override Task<SqlServerCacheLock> WaitForLockAsync(string resource) =>
        AcquireLockAsync(resource, timeoutMs: (int)LockTimeout.TotalMilliseconds);

    /// <inheritdoc/>
    public override async Task ReleaseLockAsync(SqlServerCacheLock cacheLock)
    {
        if (cacheLock.Connection is null)
            return;

        try
        {
            using var cmd = new SqlCommand("sp_releaseapplock", cacheLock.Connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@Resource", cacheLock.Resource);
            cmd.Parameters.AddWithValue("@LockOwner", "Session");

            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            await cacheLock.Connection.DisposeAsync();
        }
    }

    private async Task<SqlServerCacheLock> AcquireLockAsync(string resource, int timeoutMs)
    {
        var cacheLock = new SqlServerCacheLock(resource, LockDuration, LockTimeout);
        var connection = new SqlConnection(_connectionString);

        try
        {
            await connection.OpenAsync();

            using var cmd = new SqlCommand("sp_getapplock", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@Resource", resource);
            cmd.Parameters.AddWithValue("@LockMode", "Exclusive");
            cmd.Parameters.AddWithValue("@LockOwner", "Session");
            cmd.Parameters.AddWithValue("@LockTimeout", timeoutMs);

            var returnParam = new SqlParameter
            {
                ParameterName = "@ReturnValue",
                SqlDbType = SqlDbType.Int,
                Direction = ParameterDirection.ReturnValue
            };
            cmd.Parameters.Add(returnParam);

            await cmd.ExecuteNonQueryAsync();

            // sp_getapplock return codes: 0 = granted immediately, 1 = granted after wait.
            // Negative values indicate failure (-1 = timeout, -2 = cancelled, -3 = deadlock victim).
            cacheLock.IsAcquired = (int)returnParam.Value is 0 or 1;

            if (cacheLock.IsAcquired)
            {
                cacheLock.Connection = connection;
            }
            else
            {
                await connection.DisposeAsync();
            }
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        return cacheLock;
    }
}
