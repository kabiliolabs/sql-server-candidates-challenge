using SyncAgent.Core.Models;

namespace SyncAgent.Core.Interfaces;

public interface ISyncTaskValidator
{
    SyncTaskValidationResult Validate(SyncTask task);
}
