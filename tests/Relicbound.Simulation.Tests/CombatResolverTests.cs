using System;
using Relicbound.Core.Abilities;
using Relicbound.Core.Combat;
using Relicbound.Simulation.Combat;
using Xunit;

namespace Relicbound.Simulation.Tests
{
    public sealed class CombatResolverTests
    {
        private readonly CombatResolver _resolver = new();

        [Fact]
        public void Resolve_WithoutModifiers_DealsOnlyBaseDamage()
        {
            var result = _resolver.Resolve(CreateCrushingBlow(), Array.Empty<AbilityModifierDefinition>());

            Assert.Equal(40L, result.TotalDamage);
            Assert.Equal(40L, result.GetDamage(DamageType.Physical));
            Assert.Equal(0L, result.GetDamage(DamageType.Lightning));
            Assert.Single(result.Contributions);
        }

        [Fact]
        public void Resolve_WithLightningInfusion_ComposesDamageAndPreservesSources()
        {
            var result = _resolver.Resolve(CreateCrushingBlow(), new[] { CreateLightningInfusion() });

            Assert.Equal("crushing-blow", result.AbilityId);
            Assert.Equal(52L, result.TotalDamage);
            Assert.Equal(40L, result.GetDamage(DamageType.Physical));
            Assert.Equal(12L, result.GetDamage(DamageType.Lightning));
            Assert.Collection(
                result.Contributions,
                physical =>
                {
                    Assert.Equal("crushing-blow", physical.SourceId);
                    Assert.Equal(DamageType.Physical, physical.DamageType);
                    Assert.Equal(40, physical.Amount);
                },
                lightning =>
                {
                    Assert.Equal("lightning-infusion", lightning.SourceId);
                    Assert.Equal(DamageType.Lightning, lightning.DamageType);
                    Assert.Equal(12, lightning.Amount);
                });
        }

        [Fact]
        public void Resolve_SameInputs_ProduceEquivalentResults()
        {
            var ability = CreateCrushingBlow();
            var modifiers = new[] { CreateLightningInfusion() };

            var first = _resolver.Resolve(ability, modifiers);
            var second = _resolver.Resolve(ability, modifiers);

            Assert.Equal(first.AbilityId, second.AbilityId);
            Assert.Equal(first.TotalDamage, second.TotalDamage);
            Assert.Equal(first.Contributions, second.Contributions);
        }

        [Fact]
        public void Resolve_DuplicateModifier_ThrowsInsteadOfStackingTwice()
        {
            var lightningInfusion = CreateLightningInfusion();

            Assert.Throws<ArgumentException>(() => _resolver.Resolve(
                CreateCrushingBlow(),
                new[] { lightningInfusion, lightningInfusion }));
        }

        [Fact]
        public void AbilityDefinition_CopiesEffectCollection()
        {
            var original = new[] { new DamageEffect(DamageType.Physical, 40) };
            var ability = new AbilityDefinition("crushing-blow", original);
            original[0] = new DamageEffect(DamageType.Physical, 999);

            var result = _resolver.Resolve(ability, Array.Empty<AbilityModifierDefinition>());

            Assert.Equal(40L, result.TotalDamage);
        }

        [Fact]
        public void DamageEffect_NonPositiveDamage_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new DamageEffect(DamageType.Physical, 0));
        }

        private static AbilityDefinition CreateCrushingBlow()
        {
            return new AbilityDefinition(
                "crushing-blow",
                new[] { new DamageEffect(DamageType.Physical, 40) });
        }

        private static AbilityModifierDefinition CreateLightningInfusion()
        {
            return new AbilityModifierDefinition(
                "lightning-infusion",
                new[] { new DamageEffect(DamageType.Lightning, 12) });
        }
    }
}
