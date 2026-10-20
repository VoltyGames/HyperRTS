using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.GameEntities
{
    /// <summary>Values shared by every ownable unit and building.</summary>
    public struct GameEntitySpec
    {
        public EntityInfo Info;
        public byte Owner;
        public float MaxHealth;
        public float VisionRange;
        public float EyeHeight;
        public float BuildTime;
        public int Population;
        public bool CountsForVictory;
        public Entity DeathPrefab;
    }
}
