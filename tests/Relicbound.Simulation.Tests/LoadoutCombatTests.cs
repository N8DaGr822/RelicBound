using System;
using System.Linq;
using Relicbound.Core.Abilities;
using Relicbound.Core.Combat;
using Relicbound.Simulation.Combat;
using Relicbound.Simulation.Entities;
using Xunit;

namespace Relicbound.Simulation.Tests
{
    public sealed class LoadoutCombatTests
    {
        private readonly AbilityDefinition _ability = new(
            "crushing-blow", new[] { new DamageEffect(DamageType.Physical, 40) });
        private readonly AbilityModifierDefinition _infusion = new(
            "lightning-infusion", new[] { new DamageEffect(DamageType.Lightning, 12) },
            null, ModifierSource.Discovery);
        private readonly AbilityModifierDefinition _stormheart = new(
            "stormheart", Array.Empty<DamageEffect>(),
            new ChainEffect(DamageType.Lightning, 2), ModifierSource.Relic);
        private readonly AbilityModifierDefinition _tempest = new(
            "tempest", Array.Empty<DamageEffect>(),
            new ChainEffect(DamageType.Lightning, 1, true), ModifierSource.Mastery);

        [Fact]
        public void ResolveLoadout_SameRelicWithPersonalMasteryProducesDifferentChainCounts()
        {
            var resolver = CreateResolver();
            var novice = CreateCharacter("novice");
            var veteran = CreateCharacter("veteran");
            veteran.UnlockMastery(_tempest.Id);
            var noviceEnemies = Enemies();
            var veteranEnemies = Enemies();

            var noviceResults = resolver.ResolveLoadout(novice, new Combatant("primary", 100),
                _ability, new AbilityLoadout(_infusion.Id), noviceEnemies);
            var veteranResults = resolver.ResolveLoadout(veteran, new Combatant("primary", 100),
                _ability, new AbilityLoadout(_infusion.Id, _tempest.Id), veteranEnemies);

            Assert.Equal(novice.EquippedRelicId, veteran.EquippedRelicId);
            Assert.False(novice.HasMastery(_tempest.Id));
            Assert.Equal(3, noviceResults.Count);
            Assert.Equal(4, veteranResults.Count);
            Assert.Equal(52L, noviceResults[0].Damage.TotalDamage);
            Assert.Equal(noviceResults[0].Damage.Contributions, veteranResults[0].Damage.Contributions);
            Assert.Equal(100, noviceEnemies[2].Health);
            Assert.Equal(88, veteranEnemies[2].Health);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ResolveLoadout_LockedSelectionLeavesAllHealthUnchanged(bool lockedMastery)
        {
            var attacker = new Combatant("player", 100);
            attacker.EquipRelic(_stormheart.Id);
            if (lockedMastery)
            {
                attacker.UnlockDiscovery(_infusion.Id);
            }

            var primary = new Combatant("primary", 100);
            var enemies = Enemies();
            var loadout = new AbilityLoadout(_infusion.Id, lockedMastery ? _tempest.Id : null);

            Assert.Throws<InvalidOperationException>(() =>
                CreateResolver().ResolveLoadout(attacker, primary, _ability, loadout, enemies));

            Assert.Equal(100, attacker.Health);
            Assert.Equal(100, primary.Health);
            Assert.All(enemies, enemy => Assert.Equal(100, enemy.Health));
            Assert.False(attacker.HasMastery(_tempest.Id));
        }

        [Fact]
        public void ResolveLoadout_UnlockedButUnselectedMasteryDoesNotActivate()
        {
            var attacker = CreateCharacter("player");
            attacker.UnlockMastery(_tempest.Id);

            var results = CreateResolver().ResolveLoadout(
                attacker, new Combatant("primary", 100), _ability,
                new AbilityLoadout(_infusion.Id), Enemies());

            Assert.Equal(3, results.Count);
            Assert.Equal(2, results[0].Damage.ChainTargetCount);
        }

        [Fact]
        public void ResolveLoadout_EmptyLoadoutDoesNotActivateEarnedUnlocks()
        {
            var attacker = new Combatant("player", 100);
            attacker.UnlockDiscovery(_infusion.Id);
            attacker.UnlockMastery(_tempest.Id);
            var enemies = Enemies();

            var results = CreateResolver().ResolveLoadout(
                attacker, new Combatant("primary", 100), _ability, new AbilityLoadout(), enemies);

            Assert.Equal(40L, Assert.Single(results).Damage.TotalDamage);
            Assert.All(enemies, enemy => Assert.Equal(100, enemy.Health));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ResolveLoadout_MissingEffectPrerequisiteRejectsBeforeMutation(bool missingRelic)
        {
            var attacker = CreateCharacter("player");
            attacker.UnlockMastery(_tempest.Id);
            if (missingRelic)
            {
                attacker.EquipRelic(null);
            }

            var loadout = missingRelic
                ? new AbilityLoadout(_infusion.Id, _tempest.Id)
                : new AbilityLoadout(masteryId: _tempest.Id);
            var primary = new Combatant("primary", 100);
            var enemies = Enemies();

            Assert.Throws<ArgumentException>(() =>
                CreateResolver().ResolveLoadout(attacker, primary, _ability, loadout, enemies));

            Assert.Equal(100, primary.Health);
            Assert.All(enemies, enemy => Assert.Equal(100, enemy.Health));
        }

        [Theory]
        [InlineData("unknown", null, "stormheart")]
        [InlineData("LIGHTNING-INFUSION", null, "stormheart")]
        [InlineData("tempest", null, "stormheart")]
        [InlineData("lightning-infusion", "stormheart", "stormheart")]
        [InlineData("lightning-infusion", null, "tempest")]
        [InlineData("lightning-infusion", null, "unknown-relic")]
        public void ResolveLoadout_UnknownOrWrongCategoryIdsCannotSupplyEffects(
            string discoveryId, string? masteryId, string relicId)
        {
            var attacker = new Combatant("player", 100);
            attacker.UnlockDiscovery(discoveryId);
            if (masteryId is not null)
            {
                attacker.UnlockMastery(masteryId);
            }

            attacker.EquipRelic(relicId);
            var primary = new Combatant("primary", 100);
            var enemies = Enemies();

            Assert.Throws<ArgumentException>(() => CreateResolver().ResolveLoadout(
                attacker, primary, _ability, new AbilityLoadout(discoveryId, masteryId), enemies));

            Assert.Equal(100, primary.Health);
            Assert.All(enemies, enemy => Assert.Equal(100, enemy.Health));
        }

        [Fact]
        public void EquippingSameRelicOnAnotherCharacterDoesNotTransferUnlocks()
        {
            var veteran = CreateCharacter("veteran");
            veteran.UnlockMastery(_tempest.Id);
            var recipient = new Combatant("recipient", 100);

            veteran.EquipRelic(null);
            recipient.EquipRelic(_stormheart.Id);

            Assert.True(veteran.HasDiscovery(_infusion.Id));
            Assert.True(veteran.HasMastery(_tempest.Id));
            Assert.False(recipient.HasDiscovery(_infusion.Id));
            Assert.False(recipient.HasMastery(_tempest.Id));
            Assert.Null(veteran.EquippedRelicId);
            Assert.Equal(_stormheart.Id, recipient.EquippedRelicId);
        }

        [Fact]
        public void UnlocksAreIdempotentAndCaseSensitive()
        {
            var attacker = CreateCharacter("player");
            attacker.UnlockDiscovery(_infusion.Id);
            attacker.UnlockMastery(_tempest.Id);
            attacker.UnlockMastery(_tempest.Id);

            Assert.False(attacker.HasDiscovery("LIGHTNING-INFUSION"));
            Assert.False(attacker.HasMastery("TEMPEST"));
            var results = CreateResolver().ResolveLoadout(
                attacker, new Combatant("primary", 100), _ability,
                new AbilityLoadout(_infusion.Id, _tempest.Id), Enemies());
            Assert.Equal(52L, results[0].Damage.TotalDamage);
            Assert.Equal(4, results.Count);
        }

        [Fact]
        public void Resolver_CopiesTrustedCatalogAndUsesRegisteredDamage()
        {
            var catalog = new[] { _infusion, _stormheart, _tempest };
            var resolver = new CombatResolver(catalog);
            catalog[0] = new AbilityModifierDefinition(
                _infusion.Id, new[] { new DamageEffect(DamageType.Lightning, 999) },
                null, ModifierSource.Discovery);

            var results = resolver.ResolveLoadout(
                CreateCharacter("player"), new Combatant("primary", 100),
                _ability, new AbilityLoadout(_infusion.Id), Enemies());

            Assert.Equal(52L, results[0].Damage.TotalDamage);
        }

        [Fact]
        public void Resolver_RejectsDuplicateNullOrUnclassifiedCatalogEntries()
        {
            Assert.Throws<ArgumentException>(() => new CombatResolver(new[] { _infusion, _infusion }));
            Assert.Throws<ArgumentException>(() => new CombatResolver(new AbilityModifierDefinition[] { null! }));
            Assert.Throws<ArgumentException>(() => new CombatResolver(new[]
            {
                new AbilityModifierDefinition("legacy", new[] { new DamageEffect(DamageType.Physical, 1) })
            }));
            Assert.Throws<ArgumentNullException>(() => new CombatResolver(null!));
        }

        [Fact]
        public void ResolveLoadout_NullLoadoutOrAttackerIsRejectedWithoutMutation()
        {
            var resolver = CreateResolver();
            var primary = new Combatant("primary", 100);
            var enemies = Enemies();

            Assert.Throws<ArgumentNullException>(() => resolver.ResolveLoadout(
                null!, primary, _ability, new AbilityLoadout(), enemies));
            Assert.Throws<ArgumentNullException>(() => resolver.ResolveLoadout(
                CreateCharacter("player"), primary, _ability, null!, enemies));

            Assert.Equal(100, primary.Health);
            Assert.All(enemies, enemy => Assert.Equal(100, enemy.Health));
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public void LoadoutAndCharacter_RejectBlankIds(string id)
        {
            var attacker = new Combatant("player", 100);
            Assert.Throws<ArgumentException>(() => new AbilityLoadout(discoveryId: id));
            Assert.Throws<ArgumentException>(() => new AbilityLoadout(masteryId: id));
            Assert.Throws<ArgumentException>(() => attacker.UnlockDiscovery(id));
            Assert.Throws<ArgumentException>(() => attacker.UnlockMastery(id));
            Assert.Throws<ArgumentException>(() => attacker.EquipRelic(id));
        }

        private CombatResolver CreateResolver() => new(new[] { _infusion, _stormheart, _tempest });

        private Combatant CreateCharacter(string id)
        {
            var character = new Combatant(id, 100);
            character.UnlockDiscovery(_infusion.Id);
            character.EquipRelic(_stormheart.Id);
            return character;
        }

        private static Combatant[] Enemies()
        {
            return Enumerable.Range(0, 3).Select(index => new Combatant($"enemy-{index}", 100)).ToArray();
        }
    }
}
