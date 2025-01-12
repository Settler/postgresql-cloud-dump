using EasyCronJob.Core;
using PgCloudDump.Service;
using Serilog;

Log.Logger = new LoggerConfiguration()
             .WriteTo.Console()
             .CreateLogger();

try
{
    Log.Information("Starting web application");
    
    var builder = WebApplication.CreateBuilder(args);
    var logger = new LoggerConfiguration()
                 .ReadFrom.Configuration(builder.Configuration)
                 .Enrich.FromLogContext()
                 .CreateLogger();

    builder.Services.Configure<BackupOptions>(builder.Configuration.GetSection(nameof(BackupOptions)));

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddSingleton<BackupJob>();
    builder.Services.ApplyResulation<BackupJob>(options =>
                                                {
                                                    var backupOptions = builder.Configuration.GetRequiredSection(nameof(BackupOptions)).Get<BackupOptions>();
                                                    options.CronExpression = backupOptions.CronExpression;
                                                    options.TimeZoneInfo = TimeZoneInfo.Local;
                                                });

    builder.Services.AddSerilog(logger);

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}