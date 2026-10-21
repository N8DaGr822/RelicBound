using System;

namespace Relicbound.Core.Abilities
{
    public sealed class AbilityLoadout
    {
        public AbilityLoadout(string? discoveryId = null, string? masteryId = null)
        {
            if (discoveryId != null && string.IsNullOrWhiteSpace(discoveryId))
            {
                throw new ArgumentException("A selected discovery ID cannot be blank.", nameof(discoveryId));
            }

            if (masteryId != null && string.IsNullOrWhiteSpace(masteryId))
            {
                throw new ArgumentException("A selected mastery ID cannot be blank.", nameof(masteryId));
            }

            DiscoveryId = discoveryId;
            MasteryId = masteryId;
        }

        public string? DiscoveryId { get; }

        public string? MasteryId { get; }
    }
}
