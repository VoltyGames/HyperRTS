using System.Collections.Generic;
using HyperRTS.Simulation.Resources;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Base for <c>UnitAuthoring</c> and <c>BuildingAuthoring</c>: identity, owner, health, vision, cost.</summary>
    public abstract class GameEntityAuthoring : MonoBehaviour
    {
        public const int MaxOwner = 15;

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

        /// <summary>Edge-to-edge reach is measured from this, matching the baked <c>EntityRadius</c>.</summary>
        public abstract float EntityRadius { get; }

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
                DeathPrefab = deathSpawn != null ? baker.GetEntity(deathSpawn, TransformUsageFlags.Dynamic) : Entity.Null,
            });

            var costs = baker.SetBuffer<ResourceCost>(entity);
            foreach (var quantity in cost)
            {
                if (quantity.type != null)
                {
                    costs.Add(new ResourceCost { Type = quantity.type, Amount = quantity.amount });
                }
            }

            var required = baker.SetBuffer<Prerequisite>(entity);
            foreach (var prerequisite in prerequisites)
            {
                if (prerequisite != null)
                {
                    baker.DependsOn(prerequisite);
                    required.Add(new Prerequisite { TypeId = prerequisite.TypeId });
                }
            }
        }
    }
}
