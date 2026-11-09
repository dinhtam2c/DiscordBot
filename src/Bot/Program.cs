using Infrastructure;
using Modules.Music;
using Modules.Utility;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure();
builder.Services.AddUtilityModule();
builder.Services.AddMusicModule();

await builder.Build().RunAsync();
