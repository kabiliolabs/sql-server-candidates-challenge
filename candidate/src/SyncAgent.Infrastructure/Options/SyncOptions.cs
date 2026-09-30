using System.ComponentModel.DataAnnotations;

namespace SyncAgent.Infrastructure.Options
{
    public sealed class SyncOptions
    {
        public const string SectionName = "Sync";

        [Range(1, 100_000)]
        public int MaxRecords { get; init; } = 5000;

        [Range(1, 600)]
        public int CommandTimeoutSeconds { get; init; } = 30;
    }
}