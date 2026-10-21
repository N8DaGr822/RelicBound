using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Relicbound.Contracts;

namespace Relicbound.Server.Tests;

public sealed class ArenaHttpTests
{
    private static readonly JsonSerializerOptions Json = new() { IncludeFields = true };

    [Theory]
    [InlineData(null)]
    [InlineData("Bearer unknown")]
    [InlineData("Basic unknown")]
    public async Task ProtectedEndpointsRejectInvalidAuthorization(string? authorization)
    {
        using var app = new ArenaApplication();
        using var client = app.CreateClient();
        foreach (var (method, path) in new[]
        {
            (HttpMethod.Get, "/api/world"),
            (HttpMethod.Post, "/api/commands"),
            (HttpMethod.Delete, "/api/session")
        })
        {
            using var response = await Send(client, method, path, authorization,
                method == HttpMethod.Post ? new WorldCommand { Sequence = 1, Kind = "interact" } : null);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task SessionsShareWorldButCommandsUseTokenIdentity()
    {
        using var app = new ArenaApplication();
        using var client = app.CreateClient();
        var first = await CreateSession(client);
        var second = await CreateSession(client);
        Assert.NotEqual(first.PlayerId, second.PlayerId);
        Assert.NotEqual(first.Token, second.Token);
        Assert.Equal(64, first.Token.Length);

        // Identity: An extra client-supplied player ID cannot redirect the command.
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/commands");
        request.Headers.Add("Authorization", $"Bearer {first.Token}");
        request.Content = JsonContent.Create(new { Sequence = 1, Kind = "interact", PlayerId = second.PlayerId });
        using var response = await client.SendAsync(request);
        var reply = await Read<CommandReply>(response);
        Assert.True(reply.Success);
        var firstView = await Snapshot(client, first);
        var secondView = await Snapshot(client, second);
        Assert.Equal(JsonSerializer.Serialize(firstView, Json), JsonSerializer.Serialize(secondView, Json));
        Assert.True(firstView.Actors.Single(actor => actor.Id == first.PlayerId).InfusionUnlocked);
        Assert.False(firstView.Actors.Single(actor => actor.Id == second.PlayerId).InfusionUnlocked);
    }

    [Fact]
    public async Task RetryReturnsSameReplyAndConflictingOrStaleSequencesDoNotMove()
    {
        using var app = new ArenaApplication();
        using var client = app.CreateClient();
        var session = await CreateSession(client);
        var command = new WorldCommand { Sequence = 2, Kind = "move", X = 1 };
        var original = await Execute(client, session, command);
        var retry = await Execute(client, session, command);
        Assert.True(original.Success);
        Assert.Equal(JsonSerializer.Serialize(original, Json), JsonSerializer.Serialize(retry, Json));
        Assert.False((await Execute(client, session, new WorldCommand { Sequence = 2, Kind = "move", Z = 1 })).Success);
        Assert.False((await Execute(client, session, new WorldCommand { Sequence = 1, Kind = "move", X = -1 })).Success);
        var player = (await Snapshot(client, session)).Actors.Single(actor => actor.Id == session.PlayerId);
        Assert.Equal(-3, player.X);
        Assert.Equal(-4, player.Z);
    }

    [Fact]
    public async Task GameplayFailureIsCachedAndRequiresNewSequenceForCorrection()
    {
        using var app = new ArenaApplication();
        using var client = app.CreateClient();
        var session = await CreateSession(client);
        var invalid = new WorldCommand { Sequence = 1, Kind = "move", X = 2 };
        var first = await Execute(client, session, invalid);
        Assert.False(first.Success);
        Assert.Equal(JsonSerializer.Serialize(first, Json), JsonSerializer.Serialize(await Execute(client, session, invalid), Json));
        Assert.False((await Execute(client, session, new WorldCommand { Sequence = 1, Kind = "move", X = 1 })).Success);
        Assert.True((await Execute(client, session, new WorldCommand { Sequence = 2, Kind = "move", X = 1 })).Success);
    }

    [Fact]
    public async Task DisconnectRevokesTokenAndReleasesCapacity()
    {
        using var app = new ArenaApplication();
        using var client = app.CreateClient();
        var sessions = new List<SessionReply>();
        for (var index = 0; index < 4; index++) sessions.Add(await CreateSession(client));
        using var full = await client.PostAsync("/api/sessions", null);
        Assert.Equal(HttpStatusCode.Conflict, full.StatusCode);
        using var removed = await Send(client, HttpMethod.Delete, "/api/session", $"Bearer {sessions[0].Token}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        foreach (var (method, path) in new[]
        {
            (HttpMethod.Get, "/api/world"), (HttpMethod.Post, "/api/commands"), (HttpMethod.Delete, "/api/session")
        })
        {
            using var rejected = await Send(client, method, path, $"Bearer {sessions[0].Token}",
                method == HttpMethod.Post ? new WorldCommand { Sequence = 1, Kind = "interact" } : null);
            Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        }
        var replacement = await CreateSession(client);
        var world = await Snapshot(client, replacement);
        Assert.Equal(4, world.Actors.Count(actor => actor.IsPlayer));
        Assert.DoesNotContain(world.Actors, actor => actor.Id == sessions[0].PlayerId);
    }

    [Fact]
    public async Task SessionIsValidAtTwoMinutesAndAuthenticatedReadsRefreshExpiry()
    {
        using var app = new ArenaApplication();
        using var client = app.CreateClient();
        var session = await CreateSession(client);
        app.Time.Advance(TimeSpan.FromMinutes(2));
        await Snapshot(client, session);
        app.Time.Advance(TimeSpan.FromMinutes(2));
        await Snapshot(client, session);
        app.Time.Advance(TimeSpan.FromMinutes(2) + TimeSpan.FromTicks(1));
        using var expired = await Send(client, HttpMethod.Get, "/api/world", $"Bearer {session.Token}");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        var replacement = await CreateSession(client);
        Assert.DoesNotContain((await Snapshot(client, replacement)).Actors, actor => actor.Id == session.PlayerId);
    }

    [Fact]
    public async Task CreatingSessionExpiresInactivePlayersAndReleasesAllSlots()
    {
        using var app = new ArenaApplication();
        using var client = app.CreateClient();
        var oldSessions = new List<SessionReply>();
        for (var index = 0; index < 4; index++) oldSessions.Add(await CreateSession(client));
        app.Time.Advance(TimeSpan.FromMinutes(2) + TimeSpan.FromTicks(1));
        var replacement = await CreateSession(client);
        Assert.Single((await Snapshot(client, replacement)).Actors, actor => actor.IsPlayer);
        foreach (var session in oldSessions)
        {
            using var response = await Send(client, HttpMethod.Post, "/api/commands", $"Bearer {session.Token}",
                new WorldCommand { Sequence = 1, Kind = "interact" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task ConcurrentSessionCreationNeverExceedsCapacity()
    {
        using var app = new ArenaApplication();
        using var client = app.CreateClient();
        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => client.PostAsync("/api/sessions", null))));
        try
        {
            Assert.Equal(4, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
            Assert.Equal(8, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
            var sessions = await Task.WhenAll(responses.Where(response => response.StatusCode == HttpStatusCode.OK).Select(Read<SessionReply>));
            Assert.Equal(4, sessions.Select(session => session.Token).Distinct().Count());
            Assert.Equal(4, sessions.Select(session => session.PlayerId).Distinct().Count());
            Assert.Equal(4, (await Snapshot(client, sessions[0])).Actors.Count(actor => actor.IsPlayer));
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    [Fact]
    public async Task ConcurrentIdenticalRetriesSpendResourceAndMoveOnlyOnce()
    {
        using var app = new ArenaApplication();
        using var client = app.CreateClient();
        var session = await CreateSession(client);
        var command = new WorldCommand { Sequence = 1, Kind = "dodge", X = 1 };
        var replies = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => Execute(client, session, command))));
        Assert.All(replies, reply => Assert.True(reply.Success));
        Assert.Single(replies.Select(reply => JsonSerializer.Serialize(reply, Json)).Distinct());
        var player = (await Snapshot(client, session)).Actors.Single(actor => actor.Id == session.PlayerId);
        Assert.Equal(-2, player.X);
        Assert.Equal(-4, player.Z);
        Assert.Equal(90, player.Resource);
    }

    [Fact]
    public async Task ConcurrentConflictingSequencesAcceptExactlyOneCommand()
    {
        using var app = new ArenaApplication();
        using var client = app.CreateClient();
        var session = await CreateSession(client);
        var replies = await Task.WhenAll(
            Task.Run(() => Execute(client, session, new WorldCommand { Sequence = 1, Kind = "dodge", X = 1 })),
            Task.Run(() => Execute(client, session, new WorldCommand { Sequence = 1, Kind = "dodge", Z = 1 })));
        Assert.Single(replies, reply => reply.Success);
        Assert.Single(replies, reply => !reply.Success);
        var player = (await Snapshot(client, session)).Actors.Single(actor => actor.Id == session.PlayerId);
        Assert.True((player.X == -2 && player.Z == -4) || (player.X == -4 && player.Z == -2));
        Assert.Equal(90, player.Resource);
    }

    [Fact]
    public async Task MalformedJsonIsRejectedBeforeCommandExecution()
    {
        using var app = new ArenaApplication();
        using var client = app.CreateClient();
        var session = await CreateSession(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/commands");
        request.Headers.Add("Authorization", $"Bearer {session.Token}");
        request.Content = new StringContent("{", Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await Execute(client, session, new WorldCommand { Sequence = 1, Kind = "interact" })).Success);
    }

    private static async Task<SessionReply> CreateSession(HttpClient client)
    {
        using var response = await client.PostAsync("/api/sessions", null);
        return await Read<SessionReply>(response);
    }

    private static async Task<WorldSnapshot> Snapshot(HttpClient client, SessionReply session)
    {
        using var response = await Send(client, HttpMethod.Get, "/api/world", $"Bearer {session.Token}");
        return await Read<WorldSnapshot>(response);
    }

    private static async Task<CommandReply> Execute(HttpClient client, SessionReply session, WorldCommand command)
    {
        using var response = await Send(client, HttpMethod.Post, "/api/commands", $"Bearer {session.Token}", command);
        return await Read<CommandReply>(response);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path,
        string? authorization, WorldCommand? command = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (authorization is not null) request.Headers.TryAddWithoutValidation("Authorization", authorization);
        if (command is not null) request.Content = JsonContent.Create(command, options: Json);
        return await client.SendAsync(request);
    }
}
