using System;
using System.Linq;
using System.Text.Json;
using Relicbound.Core.Abilities;
using Relicbound.Core.Combat;
using Relicbound.Simulation.Combat;
using Relicbound.Simulation.Entities;
using Xunit;

namespace Relicbound.Simulation.Tests
{
    public sealed class ReactionCombatTests
    {
        private static CombatEvent Hit() => new(3, 0, null, "strike", "player", "enemy", DamageType.Physical, 40, "primary-hit");

        private static ReactionDefinition[] Cycle(int max = 1) => new[]
        {
            new ReactionDefinition("spark", DamageType.Physical, new DamageEffect(DamageType.Lightning, 2), max),
            new ReactionDefinition("echo", DamageType.Lightning, new DamageEffect(DamageType.Physical, 1), max)
        };

        [Fact]
        public void CyclicReactionsTerminateAtPerEffectLimits()
        {
            var events = CombatEventProcessor.Process(new[] { Hit() }, Cycle());
            Assert.Equal(new[] { "strike", "spark", "echo" }, events.Select(item => item.SourceId));
            Assert.Equal(new[] { 0, 1, 2 }, events.Select(item => item.Sequence));
            Assert.Equal(new int?[] { null, 0, 1 }, events.Select(item => item.ParentSequence));
            Assert.All(events, item => Assert.Equal(3, item.Tick));
        }

        [Fact]
        public void ExplicitlyRepeatingCycleHitsGlobalBudget()
        {
            Assert.Throws<InvalidOperationException>(() =>
                CombatEventProcessor.Process(new[] { Hit() }, Cycle(int.MaxValue), 8));
        }

        [Fact]
        public void SameTypeReactionCannotAutomaticallyRetriggerItself()
        {
            var reaction = new ReactionDefinition("echo", DamageType.Physical, new DamageEffect(DamageType.Physical, 1));
            Assert.Equal(2, CombatEventProcessor.Process(new[] { Hit() }, new[] { reaction }).Count);
        }

        [Fact]
        public void QueueUsesBreadthFirstOrderAndProducesRepeatableJson()
        {
            var roots = new[] { Hit(), Hit() with { TargetId = "second" } };
            var first = CombatEventProcessor.Process(roots, Cycle());
            var second = CombatEventProcessor.Process(roots, Cycle());
            Assert.Equal(new[] { "strike", "strike", "spark", "echo" }, first.Select(item => item.SourceId));
            Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        }

        [Fact]
        public void BudgetFailureDoesNotCommitAnyCombatState()
        {
            var attacker = new Combatant("player", 100);
            var enemy = new Combatant("enemy", 100);
            var ability = new AbilityDefinition("strike", new[] { new DamageEffect(DamageType.Physical, 40) }, 10, 2);
            var modifier = new AbilityModifierDefinition("cycle", Array.Empty<DamageEffect>(), null,
                ModifierSource.Unspecified, Cycle(int.MaxValue));
            Assert.Throws<InvalidOperationException>(() => new CombatResolver().Resolve(attacker, enemy, ability,
                new[] { modifier }, Array.Empty<Combatant>(), 3));
            Assert.Equal(100, enemy.Health);
            Assert.Equal(100, attacker.Resource);
            Assert.Equal(0, attacker.LastActionTick);
            Assert.Equal(0, attacker.GetAbilityReadyTick(ability.Id));
        }

        [Fact]
        public void SuccessfulReactionsAreAppliedAndLogged()
        {
            var enemy = new Combatant("enemy", 100);
            var ability = new AbilityDefinition("strike", new[] { new DamageEffect(DamageType.Physical, 40) });
            var modifier = new AbilityModifierDefinition("cycle", Array.Empty<DamageEffect>(), null,
                ModifierSource.Unspecified, Cycle());
            var action = new CombatResolver().Resolve(new Combatant("player", 100), enemy, ability, new[] { modifier });
            Assert.Equal(57, enemy.Health);
            Assert.Equal(43, action.AppliedDamage);
            Assert.Equal(3, action.Events.Count);
            Assert.Equal("reaction-hit", action.Events[2].Reason);
        }

        [Fact]
        public void DuplicateReactionIdsAndExcessiveSeedEventsAreRejected()
        {
            var cycle = Cycle();
            Assert.Throws<ArgumentException>(() => CombatEventProcessor.Process(new[] { Hit() }, new[] { cycle[0], cycle[0] }));
            Assert.Throws<InvalidOperationException>(() => CombatEventProcessor.Process(
                Enumerable.Repeat(Hit(), CombatEventProcessor.MaxEventsPerAction + 1), Array.Empty<ReactionDefinition>()));
        }
    }
}
