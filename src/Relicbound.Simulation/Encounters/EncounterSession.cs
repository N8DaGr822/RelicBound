using System;
using System.Collections.Generic;
using System.Linq;
using Relicbound.Core.Encounters;
using Relicbound.Simulation.Combat;
using Relicbound.Simulation.Entities;

namespace Relicbound.Simulation.Encounters
{
    public sealed class EncounterSession
    {
        private readonly CombatResolver _resolver;

        public EncounterSession(
            EncounterDefinition definition,
            Combatant participant,
            IEnumerable<Combatant> enemies,
            CombatResolver resolver)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (participant is null)
            {
                throw new ArgumentNullException(nameof(participant));
            }

            if (enemies is null)
            {
                throw new ArgumentNullException(nameof(enemies));
            }

            if (resolver is null)
            {
                throw new ArgumentNullException(nameof(resolver));
            }

            if (!participant.IsAlive)
            {
                throw new InvalidOperationException("The encounter participant must be alive.");
            }

            var snapshot = enemies.ToArray();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var enemy in snapshot)
            {
                if (enemy is null || !enemy.IsAlive ||
                    string.Equals(enemy.Id, participant.Id, StringComparison.Ordinal) || !ids.Add(enemy.Id))
                {
                    throw new ArgumentException("Encounter enemies must be living, distinct, and separate from the participant.", nameof(enemies));
                }
            }

            if (!ids.SetEquals(definition.EnemyIds))
            {
                throw new ArgumentException("The enemy roster must exactly match the encounter definition.", nameof(enemies));
            }

            Definition = definition;
            Participant = participant;
            Enemies = Array.AsReadOnly(snapshot);
            _resolver = resolver;
        }

        public EncounterDefinition Definition { get; }

        public Combatant Participant { get; }

        public IReadOnlyList<Combatant> Enemies { get; }

        public bool IsCompleted { get; private set; }

        public bool Interact(Combatant participant)
        {
            ValidateParticipant(participant);
            if (Definition.Objective != EncounterObjective.Interaction)
            {
                throw new InvalidOperationException("A combat encounter cannot be completed by interaction.");
            }

            return Complete();
        }

        public bool TryComplete(Combatant participant)
        {
            ValidateParticipant(participant);
            if (IsCompleted || Definition.Objective != EncounterObjective.DefeatAllEnemies ||
                Enemies.Any(enemy => enemy.IsAlive))
            {
                return false;
            }

            return Complete();
        }

        private void ValidateParticipant(Combatant participant)
        {
            if (participant is null)
            {
                throw new ArgumentNullException(nameof(participant));
            }

            // Encounter Ownership: An ID alias cannot substitute another runtime character instance.
            if (!ReferenceEquals(participant, Participant))
            {
                throw new ArgumentException("Only the bound participant can complete this encounter.", nameof(participant));
            }

            if (!IsCompleted && !participant.IsAlive)
            {
                throw new InvalidOperationException("A dead participant cannot complete the encounter.");
            }
        }

        private bool Complete()
        {
            if (IsCompleted)
            {
                return false;
            }

            // Encounter Commit: A failed progression award must leave the session incomplete.
            _resolver.CompleteEncounter(Participant, Definition.Id);
            IsCompleted = true;
            return true;
        }
    }
}
