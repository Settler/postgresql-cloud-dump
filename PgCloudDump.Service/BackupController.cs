using Microsoft.AspNetCore.Mvc;

namespace PgCloudDump.Service;

[ApiController]
[Route("[controller]")]
public class BackupController(BackupJob backupJob) : ControllerBase
{
    [HttpPost("[action]")]
    public Task BackupNow(CancellationToken cancellationToken = default)
    {
        return backupJob.DoWork(cancellationToken);
    }
}