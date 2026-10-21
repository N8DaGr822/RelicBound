using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Relicbound.Server;

namespace Relicbound.Server.Tests;

internal sealed class ArenaApplication : WebApplicationFactory<Program>
{
    public ManualTimeProvider Time { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
            // HTTP Tests: Freeze arena ticks so movement assertions do not race the AI.
            var clock = services.Single(service => service.ServiceType == typeof(IHostedService) &&
                service.ImplementationType == typeof(ArenaClock));
            services.Remove(clock);
        });
    }

    internal sealed class ManualTimeProvider : TimeProvider
    {
        private long _ticks = new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero).Ticks;

        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

        public void Advance(TimeSpan duration) => Interlocked.Add(ref _ticks, duration.Ticks);
    }
}
