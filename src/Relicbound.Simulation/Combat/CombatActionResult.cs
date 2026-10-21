namespace Relicbound.Simulation.Combat
{
    public sealed record CombatActionResult(
        string AttackerId,
        string TargetId,
        CombatResult Damage,
        int TargetHealthBefore,
        int TargetHealthAfter)
    {
        public int AppliedDamage => TargetHealthBefore - TargetHealthAfter;

        public string? TriggerSourceId { get; init; }

        public long Tick { get; init; }

        public int ResourceSpent { get; init; }

        public long? AbilityReadyTick { get; init; }

        public int BarrierBefore { get; init; }

        public int BarrierAfter { get; init; }

        public IReadOnlyList<CombatEvent> Events { get; init; } = Array.Empty<CombatEvent>();
    }
}
