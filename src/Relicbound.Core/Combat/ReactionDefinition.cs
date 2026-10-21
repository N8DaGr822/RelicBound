using System;

namespace Relicbound.Core.Combat
{
    public sealed class ReactionDefinition
    {
        public ReactionDefinition(string id, DamageType triggerDamageType, DamageEffect effect, int maxActivations = 1)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A reaction ID is required.", nameof(id));
            if (!Enum.IsDefined(typeof(DamageType), triggerDamageType)) throw new ArgumentOutOfRangeException(nameof(triggerDamageType));
            if (effect is null) throw new ArgumentNullException(nameof(effect));
            if (maxActivations <= 0) throw new ArgumentOutOfRangeException(nameof(maxActivations));
            Id = id;
            TriggerDamageType = triggerDamageType;
            Effect = effect;
            MaxActivations = maxActivations;
        }

        public string Id { get; }
        public DamageType TriggerDamageType { get; }
        public DamageEffect Effect { get; }
        public int MaxActivations { get; }
    }
}
