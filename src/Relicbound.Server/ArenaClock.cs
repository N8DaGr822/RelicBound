using Relicbound.Simulation.World;

namespace Relicbound.Server;

public sealed class ArenaClock(GameWorld world) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                world.Advance();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host Shutdown: Cancellation ends the tick loop without a fault.
        }
    }
}
