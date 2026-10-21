using System;
using Relicbound.Core.Abilities;
using Relicbound.Core.Combat;
using Relicbound.Core.Encounters;
using Relicbound.Core.Progression;
using Relicbound.Simulation.Combat;
using Relicbound.Simulation.Encounters;
using Relicbound.Simulation.Entities;
using Xunit;

namespace Relicbound.Simulation.Tests
{
    public sealed class EncounterSessionTests
    {
        private readonly CombatResolver _resolver = new(new[]
        {
            new AbilityModifierDefinition("infusion", new[] { new DamageEffect(DamageType.Lightning, 12) },
                null, ModifierSource.Discovery),
            new AbilityModifierDefinition("tempest", Array.Empty<DamageEffect>(),
                new ChainEffect(DamageType.Lightning, 1, true), ModifierSource.Mastery)
        }, new ProgressionRules("shrine", "infusion", "trial", "tempest", DamageType.Lightning, 3));

        [Fact]
        public void Shrine_RequiresInteractionAndRewardsOnlyOnce()
        {
            var player = new Combatant("player", 100);
            var session = Shrine(player);

            Assert.False(session.TryComplete(player));
            Assert.False(player.HasDiscovery("infusion"));
            Assert.True(session.Interact(player));
            Assert.True(session.IsCompleted);
            Assert.True(player.HasDiscovery("infusion"));
            Assert.False(session.Interact(player));
            Assert.False(session.TryComplete(player));
            Assert.Equal(0, player.GetMasteryChainAttacks("tempest"));
            Assert.Equal(100, player.Resource);
        }

        [Fact]
        public void Trial_RequiresAllBoundEnemiesToDie()
        {
            var player = new Combatant("player", 100);
            var first = new Combatant("first", 100);
            var second = new Combatant("second", 100);
            var session = Trial(player, first, second);

            Assert.False(session.TryComplete(player));
            Assert.Throws<InvalidOperationException>(() => session.Interact(player));
            Defeat(first);
            Assert.False(session.TryComplete(player));
            Assert.False(player.HasCompletedEncounter("trial"));
            Defeat(second);
            Assert.True(session.TryComplete(player));
            Assert.True(player.HasCompletedEncounter("trial"));
            Assert.False(session.TryComplete(player));
            Assert.False(player.HasMastery("tempest"));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AnotherCharacterOrIdAliasCannotClaimEncounter(bool sameId)
        {
            var player = new Combatant("player", 100);
            var other = new Combatant(sameId ? "player" : "other", 100);
            var session = Shrine(player);

            Assert.Throws<ArgumentException>(() => session.Interact(other));
            Assert.Throws<ArgumentException>(() => session.TryComplete(other));
            Assert.False(session.IsCompleted);
            Assert.False(player.HasDiscovery("infusion"));
            Assert.False(other.HasDiscovery("infusion"));
        }

        [Fact]
        public void UnrelatedEnemyWithSameIdDoesNotSatisfyObjective()
        {
            var player = new Combatant("player", 100);
            var guardian = new Combatant("guardian", 100);
            var session = Trial(player, guardian);
            Defeat(new Combatant("guardian", 100));

            Assert.False(session.TryComplete(player));
            Assert.Equal(100, guardian.Health);
            Assert.False(player.HasCompletedEncounter("trial"));
        }

        [Fact]
        public void DeadParticipantCannotReceiveEncounterRewards()
        {
            var player = new Combatant("player", 100);
            var guardian = new Combatant("guardian", 100);
            var trial = Trial(player, guardian);
            var shrine = Shrine(player);
            Defeat(guardian);
            Defeat(player);

            Assert.Throws<InvalidOperationException>(() => trial.TryComplete(player));
            Assert.Throws<InvalidOperationException>(() => shrine.Interact(player));
            Assert.False(trial.IsCompleted);
            Assert.False(shrine.IsCompleted);
            Assert.False(player.HasDiscovery("infusion"));
            Assert.False(player.HasCompletedEncounter("trial"));
        }

        [Fact]
        public void FailedRewardDoesNotMarkSessionCompleted()
        {
            var player = new Combatant("player", 100);
            var session = new EncounterSession(
                new EncounterDefinition("unknown", EncounterObjective.Interaction, Array.Empty<string>()),
                player, Array.Empty<Combatant>(), _resolver);

            Assert.Throws<ArgumentException>(() => session.Interact(player));
            Assert.False(session.IsCompleted);
            Assert.False(player.HasCompletedEncounter("unknown"));
            Assert.False(player.HasDiscovery("infusion"));
        }

        [Fact]
        public void SessionAndDefinitionCopyRosterCollectionsButObserveLiveHealth()
        {
            var player = new Combatant("player", 100);
            var ids = new[] { "guardian" };
            var definition = new EncounterDefinition("trial", EncounterObjective.DefeatAllEnemies, ids);
            var guardian = new Combatant("guardian", 100);
            var enemies = new[] { guardian };
            var session = new EncounterSession(definition, player, enemies, _resolver);
            ids[0] = "replacement";
            enemies[0] = new Combatant("replacement", 100);
            Defeat(guardian);

            Assert.Equal("guardian", Assert.Single(definition.EnemyIds));
            Assert.Same(guardian, Assert.Single(session.Enemies));
            Assert.True(session.TryComplete(player));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void InvalidRosterCannotStartEncounter(int invalidCase)
        {
            var player = new Combatant("player", 100);
            var guardian = new Combatant("guardian", 100);
            var definition = new EncounterDefinition("trial", EncounterObjective.DefeatAllEnemies, new[] { "guardian" });
            var enemies = invalidCase switch
            {
                0 => Array.Empty<Combatant>(),
                1 => new[] { new Combatant("wrong", 100) },
                2 => new[] { guardian, new Combatant("extra", 100) },
                3 => new[] { guardian, new Combatant("guardian", 100) },
                4 => new Combatant[] { null! },
                _ => new[] { new Combatant(player.Id, 100) }
            };

            Assert.Throws<ArgumentException>(() => new EncounterSession(definition, player, enemies, _resolver));
            Assert.False(player.HasCompletedEncounter("trial"));
        }

        [Fact]
        public void AlreadyDeadEnemyCannotBeUsedToStartCompletedTrial()
        {
            var guardian = new Combatant("guardian", 100);
            Defeat(guardian);
            Assert.Throws<ArgumentException>(() => Trial(new Combatant("player", 100), guardian));
        }

        [Fact]
        public void DefinitionRejectsEmptyCombatObjectivesAndEnemiesForInteraction()
        {
            Assert.Throws<ArgumentException>(() =>
                new EncounterDefinition("trial", EncounterObjective.DefeatAllEnemies, Array.Empty<string>()));
            Assert.Throws<ArgumentException>(() =>
                new EncounterDefinition("shrine", EncounterObjective.Interaction, new[] { "enemy" }));
            Assert.Throws<ArgumentException>(() =>
                new EncounterDefinition("trial", EncounterObjective.DefeatAllEnemies, new[] { "enemy", "enemy" }));
            Assert.Throws<ArgumentException>(() =>
                new EncounterDefinition("trial", EncounterObjective.DefeatAllEnemies, new[] { " " }));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new EncounterDefinition("trial", (EncounterObjective)999, Array.Empty<string>()));
        }

        [Fact]
        public void SeparateSessionsDoNotRepeatPersonalRewardCredit()
        {
            var player = new Combatant("player", 100);
            Assert.True(Shrine(player).Interact(player));
            Assert.True(Shrine(player).Interact(player));
            Assert.True(player.HasDiscovery("infusion"));
            Assert.Equal(0, player.GetMasteryChainAttacks("tempest"));
        }

        private EncounterSession Shrine(Combatant player)
        {
            return new EncounterSession(
                new EncounterDefinition("shrine", EncounterObjective.Interaction, Array.Empty<string>()),
                player, Array.Empty<Combatant>(), _resolver);
        }

        private EncounterSession Trial(Combatant player, params Combatant[] enemies)
        {
            return new EncounterSession(new EncounterDefinition("trial", EncounterObjective.DefeatAllEnemies,
                Array.ConvertAll(enemies, enemy => enemy.Id)), player, enemies, _resolver);
        }

        private static void Defeat(Combatant enemy)
        {
            new CombatResolver().Resolve(new Combatant("attacker", 100), enemy,
                new AbilityDefinition("test-strike", new[] { new DamageEffect(DamageType.Physical, 100) }),
                Array.Empty<AbilityModifierDefinition>());
        }
    }
}
