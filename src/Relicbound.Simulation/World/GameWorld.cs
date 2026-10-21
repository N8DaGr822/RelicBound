using System;
using System.Collections.Generic;
using System.Linq;
using Relicbound.Contracts;
using Relicbound.Core.Abilities;
using Relicbound.Core.Combat;
using Relicbound.Core.Encounters;
using Relicbound.Core.Progression;
using Relicbound.Simulation.Combat;
using Relicbound.Simulation.Encounters;
using Relicbound.Simulation.Entities;

namespace Relicbound.Simulation.World
{
    public sealed class GameWorld
    {
        private sealed class Actor
        {
            public required Combatant Model;
            public int X;
            public int Z;
            public long FrozenUntil;
            public string? FrozenBy;
            public long DodgeUntil;
            public long LastMoveTick = -1;
            public long Sequence;
            public WorldCommand? LastCommand;
            public CommandReply? LastReply;
            public AbilityLoadout Loadout = new();
            public EncounterSession? Shrine;
            public EncounterSession? Trial;
        }

        private readonly object _gate = new();
        private readonly Dictionary<string, Actor> _players = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Actor> _enemies = new(StringComparer.Ordinal);
        private readonly Queue<EventSnapshot> _events = new();
        private readonly CombatResolver _resolver;
        private readonly AbilityDefinition _strike = new("crushing-blow", new[] { new DamageEffect(DamageType.Physical, 40) }, 10, 4);
        private long _tick;
        private string _branch = string.Empty;

        public GameWorld()
        {
            var infusion = new AbilityModifierDefinition("lightning-infusion", new[] { new DamageEffect(DamageType.Lightning, 12) }, null, ModifierSource.Discovery);
            var relic = new AbilityModifierDefinition("stormheart", Array.Empty<DamageEffect>(), new ChainEffect(DamageType.Lightning, 2), ModifierSource.Relic);
            var tempest = new AbilityModifierDefinition("tempest", Array.Empty<DamageEffect>(), new ChainEffect(DamageType.Lightning, 1, true), ModifierSource.Mastery);
            var guardian = new AbilityModifierDefinition("storm-guardian", Array.Empty<DamageEffect>(), null, ModifierSource.Mastery, Array.Empty<ReactionDefinition>(), 20);
            _resolver = new CombatResolver(new[] { infusion, relic, tempest, guardian },
                new ProgressionRules("runic-shrine", infusion.Id, "storm-trial", tempest.Id, DamageType.Lightning, 3, guardian.Id));
            _enemies.Add("guardian", new Actor { Model = new Combatant("guardian", 180), X = 6, Z = 6 });
            _enemies.Add("skeleton-a", new Actor { Model = new Combatant("skeleton-a", 120), X = 3, Z = 3 });
            _enemies.Add("skeleton-b", new Actor { Model = new Combatant("skeleton-b", 120), X = 4, Z = 3 });
        }

        public void Join(string playerId)
        {
            lock (_gate)
            {
                if (string.IsNullOrWhiteSpace(playerId) || _enemies.ContainsKey(playerId)) throw new ArgumentException("Invalid player ID.", nameof(playerId));
                if (_players.ContainsKey(playerId)) return;
                if (_players.Count >= 4) throw new InvalidOperationException("The arena is full.");
                var model = new Combatant(playerId, 100);
                var actor = new Actor { Model = model, X = -4, Z = -4 };
                actor.Shrine = new EncounterSession(new EncounterDefinition("runic-shrine", EncounterObjective.Interaction, Array.Empty<string>()),
                    model, Array.Empty<Combatant>(), _resolver);
                if (_enemies["guardian"].Model.IsAlive)
                {
                    actor.Trial = new EncounterSession(new EncounterDefinition("storm-trial", EncounterObjective.DefeatAllEnemies, new[] { "guardian" }),
                        model, new[] { _enemies["guardian"].Model }, _resolver);
                }
                _players.Add(playerId, actor);
            }
        }

        public void Disconnect(string playerId)
        {
            lock (_gate) { _players.Remove(playerId); }
        }

        public CommandReply Execute(string playerId, WorldCommand command)
        {
            if (command is null) throw new ArgumentNullException(nameof(command));
            lock (_gate)
            {
                if (!_players.TryGetValue(playerId, out var player)) return Reply(command.Sequence, false, "Session is not active.");
                if (command.Sequence == player.Sequence && player.LastCommand is not null && SameCommand(command, player.LastCommand))
                    return Reply(player.LastReply!.Sequence, player.LastReply.Success, player.LastReply.Message);
                if (command.Sequence <= player.Sequence || command.Sequence <= 0) return Reply(command.Sequence, false, "Stale or conflicting command sequence.");
                CommandReply result;
                try
                {
                    if (!player.Model.IsAlive) throw new InvalidOperationException("The character is dead.");
                    ExecuteCore(player, command);
                    result = Reply(command.Sequence, true, "Accepted.");
                }
                catch (ArgumentException exception) { result = Reply(command.Sequence, false, exception.Message); }
                catch (InvalidOperationException exception) { result = Reply(command.Sequence, false, exception.Message); }
                player.Sequence = command.Sequence;
                player.LastCommand = new WorldCommand { Sequence = command.Sequence, Kind = command.Kind, X = command.X, Z = command.Z,
                    TargetId = command.TargetId, AllyId = command.AllyId, DiscoveryId = command.DiscoveryId, MasteryId = command.MasteryId };
                player.LastReply = result;
                return Reply(result.Sequence, result.Success, result.Message);
            }
        }

        private void ExecuteCore(Actor player, WorldCommand command)
        {
            switch (command.Kind)
            {
                case "move":
                case "dodge":
                    if (Math.Abs((long)command.X) + Math.Abs((long)command.Z) != 1) throw new ArgumentException("Movement must be one cardinal direction.");
                    if (player.LastMoveTick == _tick) throw new InvalidOperationException("Movement is limited to once per tick.");
                    var distance = command.Kind == "dodge" ? 2 : 1;
                    var nextX = player.X + command.X * distance;
                    var nextZ = player.Z + command.Z * distance;
                    if (Math.Abs(nextX) > 10 || Math.Abs(nextZ) > 10) throw new InvalidOperationException("Arena boundary reached.");
                    if (command.Kind == "dodge")
                    {
                        Spend(player, "dodge", 10, 20);
                        player.DodgeUntil = _tick + 3;
                    }
                    player.X = nextX;
                    player.Z = nextZ;
                    player.LastMoveTick = _tick;
                    break;
                case "interact":
                    if (Math.Abs(player.X + 4) + Math.Abs(player.Z + 3) > 2) throw new InvalidOperationException("The shrine is out of range.");
                    player.Shrine!.Interact(player.Model);
                    break;
                case "loadout":
                    var discovery = string.IsNullOrEmpty(command.DiscoveryId) ? null : command.DiscoveryId;
                    var mastery = string.IsNullOrEmpty(command.MasteryId) ? null : command.MasteryId;
                    if (discovery is not null && (discovery != "lightning-infusion" || !player.Model.HasDiscovery(discovery))) throw new InvalidOperationException("Discovery is locked.");
                    if (mastery is not null && ((mastery != "tempest" && mastery != "storm-guardian") || !player.Model.HasMastery(mastery))) throw new InvalidOperationException("Mastery is locked.");
                    if (discovery is null && mastery is not null) throw new InvalidOperationException("Mastery requires Lightning Infusion.");
                    player.Loadout = new AbilityLoadout(discovery, mastery);
                    // Starter Equipment: Activate Stormheart only with its required infusion.
                    player.Model.EquipRelic(discovery is null ? null : "stormheart");
                    break;
                case "branch":
                    if (_branch.Length > 0) throw new InvalidOperationException("The expedition branch is already selected.");
                    if (Math.Abs(player.X + 4) + Math.Abs(player.Z + 3) > 2) throw new InvalidOperationException("Choose a branch at the shrine.");
                    if (command.TargetId != "ruin" && command.TargetId != "silence") throw new ArgumentException("Unknown branch.");
                    _branch = command.TargetId;
                    if (_branch == "silence") _enemies["guardian"].Model.GrantBarrier(40);
                    break;
                case "freeze":
                case "attack":
                    if (command.TargetId is null || !_enemies.TryGetValue(command.TargetId, out var target) || !target.Model.IsAlive) throw new ArgumentException("A living enemy is required.");
                    if (Distance(player, target) > (command.Kind == "freeze" ? 5 : 2)) throw new InvalidOperationException("The enemy is out of range.");
                    if (command.Kind == "freeze")
                    {
                        if (target.FrozenUntil > _tick) throw new InvalidOperationException("The target is already frozen.");
                        Spend(player, "freeze", 10, 20);
                        target.FrozenUntil = _tick + 40;
                        target.FrozenBy = player.Model.Id;
                        break;
                    }
                    var shatter = target.FrozenUntil > _tick && target.FrozenBy != player.Model.Id;
                    var reactions = shatter
                        ? new[] { new ReactionDefinition("cooperative-shatter", DamageType.Physical, new DamageEffect(DamageType.Physical, 20)) }
                        : Array.Empty<ReactionDefinition>();
                    Combatant? ally = null;
                    if (!string.IsNullOrEmpty(command.AllyId))
                    {
                        if (!_players.TryGetValue(command.AllyId, out var allyActor) || Distance(player, allyActor) > 4) throw new ArgumentException("A nearby ally is required.");
                        ally = allyActor.Model;
                    }
                    var candidates = _enemies.Values.Where(enemy => !ReferenceEquals(enemy, target) && Distance(target, enemy) <= 4)
                        .OrderBy(enemy => Distance(target, enemy)).ThenBy(enemy => enemy.Model.Id, StringComparer.Ordinal).Select(enemy => enemy.Model).ToArray();
                    var actions = _resolver.ResolveLoadout(player.Model, target.Model, _strike, player.Loadout, candidates, _tick, ally, reactions);
                    AppendEvents(player.Model.Id + ":" + command.Sequence, actions);
                    if (shatter) target.FrozenUntil = 0;
                    foreach (var participant in _players.Values.Where(value => value.Model.IsAlive)) participant.Trial?.TryComplete(participant.Model);
                    break;
                default:
                    throw new ArgumentException("Unknown command.");
            }
        }

        private void Spend(Actor actor, string abilityId, int cost, int cooldown)
        {
            if (actor.Model.Resource < cost || _tick < actor.Model.GetAbilityReadyTick(abilityId)) throw new InvalidOperationException("Insufficient resource or active cooldown.");
            actor.Model.CommitAbilityUse(abilityId, cost, _tick, _tick + cooldown);
        }

        public void Advance()
        {
            lock (_gate)
            {
                _tick++;
                foreach (var player in _players.Values.Where(value => value.Model.IsAlive))
                    if (_tick % 10 == 0 && _branch != "silence") player.Model.RestoreResource(1);
                if (_tick % 5 != 0) return;
                foreach (var enemy in _enemies.Values.OrderBy(value => value.Model.Id, StringComparer.Ordinal))
                {
                    if (!enemy.Model.IsAlive || enemy.FrozenUntil > _tick) continue;
                    var player = _players.Values.Where(value => value.Model.IsAlive).OrderBy(value => Distance(enemy, value))
                        .ThenBy(value => value.Model.Id, StringComparer.Ordinal).FirstOrDefault();
                    if (player is null) continue;
                    if (Distance(enemy, player) > 1)
                    {
                        if (enemy.X != player.X) enemy.X += Math.Sign(player.X - enemy.X);
                        else enemy.Z += Math.Sign(player.Z - enemy.Z);
                    }
                    else if (player.DodgeUntil <= _tick && enemy.Model.GetAbilityReadyTick("claw") <= _tick)
                    {
                        var amount = enemy.Model.Id == "guardian" && _branch == "ruin" ? 14 : 8;
                        var hit = _resolver.Resolve(enemy.Model, player.Model,
                            new AbilityDefinition("claw", new[] { new DamageEffect(DamageType.Physical, amount) }, 0, 10),
                            Array.Empty<AbilityModifierDefinition>(), Array.Empty<Combatant>(), _tick);
                        AppendEvents("ai:" + enemy.Model.Id + ":" + _tick, hit);
                    }
                }
            }
        }

        public WorldSnapshot Snapshot()
        {
            lock (_gate)
            {
                return new WorldSnapshot
                {
                    Tick = _tick,
                    Branch = _branch,
                    Actors = _players.Values.Concat(_enemies.Values).OrderBy(value => value.Model.Id, StringComparer.Ordinal).Select(value => new ActorSnapshot
                    {
                        Id = value.Model.Id, IsPlayer = _players.ContainsKey(value.Model.Id), X = value.X, Z = value.Z,
                        Health = value.Model.Health, MaxHealth = value.Model.MaxHealth, Barrier = value.Model.Barrier,
                        Resource = value.Model.Resource, Frozen = value.FrozenUntil > _tick, Dodging = value.DodgeUntil > _tick,
                        InfusionUnlocked = value.Model.HasDiscovery("lightning-infusion"), TempestUnlocked = value.Model.HasMastery("tempest"),
                        GuardianUnlocked = value.Model.HasMastery("storm-guardian"), MasteryProgress = value.Model.GetMasteryChainAttacks("tempest")
                    }).ToArray(),
                    Events = _events.Select(value => new EventSnapshot { ActionId = value.ActionId, Tick = value.Tick, Sequence = value.Sequence,
                        ParentSequence = value.ParentSequence, SourceId = value.SourceId, TargetId = value.TargetId, Amount = value.Amount, Reason = value.Reason }).ToArray()
                };
            }
        }

        private void AppendEvents(string actionId, IReadOnlyList<CombatActionResult> actions)
        {
            foreach (var item in actions.SelectMany(action => action.Events).OrderBy(value => value.Sequence))
            {
                _events.Enqueue(new EventSnapshot { ActionId = actionId, Tick = item.Tick, Sequence = item.Sequence,
                    ParentSequence = item.ParentSequence ?? -1, SourceId = item.SourceId, TargetId = item.TargetId, Amount = item.Amount, Reason = item.Reason });
                while (_events.Count > 128) _events.Dequeue();
            }
        }

        private static int Distance(Actor first, Actor second) => Math.Abs(first.X - second.X) + Math.Abs(first.Z - second.Z);
        private static CommandReply Reply(long sequence, bool success, string message) => new() { Sequence = sequence, Success = success, Message = message };
        private static bool SameCommand(WorldCommand first, WorldCommand second) => first.Kind == second.Kind && first.X == second.X && first.Z == second.Z &&
            first.TargetId == second.TargetId && first.AllyId == second.AllyId && first.DiscoveryId == second.DiscoveryId && first.MasteryId == second.MasteryId;
    }
}
