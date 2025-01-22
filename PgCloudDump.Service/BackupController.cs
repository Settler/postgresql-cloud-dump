using Microsoft.AspNetCore.Mvc;

namespace PgCloudDump.Service;

[ApiController]
[Route("[controller]")]
public class BackupController(BackupJob backupJob, RestoreService restoreService) : ControllerBase
{
    [HttpPost("[action]")]
    public Task BackupNow(CancellationToken cancellationToken = default)
    {
        return backupJob.DoWork(cancellationToken);
    }

    [HttpPost("[action]")]
    public Task Restore(CancellationToken cancellationToken = default)
    {
        return restoreService.RestoreAsync(cancellationToken);
    }
}