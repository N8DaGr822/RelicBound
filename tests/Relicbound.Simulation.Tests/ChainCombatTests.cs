using System;
using System.Collections.Generic;
using System.Linq;
using Relicbound.Core.Abilities;
using Relicbound.Core.Combat;
using Relicbound.Simulation.Combat;
using Relicbound.Simulation.Entities;
using Xunit;

namespace Relicbound.Simulation.Tests
{
    public sealed class ChainCombatTests
    {
        private readonly CombatResolver _resolver = new();
        private readonly AbilityDefinition _ability = new(
            "crushing-blow", new[] { new DamageEffect(DamageType.Physical, 40) });
        private readonly AbilityModifierDefinition _infusion = new(
            "lightning-infusion", new[] { new DamageEffect(DamageType.Lightning, 12) });
        private readonly AbilityModifierDefinition _stormheart = Chain("stormheart", 2);
        private readonly AbilityModifierDefinition _tempest = Chain("tempest", 1, true);

        [Theory]
        [InlineData(false, false, 0)]
        [InlineData(true, false, 2)]
        [InlineData(true, true, 3)]
        public void Resolve_ComposesChainCountWithoutChangingPrimaryDamage(bool relic, bool mastery, int expectedChains)
        {
            var modifiers = new List<AbilityModifierDefinition> { _infusion };
            if (relic)
            {
                modifiers.Add(_stormheart);
            }

            if (mastery)
            {
                modifiers.Add(_tempest);
            }
            var attacker = new Combatant("player", 100);
            var target = new Combatant("primary", 100);
            var candidates = Enemies(4);

            var results = _resolver.Resolve(attacker, target, _ability, modifiers, candidates);

            Assert.Equal(1 + expectedChains, results.Count);
            Assert.Equal(52L, results[0].Damage.TotalDamage);
            Assert.Equal(48, target.Health);
            Assert.Equal(100, attacker.Health);
            Assert.Equal(expectedChains, results[0].Damage.ChainTargetCount);
            for (var index = 0; index < candidates.Length; index++)
            {
                Assert.Equal(index < expectedChains ? 88 : 100, candidates[index].Health);
            }

            foreach (var result in results.Skip(1))
            {
                Assert.Equal("stormheart", result.TriggerSourceId);
                Assert.Equal(12, result.AppliedDamage);
                Assert.Equal(0L, result.Damage.GetDamage(DamageType.Physical));
                Assert.Equal(0, result.Damage.ChainTargetCount);
                Assert.Null(result.Damage.ChainSourceId);
                Assert.Equal("lightning-infusion", Assert.Single(result.Damage.Contributions).SourceId);
            }
        }

        [Fact]
        public void Resolve_ExtensionMayPrecedeRelicInLoadout()
        {
            var first = _resolver.Resolve(_ability, new[] { _infusion, _stormheart, _tempest });
            var second = _resolver.Resolve(_ability, new[] { _tempest, _stormheart, _infusion });

            Assert.Equal(first.Contributions, second.Contributions);
            Assert.Equal(first.ChainTargetCount, second.ChainTargetCount);
            Assert.Equal(first.ChainDamageType, second.ChainDamageType);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void Resolve_InsufficientTargetsUsesOnlyAvailableEnemies(int available)
        {
            var results = _resolver.Resolve(
                new Combatant("player", 100), new Combatant("primary", 100),
                _ability, new[] { _infusion, _stormheart, _tempest }, Enemies(available));

            Assert.Equal(1 + available, results.Count);
        }

        [Fact]
        public void Resolve_CyclicAndDuplicateCandidatesCannotRetriggerChains()
        {
            var attacker = new Combatant("player", 100);
            var primary = new Combatant("primary", 100);
            var enemies = Enemies(4);
            var alias = new Combatant(enemies[0].Id, 100);
            var dead = new Combatant("dead", 1);
            _resolver.Resolve(attacker, dead, _ability, Array.Empty<AbilityModifierDefinition>());
            var candidates = new[]
            {
                attacker, primary, dead, enemies[0], enemies[0], alias,
                primary, enemies[1], attacker, enemies[2], enemies[0], enemies[3]
            };

            var results = _resolver.Resolve(
                attacker, primary, _ability, new[] { _infusion, _stormheart, _tempest }, candidates);

            Assert.Equal(new[] { "primary", "enemy-0", "enemy-1", "enemy-2" }, results.Select(result => result.TargetId));
            Assert.Equal(48, primary.Health);
            Assert.Equal(100, attacker.Health);
            Assert.Equal(100, alias.Health);
            Assert.Equal(0, dead.Health);
            Assert.Equal(100, enemies[3].Health);
            Assert.All(enemies.Take(3), enemy => Assert.Equal(88, enemy.Health));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void Resolve_InvalidCombinationLeavesAllHealthUnchanged(int scenario)
        {
            var modifiers = scenario switch
            {
                0 => new[] { _stormheart },
                1 => new[] { _infusion, _tempest },
                2 => new[] { _infusion, _stormheart, _stormheart },
                3 => new[] { _infusion, _stormheart, _tempest, _tempest },
                4 => new[] { _infusion, _stormheart, Chain("other-relic", 2) },
                _ => new[]
                {
                    _infusion, _stormheart,
                    new AbilityModifierDefinition("wrong-type", Array.Empty<DamageEffect>(),
                        new ChainEffect(DamageType.Physical, 1, true))
                }
            };
            var attacker = new Combatant("player", 100);
            var primary = new Combatant("primary", 100);
            var candidates = Enemies(3);

            Assert.Throws<ArgumentException>(() =>
                _resolver.Resolve(attacker, primary, _ability, modifiers, candidates));

            Assert.Equal(100, attacker.Health);
            Assert.Equal(100, primary.Health);
            Assert.All(candidates, enemy => Assert.Equal(100, enemy.Health));
        }

        [Fact]
        public void Resolve_ChainBudgetIsEnforcedBeforeMutation()
        {
            var primary = new Combatant("primary", 100);
            var candidates = Enemies(CombatResolver.MaxChainTargets + 1);
            var modifiers = new[] { _infusion, Chain("large-chain", CombatResolver.MaxChainTargets), _tempest };

            Assert.Throws<ArgumentException>(() => _resolver.Resolve(
                new Combatant("player", 100), primary, _ability, modifiers, candidates));

            Assert.Equal(100, primary.Health);
            Assert.All(candidates, enemy => Assert.Equal(100, enemy.Health));
        }

        [Fact]
        public void Resolve_ExactBudgetTerminatesWithoutSecondaryActivation()
        {
            var candidates = Enemies(CombatResolver.MaxChainTargets + 1);
            var results = _resolver.Resolve(
                new Combatant("player", 100), new Combatant("primary", 100),
                _ability, new[] { _infusion, Chain("large-chain", CombatResolver.MaxChainTargets) }, candidates);

            Assert.Equal(1 + CombatResolver.MaxChainTargets, results.Count);
            Assert.All(results.Skip(1), result => Assert.Equal(0, result.Damage.ChainTargetCount));
            Assert.Equal(100, candidates.Last().Health);
        }

        [Fact]
        public void Resolve_LargeChainCountsAreRejectedWithoutIntegerOverflow()
        {
            Assert.Throws<ArgumentException>(() => _resolver.Resolve(
                _ability, new[] { _infusion, Chain("large-chain", int.MaxValue), Chain("large-bonus", int.MaxValue, true) }));
        }

        [Fact]
        public void Resolve_CandidateEnumerationFailureDoesNotApplyPartialDamage()
        {
            var primary = new Combatant("primary", 100);
            var enemy = new Combatant("enemy", 100);

            Assert.Throws<InvalidOperationException>(() => _resolver.Resolve(
                new Combatant("player", 100), primary, _ability,
                new[] { _infusion, _stormheart }, FailingCandidates(enemy)));

            Assert.Equal(100, primary.Health);
            Assert.Equal(100, enemy.Health);
        }

        [Fact]
        public void Resolve_NullCandidateAfterValidTargetsDoesNotApplyPartialDamage()
        {
            var primary = new Combatant("primary", 100);
            var enemies = Enemies(2);

            Assert.Throws<ArgumentException>(() => _resolver.Resolve(
                new Combatant("player", 100), primary, _ability, new[] { _infusion, _stormheart },
                new[] { enemies[0], enemies[1], null! }));

            Assert.Equal(100, primary.Health);
            Assert.All(enemies, enemy => Assert.Equal(100, enemy.Health));
        }

        [Fact]
        public void Resolve_PrimaryDeathDoesNotCancelChainsAndChainOverkillIsClamped()
        {
            var primary = new Combatant("primary", 1);
            var enemy = new Combatant("enemy", 5);
            var results = _resolver.Resolve(
                new Combatant("player", 100), primary, _ability, new[] { _infusion, _stormheart }, new[] { enemy });

            Assert.Equal(0, primary.Health);
            Assert.Equal(0, enemy.Health);
            Assert.Equal(5, results[1].AppliedDamage);
            Assert.Equal(12L, results[1].Damage.TotalDamage);
        }

        [Fact]
        public void Resolve_FreshIdenticalStateProducesIdenticalOrderedResults()
        {
            IReadOnlyList<CombatActionResult> Run()
            {
                return _resolver.Resolve(
                    new Combatant("player", 100), new Combatant("primary", 100),
                    _ability, new[] { _infusion, _stormheart, _tempest }, Enemies(4));
            }

            var first = Run();
            var second = Run();
            Assert.Equal(first.Count, second.Count);
            for (var index = 0; index < first.Count; index++)
            {
                Assert.Equal(first[index].AttackerId, second[index].AttackerId);
                Assert.Equal(first[index].TargetId, second[index].TargetId);
                Assert.Equal(first[index].TargetHealthBefore, second[index].TargetHealthBefore);
                Assert.Equal(first[index].TargetHealthAfter, second[index].TargetHealthAfter);
                Assert.Equal(first[index].TriggerSourceId, second[index].TriggerSourceId);
                Assert.Equal(first[index].Damage.Contributions, second[index].Damage.Contributions);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void ChainEffect_NonPositiveTargetCountIsRejected(int count)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChainEffect(DamageType.Lightning, count));
        }

        private static AbilityModifierDefinition Chain(string id, int count, bool extension = false)
        {
            return new AbilityModifierDefinition(
                id, Array.Empty<DamageEffect>(), new ChainEffect(DamageType.Lightning, count, extension));
        }

        private static Combatant[] Enemies(int count)
        {
            return Enumerable.Range(0, count).Select(index => new Combatant($"enemy-{index}", 100)).ToArray();
        }

        private static IEnumerable<Combatant> FailingCandidates(Combatant enemy)
        {
            yield return enemy;
            throw new InvalidOperationException("Candidate source failed.");
        }
    }
}
