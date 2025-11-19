using Microsoft.EntityFrameworkCore;
using ZKAttendanceService;
using ZKAttendanceService.Configuration;
using ZKAttendanceService.Services;
using ZKAttendanceService.Data;

var builder = Host.CreateApplicationBuilder(args);

// DbContext
builder.Services.AddDbContext<ZKAttendanceWebDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.CommandTimeout(900);
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 10,
                maxRetryDelay: TimeSpan.FromSeconds(60),
                errorNumbersToAdd: null
            );
            sqlOptions.MaxBatchSize(100);
        }
    ),
    ServiceLifetime.Scoped
);

// Configuration
builder.Services.Configure<BranchConfiguration>(
    builder.Configuration.GetSection("BranchConfiguration"));

builder.Services.Configure<DeviceConfiguration>(
    builder.Configuration.GetSection("DeviceConfiguration"));

builder.Services.Configure<SyncConfiguration>(
    builder.Configuration.GetSection("SyncConfiguration"));

// Services
builder.Services.AddScoped<IZKDeviceService, ZKDeviceService>();
builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddScoped<IConfigurationService, ConfigurationService>();
builder.Services.AddScoped<PeakHourService>();  

// Worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();
