using HyperRTS.Simulation.Common;
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

        [Tooltip("Optional prefab spawned where this dies (wreck, debris).")]
        public GameObject deathSpawn;

        [Tooltip("The owner is defeated once no entity with this flag remains.")]
        public bool countsForVictory = true;

        /// <summary>Edge-to-edge reach is measured from this, matching <see cref="Navigation.EntityRadius"/> at runtime.</summary>
        public abstract float Radius { get; }

        protected virtual int Population => 0;

        /// <summary>Bakes the shared components; subclasses call this before adding their own.</summary>
        protected void BakeGameEntity(IBaker baker, Entity entity)
        {
            var writer = new BakerWriter(baker, entity);
            var info = BakeProducible(baker, out var costs, out var required);
            GameEntitySetup.Add(ref writer, new GameEntitySpec
            {
                TypeId = info.TypeId,
                Name = info.Name,
                Icon = info.Icon,
                Owner = (byte)owner,
                MaxHealth = maxHealth,
                VisionRange = visionRange,
                EyeHeight = eyeHeight,
                BuildTime = buildTime,
                Population = Population,
                CountsForVictory = countsForVictory,
                DeathPrefab = deathSpawn != null ? baker.GetEntity(deathSpawn, TransformUsageFlags.Dynamic) : Entity.Null,
            }, costs, required);
        }
    }
}
