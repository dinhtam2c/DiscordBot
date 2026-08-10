using Application;
using Bot;
using Infrastructure;
using Modules.Music;
using Modules.Utility;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure();

builder.Services.AddUtilityModule();
builder.Services.AddMusicModule();

builder.Services.AddHostedService<DiscordBotService>();

var host = builder.Build();
host.Run();
