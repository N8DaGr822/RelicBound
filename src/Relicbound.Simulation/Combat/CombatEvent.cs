using Relicbound.Core.Combat;

namespace Relicbound.Simulation.Combat
{
    public sealed record CombatEvent(
        long Tick,
        int Sequence,
        int? ParentSequence,
        string SourceId,
        string AttackerId,
        string TargetId,
        DamageType? DamageType,
        int Amount,
        string Reason);
}
