using System;
using System.Linq;
using System.Text.Json;
using Relicbound.Contracts;
using Relicbound.Simulation.World;
using Xunit;

namespace Relicbound.Simulation.Tests
{
    public sealed class GameWorldTests
    {
        [Fact]
        public void RetryDoesNotMoveTwiceAndConflictingSequenceIsRejected()
        {
            var world = Create();
            var command = new WorldCommand { Sequence = 1, Kind = "move", X = 1 };
            Assert.True(world.Execute("a", command).Success);
            Assert.True(world.Execute("a", command).Success);
            Assert.False(world.Execute("a", new WorldCommand { Sequence = 1, Kind = "move", Z = 1 }).Success);
            Assert.False(world.Execute("a", new WorldCommand { Sequence = 2, Kind = "move", X = 1 }).Success);
            Assert.Equal(-3, Player(world).X);
            Assert.Equal(-4, Player(world).Z);
        }

        [Fact]
        public void InvalidCommandsDoNotMutateHealthResourceOrPosition()
        {
            var world = Create();
            var before = JsonSerializer.Serialize(world.Snapshot(), Options);
            Assert.False(world.Execute("a", new WorldCommand { Sequence = 1, Kind = "move", X = int.MinValue }).Success);
            Assert.False(world.Execute("a", new WorldCommand { Sequence = 2, Kind = "attack", TargetId = "guardian" }).Success);
            Assert.False(world.Execute("a", new WorldCommand { Sequence = 3, Kind = "loadout", MasteryId = "tempest" }).Success);
            Assert.Equal(before, JsonSerializer.Serialize(world.Snapshot(), Options));
        }

        [Fact]
        public void ShrineProgressionBelongsToTheInteractingPlayer()
        {
            var world = Create();
            world.Join("b");
            Assert.True(world.Execute("a", new WorldCommand { Sequence = 1, Kind = "interact" }).Success);
            Assert.True(Player(world).InfusionUnlocked);
            Assert.False(Player(world, "b").InfusionUnlocked);
            Assert.True(world.Execute("a", new WorldCommand { Sequence = 2, Kind = "loadout", DiscoveryId = "lightning-infusion" }).Success);
            Assert.False(world.Execute("b", new WorldCommand { Sequence = 1, Kind = "loadout", DiscoveryId = "lightning-infusion" }).Success);
        }

        [Theory]
        [InlineData(true, 60)]
        [InlineData(false, 40)]
        public void ShatterRequiresAnotherPlayersFreeze(bool cooperative, int expectedDamage)
        {
            var world = Create();
            world.Join("b");
            var enemy = Approach(world);
            Assert.True(world.Execute("a", new WorldCommand { Sequence = 1, Kind = "freeze", TargetId = enemy.Id }).Success);
            var attacker = cooperative ? "b" : "a";
            Assert.True(world.Execute(attacker, new WorldCommand { Sequence = cooperative ? 1 : 2, Kind = "attack", TargetId = enemy.Id }).Success);
            var after = world.Snapshot().Actors.Single(actor => actor.Id == enemy.Id);
            Assert.Equal(expectedDamage, enemy.Health - after.Health);
            Assert.Equal(!cooperative, after.Frozen);
        }

        [Fact]
        public void SnapshotMutationCannotChangeAuthoritativeState()
        {
            var world = Create();
            Player(world).Health = 0;
            Player(world).X = 900;
            Assert.Equal(100, Player(world).Health);
            Assert.Equal(-4, Player(world).X);
        }

        [Fact]
        public void BranchChoiceIsSharedAndCannotBeReplaced()
        {
            var world = Create();
            world.Join("b");
            Assert.True(world.Execute("a", new WorldCommand { Sequence = 1, Kind = "branch", TargetId = "silence" }).Success);
            Assert.False(world.Execute("b", new WorldCommand { Sequence = 1, Kind = "branch", TargetId = "ruin" }).Success);
            Assert.Equal("silence", world.Snapshot().Branch);
            Assert.Equal(40, world.Snapshot().Actors.Single(actor => actor.Id == "guardian").Barrier);
        }

        [Fact]
        public void IdenticalCommandAndTickHistoryProducesIdenticalSnapshots()
        {
            var first = Create();
            var second = Create();
            foreach (var world in new[] { first, second })
            {
                world.Execute("a", new WorldCommand { Sequence = 1, Kind = "interact" });
                world.Execute("a", new WorldCommand { Sequence = 2, Kind = "dodge", X = 1 });
                for (var tick = 0; tick < 80; tick++) world.Advance();
            }
            Assert.Equal(JsonSerializer.Serialize(first.Snapshot(), Options), JsonSerializer.Serialize(second.Snapshot(), Options));
        }

        [Fact]
        public void DisconnectReleasesCapacityAndRejectsFurtherCommands()
        {
            var world = Create();
            world.Join("b");
            world.Join("c");
            world.Join("d");
            Assert.Throws<InvalidOperationException>(() => world.Join("e"));
            world.Disconnect("a");
            world.Join("e");
            Assert.False(world.Execute("a", new WorldCommand { Sequence = 1, Kind = "interact" }).Success);
            Assert.Equal(4, world.Snapshot().Actors.Count(actor => actor.IsPlayer));
        }

        private static readonly JsonSerializerOptions Options = new() { IncludeFields = true };
        private static GameWorld Create()
        {
            var world = new GameWorld();
            world.Join("a");
            return world;
        }

        private static ActorSnapshot Player(GameWorld world, string id = "a") => world.Snapshot().Actors.Single(actor => actor.Id == id);

        private static ActorSnapshot Approach(GameWorld world)
        {
            for (var tick = 0; tick < 100; tick++)
            {
                var player = Player(world);
                var enemy = world.Snapshot().Actors.FirstOrDefault(actor => !actor.IsPlayer && Math.Abs(actor.X - player.X) + Math.Abs(actor.Z - player.Z) <= 2);
                if (enemy is not null) return enemy;
                world.Advance();
            }
            throw new InvalidOperationException("No enemy reached the player.");
        }
    }
}
