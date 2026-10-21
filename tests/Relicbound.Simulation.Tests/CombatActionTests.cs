using System;
using System.Collections.Generic;
using Relicbound.Core.Abilities;
using Relicbound.Core.Combat;
using Relicbound.Simulation.Combat;
using Relicbound.Simulation.Entities;
using Xunit;

namespace Relicbound.Simulation.Tests
{
    public sealed class CombatActionTests
    {
        private readonly CombatResolver _resolver = new();
        private readonly AbilityDefinition _ability = new(
            "crushing-blow", new[] { new DamageEffect(DamageType.Physical, 40) });
        private readonly AbilityModifierDefinition _infusion = new(
            "lightning-infusion", new[] { new DamageEffect(DamageType.Lightning, 12) });

        [Theory]
        [InlineData(false, 60)]
        [InlineData(true, 48)]
        public void Resolve_AppliesDamageOnlyToTarget(bool infused, int expectedHealth)
        {
            var attacker = new Combatant("player", 100);
            var target = new Combatant("enemy", 100);
            var modifiers = infused ? new[] { _infusion } : Array.Empty<AbilityModifierDefinition>();

            var result = _resolver.Resolve(attacker, target, _ability, modifiers);

            Assert.Equal("player", result.AttackerId);
            Assert.Equal("enemy", result.TargetId);
            Assert.Equal(100, result.TargetHealthBefore);
            Assert.Equal(expectedHealth, result.TargetHealthAfter);
            Assert.Equal(100 - expectedHealth, result.AppliedDamage);
            Assert.Equal(expectedHealth, target.Health);
            Assert.Equal(100, target.MaxHealth);
            Assert.True(target.IsAlive);
            Assert.Equal(100, attacker.Health);
            Assert.Equal(infused ? 12L : 0L, result.Damage.GetDamage(DamageType.Lightning));
        }

        [Theory]
        [InlineData(52, 52)]
        [InlineData(15, 15)]
        public void Resolve_LethalDamageClampsHealthAndPreservesCalculatedDamage(int health, int applied)
        {
            var target = new Combatant("enemy", health);

            var result = _resolver.Resolve(new Combatant("player", 100), target, _ability, new[] { _infusion });

            Assert.Equal(0, target.Health);
            Assert.False(target.IsAlive);
            Assert.Equal(applied, result.AppliedDamage);
            Assert.Equal(52L, result.Damage.TotalDamage);
        }

        [Fact]
        public void Resolve_LargeDamageDoesNotOverflowHealth()
        {
            var ability = new AbilityDefinition("large-hit", new[]
            {
                new DamageEffect(DamageType.Physical, int.MaxValue),
                new DamageEffect(DamageType.Lightning, int.MaxValue)
            });
            var target = new Combatant("enemy", int.MaxValue);

            var result = _resolver.Resolve(
                new Combatant("player", 100), target, ability, Array.Empty<AbilityModifierDefinition>());

            Assert.Equal(2L * int.MaxValue, result.Damage.TotalDamage);
            Assert.Equal(int.MaxValue, result.AppliedDamage);
            Assert.Equal(0, target.Health);
        }

        [Fact]
        public void Resolve_DuplicateModifierLeavesBothCombatantsUnchanged()
        {
            var attacker = new Combatant("player", 100);
            var target = new Combatant("enemy", 100);

            Assert.Throws<ArgumentException>(() =>
                _resolver.Resolve(attacker, target, _ability, new[] { _infusion, _infusion }));

            Assert.Equal(100, attacker.Health);
            Assert.Equal(100, target.Health);
        }

        [Fact]
        public void Resolve_ThrowingModifierEnumerationLeavesHealthUnchanged()
        {
            var attacker = new Combatant("player", 100);
            var target = new Combatant("enemy", 100);

            Assert.Throws<InvalidOperationException>(() =>
                _resolver.Resolve(attacker, target, _ability, FailingModifiers()));

            Assert.Equal(100, attacker.Health);
            Assert.Equal(100, target.Health);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Resolve_SameIdentityIsRejected(bool sameInstance)
        {
            var attacker = new Combatant("player", 100);
            var target = sameInstance ? attacker : new Combatant("player", 100);

            Assert.Throws<ArgumentException>(() =>
                _resolver.Resolve(attacker, target, _ability, Array.Empty<AbilityModifierDefinition>()));

            Assert.Equal(100, attacker.Health);
            Assert.Equal(100, target.Health);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Resolve_DeadParticipantIsRejected(bool deadAttacker)
        {
            var dead = new Combatant("dead", 1);
            _resolver.Resolve(new Combatant("killer", 100), dead, _ability, Array.Empty<AbilityModifierDefinition>());
            var alive = new Combatant("alive", 100);

            Assert.Throws<InvalidOperationException>(() =>
                _resolver.Resolve(
                    deadAttacker ? dead : alive,
                    deadAttacker ? alive : dead,
                    _ability,
                    Array.Empty<AbilityModifierDefinition>()));

            Assert.Equal(0, dead.Health);
            Assert.Equal(100, alive.Health);
        }

        [Fact]
        public void Resolve_NullInputsLeaveHealthUnchanged()
        {
            var attacker = new Combatant("player", 100);
            var target = new Combatant("enemy", 100);
            var modifiers = new[] { _infusion };

            Assert.Throws<ArgumentNullException>(() => _resolver.Resolve(null!, target, _ability, modifiers));
            Assert.Throws<ArgumentNullException>(() => _resolver.Resolve(attacker, null!, _ability, modifiers));
            Assert.Throws<ArgumentNullException>(() => _resolver.Resolve(attacker, target, null!, modifiers));
            Assert.Throws<ArgumentNullException>(() => _resolver.Resolve(attacker, target, _ability, null!));

            Assert.Equal(100, attacker.Health);
            Assert.Equal(100, target.Health);
        }

        [Fact]
        public void Resolve_RepeatedAttacksAccumulateWithoutChangingEarlierResult()
        {
            var attacker = new Combatant("player", 100);
            var target = new Combatant("enemy", 100);

            var first = _resolver.Resolve(attacker, target, _ability, new[] { _infusion });
            var second = _resolver.Resolve(attacker, target, _ability, new[] { _infusion });

            Assert.Equal(100, first.TargetHealthBefore);
            Assert.Equal(48, first.TargetHealthAfter);
            Assert.Equal(52, first.AppliedDamage);
            Assert.Equal(48, second.TargetHealthBefore);
            Assert.Equal(0, second.TargetHealthAfter);
            Assert.Equal(48, second.AppliedDamage);
            Assert.Equal(0, target.Health);
        }

        [Fact]
        public void Resolve_IdenticalInitialStateProducesEquivalentOutcomes()
        {
            var first = _resolver.Resolve(
                new Combatant("player", 100), new Combatant("enemy", 100), _ability, new[] { _infusion });
            var second = _resolver.Resolve(
                new Combatant("player", 100), new Combatant("enemy", 100), _ability, new[] { _infusion });

            Assert.Equal(first.AttackerId, second.AttackerId);
            Assert.Equal(first.TargetId, second.TargetId);
            Assert.Equal(first.TargetHealthBefore, second.TargetHealthBefore);
            Assert.Equal(first.TargetHealthAfter, second.TargetHealthAfter);
            Assert.Equal(first.Damage.AbilityId, second.Damage.AbilityId);
            Assert.Equal(first.Damage.Contributions, second.Damage.Contributions);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Combatant_NonPositiveMaximumHealthIsRejected(int health)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Combatant("player", health));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Combatant_MissingIdentityIsRejected(string? id)
        {
            Assert.Throws<ArgumentException>(() => new Combatant(id!, 100));
        }

        private IEnumerable<AbilityModifierDefinition> FailingModifiers()
        {
            yield return _infusion;
            throw new InvalidOperationException("Modifier source failed.");
        }
    }
}
