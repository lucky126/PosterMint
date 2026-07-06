using PosterMint.Infrastructure.Extensions;
using PosterMint.Infrastructure.Persistence;
using PosterMint.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddPosterMintInfrastructure(builder.Configuration);
builder.Services.AddHostedService<DatabaseHeartbeatWorker>();

var host = builder.Build();
await host.Services.InitializeDatabaseAsync();
await host.RunAsync();
