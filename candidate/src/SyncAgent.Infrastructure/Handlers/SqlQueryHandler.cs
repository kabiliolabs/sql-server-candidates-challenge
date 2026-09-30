using Dapper;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Models;
using SyncAgent.Infrastructure.Data;
using SyncAgent.Infrastructure.Options;

namespace SyncAgent.Infrastructure.Handlers
{
    public abstract class SqlQueryHandler<TRecord>(
        IDbConnectionFactory connectionFactory,
        IOptions<SyncOptions> options,
        ILogger logger) : ISyncTaskHandler where TRecord : class
    {
        public abstract string TaskType { get; }

        protected abstract string Sql { get; }

        public async Task<IReadOnlyList<object>> ExecuteAsync(SyncQuery query, CancellationToken cancellationToken)
        {
            var maxRecords = options.Value.MaxRecords;

            await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

            var command = new CommandDefinition(
                Sql,
                new { ModifiedSince = query.ModifiedSinceUtc, MaxRecords = maxRecords },
                commandTimeout: options.Value.CommandTimeoutSeconds,
                cancellationToken: cancellationToken);

            var records = (await connection.QueryAsync<TRecord>(command)).AsList();

            if (records.Count >= maxRecords)
                logger.LogWarning("{TaskType} hit the MaxRecords limit ({MaxRecords}); results are truncated.",
                    TaskType, maxRecords);

            return records;
        }
    }
}