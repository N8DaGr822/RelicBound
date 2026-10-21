using System;

namespace Relicbound.Contracts
{
    [Serializable]
    public sealed class WorldCommand
    {
        public long Sequence;
        public string Kind = string.Empty;
        public int X;
        public int Z;
        public string TargetId = string.Empty;
        public string AllyId = string.Empty;
        public string DiscoveryId = string.Empty;
        public string MasteryId = string.Empty;
    }

    [Serializable]
    public sealed class CommandReply
    {
        public long Sequence;
        public bool Success;
        public string Message = string.Empty;
    }

    [Serializable]
    public sealed class SessionReply
    {
        public string PlayerId = string.Empty;
        public string Token = string.Empty;
    }

    [Serializable]
    public sealed class ActorSnapshot
    {
        public string Id = string.Empty;
        public bool IsPlayer;
        public int X;
        public int Z;
        public int Health;
        public int MaxHealth;
        public int Barrier;
        public int Resource;
        public bool Frozen;
        public bool Dodging;
        public bool InfusionUnlocked;
        public bool TempestUnlocked;
        public bool GuardianUnlocked;
        public int MasteryProgress;
    }

    [Serializable]
    public sealed class EventSnapshot
    {
        public string ActionId = string.Empty;
        public long Tick;
        public int Sequence;
        public int ParentSequence = -1;
        public string SourceId = string.Empty;
        public string TargetId = string.Empty;
        public int Amount;
        public string Reason = string.Empty;
    }

    [Serializable]
    public sealed class WorldSnapshot
    {
        public long Tick;
        public string Branch = string.Empty;
        public ActorSnapshot[] Actors = Array.Empty<ActorSnapshot>();
        public EventSnapshot[] Events = Array.Empty<EventSnapshot>();
    }
}
