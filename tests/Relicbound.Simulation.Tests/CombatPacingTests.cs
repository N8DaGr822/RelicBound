using System;
using System.Collections.Generic;
using Relicbound.Core.Abilities;
using Relicbound.Core.Combat;
using Relicbound.Simulation.Combat;
using Relicbound.Simulation.Entities;
using Xunit;

namespace Relicbound.Simulation.Tests
{
    public sealed class CombatPacingTests
    {
        private readonly CombatResolver _resolver = new();
        private readonly AbilityDefinition _ability = new(
            "crushing-blow", new[] { new DamageEffect(DamageType.Physical, 40) }, 10, 2);

        [Fact]
        public void Resolve_SpendsResourceAndRecordsDeterministicReadyTick()
        {
            var attacker = new Combatant("player", 100);
            var target = new Combatant("enemy", 100);

            var result = Assert.Single(Attack(attacker, target, 5));

            Assert.Equal(90, attacker.Resource);
            Assert.Equal(100, attacker.MaxResource);
            Assert.Equal(5, attacker.LastActionTick);
            Assert.Equal(7, attacker.GetAbilityReadyTick(_ability.Id));
            Assert.Equal(5, result.Tick);
            Assert.Equal(10, result.ResourceSpent);
            Assert.Equal(7L, result.AbilityReadyTick);
            Assert.Equal(60, target.Health);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void Resolve_BeforeReadyTickRejectsWithoutMutation(long rejectedTick)
        {
            var attacker = new Combatant("player", 100);
            Attack(attacker, new Combatant("first", 100), 0);
            var target = new Combatant("next", 100);

            Assert.Throws<InvalidOperationException>(() => Attack(attacker, target, rejectedTick));

            Assert.Equal(100, target.Health);
            Assert.Equal(90, attacker.Resource);
            Assert.Equal(0, attacker.LastActionTick);
            Assert.Equal(2, attacker.GetAbilityReadyTick(_ability.Id));
        }

        [Fact]
        public void Resolve_AtReadyTickSucceedsAndEarlierSnapshotStaysUnchanged()
        {
            var attacker = new Combatant("player", 100);
            var first = Assert.Single(Attack(attacker, new Combatant("first", 100), 0));
            var second = Assert.Single(Attack(attacker, new Combatant("next", 100), 2));

            Assert.Equal(80, attacker.Resource);
            Assert.Equal(4, attacker.GetAbilityReadyTick(_ability.Id));
            Assert.Equal(2L, first.AbilityReadyTick);
            Assert.Equal(4L, second.AbilityReadyTick);
        }

        [Fact]
        public void Resolve_ExactResourceCostSucceedsThenInsufficientResourceIsRejected()
        {
            var attacker = new Combatant("player", 100, 10);
            Attack(attacker, new Combatant("first", 100), 0);
            var target = new Combatant("next", 100);

            Assert.Equal(0, attacker.Resource);
            Assert.Throws<InvalidOperationException>(() => Attack(attacker, target, 2));
            Assert.Equal(100, target.Health);
            Assert.Equal(0, attacker.Resource);
            Assert.Equal(2, attacker.GetAbilityReadyTick(_ability.Id));
            Assert.Equal(0, attacker.LastActionTick);
        }

        [Fact]
        public void Resolve_RejectedFutureCommandDoesNotAdvanceTimeOrCooldown()
        {
            var attacker = new Combatant("player", 100);
            var modifier = new AbilityModifierDefinition("infusion", new[] { new DamageEffect(DamageType.Lightning, 12) });

            Assert.Throws<ArgumentException>(() => _resolver.Resolve(attacker,
                new Combatant("invalid", 100), _ability, new[] { modifier, modifier },
                Array.Empty<Combatant>(), 100));

            Assert.Equal(100, attacker.Resource);
            Assert.Equal(0, attacker.LastActionTick);
            Assert.Equal(0, attacker.GetAbilityReadyTick(_ability.Id));
            Assert.Single(Attack(attacker, new Combatant("valid", 100), 0));
        }

        [Fact]
        public void Resolve_BackwardsTimeIsRejectedEvenForAnotherAbility()
        {
            var attacker = new Combatant("player", 100);
            Attack(attacker, new Combatant("first", 100), 5);
            var target = new Combatant("next", 100);
            var other = new AbilityDefinition("other", new[] { new DamageEffect(DamageType.Physical, 1) });

            Assert.Throws<InvalidOperationException>(() => _resolver.Resolve(attacker, target,
                other, Array.Empty<AbilityModifierDefinition>(), Array.Empty<Combatant>(), 4));
            Assert.Equal(100, target.Health);
            Assert.Equal(90, attacker.Resource);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(long.MaxValue)]
        public void Resolve_InvalidOrOverflowingTickLeavesStateUnchanged(long tick)
        {
            var attacker = new Combatant("player", 100);
            var target = new Combatant("enemy", 100);

            Assert.Throws<ArgumentOutOfRangeException>(() => Attack(attacker, target, tick));
            Assert.Equal(100, target.Health);
            Assert.Equal(100, attacker.Resource);
            Assert.Equal(0, attacker.GetAbilityReadyTick(_ability.Id));
        }

        [Fact]
        public void Resolve_CooldownsArePerCharacterAndAbilityId()
        {
            var attacker = new Combatant("player", 100);
            Attack(attacker, new Combatant("first", 100), 0);
            var otherAbility = new AbilityDefinition("other", _ability.Effects, 10, 2);
            _resolver.Resolve(attacker, new Combatant("next", 100), otherAbility,
                Array.Empty<AbilityModifierDefinition>(), Array.Empty<Combatant>(), 0);
            var otherCharacter = new Combatant("other-player", 100);
            Attack(otherCharacter, new Combatant("third", 100), 0);
            var sameId = new AbilityDefinition(_ability.Id, _ability.Effects);

            Assert.Throws<InvalidOperationException>(() => _resolver.Resolve(attacker,
                new Combatant("fourth", 100), sameId, Array.Empty<AbilityModifierDefinition>(),
                Array.Empty<Combatant>(), 0));
            Assert.Equal(80, attacker.Resource);
            Assert.Equal(90, otherCharacter.Resource);
        }

        [Fact]
        public void Resolve_ChainHitsSpendResourceOnlyOnce()
        {
            var attacker = new Combatant("player", 100);
            var infusion = new AbilityModifierDefinition("infusion", new[] { new DamageEffect(DamageType.Lightning, 12) });
            var relic = new AbilityModifierDefinition("relic", Array.Empty<DamageEffect>(), new ChainEffect(DamageType.Lightning, 2));

            var results = _resolver.Resolve(attacker, new Combatant("primary", 100), _ability,
                new[] { infusion, relic }, new[] { new Combatant("a", 100), new Combatant("b", 100) }, 3);

            Assert.Equal(3, results.Count);
            Assert.Equal(90, attacker.Resource);
            Assert.Equal(10, results[0].ResourceSpent);
            Assert.Equal(0, results[1].ResourceSpent);
            Assert.Equal(0, results[2].ResourceSpent);
            Assert.Null(results[1].AbilityReadyTick);
            Assert.All(results, result => Assert.Equal(3, result.Tick));
        }

        [Fact]
        public void Resolve_FailingTargetEnumerationDoesNotSpendResourceOrStartCooldown()
        {
            var attacker = new Combatant("player", 100);
            var primary = new Combatant("primary", 100);

            Assert.Throws<InvalidOperationException>(() => _resolver.Resolve(attacker, primary,
                _ability, Array.Empty<AbilityModifierDefinition>(), FailingCandidates(), 2));

            Assert.Equal(100, attacker.Resource);
            Assert.Equal(100, primary.Health);
            Assert.Equal(0, attacker.LastActionTick);
            Assert.Equal(0, attacker.GetAbilityReadyTick(_ability.Id));
        }

        [Fact]
        public void RestoreResource_IsBoundedAndDoesNotClearCooldown()
        {
            var attacker = new Combatant("player", 100);
            Attack(attacker, new Combatant("first", 100), 0);
            attacker.RestoreResource(int.MaxValue);

            Assert.Equal(100, attacker.Resource);
            Assert.Equal(2, attacker.GetAbilityReadyTick(_ability.Id));
            Assert.Throws<InvalidOperationException>(() => Attack(attacker, new Combatant("next", 100), 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => attacker.RestoreResource(-1));
            Assert.Equal(100, attacker.Resource);
        }

        [Fact]
        public void Resolve_LegacyDefinitionRemainsFreeWithNoCooldown()
        {
            var attacker = new Combatant("player", 100, 0);
            var ability = new AbilityDefinition("legacy", _ability.Effects);
            var target = new Combatant("enemy", 100);
            _resolver.Resolve(attacker, target, ability, Array.Empty<AbilityModifierDefinition>());
            _resolver.Resolve(attacker, target, ability, Array.Empty<AbilityModifierDefinition>());

            Assert.Equal(20, target.Health);
            Assert.Equal(0, attacker.Resource);
            Assert.Equal(0, attacker.GetAbilityReadyTick(ability.Id));
        }

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(0, -1)]
        public void AbilityDefinition_NegativePacingValuesAreRejected(int cost, int cooldown)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new AbilityDefinition("invalid", _ability.Effects, cost, cooldown));
        }

        private IReadOnlyList<CombatActionResult> Attack(Combatant attacker, Combatant target, long tick)
        {
            return _resolver.Resolve(attacker, target, _ability,
                Array.Empty<AbilityModifierDefinition>(), Array.Empty<Combatant>(), tick);
        }

        private static IEnumerable<Combatant> FailingCandidates()
        {
            yield return new Combatant("candidate", 100);
            throw new InvalidOperationException("Candidate source failed.");
        }
    }
}
