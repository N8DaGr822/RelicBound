using System;
using System.Collections.Generic;
using System.Linq;
using Relicbound.Core.Combat;

namespace Relicbound.Simulation.Combat
{
    public static class CombatEventProcessor
    {
        public const int MaxEventsPerAction = 128;

        public static IReadOnlyList<CombatEvent> Process(
            IEnumerable<CombatEvent> initialEvents,
            IEnumerable<ReactionDefinition> reactions,
            int maxEvents = MaxEventsPerAction)
        {
            if (initialEvents is null) throw new ArgumentNullException(nameof(initialEvents));
            if (reactions is null) throw new ArgumentNullException(nameof(reactions));
            if (maxEvents <= 0 || maxEvents > MaxEventsPerAction) throw new ArgumentOutOfRangeException(nameof(maxEvents));
            var definitions = reactions.ToArray();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var reaction in definitions)
            {
                if (reaction is null || !ids.Add(reaction.Id))
                    throw new ArgumentException("Reaction definitions must be nonnull and have distinct IDs.", nameof(reactions));
            }

            var queue = new Queue<CombatEvent>();
            var events = new List<CombatEvent>();
            var activations = new Dictionary<string, int>(StringComparer.Ordinal);
            var sequence = 0;

            void Enqueue(CombatEvent combatEvent)
            {
                if (sequence >= maxEvents)
                    throw new InvalidOperationException("Combat event budget exceeded; the action cannot be committed.");
                queue.Enqueue(combatEvent with { Sequence = sequence++ });
            }

            foreach (var initial in initialEvents)
            {
                if (initial is null || initial.Tick < 0 || initial.Amount <= 0 ||
                    string.IsNullOrWhiteSpace(initial.SourceId) || string.IsNullOrWhiteSpace(initial.AttackerId) ||
                    string.IsNullOrWhiteSpace(initial.TargetId) || string.IsNullOrWhiteSpace(initial.Reason) ||
                    (initial.DamageType.HasValue && !Enum.IsDefined(initial.DamageType.Value)))
                    throw new ArgumentException("Initial combat events must be valid.", nameof(initialEvents));
                Enqueue(initial with { ParentSequence = null });
            }

            // Reaction Ordering: FIFO events, then definition order; all expansion precedes state mutation.
            while (queue.TryDequeue(out var current))
            {
                events.Add(current);
                foreach (var reaction in definitions)
                {
                    activations.TryGetValue(reaction.Id, out var count);
                    if (current.DamageType != reaction.TriggerDamageType || count >= reaction.MaxActivations ||
                        (current.SourceId == reaction.Id && reaction.MaxActivations == 1))
                        continue;
                    activations[reaction.Id] = count + 1;
                    Enqueue(new CombatEvent(current.Tick, 0, current.Sequence, reaction.Id,
                        current.AttackerId, current.TargetId, reaction.Effect.DamageType,
                        reaction.Effect.Amount, "reaction-hit"));
                }
            }

            return events.AsReadOnly();
        }
    }
}
