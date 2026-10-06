using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Resources;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.GameEntities
{
    /// <summary>Base for <c>UnitAuthoring</c> and <c>BuildingAuthoring</c>: identity, owner, health, vision, cost.</summary>
    public abstract class GameEntityAuthoring : AuthoringBehaviour
    {
        [Header("Identity")]
        [Tooltip("Name shown in the HUD. Instances sharing a name are one type (double-click, prerequisites).")]
        public string displayName;

        [Tooltip("Icon for HUD buttons and the selection panel.")]
        public Texture2D icon;

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

        [Tooltip("Optional prefab spawned where this dies (wreck, debris).")]
        public GameObject deathSpawn;

        [Tooltip("Experience a veteran-capable killer earns for destroying this.")]
        [Min(0f)]
        public float experienceValue = 10f;

        [Tooltip("The owner is defeated once no entity with this flag remains.")]
        public bool countsForVictory = true;

        [Header("Production")]
        [Tooltip("Resources spent to produce or place this.")]
        public List<ResourceQuantity> cost = new();

        [Tooltip("Seconds to produce (units) or construct (buildings).")]
        [Min(0f)]
        public float buildTime = 10f;

        [Tooltip("Buildings the owner must have completed before this can be produced.")]
        public List<GameEntityAuthoring> prerequisites = new();

        public int TypeId => EntityInfo.TypeIdFromName(DisplayName);

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        /// <summary>Edge-to-edge reach is measured from this, matching <see cref="Navigation.EntityRadius"/> at runtime.</summary>
        public abstract float Radius { get; }

        protected virtual int Population => 0;

        private void Reset() => displayName = name;

        /// <summary>Bakes the shared components; subclasses call this before adding their own.</summary>
        protected void BakeGameEntity(IBaker baker, Entity entity)
        {
            var sink = new BakerSink(baker, entity);
            var displayText = new FixedString64Bytes();
            displayText.CopyFromTruncated(DisplayName);
            GameEntitySetup.Add(ref sink, new GameEntitySpec
            {
                TypeId = TypeId,
                Name = displayText,
                Icon = icon,
                Owner = (byte)owner,
                MaxHealth = maxHealth,
                VisionRange = visionRange,
                BuildTime = buildTime,
                Population = Population,
                CountsForVictory = countsForVictory,
                ExperienceValue = experienceValue,
                DeathPrefab = deathSpawn != null ? baker.GetEntity(deathSpawn, TransformUsageFlags.Dynamic) : Entity.Null,
            }, ProducibleBaking.Costs(cost), ProducibleBaking.Prerequisites(baker, prerequisites));
        }
    }
}
