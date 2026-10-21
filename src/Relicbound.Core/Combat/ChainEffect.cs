using System;

namespace Relicbound.Core.Combat
{
    public sealed class ChainEffect
    {
        public ChainEffect(DamageType damageType, int targetCount, bool requiresExistingChain = false)
        {
            if (!Enum.IsDefined(typeof(DamageType), damageType))
            {
                throw new ArgumentOutOfRangeException(nameof(damageType));
            }

            if (targetCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetCount));
            }

            DamageType = damageType;
            TargetCount = targetCount;
            RequiresExistingChain = requiresExistingChain;
        }

        public DamageType DamageType { get; }

        public int TargetCount { get; }

        public bool RequiresExistingChain { get; }
    }
}
