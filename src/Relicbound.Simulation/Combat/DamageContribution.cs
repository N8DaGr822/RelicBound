using Relicbound.Core.Combat;

namespace Relicbound.Simulation.Combat
{
    public sealed record DamageContribution(string SourceId, DamageType DamageType, int Amount);
}
