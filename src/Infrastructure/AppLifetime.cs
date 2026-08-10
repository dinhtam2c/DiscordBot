using Application.Abstractions;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

internal sealed class AppLifetime(IHostApplicationLifetime lifetime) : IAppLifetime
{
    public CancellationToken Stopping => lifetime.ApplicationStopping;
}
