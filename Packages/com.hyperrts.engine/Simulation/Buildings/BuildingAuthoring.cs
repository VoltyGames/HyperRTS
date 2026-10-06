using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>A static structure that blocks pathing. Add Producer, Resource Drop-Off etc. for its role.</summary>
    [AddComponentMenu(HyperRTSMenu.Buildings + "Building")]
    [Icon(HyperRTSIcons.Buildings)]
    [HelpURL(HyperRTSDocs.GettingStarted)]
    [DisallowMultipleComponent]
    public class BuildingAuthoring : GameEntityAuthoring
    {
        [Header("Building")]
        [Tooltip("Ground footprint (X by Z) blocked for pathing and placement.")]
        public Vector2 footprint = new(4f, 4f);

        [Tooltip("Population cap added for the owner once complete.")]
        [Min(0)]
        public int populationProvided;

        [Tooltip("Power generated once complete; negative draws power and stops working while the owner is low.")]
        public float power;

        [Tooltip("Start as a construction site instead of finished (for scene-placed buildings).")]
        public bool startsUnderConstruction;

        [Tooltip("Ground the footprint must cover when placed: land, water, or shoreline (both).")]
        public PlacementSurface placementSurface;

        public override float Radius => EntityRadius.Of(new NavObstacle { Size = footprint });

        public class Baker : Baker<BuildingAuthoring>
        {
            public override void Bake(BuildingAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                authoring.BakeGameEntity(this, entity);

                var sink = new BakerSink(this, entity);
                BuildingSetup.Add(ref sink, authoring.footprint, authoring.populationProvided,
                    !authoring.startsUnderConstruction, authoring.power, authoring.placementSurface);
            }
        }
    }

    /// <summary>Marks a static structure.</summary>
    public struct BuildingTag : IComponentData { }
}
