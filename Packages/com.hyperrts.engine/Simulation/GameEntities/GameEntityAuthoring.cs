using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Production;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.GameEntities
{
    /// <summary>Base for <c>UnitAuthoring</c> and <c>BuildingAuthoring</c>: identity, owner, health, vision, cost.</summary>
    [RequiresGhost]
    public abstract class GameEntityAuthoring : ProducibleAuthoring
    {
        [Header("Ownership")]
        [Tooltip("Owning player number from MatchAuthoring (1 = first player). 0 = neutral.")]
        [Owner]
        public int owner = 1;

        [Header("Durability")]
        [Tooltip("Maximum and starting health.")]
        [Min(1f)]
        public float maxHealth = 100f;

        [Tooltip("Sight radius in world units, used by fog of war and auto-targeting.")]
        [Min(0f)]
        public float visionRange = 12f;

        [Tooltip("Eye height above the ground in world units; higher eyes see over more terrain.")]
        [Min(0f)]
        public float eyeHeight = 2f;

        [Tooltip("As tall as a vehicle or a building: seen over walls and hedges that hide infantry.")]
        public bool tall;

        [Tooltip("Optional prefab spawned where this dies (wreck, debris).")]
        [PrefabAsset("Death spawn"), GhostPrefab("Death spawn", OnlyOnGhosts = true)]
        public GameObject deathSpawn;

        [Tooltip("The owner is defeated once no entity with this flag remains.")]
        public bool countsForVictory = true;

        /// <summary>Edge-to-edge reach is measured from this, matching <see cref="Navigation.EntityRadius"/> at runtime.</summary>
        public abstract float Radius { get; }

        protected virtual int Population => 0;

        /// <summary>Bakes the shared components; subclasses call this before adding their own.</summary>
        protected void BakeGameEntity(IBaker baker, Entity entity)
        {
            var writer = baker.Writer(entity);
            var info = BakeProducible(baker, out var costs, out var required);
            GameEntitySetup.Add(writer, new GameEntitySpec
            {
                Info = info,
                Owner = (byte)owner,
                MaxHealth = maxHealth,
                VisionRange = visionRange,
                EyeHeight = eyeHeight,
                Tall = tall,
                BuildTime = buildTime,
                Population = Population,
                CountsForVictory = countsForVictory,
                DeathPrefab = deathSpawn != null ? baker.GetEntity(deathSpawn, TransformUsageFlags.Dynamic) : Entity.Null,
            }, costs, required);
        }
    }
}
