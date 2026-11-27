using System.Collections.Generic;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Ground.Decals;

/// <summary>Lays decals such as oil stains and tyre tracks on a building's apron while it stands finished and seen.</summary>
[AddComponentMenu(HyperRTSMenu.Look + "Effects/Building Decals")]
[Icon(HyperRTSIcons.BuildingDecalsAuthoring)]
[HelpURL(HyperRTSDocs.Presentation)]
[DisallowMultipleComponent]
[RequiresAuthoring(typeof(BuildingAuthoring), "a Building")]
public class BuildingDecalsAuthoring : AuthoringBehaviour
{
    [Tooltip("The decals on the apron, in the building's space.")]
    [NoEmptyEntries(Key = nameof(BuildingDecalPlacement.material))]
    public List<BuildingDecalPlacement> decals = [];

    [Tooltip("How far each decal reaches above and below its centre, in total; short, so it marks the apron and the "
             + "ground beside it but not the walls.")]
    [Unit("m")]
    [Min(0.05f)]
    public float depth = 0.6f;

    public static void Add(EntityWriter writer, IReadOnlyList<BuildingDecal> decals) => writer.AddBuffer(decals);

    public class Baker : Baker<BuildingDecalsAuthoring>
    {
        public override void Bake(BuildingDecalsAuthoring authoring)
        {
            var decals = new List<BuildingDecal>();
            foreach (var placement in authoring.decals)
            {
                if (placement.material == null)
                {
                    continue;
                }

                decals.Add(new BuildingDecal
                {
                    Material = placement.material,
                    Position = placement.position,
                    Size = new float3(placement.size.x, placement.size.y, authoring.depth),
                    Yaw = math.radians(placement.yaw),
                });
            }

            Add(this.Writer(), decals);
        }
    }
}
