using HyperRTS.Simulation.Buildings.Placement;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>A static structure that blocks pathing. Add Producer, Resource Drop-Off etc. for its role.</summary>
    [AddComponentMenu(HyperRTSMenu.Entity + "Buildings/Building")]
    [Icon(HyperRTSIcons.BuildingAuthoring)]
    [HelpURL(HyperRTSDocs.Buildings)]
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

        [Tooltip("Enemies who saw it keep seeing it where they last did once it is in the fog.")]
        public bool rememberedInFog = true;

        [Tooltip("Units walk across the footprint instead of round it. A Destructible Crushed By rating still closes it to units that cannot crush that much (tank traps); with none it is fully open (trenches).")]
        public bool openFootprint;

        [Tooltip("Placed by dragging a line of segments, for walls. Footprint X is each segment's length.")]
        public bool placedInLines;

        public override float Radius => EntityRadius.Get(new NavObstacle { Size = footprint });

        public class Baker : Baker<BuildingAuthoring>
        {
            public override void Bake(BuildingAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                authoring.BakeGameEntity(this, entity);

                var writer = this.Writer(entity);
                BuildingSetup.Add(writer, authoring.footprint, authoring.populationProvided,
                    !authoring.startsUnderConstruction, authoring.power, authoring.rememberedInFog, authoring.openFootprint);
                if (authoring.placementSurface != PlacementSurface.Land)
                {
                    writer.Add(new BuildingPlacement { Surface = authoring.placementSurface });
                }

                if (authoring.placedInLines)
                {
                    writer.Add<LinePlacement>();
                }
            }
        }
    }
}
