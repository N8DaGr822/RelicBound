using System;
using Relicbound.Core.Combat;

namespace Relicbound.Core.Progression
{
    public sealed class ProgressionRules
    {
        public ProgressionRules(
            string discoveryEncounterId,
            string discoveryId,
            string masteryEncounterId,
            string masteryId,
            DamageType chainDamageType,
            int requiredChainAttacks)
            : this(discoveryEncounterId, discoveryId, masteryEncounterId, masteryId, chainDamageType, requiredChainAttacks, null)
        {
        }

        public ProgressionRules(
            string discoveryEncounterId,
            string discoveryId,
            string masteryEncounterId,
            string masteryId,
            DamageType chainDamageType,
            int requiredChainAttacks,
            string? protectiveMasteryId)
        {
            if (protectiveMasteryId != null)
            {
                ValidateId(protectiveMasteryId, nameof(protectiveMasteryId));
                if (protectiveMasteryId == masteryId)
                    throw new ArgumentException("Mastery branches must have distinct IDs.", nameof(protectiveMasteryId));
            }
            ValidateId(discoveryEncounterId, nameof(discoveryEncounterId));
            ValidateId(discoveryId, nameof(discoveryId));
            ValidateId(masteryEncounterId, nameof(masteryEncounterId));
            ValidateId(masteryId, nameof(masteryId));
            if (string.Equals(discoveryEncounterId, masteryEncounterId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Discovery and mastery encounters must have distinct IDs.", nameof(masteryEncounterId));
            }

            if (!Enum.IsDefined(typeof(DamageType), chainDamageType))
            {
                throw new ArgumentOutOfRangeException(nameof(chainDamageType));
            }

            if (requiredChainAttacks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requiredChainAttacks));
            }

            DiscoveryEncounterId = discoveryEncounterId;
            DiscoveryId = discoveryId;
            MasteryEncounterId = masteryEncounterId;
            MasteryId = masteryId;
            ChainDamageType = chainDamageType;
            RequiredChainAttacks = requiredChainAttacks;
            ProtectiveMasteryId = protectiveMasteryId;
        }

        public string DiscoveryEncounterId { get; }
        public string DiscoveryId { get; }
        public string MasteryEncounterId { get; }
        public string MasteryId { get; }
        public DamageType ChainDamageType { get; }
        public int RequiredChainAttacks { get; }
        public string? ProtectiveMasteryId { get; }

        private static void ValidateId(string id, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A progression ID is required.", parameterName);
            }
        }
    }
}
