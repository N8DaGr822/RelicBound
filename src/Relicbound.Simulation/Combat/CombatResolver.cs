using System;
using System.Collections.Generic;
using System.Linq;
using Relicbound.Core.Abilities;
using Relicbound.Core.Combat;
using Relicbound.Core.Progression;
using Relicbound.Simulation.Entities;

namespace Relicbound.Simulation.Combat
{
    public sealed class CombatResolver
    {
        public const int MaxChainTargets = 32;

        private readonly Dictionary<string, AbilityModifierDefinition> _modifierDefinitions;
        private readonly ProgressionRules? _progressionRules;

        public CombatResolver()
            : this(Array.Empty<AbilityModifierDefinition>())
        {
        }

        public CombatResolver(IEnumerable<AbilityModifierDefinition> modifierDefinitions)
            : this(modifierDefinitions, null)
        {
        }

        public CombatResolver(IEnumerable<AbilityModifierDefinition> modifierDefinitions, ProgressionRules? progressionRules)
        {
            if (modifierDefinitions is null)
            {
                throw new ArgumentNullException(nameof(modifierDefinitions));
            }

            _modifierDefinitions = new Dictionary<string, AbilityModifierDefinition>(StringComparer.Ordinal);
            foreach (var definition in modifierDefinitions)
            {
                if (definition is null || definition.Source == ModifierSource.Unspecified)
                {
                    throw new ArgumentException("Catalog modifiers must declare their source.", nameof(modifierDefinitions));
                }

                if (!_modifierDefinitions.TryAdd(definition.Id, definition))
                {
                    throw new ArgumentException($"Duplicate modifier definition '{definition.Id}'.", nameof(modifierDefinitions));
                }
            }

            if (progressionRules is not null)
            {
                GetModifier(progressionRules.DiscoveryId, ModifierSource.Discovery);
                GetModifier(progressionRules.MasteryId, ModifierSource.Mastery);
                if (progressionRules.ProtectiveMasteryId is not null)
                {
                    GetModifier(progressionRules.ProtectiveMasteryId, ModifierSource.Mastery);
                }
            }

            _progressionRules = progressionRules;
        }

        public bool CompleteEncounter(Combatant character, string encounterId)
        {
            if (character is null)
            {
                throw new ArgumentNullException(nameof(character));
            }

            if (_progressionRules is null)
            {
                throw new InvalidOperationException("No progression rules are configured.");
            }

            if (encounterId != _progressionRules.DiscoveryEncounterId &&
                encounterId != _progressionRules.MasteryEncounterId)
            {
                throw new ArgumentException("The encounter is not registered for this progression path.", nameof(encounterId));
            }

            if (!character.IsAlive)
            {
                throw new InvalidOperationException("A dead character cannot complete an encounter.");
            }

            // Encounter Rewards: Repeated completion is idempotent and never grants combat credit.
            var completed = character.RecordEncounterCompletion(encounterId);
            if (encounterId == _progressionRules.DiscoveryEncounterId)
            {
                character.UnlockDiscovery(_progressionRules.DiscoveryId);
            }

            TryUnlockMastery(character);
            return completed;
        }

        public IReadOnlyList<CombatActionResult> ResolveLoadout(
            Combatant attacker,
            Combatant target,
            AbilityDefinition ability,
            AbilityLoadout loadout,
            IEnumerable<Combatant> chainCandidates)
        {
            return ResolveLoadout(attacker, target, ability, loadout, chainCandidates, 0);
        }

        public IReadOnlyList<CombatActionResult> ResolveLoadout(
            Combatant attacker,
            Combatant target,
            AbilityDefinition ability,
            AbilityLoadout loadout,
            IEnumerable<Combatant> chainCandidates,
            long tick)
        {
            return ResolveLoadout(attacker, target, ability, loadout, chainCandidates, tick, null);
        }

        public IReadOnlyList<CombatActionResult> ResolveLoadout(
            Combatant attacker,
            Combatant target,
            AbilityDefinition ability,
            AbilityLoadout loadout,
            IEnumerable<Combatant> chainCandidates,
            long tick,
            Combatant? ally)
        {
            return ResolveLoadout(attacker, target, ability, loadout, chainCandidates, tick, ally, Array.Empty<ReactionDefinition>());
        }

        public IReadOnlyList<CombatActionResult> ResolveLoadout(
            Combatant attacker,
            Combatant target,
            AbilityDefinition ability,
            AbilityLoadout loadout,
            IEnumerable<Combatant> chainCandidates,
            long tick,
            Combatant? ally,
            IEnumerable<ReactionDefinition> contextualReactions)
        {
            if (attacker is null)
            {
                throw new ArgumentNullException(nameof(attacker));
            }

            if (loadout is null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            // Loadout Validation: Resolve trusted definitions in discovery, relic, mastery order.
            var modifiers = new List<AbilityModifierDefinition>();
            if (loadout.DiscoveryId is not null)
            {
                var discovery = GetModifier(loadout.DiscoveryId, ModifierSource.Discovery);
                if (!attacker.HasDiscovery(discovery.Id))
                {
                    throw new InvalidOperationException($"Discovery '{discovery.Id}' is not unlocked for '{attacker.Id}'.");
                }

                modifiers.Add(discovery);
            }

            if (attacker.EquippedRelicId is not null)
            {
                modifiers.Add(GetModifier(attacker.EquippedRelicId, ModifierSource.Relic));
            }

            if (loadout.MasteryId is not null)
            {
                var mastery = GetModifier(loadout.MasteryId, ModifierSource.Mastery);
                if (!attacker.HasMastery(mastery.Id))
                {
                    throw new InvalidOperationException($"Mastery '{mastery.Id}' is not unlocked for '{attacker.Id}'.");
                }

                modifiers.Add(mastery);
            }

            // Combat Prerequisites: Reuse chain validation before the existing pipeline changes health.
            if (contextualReactions is null)
            {
                throw new ArgumentNullException(nameof(contextualReactions));
            }

            var context = contextualReactions.ToArray();
            if (context.Length > 0)
            {
                modifiers.Add(new AbilityModifierDefinition("world-context", Array.Empty<DamageEffect>(), null,
                    ModifierSource.Unspecified, context));
            }

            var actions = Resolve(attacker, target, ability, modifiers, chainCandidates, tick, ally);
            if (_progressionRules is not null &&
                loadout.DiscoveryId == _progressionRules.DiscoveryId &&
                actions[0].Damage.ChainDamageType == _progressionRules.ChainDamageType &&
                actions.Skip(1).Any(action => action.AppliedDamage > 0))
            {
                // Mastery Credit: One successful chained attack counts once, regardless of target count.
                attacker.RecordMasteryChainAttack(_progressionRules.MasteryId);
                TryUnlockMastery(attacker);
            }

            return actions;
        }

        private void TryUnlockMastery(Combatant character)
        {
            if (_progressionRules is not null &&
                character.HasDiscovery(_progressionRules.DiscoveryId) &&
                character.HasCompletedEncounter(_progressionRules.MasteryEncounterId) &&
                character.GetMasteryChainAttacks(_progressionRules.MasteryId) >= _progressionRules.RequiredChainAttacks)
            {
                character.UnlockMastery(_progressionRules.MasteryId);
                if (_progressionRules.ProtectiveMasteryId is not null)
                {
                    character.UnlockMastery(_progressionRules.ProtectiveMasteryId);
                }
            }
        }

        private AbilityModifierDefinition GetModifier(string id, ModifierSource source)
        {
            if (!_modifierDefinitions.TryGetValue(id, out var definition) || definition.Source != source)
            {
                throw new ArgumentException($"Modifier '{id}' is not a registered {source} definition.");
            }

            return definition;
        }

        public CombatActionResult Resolve(
            Combatant attacker,
            Combatant target,
            AbilityDefinition ability,
            IEnumerable<AbilityModifierDefinition> modifiers)
        {
            return Resolve(attacker, target, ability, modifiers, Array.Empty<Combatant>())[0];
        }

        public IReadOnlyList<CombatActionResult> Resolve(
            Combatant attacker,
            Combatant target,
            AbilityDefinition ability,
            IEnumerable<AbilityModifierDefinition> modifiers,
            IEnumerable<Combatant> chainCandidates)
        {
            return Resolve(attacker, target, ability, modifiers, chainCandidates, 0);
        }

        public IReadOnlyList<CombatActionResult> Resolve(
            Combatant attacker,
            Combatant target,
            AbilityDefinition ability,
            IEnumerable<AbilityModifierDefinition> modifiers,
            IEnumerable<Combatant> chainCandidates,
            long tick)
        {
            return Resolve(attacker, target, ability, modifiers, chainCandidates, tick, null);
        }

        public IReadOnlyList<CombatActionResult> Resolve(
            Combatant attacker,
            Combatant target,
            AbilityDefinition ability,
            IEnumerable<AbilityModifierDefinition> modifiers,
            IEnumerable<Combatant> chainCandidates,
            long tick,
            Combatant? ally)
        {
            if (attacker is null)
            {
                throw new ArgumentNullException(nameof(attacker));
            }

            if (target is null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (string.Equals(attacker.Id, target.Id, StringComparison.Ordinal))
            {
                throw new ArgumentException("An attacker cannot target itself.", nameof(target));
            }

            if (!attacker.IsAlive || !target.IsAlive)
            {
                throw new InvalidOperationException("The attacker and target must both be alive.");
            }

            if (chainCandidates is null)
            {
                throw new ArgumentNullException(nameof(chainCandidates));
            }

            if (ability is null)
            {
                throw new ArgumentNullException(nameof(ability));
            }

            if (tick < 0 || tick > long.MaxValue - ability.CooldownTicks)
            {
                throw new ArgumentOutOfRangeException(nameof(tick));
            }

            if (tick < attacker.LastActionTick)
            {
                throw new InvalidOperationException("Combat time cannot move backwards.");
            }

            if (tick < attacker.GetAbilityReadyTick(ability.Id))
            {
                throw new InvalidOperationException($"Ability '{ability.Id}' is on cooldown.");
            }

            if (attacker.Resource < ability.ResourceCost)
            {
                throw new InvalidOperationException("The attacker has insufficient resource.");
            }

            var readyTick = tick + ability.CooldownTicks;

            // Combat Atomicity: Finish validation and modifier enumeration before mutating health.
            var damage = Resolve(ability, modifiers);
            if (damage.AllyBarrier > 0 && (ally is null || !ally.IsAlive || ally.Id == attacker.Id || ally.Id == target.Id))
            {
                throw new ArgumentException("A protective mastery requires a distinct, living ally.", nameof(ally));
            }
            var chainDamage = new CombatResult(
                ability.Id,
                damage.Contributions.Where(contribution => contribution.DamageType == damage.ChainDamageType));
            var targets = new List<Combatant>();
            var visitedIds = new HashSet<string>(StringComparer.Ordinal) { attacker.Id, target.Id };
            foreach (var candidate in chainCandidates)
            {
                if (candidate is null)
                {
                    throw new ArgumentException("Chain candidates cannot contain null entries.", nameof(chainCandidates));
                }

                if (damage.AllyBarrier > 0 && candidate.Id == ally!.Id)
                {
                    throw new ArgumentException("The protected ally cannot also be an enemy candidate.", nameof(chainCandidates));
                }

                if (candidate.IsAlive && visitedIds.Add(candidate.Id) && targets.Count < damage.ChainTargetCount)
                {
                    targets.Add(candidate);
                }
            }

            var hitTargets = new[] { target }.Concat(targets).ToArray();
            var initialEvents = new List<CombatEvent>();
            foreach (var hitTarget in hitTargets)
            {
                var hitDamage = ReferenceEquals(hitTarget, target) ? damage : chainDamage;
                foreach (var contribution in hitDamage.Contributions)
                {
                    initialEvents.Add(new CombatEvent(tick, 0, null, contribution.SourceId, attacker.Id,
                        hitTarget.Id, contribution.DamageType, contribution.Amount,
                        ReferenceEquals(hitTarget, target) ? "primary-hit" : "chain-hit"));
                }
            }

            if (damage.AllyBarrier > 0)
            {
                initialEvents.Add(new CombatEvent(tick, 0, null, damage.BarrierSourceId!, attacker.Id,
                    ally!.Id, null, damage.AllyBarrier, "mastery-barrier"));
            }

            var events = CombatEventProcessor.Process(initialEvents, damage.Reactions);
            var actions = new List<CombatActionResult>();
            foreach (var hitTarget in hitTargets)
            {
                var targetEvents = events.Where(combatEvent => combatEvent.TargetId == hitTarget.Id).ToArray();
                var primary = ReferenceEquals(hitTarget, target);
                var resolvedDamage = new CombatResult(ability.Id, targetEvents.Select(combatEvent =>
                    new DamageContribution(combatEvent.SourceId, combatEvent.DamageType!.Value, combatEvent.Amount)))
                {
                    ChainDamageType = primary ? damage.ChainDamageType : null,
                    ChainTargetCount = primary ? damage.ChainTargetCount : 0,
                    ChainSourceId = primary ? damage.ChainSourceId : null,
                    AllyBarrier = primary ? damage.AllyBarrier : 0,
                    BarrierSourceId = primary ? damage.BarrierSourceId : null
                };
                var absorbed = (int)Math.Min(resolvedDamage.TotalDamage, hitTarget.Barrier);
                var healthLost = (int)Math.Min(resolvedDamage.TotalDamage - absorbed, hitTarget.Health);
                actions.Add(new CombatActionResult(attacker.Id, hitTarget.Id, resolvedDamage,
                    hitTarget.Health, hitTarget.Health - healthLost)
                {
                    TriggerSourceId = primary ? null : damage.ChainSourceId,
                    Tick = tick,
                    ResourceSpent = primary ? ability.ResourceCost : 0,
                    AbilityReadyTick = primary ? readyTick : null,
                    BarrierBefore = hitTarget.Barrier,
                    BarrierAfter = hitTarget.Barrier - absorbed,
                    Events = Array.AsReadOnly(targetEvents)
                });
            }

            if (damage.AllyBarrier > 0)
            {
                actions.Add(new CombatActionResult(attacker.Id, ally!.Id,
                    new CombatResult(ability.Id, Array.Empty<DamageContribution>()), ally.Health, ally.Health)
                {
                    Tick = tick,
                    TriggerSourceId = damage.BarrierSourceId,
                    BarrierBefore = ally.Barrier,
                    BarrierAfter = ally.Barrier + Math.Min(damage.AllyBarrier, ally.MaxHealth - ally.Barrier),
                    Events = Array.AsReadOnly(events.Where(combatEvent => combatEvent.TargetId == ally.Id).ToArray())
                });
            }

            var result = actions.AsReadOnly();

            // Combat Commit: The complete bounded event plan must succeed before any state changes.
            attacker.CommitAbilityUse(ability.Id, ability.ResourceCost, tick, readyTick);
            for (var index = 0; index < hitTargets.Length; index++)
            {
                hitTargets[index].ApplyDamage(actions[index].Damage.TotalDamage);
            }

            if (damage.AllyBarrier > 0)
            {
                ally!.GrantBarrier(damage.AllyBarrier);
            }

            return result;
        }

        public CombatResult Resolve(
            AbilityDefinition ability,
            IEnumerable<AbilityModifierDefinition> modifiers)
        {
            if (ability is null)
            {
                throw new ArgumentNullException(nameof(ability));
            }

            if (modifiers is null)
            {
                throw new ArgumentNullException(nameof(modifiers));
            }

            var contributions = new List<DamageContribution>();
            AddContributions(ability.Id, ability.Effects, contributions);

            var appliedModifierIds = new HashSet<string>(StringComparer.Ordinal);
            ChainEffect? chain = null;
            string? chainSourceId = null;
            var extensions = new List<ChainEffect>();
            var reactions = new List<ReactionDefinition>();
            var allyBarrier = 0;
            string? barrierSourceId = null;
            foreach (var modifier in modifiers)
            {
                if (modifier is null)
                {
                    throw new ArgumentException("Modifiers cannot contain null entries.", nameof(modifiers));
                }

                if (!appliedModifierIds.Add(modifier.Id))
                {
                    throw new ArgumentException(
                        $"Modifier '{modifier.Id}' was applied more than once.",
                        nameof(modifiers));
                }

                AddContributions(modifier.Id, modifier.Effects, contributions);
                reactions.AddRange(modifier.Reactions);
                if (modifier.AllyBarrier > 0)
                {
                    if (allyBarrier > 0)
                    {
                        throw new ArgumentException("Only one protective transformation is supported.", nameof(modifiers));
                    }

                    allyBarrier = modifier.AllyBarrier;
                    barrierSourceId = modifier.Id;
                }
                if (modifier.ChainEffect is { } chainEffect)
                {
                    if (chainEffect.RequiresExistingChain)
                    {
                        extensions.Add(chainEffect);
                    }
                    else
                    {
                        if (chain is not null)
                        {
                            throw new ArgumentException("Only one chain-starting effect is supported per action.", nameof(modifiers));
                        }

                        chain = chainEffect;
                        chainSourceId = modifier.Id;
                    }
                }
            }

            long chainTargetCount = chain?.TargetCount ?? 0;
            foreach (var extension in extensions)
            {
                if (chain is null || extension.DamageType != chain.DamageType)
                {
                    throw new ArgumentException("A chain extension requires a matching chain effect.", nameof(modifiers));
                }

                chainTargetCount += extension.TargetCount;
            }

            if (chain is not null && !contributions.Any(contribution => contribution.DamageType == chain.DamageType))
            {
                throw new ArgumentException("A chain effect requires matching damage.", nameof(modifiers));
            }

            if (chainTargetCount > MaxChainTargets)
            {
                throw new ArgumentException($"An action cannot exceed {MaxChainTargets} chain targets.", nameof(modifiers));
            }

            if (allyBarrier > 0 && (chain is null || extensions.Count > 0))
            {
                throw new ArgumentException("Protective mastery requires a chain and cannot combine with chain extensions.", nameof(modifiers));
            }

            return new CombatResult(ability.Id, contributions)
            {
                ChainDamageType = chain?.DamageType,
                ChainTargetCount = allyBarrier > 0 ? 0 : (int)chainTargetCount,
                ChainSourceId = chainSourceId,
                Reactions = reactions.AsReadOnly(),
                AllyBarrier = allyBarrier,
                BarrierSourceId = barrierSourceId
            };
        }

        private static void AddContributions(
            string sourceId,
            IReadOnlyList<DamageEffect> effects,
            ICollection<DamageContribution> contributions)
        {
            foreach (var effect in effects)
            {
                contributions.Add(new DamageContribution(
                    sourceId,
                    effect.DamageType,
                    effect.Amount));
            }
        }
    }
}
