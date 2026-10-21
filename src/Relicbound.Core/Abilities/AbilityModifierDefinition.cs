using System;
using System.Collections.Generic;
using System.Linq;
using Relicbound.Core.Combat;

namespace Relicbound.Core.Abilities
{
    public sealed class AbilityModifierDefinition
    {
        public AbilityModifierDefinition(string id, IEnumerable<DamageEffect> effects)
            : this(id, effects, null)
        {
        }

        public AbilityModifierDefinition(string id, IEnumerable<DamageEffect> effects, ChainEffect? chainEffect)
            : this(id, effects, chainEffect, ModifierSource.Unspecified)
        {
        }

        public AbilityModifierDefinition(
            string id,
            IEnumerable<DamageEffect> effects,
            ChainEffect? chainEffect,
            ModifierSource source)
            : this(id, effects, chainEffect, source, Array.Empty<ReactionDefinition>(), 0)
        {
        }

        public AbilityModifierDefinition(
            string id,
            IEnumerable<DamageEffect> effects,
            ChainEffect? chainEffect,
            ModifierSource source,
            IEnumerable<ReactionDefinition> reactions,
            int allyBarrier = 0)
        {
            if (reactions is null) throw new ArgumentNullException(nameof(reactions));
            if (allyBarrier < 0) throw new ArgumentOutOfRangeException(nameof(allyBarrier));
            var reactionSnapshot = reactions.ToArray();
            if (reactionSnapshot.Any(reaction => reaction is null))
                throw new ArgumentException("Reactions cannot contain null entries.", nameof(reactions));
            if (!Enum.IsDefined(typeof(ModifierSource), source))
            {
                throw new ArgumentOutOfRangeException(nameof(source));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A modifier ID is required.", nameof(id));
            }

            if (effects is null)
            {
                throw new ArgumentNullException(nameof(effects));
            }

            var snapshot = effects.ToArray();
            if ((snapshot.Length == 0 && chainEffect is null && reactionSnapshot.Length == 0 && allyBarrier == 0) || snapshot.Any(effect => effect is null))
            {
                throw new ArgumentException("A modifier must have valid effects.", nameof(effects));
            }

            Id = id;
            Effects = Array.AsReadOnly(snapshot);
            ChainEffect = chainEffect;
            Source = source;
            Reactions = Array.AsReadOnly(reactionSnapshot);
            AllyBarrier = allyBarrier;
        }

        public string Id { get; }

        public IReadOnlyList<DamageEffect> Effects { get; }

        public ChainEffect? ChainEffect { get; }

        public ModifierSource Source { get; }

        public IReadOnlyList<ReactionDefinition> Reactions { get; }

        public int AllyBarrier { get; }
    }
}
