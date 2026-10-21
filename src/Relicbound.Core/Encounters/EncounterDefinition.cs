using System;
using System.Collections.Generic;
using System.Linq;

namespace Relicbound.Core.Encounters
{
    public sealed class EncounterDefinition
    {
        public EncounterDefinition(string id, EncounterObjective objective, IEnumerable<string> enemyIds)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("An encounter ID is required.", nameof(id));
            }

            if (!Enum.IsDefined(typeof(EncounterObjective), objective))
            {
                throw new ArgumentOutOfRangeException(nameof(objective));
            }

            if (enemyIds is null)
            {
                throw new ArgumentNullException(nameof(enemyIds));
            }

            var snapshot = enemyIds.ToArray();
            if (snapshot.Any(string.IsNullOrWhiteSpace) ||
                snapshot.Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
            {
                throw new ArgumentException("Encounter enemy IDs must be nonblank and distinct.", nameof(enemyIds));
            }

            if ((objective == EncounterObjective.Interaction && snapshot.Length != 0) ||
                (objective == EncounterObjective.DefeatAllEnemies && snapshot.Length == 0))
            {
                throw new ArgumentException("Interaction encounters have no enemies; combat encounters require enemies.", nameof(enemyIds));
            }

            Id = id;
            Objective = objective;
            EnemyIds = Array.AsReadOnly(snapshot);
        }

        public string Id { get; }

        public EncounterObjective Objective { get; }

        public IReadOnlyList<string> EnemyIds { get; }
    }
}
