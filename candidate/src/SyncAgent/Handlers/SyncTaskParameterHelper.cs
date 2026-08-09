using SyncAgent.Models;
using System;

namespace SyncAgent.Handlers;

internal static class SyncTaskParameterHelper
{
    public static DateTime GetModifiedSinceUtc(SyncTask task)
    {
        var value = task.Parameters.ModifiedSince
            ?? throw new ArgumentException(
                "modifiedSince is required.");

        return value.UtcDateTime;
    }
}