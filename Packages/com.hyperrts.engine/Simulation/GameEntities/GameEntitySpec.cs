using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.GameEntities
{
    /// <summary>Values shared by every ownable unit and building.</summary>
    public struct GameEntitySpec
    {
        public int TypeId;
        public FixedString64Bytes Name;
        public UnityObjectRef<Texture2D> Icon;
        public byte Owner;
        public float MaxHealth;
        public float VisionRange;
        public float BuildTime;
        public int Population;
        public bool CountsForVictory;
        public Entity DeathPrefab;
        public float ExperienceValue;
    }
}
