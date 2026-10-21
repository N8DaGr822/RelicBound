using System;
using System.Collections.Generic;
using System.Linq;
using Relicbound.Core.Combat;

namespace Relicbound.Core.Abilities
{
    public sealed class AbilityDefinition
    {
        public AbilityDefinition(string id, IEnumerable<DamageEffect> effects)
            : this(id, effects, 0, 0)
        {
        }

        public AbilityDefinition(string id, IEnumerable<DamageEffect> effects, int resourceCost, int cooldownTicks)
        {
            if (resourceCost < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(resourceCost));
            }

            if (cooldownTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cooldownTicks));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("An ability ID is required.", nameof(id));
            }

            if (effects is null)
            {
                throw new ArgumentNullException(nameof(effects));
            }

            var snapshot = effects.ToArray();
            if (snapshot.Length == 0 || snapshot.Any(effect => effect is null))
            {
                throw new ArgumentException("An ability must have valid effects.", nameof(effects));
            }

            Id = id;
            Effects = Array.AsReadOnly(snapshot);
            ResourceCost = resourceCost;
            CooldownTicks = cooldownTicks;
        }

        public string Id { get; }

        public IReadOnlyList<DamageEffect> Effects { get; }

        public int ResourceCost { get; }

        public int CooldownTicks { get; }
    }
}
