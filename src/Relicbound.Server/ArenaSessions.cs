using System.Security.Cryptography;
using Relicbound.Contracts;
using Relicbound.Simulation.World;

namespace Relicbound.Server;

// Session Ownership: Commands never accept a client-supplied player identity.
public sealed class ArenaSessions(GameWorld world, TimeProvider timeProvider)
{
    public ArenaSessions(GameWorld world) : this(world, TimeProvider.System) { }

    private sealed record Session(string PlayerId, DateTimeOffset CreatedAt)
    {
        public DateTimeOffset LastSeen { get; set; } = CreatedAt;
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Session> _sessions = new(StringComparer.Ordinal);

    public SessionReply Create()
    {
        lock (_gate)
        {
            Expire();
            var playerId = Guid.NewGuid().ToString("N");
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            world.Join(playerId);
            _sessions.Add(token, new Session(playerId, timeProvider.GetUtcNow()));
            return new SessionReply { PlayerId = playerId, Token = token };
        }
    }

    public IResult Read(string authorization)
    {
        lock (_gate)
        {
            return Find(authorization) is null ? Results.Unauthorized() : Results.Ok(world.Snapshot());
        }
    }

    public IResult Execute(string authorization, WorldCommand command)
    {
        lock (_gate)
        {
            var session = Find(authorization);
            return session is null ? Results.Unauthorized() : Results.Ok(world.Execute(session.PlayerId, command));
        }
    }

    public IResult Remove(string authorization)
    {
        lock (_gate)
        {
            var session = Find(authorization);
            if (session is null)
            {
                return Results.Unauthorized();
            }
            _sessions.Remove(authorization[7..]);
            world.Disconnect(session.PlayerId);
            return Results.NoContent();
        }
    }

    private Session? Find(string authorization)
    {
        Expire();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ||
            !_sessions.TryGetValue(authorization[7..], out var session))
        {
            return null;
        }
        session.LastSeen = timeProvider.GetUtcNow();
        return session;
    }

    private void Expire()
    {
        var cutoff = timeProvider.GetUtcNow().AddMinutes(-2);
        foreach (var entry in _sessions.Where(entry => entry.Value.LastSeen < cutoff).ToArray())
        {
            world.Disconnect(entry.Value.PlayerId);
            _sessions.Remove(entry.Key);
        }
    }
}
