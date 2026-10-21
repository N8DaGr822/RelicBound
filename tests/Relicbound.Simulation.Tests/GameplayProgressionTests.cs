using System;
using System.Linq;
using Relicbound.Core.Abilities;
using Relicbound.Core.Combat;
using Relicbound.Core.Progression;
using Relicbound.Simulation.Combat;
using Relicbound.Simulation.Entities;
using Xunit;

namespace Relicbound.Simulation.Tests
{
    public sealed class GameplayProgressionTests
    {
        private readonly AbilityDefinition _ability = new(
            "crushing-blow", new[] { new DamageEffect(DamageType.Physical, 40) }, 10, 2);
        private readonly AbilityModifierDefinition _infusion = new(
            "lightning-infusion", new[] { new DamageEffect(DamageType.Lightning, 12) }, null, ModifierSource.Discovery);
        private readonly AbilityModifierDefinition _stormheart = new(
            "stormheart", Array.Empty<DamageEffect>(), new ChainEffect(DamageType.Lightning, 2), ModifierSource.Relic);
        private readonly AbilityModifierDefinition _tempest = new(
            "tempest", Array.Empty<DamageEffect>(), new ChainEffect(DamageType.Lightning, 1, true), ModifierSource.Mastery);
        private readonly ProgressionRules _rules = new(
            "runic-shrine", "lightning-infusion", "storm-trial", "tempest", DamageType.Lightning, 3);

        [Fact]
        public void CompleteEncounter_DiscoveryRewardIsPersonalAndIdempotent()
        {
            var resolver = CreateResolver();
            var character = new Combatant("player", 100);
            var other = new Combatant("other", 100);

            Assert.True(resolver.CompleteEncounter(character, _rules.DiscoveryEncounterId));
            Assert.False(resolver.CompleteEncounter(character, _rules.DiscoveryEncounterId));
            Assert.True(character.HasDiscovery(_infusion.Id));
            Assert.True(character.HasCompletedEncounter(_rules.DiscoveryEncounterId));
            Assert.False(other.HasDiscovery(_infusion.Id));
            Assert.False(character.HasMastery(_tempest.Id));
            Assert.Equal(0, character.GetMasteryChainAttacks(_tempest.Id));
            Assert.Equal(100, character.Resource);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Tempest_RequiresBothCombatCreditAndTrialInEitherOrder(bool trialFirst)
        {
            var resolver = CreateResolver();
            var character = PrepareCharacter(resolver);
            if (trialFirst)
            {
                resolver.CompleteEncounter(character, _rules.MasteryEncounterId);
            }

            Assert.False(character.HasMastery(_tempest.Id));
            for (var index = 0; index < 3; index++)
            {
                Attack(resolver, character, index * 2);
                Assert.Equal(index + 1, character.GetMasteryChainAttacks(_tempest.Id));
                Assert.Equal(trialFirst && index == 2, character.HasMastery(_tempest.Id));
            }

            if (!trialFirst)
            {
                Assert.False(character.HasMastery(_tempest.Id));
                resolver.CompleteEncounter(character, _rules.MasteryEncounterId);
            }

            Assert.True(character.HasMastery(_tempest.Id));
            Assert.Equal(70, character.Resource);
            Assert.False(resolver.CompleteEncounter(character, _rules.MasteryEncounterId));
            Assert.Equal(3, character.GetMasteryChainAttacks(_tempest.Id));
        }

        [Fact]
        public void EarnedMasteryIsNotAutomaticallySelected()
        {
            var resolver = CreateResolver();
            var character = PrepareCharacter(resolver);
            resolver.CompleteEncounter(character, _rules.MasteryEncounterId);
            for (var index = 0; index < 3; index++)
            {
                Attack(resolver, character, index * 2);
            }

            var withoutMastery = resolver.ResolveLoadout(character, new Combatant("primary", 100),
                _ability, new AbilityLoadout(_infusion.Id), Enemies(), 6);
            var withMastery = resolver.ResolveLoadout(character, new Combatant("primary", 100),
                _ability, new AbilityLoadout(_infusion.Id, _tempest.Id), Enemies(), 8);

            Assert.Equal(3, withoutMastery.Count);
            Assert.Equal(4, withMastery.Count);
            Assert.Equal(52L, withMastery[0].Damage.TotalDamage);
        }

        [Fact]
        public void PrimaryOnlyAttackDoesNotEarnChainCredit()
        {
            var resolver = CreateResolver();
            var character = PrepareCharacter(resolver);
            resolver.ResolveLoadout(character, new Combatant("primary", 100), _ability,
                new AbilityLoadout(_infusion.Id), Array.Empty<Combatant>(), 0);
            character.EquipRelic(null);
            resolver.ResolveLoadout(character, new Combatant("next", 100), _ability,
                new AbilityLoadout(_infusion.Id), Enemies(), 2);

            Assert.Equal(0, character.GetMasteryChainAttacks(_tempest.Id));
        }

        [Fact]
        public void RawModifierExecutionDoesNotAwardProgression()
        {
            var resolver = CreateResolver();
            var character = PrepareCharacter(resolver);
            resolver.Resolve(character, new Combatant("primary", 100), _ability,
                new[] { _infusion, _stormheart }, Enemies(), 0);

            Assert.Equal(0, character.GetMasteryChainAttacks(_tempest.Id));
            Assert.Equal(90, character.Resource);
        }

        [Fact]
        public void CooldownFailureDoesNotAdvanceAnyCombatOrProgressionState()
        {
            var resolver = CreateResolver();
            var character = PrepareCharacter(resolver);
            Attack(resolver, character, 0);
            var primary = new Combatant("primary", 100);
            var enemies = Enemies();

            Assert.Throws<InvalidOperationException>(() => resolver.ResolveLoadout(character,
                primary, _ability, new AbilityLoadout(_infusion.Id), enemies, 1));

            Assert.Equal(1, character.GetMasteryChainAttacks(_tempest.Id));
            Assert.Equal(90, character.Resource);
            Assert.Equal(2, character.GetAbilityReadyTick(_ability.Id));
            Assert.Equal(0, character.LastActionTick);
            Assert.Equal(100, primary.Health);
            Assert.All(enemies, enemy => Assert.Equal(100, enemy.Health));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void InvalidAttackLeavesHealthResourceCooldownAndProgressUnchanged(int failure)
        {
            var resolver = CreateResolver();
            var character = new Combatant("player", 100, failure == 0 ? 5 : 100);
            resolver.CompleteEncounter(character, _rules.DiscoveryEncounterId);
            character.EquipRelic(failure == 2 ? null : _stormheart.Id);
            if (failure == 2)
            {
                character.UnlockMastery(_tempest.Id);
            }

            var loadout = new AbilityLoadout(_infusion.Id, failure is 1 or 2 ? _tempest.Id : null);
            var primary = new Combatant("primary", 100);
            var enemies = Enemies();
            var candidates = failure == 3 ? new[] { enemies[0], null! } : enemies;

            Action attack = () => resolver.ResolveLoadout(
                character, primary, _ability, loadout, candidates, 10);
            if (failure <= 1)
            {
                Assert.Throws<InvalidOperationException>(attack);
            }
            else
            {
                Assert.Throws<ArgumentException>(attack);
            }

            Assert.Equal(failure == 0 ? 5 : 100, character.Resource);
            Assert.Equal(0, character.LastActionTick);
            Assert.Equal(0, character.GetAbilityReadyTick(_ability.Id));
            Assert.Equal(0, character.GetMasteryChainAttacks(_tempest.Id));
            Assert.Equal(100, primary.Health);
            Assert.All(enemies, enemy => Assert.Equal(100, enemy.Health));
        }

        [Fact]
        public void ProgressionDoesNotTransferWithRelicReassignment()
        {
            var resolver = CreateResolver();
            var character = PrepareCharacter(resolver);
            resolver.CompleteEncounter(character, _rules.MasteryEncounterId);
            Attack(resolver, character, 0);
            character.EquipRelic(null);
            var recipient = new Combatant("recipient", 100);
            recipient.EquipRelic(_stormheart.Id);

            Assert.Equal(1, character.GetMasteryChainAttacks(_tempest.Id));
            Assert.True(character.HasCompletedEncounter(_rules.MasteryEncounterId));
            Assert.Equal(0, recipient.GetMasteryChainAttacks(_tempest.Id));
            Assert.False(recipient.HasCompletedEncounter(_rules.MasteryEncounterId));
            Assert.False(recipient.HasDiscovery(_infusion.Id));
            Assert.False(recipient.HasMastery(_tempest.Id));
        }

        [Fact]
        public void TrialBeforeDiscoveryDoesNotBypassRequirements()
        {
            var resolver = CreateResolver();
            var character = new Combatant("player", 100);
            resolver.CompleteEncounter(character, _rules.MasteryEncounterId);

            Assert.False(character.HasDiscovery(_infusion.Id));
            Assert.False(character.HasMastery(_tempest.Id));
            Assert.Throws<InvalidOperationException>(() => Attack(resolver, character, 0));
            Assert.Equal(100, character.Resource);
            Assert.Equal(0, character.GetMasteryChainAttacks(_tempest.Id));
        }

        [Theory]
        [InlineData("unknown")]
        [InlineData("RUNIC-SHRINE")]
        [InlineData("")]
        [InlineData(null)]
        public void CompleteEncounter_UnknownIdCannotGrantProgression(string? id)
        {
            var resolver = CreateResolver();
            var character = new Combatant("player", 100);

            Assert.Throws<ArgumentException>(() => resolver.CompleteEncounter(character, id!));
            Assert.False(character.HasDiscovery(_infusion.Id));
            Assert.False(character.HasMastery(_tempest.Id));
            Assert.False(character.HasCompletedEncounter(_rules.DiscoveryEncounterId));
        }

        [Fact]
        public void CompleteEncounter_DeadCharacterCannotEarnReward()
        {
            var character = new Combatant("player", 1);
            new CombatResolver().Resolve(new Combatant("enemy", 100), character,
                _ability, Array.Empty<AbilityModifierDefinition>());

            Assert.Throws<InvalidOperationException>(() =>
                CreateResolver().CompleteEncounter(character, _rules.DiscoveryEncounterId));
            Assert.False(character.HasDiscovery(_infusion.Id));
            Assert.False(character.HasCompletedEncounter(_rules.DiscoveryEncounterId));
        }

        [Fact]
        public void ProgressionRules_CannotReferenceMissingOrWrongCategoryDefinitions()
        {
            Assert.Throws<ArgumentException>(() =>
                new CombatResolver(new[] { _infusion, _stormheart }, _rules));
            var invalid = new ProgressionRules("shrine", _stormheart.Id, "trial", _tempest.Id, DamageType.Lightning, 3);
            Assert.Throws<ArgumentException>(() =>
                new CombatResolver(new[] { _infusion, _stormheart, _tempest }, invalid));
        }

        [Fact]
        public void CompleteEncounter_RequiresConfiguredRules()
        {
            Assert.Throws<InvalidOperationException>(() =>
                new CombatResolver().CompleteEncounter(new Combatant("player", 100), _rules.DiscoveryEncounterId));
        }

        [Fact]
        public void EndToEndProgressionIsRepeatable()
        {
            (int resource, int credit, bool unlocked, long ready, int[] health) Run()
            {
                var resolver = CreateResolver();
                var character = PrepareCharacter(resolver);
                for (var index = 0; index < 3; index++)
                {
                    Attack(resolver, character, index * 2);
                }

                resolver.CompleteEncounter(character, _rules.MasteryEncounterId);
                var enemies = Enemies();
                resolver.ResolveLoadout(character, new Combatant("primary", 100), _ability,
                    new AbilityLoadout(_infusion.Id, _tempest.Id), enemies, 6);
                return (character.Resource, character.GetMasteryChainAttacks(_tempest.Id),
                    character.HasMastery(_tempest.Id), character.GetAbilityReadyTick(_ability.Id),
                    enemies.Select(enemy => enemy.Health).ToArray());
            }

            var first = Run();
            var second = Run();
            Assert.Equal(60, first.resource);
            Assert.Equal(4, first.credit);
            Assert.True(first.unlocked);
            Assert.Equal(8, first.ready);
            Assert.Equal(new[] { 88, 88, 88 }, first.health);
            Assert.Equal(first.resource, second.resource);
            Assert.Equal(first.credit, second.credit);
            Assert.Equal(first.unlocked, second.unlocked);
            Assert.Equal(first.ready, second.ready);
            Assert.Equal(first.health, second.health);
        }

        [Fact]
        public void MasteryCreditRequiresConfiguredChainDamageType()
        {
            var rules = new ProgressionRules(
                "runic-shrine", _infusion.Id, "storm-trial", _tempest.Id, DamageType.Physical, 1);
            var resolver = new CombatResolver(new[] { _infusion, _stormheart, _tempest }, rules);
            var character = PrepareCharacter(resolver);
            resolver.CompleteEncounter(character, rules.MasteryEncounterId);

            Attack(resolver, character, 0);

            Assert.Equal(0, character.GetMasteryChainAttacks(_tempest.Id));
            Assert.False(character.HasMastery(_tempest.Id));
        }

        [Fact]
        public void MasteryThresholdComesFromConfiguredRules()
        {
            var rules = new ProgressionRules(
                "runic-shrine", _infusion.Id, "storm-trial", _tempest.Id, DamageType.Lightning, 1);
            var resolver = new CombatResolver(new[] { _infusion, _stormheart, _tempest }, rules);
            var character = PrepareCharacter(resolver);
            resolver.CompleteEncounter(character, rules.MasteryEncounterId);

            Attack(resolver, character, 0);

            Assert.Equal(1, character.GetMasteryChainAttacks(_tempest.Id));
            Assert.True(character.HasMastery(_tempest.Id));
        }

        [Fact]
        public void ProgressionRules_RejectAmbiguousOrInvalidRequirements()
        {
            Assert.Throws<ArgumentException>(() =>
                new ProgressionRules("same", _infusion.Id, "same", _tempest.Id, DamageType.Lightning, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ProgressionRules("shrine", _infusion.Id, "trial", _tempest.Id, DamageType.Lightning, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ProgressionRules("shrine", _infusion.Id, "trial", _tempest.Id, (DamageType)999, 3));
            Assert.Throws<ArgumentException>(() =>
                new ProgressionRules(" ", _infusion.Id, "trial", _tempest.Id, DamageType.Lightning, 3));
        }

        private CombatResolver CreateResolver() => new(new[] { _infusion, _stormheart, _tempest }, _rules);

        private Combatant PrepareCharacter(CombatResolver resolver)
        {
            var character = new Combatant("player", 100);
            resolver.CompleteEncounter(character, _rules.DiscoveryEncounterId);
            character.EquipRelic(_stormheart.Id);
            return character;
        }

        private void Attack(CombatResolver resolver, Combatant character, long tick)
        {
            resolver.ResolveLoadout(character, new Combatant("primary", 100), _ability,
                new AbilityLoadout(_infusion.Id), Enemies(), tick);
        }

        private static Combatant[] Enemies()
        {
            return Enumerable.Range(0, 3).Select(index => new Combatant($"enemy-{index}", 100)).ToArray();
        }
    }
}
