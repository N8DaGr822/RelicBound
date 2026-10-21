using System;
using System.Collections.Generic;
using System.Linq;
using Relicbound.Core.Combat;

namespace Relicbound.Simulation.Combat
{
    public sealed class CombatResult
    {
        public CombatResult(string abilityId, IEnumerable<DamageContribution> contributions)
        {
            if (string.IsNullOrWhiteSpace(abilityId))
            {
                throw new ArgumentException("An ability ID is required.", nameof(abilityId));
            }

            if (contributions is null)
            {
                throw new ArgumentNullException(nameof(contributions));
            }

            AbilityId = abilityId;
            Contributions = Array.AsReadOnly(contributions.ToArray());
        }

        public string AbilityId { get; }

        public IReadOnlyList<DamageContribution> Contributions { get; }

        public DamageType? ChainDamageType { get; internal init; }

        public int ChainTargetCount { get; internal init; }

        public string? ChainSourceId { get; internal init; }

        public IReadOnlyList<ReactionDefinition> Reactions { get; internal init; } = Array.Empty<ReactionDefinition>();

        public int AllyBarrier { get; internal init; }

        public string? BarrierSourceId { get; internal init; }

        public long TotalDamage => Contributions.Sum(contribution => (long)contribution.Amount);

        public long GetDamage(DamageType damageType)
        {
            return Contributions
                .Where(contribution => contribution.DamageType == damageType)
                .Sum(contribution => (long)contribution.Amount);
        }
    }
}
