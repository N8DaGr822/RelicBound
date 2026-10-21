using System;
using System.Collections.Generic;

namespace Relicbound.Simulation.Entities
{
    public sealed class Combatant
    {
        private readonly HashSet<string> _discoveryUnlocks = new(StringComparer.Ordinal);
        private readonly HashSet<string> _masteryUnlocks = new(StringComparer.Ordinal);
        private readonly HashSet<string> _completedEncounters = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _masteryChainAttacks = new(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _abilityReadyTicks = new(StringComparer.Ordinal);

        public Combatant(string id, int maxHealth)
            : this(id, maxHealth, 100)
        {
        }

        public Combatant(string id, int maxHealth, int maxResource)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A combatant ID is required.", nameof(id));
            }

            if (maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), "Maximum health must be positive.");
            }

            if (maxResource < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxResource));
            }

            Id = id;
            MaxHealth = maxHealth;
            Health = maxHealth;
            MaxResource = maxResource;
            Resource = maxResource;
        }

        public string Id { get; }

        public int MaxHealth { get; }

        public int Health { get; private set; }

        public int Barrier { get; private set; }

        public bool IsAlive => Health > 0;

        public int MaxResource { get; }

        public int Resource { get; private set; }

        public long LastActionTick { get; private set; }

        public string? EquippedRelicId { get; private set; }

        // Character Progression: Unlocks belong to this combatant, never to equipped items.
        public void UnlockDiscovery(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A discovery ID is required.", nameof(id));
            }

            _discoveryUnlocks.Add(id);
        }

        public void UnlockMastery(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A mastery ID is required.", nameof(id));
            }

            _masteryUnlocks.Add(id);
        }

        public bool HasDiscovery(string id) => _discoveryUnlocks.Contains(id);

        public bool HasMastery(string id) => _masteryUnlocks.Contains(id);

        public bool HasCompletedEncounter(string id) => _completedEncounters.Contains(id);

        public int GetMasteryChainAttacks(string masteryId) =>
            _masteryChainAttacks.TryGetValue(masteryId, out var count) ? count : 0;

        public long GetAbilityReadyTick(string abilityId) =>
            _abilityReadyTicks.TryGetValue(abilityId, out var tick) ? tick : 0;

        public void RestoreResource(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            Resource += Math.Min(amount, MaxResource - Resource);
        }

        internal bool RecordEncounterCompletion(string encounterId) => _completedEncounters.Add(encounterId);

        internal void RecordMasteryChainAttack(string masteryId)
        {
            var count = GetMasteryChainAttacks(masteryId);
            if (count < int.MaxValue)
            {
                _masteryChainAttacks[masteryId] = count + 1;
            }
        }

        internal void CommitAbilityUse(string abilityId, int cost, long tick, long readyTick)
        {
            Resource -= cost;
            LastActionTick = tick;
            _abilityReadyTicks[abilityId] = readyTick;
        }

        public void EquipRelic(string? id)
        {
            if (id is not null && string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("An equipped relic ID cannot be blank.", nameof(id));
            }

            EquippedRelicId = id;
        }

        internal void ApplyDamage(long damage)
        {
            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage));
            }

            var absorbed = (int)Math.Min(damage, Barrier);
            Barrier -= absorbed;
            damage -= absorbed;
            // Health Bounds: Clamp before converting accumulated damage to an integer.
            Health -= (int)Math.Min(damage, Health);
        }

        internal void GrantBarrier(int amount)
        {
            Barrier += Math.Min(amount, MaxHealth - Barrier);
        }
    }
}
