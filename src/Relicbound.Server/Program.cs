using Relicbound.Contracts;
using Relicbound.Server;
using Relicbound.Simulation.World;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.IncludeFields = true;
    options.SerializerOptions.PropertyNamingPolicy = null;
});
builder.Services.AddSingleton<GameWorld>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ArenaSessions>();
builder.Services.AddHostedService<ArenaClock>();
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { Status = "ready" }));
app.MapPost("/api/sessions", (ArenaSessions sessions) =>
{
    try { return Results.Ok(sessions.Create()); }
    catch (InvalidOperationException) { return Results.Conflict(new { Message = "The arena is full." }); }
});
app.MapGet("/api/world", (HttpContext context, ArenaSessions sessions) =>
    sessions.Read(context.Request.Headers.Authorization.ToString()));
app.MapPost("/api/commands", (HttpContext context, WorldCommand command, ArenaSessions sessions) =>
    sessions.Execute(context.Request.Headers.Authorization.ToString(), command));
app.MapDelete("/api/session", (HttpContext context, ArenaSessions sessions) =>
    sessions.Remove(context.Request.Headers.Authorization.ToString()));

app.Run();

// HTTP Tests: Expose the entry point to WebApplicationFactory.
public partial class Program { }
