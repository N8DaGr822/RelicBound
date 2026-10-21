using System;
using Relicbound.Core.Abilities;
using Relicbound.Core.Combat;
using Relicbound.Simulation.Combat;
using Relicbound.Simulation.Entities;
using Xunit;

namespace Relicbound.Simulation.Tests
{
    public sealed class ProtectiveMasteryTests
    {
        private readonly AbilityDefinition _ability = new("strike", new[] { new DamageEffect(DamageType.Physical, 40) });
        private readonly AbilityModifierDefinition _infusion = new("infusion", new[] { new DamageEffect(DamageType.Lightning, 12) }, null, ModifierSource.Discovery);
        private readonly AbilityModifierDefinition _relic = new("relic", Array.Empty<DamageEffect>(), new ChainEffect(DamageType.Lightning, 2), ModifierSource.Relic);
        private readonly AbilityModifierDefinition _guardian = new("guardian", Array.Empty<DamageEffect>(), null, ModifierSource.Mastery, Array.Empty<ReactionDefinition>(), 20);

        [Fact]
        public void ProtectiveMasteryReplacesChainsWithAnAllyBarrier()
        {
            var resolver = new CombatResolver(new[] { _infusion, _relic, _guardian });
            var attacker = Player();
            var ally = new Combatant("ally", 100);
            var enemy = new Combatant("enemy", 100);
            var secondary = new Combatant("secondary", 100);
            var result = resolver.ResolveLoadout(attacker, enemy, _ability,
                new AbilityLoadout("infusion", "guardian"), new[] { secondary }, 0, ally);
            Assert.Equal(2, result.Count);
            Assert.Equal(48, enemy.Health);
            Assert.Equal(100, secondary.Health);
            Assert.Equal(100, ally.Health);
            Assert.Equal(20, ally.Barrier);
            Assert.Equal("mastery-barrier", Assert.Single(result[1].Events).Reason);
            var hit = resolver.Resolve(new Combatant("attacking-enemy", 100), ally,
                new AbilityDefinition("hit", new[] { new DamageEffect(DamageType.Physical, 25) }), Array.Empty<AbilityModifierDefinition>());
            Assert.Equal(95, ally.Health);
            Assert.Equal(0, ally.Barrier);
            Assert.Equal(5, hit.AppliedDamage);
            Assert.Equal(20, hit.BarrierBefore);
        }

        [Fact]
        public void LockedProtectiveMasteryCannotGrantBarrier()
        {
            var resolver = new CombatResolver(new[] { _infusion, _relic, _guardian });
            var player = new Combatant("player", 100);
            player.UnlockDiscovery("infusion");
            player.EquipRelic("relic");
            var ally = new Combatant("ally", 100);
            var enemy = new Combatant("enemy", 100);
            Assert.Throws<InvalidOperationException>(() => resolver.ResolveLoadout(player, enemy, _ability,
                new AbilityLoadout("infusion", "guardian"), Array.Empty<Combatant>(), 0, ally));
            Assert.Equal(100, enemy.Health);
            Assert.Equal(0, ally.Barrier);
        }

        [Fact]
        public void BarrierCapsAtAllyMaximumHealth()
        {
            var resolver = new CombatResolver();
            var player = Player();
            var ally = new Combatant("ally", 15);
            for (var index = 0; index < 2; index++)
                resolver.Resolve(player, new Combatant("enemy", 100), _ability,
                    new[] { _infusion, _relic, _guardian }, Array.Empty<Combatant>(), 0, ally);
            Assert.Equal(15, ally.Barrier);
        }

        [Fact]
        public void InvalidAllyOrMissingRelicDoesNotCommitDamage()
        {
            var player = Player();
            var enemy = new Combatant("enemy", 100);
            var resolver = new CombatResolver();
            Assert.Throws<ArgumentException>(() => resolver.Resolve(player, enemy, _ability,
                new[] { _infusion, _relic, _guardian }, Array.Empty<Combatant>(), 0, enemy));
            Assert.Throws<ArgumentException>(() => resolver.Resolve(player, enemy, _ability,
                new[] { _infusion, _guardian }, Array.Empty<Combatant>(), 0, new Combatant("ally", 100)));
            Assert.Equal(100, enemy.Health);
        }

        private static Combatant Player()
        {
            var player = new Combatant("player", 100);
            player.UnlockDiscovery("infusion");
            player.UnlockMastery("guardian");
            player.EquipRelic("relic");
            return player;
        }
    }
}
