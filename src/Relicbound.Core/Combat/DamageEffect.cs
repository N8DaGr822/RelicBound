using System;

namespace Relicbound.Core.Combat
{
    public sealed class DamageEffect
    {
        public DamageEffect(DamageType damageType, int amount)
        {
            if (!Enum.IsDefined(typeof(DamageType), damageType))
            {
                throw new ArgumentOutOfRangeException(nameof(damageType));
            }

            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Damage must be positive.");
            }

            DamageType = damageType;
            Amount = amount;
        }

        public DamageType DamageType { get; }

        public int Amount { get; }
    }
}
