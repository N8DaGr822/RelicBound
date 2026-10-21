using System;
using Relicbound.Core.Abilities;
using Relicbound.Core.Combat;
using Relicbound.Core.Encounters;
using Relicbound.Core.Progression;
using Relicbound.Simulation.Combat;
using Relicbound.Simulation.Entities;
using Relicbound.Simulation.Encounters;

var crushingBlow = new AbilityDefinition(
    "crushing-blow",
    new[] { new DamageEffect(DamageType.Physical, 40) },
    resourceCost: 10,
    cooldownTicks: 2);

var lightningInfusion = new AbilityModifierDefinition(
    "lightning-infusion",
    new[] { new DamageEffect(DamageType.Lightning, 12) },
    null,
    ModifierSource.Discovery);

var stormheart = new AbilityModifierDefinition(
    "stormheart",
    Array.Empty<DamageEffect>(),
    new ChainEffect(DamageType.Lightning, 2),
    ModifierSource.Relic);

var tempest = new AbilityModifierDefinition(
    "tempest",
    Array.Empty<DamageEffect>(),
    new ChainEffect(DamageType.Lightning, 1, requiresExistingChain: true),
    ModifierSource.Mastery);

var progression = new ProgressionRules(
    "runic-shrine", lightningInfusion.Id, "storm-trial", tempest.Id,
    DamageType.Lightning, requiredChainAttacks: 3, protectiveMasteryId: "storm-guardian");
var stormGuardian = new AbilityModifierDefinition(
    "storm-guardian", Array.Empty<DamageEffect>(), null, ModifierSource.Mastery,
    Array.Empty<ReactionDefinition>(), allyBarrier: 20);
var resolver = new CombatResolver(new[] { lightningInfusion, stormheart, tempest, stormGuardian }, progression);
var shrineDefinition = new EncounterDefinition(
    progression.DiscoveryEncounterId, EncounterObjective.Interaction, Array.Empty<string>());
var trialDefinition = new EncounterDefinition(
    progression.MasteryEncounterId, EncounterObjective.DefeatAllEnemies, new[] { "storm-trial-guardian" });
var adventurer = new Combatant("test-adventurer", 100);
var veteran = new Combatant("tempest-veteran", 100);

Console.WriteLine("=== RELICBOUND Combat Sandbox ===");
Console.WriteLine($"Ability: {crushingBlow.Id}");
Console.WriteLine($"Resource cost: {crushingBlow.ResourceCost}; cooldown: {crushingBlow.CooldownTicks} ticks");
Console.WriteLine("Discovery: Lightning Infusion");
Console.WriteLine($"Relic: Stormheart ({stormheart.ChainEffect!.TargetCount} chain targets)");

// Ability Comparison: Preview both configurations without spending resources or changing state.
var baseDamage = resolver.Resolve(crushingBlow, Array.Empty<AbilityModifierDefinition>());
var infusedDamage = resolver.Resolve(crushingBlow, new[] { lightningInfusion });
Console.WriteLine($"Base ability preview: {baseDamage.TotalDamage} damage");
Console.WriteLine($"With Lightning Infusion: {infusedDamage.TotalDamage} damage");

foreach (var attacker in new[] { adventurer, veteran })
{
    attacker.EquipRelic(stormheart.Id);
    var shrine = new EncounterSession(shrineDefinition, attacker, Array.Empty<Combatant>(), resolver);
    Console.WriteLine($"Shrine completion before interaction: {shrine.TryComplete(attacker)}");
    shrine.Interact(attacker);
    Console.WriteLine($"{attacker.Id} completes Runic Shrine and earns Lightning Infusion.");
}

Console.WriteLine("=== Earning Tempest ===");
for (var attempt = 0; attempt < progression.RequiredChainAttacks; attempt++)
{
    var tick = attempt * 2L;
    var trainingActions = resolver.ResolveLoadout(
        veteran, new Combatant($"training-primary-{attempt}", 100), crushingBlow,
        new AbilityLoadout(lightningInfusion.Id),
        new[]
        {
            new Combatant($"training-chain-a-{attempt}", 100),
            new Combatant($"training-chain-b-{attempt}", 100)
        }, tick);
    Console.WriteLine(
        $"Tick {tick}: {trainingActions.Count - 1} chains; " +
        $"mastery progress {veteran.GetMasteryChainAttacks(tempest.Id)}/{progression.RequiredChainAttacks}; " +
        $"resource {veteran.Resource}; next ready {trainingActions[0].AbilityReadyTick}");

    if (attempt == 0)
    {
        var blockedTarget = new Combatant("cooldown-target", 100);
        try
        {
            resolver.ResolveLoadout(veteran, blockedTarget, crushingBlow,
                new AbilityLoadout(lightningInfusion.Id), Array.Empty<Combatant>(), tick + 1);
        }
        catch (InvalidOperationException exception)
        {
            Console.WriteLine($"Tick {tick + 1} rejected: {exception.Message}");
            Console.WriteLine($"Unchanged: target health {blockedTarget.Health}, resource {veteran.Resource}.");
        }
    }
}

Console.WriteLine($"Three qualifying attacks alone unlock Tempest: {veteran.HasMastery(tempest.Id)}");
var trialGuardian = new Combatant("storm-trial-guardian", 40);
var trial = new EncounterSession(trialDefinition, veteran, new[] { trialGuardian }, resolver);
Console.WriteLine($"Storm Trial completion with guardian alive: {trial.TryComplete(veteran)}");
resolver.ResolveLoadout(veteran, trialGuardian, crushingBlow,
    new AbilityLoadout(lightningInfusion.Id), Array.Empty<Combatant>(), tick: 6);
Console.WriteLine($"Storm Trial completion after guardian defeat: {trial.TryComplete(veteran)}");
Console.WriteLine($"Repeated Storm Trial completion: {trial.TryComplete(veteran)}");

Console.WriteLine($"Storm Trial completed; Tempest earned: {veteran.HasMastery(tempest.Id)}");

foreach (var attacker in new[] { adventurer, veteran })
{
    var target = new Combatant("skeleton-warrior", 100);
    var chainTargets = new[]
    {
        new Combatant("skeleton-archer", 100),
        new Combatant("skeleton-mage", 100),
        new Combatant("skeleton-knight", 100)
    };

    Console.WriteLine($"=== Attacker: {attacker.Id} ===");
    if (!attacker.HasMastery(tempest.Id))
    {
        try
        {
            resolver.ResolveLoadout(
                attacker, target, crushingBlow,
                new AbilityLoadout(lightningInfusion.Id, tempest.Id), chainTargets, tick: 8);
        }
        catch (InvalidOperationException exception)
        {
            Console.WriteLine($"Locked selection rejected: {exception.Message}");
            Console.WriteLine($"Target health after rejection: {target.Health}");
        }
    }

    var loadout = new AbilityLoadout(
        lightningInfusion.Id, attacker.HasMastery(tempest.Id) ? tempest.Id : null);
    var actions = resolver.ResolveLoadout(attacker, target, crushingBlow, loadout, chainTargets, tick: 8);
    Console.WriteLine($"Selected mastery: {loadout.MasteryId ?? "none"}");
    Console.WriteLine($"Additional enemies hit: {actions.Count - 1}");
    Console.WriteLine($"Resource remaining: {attacker.Resource}; next ready: {actions[0].AbilityReadyTick}");

    foreach (var action in actions)
    {
        Console.WriteLine($"Target: {action.TargetId}");
        if (action.TriggerSourceId is not null)
        {
            Console.WriteLine($"  Chain triggered by: {action.TriggerSourceId}");
        }

        foreach (var contribution in action.Damage.Contributions)
        {
            Console.WriteLine($"  {contribution.SourceId}: {contribution.Amount} {contribution.DamageType}");
        }

        Console.WriteLine($"  Applied damage: {action.AppliedDamage}");
        Console.WriteLine($"  Target health: {action.TargetHealthBefore} -> {action.TargetHealthAfter}");
        if (args.Contains("--events"))
        {
            foreach (var combatEvent in action.Events)
            {
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(combatEvent));
            }
        }
    }

    Console.WriteLine($"Skeleton Knight remaining health: {chainTargets[2].Health}");
}

Console.WriteLine("=== Same relic, protective mastery ===");
var protectedAlly = new Combatant("protected-ally", 100);
var protection = resolver.ResolveLoadout(veteran, new Combatant("guardian-target", 100), crushingBlow,
    new AbilityLoadout(lightningInfusion.Id, stormGuardian.Id), Array.Empty<Combatant>(), 10, protectedAlly);
Console.WriteLine($"Storm Guardian: ally barrier {protection[1].BarrierBefore} -> {protection[1].BarrierAfter}; no chain hits.");
var enemyStrike = new AbilityDefinition("enemy-strike", new[] { new DamageEffect(DamageType.Physical, 25) });
var protectedHit = resolver.Resolve(new Combatant("enemy", 100), protectedAlly, enemyStrike,
    Array.Empty<AbilityModifierDefinition>(), Array.Empty<Combatant>(), 12)[0];
Console.WriteLine($"Incoming 25 damage: barrier {protectedHit.BarrierBefore} -> {protectedHit.BarrierAfter}, health {protectedHit.TargetHealthBefore} -> {protectedHit.TargetHealthAfter}.");

var exhausted = new Combatant("exhausted-adventurer", 100, maxResource: 5);
var untouchedTarget = new Combatant("resource-target", 100);
try
{
    resolver.ResolveLoadout(exhausted, untouchedTarget, crushingBlow,
        new AbilityLoadout(), Array.Empty<Combatant>(), tick: 0);
}
catch (InvalidOperationException exception)
{
    Console.WriteLine($"Resource rejection: {exception.Message}");
    Console.WriteLine($"Unchanged: target health {untouchedTarget.Health}, resource {exhausted.Resource}.");
}

Console.WriteLine("Combat resolution complete.");
