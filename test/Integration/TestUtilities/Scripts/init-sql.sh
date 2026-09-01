#!/bin/bash
set -e

/opt/mssql/bin/sqlservr &
pid=$!

echo "Waiting for SQL Server..."
for i in $(seq 1 60); do
    if /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -No -Q "SELECT 1" &>/dev/null 2>&1; then
        echo "SQL Server ready"
        break
    fi
    sleep 2
done

/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -No -Q "
    IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'ActionCache')
    CREATE DATABASE ActionCache
"

/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -No -d ActionCache -Q "
    IF NOT EXISTS (
        SELECT * FROM sys.objects
        WHERE object_id = OBJECT_ID(N'[dbo].[DistributedCache]') AND type = N'U'
    )
    CREATE TABLE [dbo].[DistributedCache] (
        [Id]                         nvarchar(449) COLLATE SQL_Latin1_General_CP1_CS_AS NOT NULL,
        [Value]                      varbinary(MAX) NOT NULL,
        [ExpiresAtTime]              datetimeoffset(7) NOT NULL,
        [SlidingExpirationInSeconds] bigint NULL,
        [AbsoluteExpiration]         datetimeoffset(7) NULL,
        PRIMARY KEY CLUSTERED ([Id] ASC)
    )
"

echo "Initialization complete"
wait $pid
