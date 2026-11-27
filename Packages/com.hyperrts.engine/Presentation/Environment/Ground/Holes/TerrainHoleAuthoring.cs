using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Ground.Holes;

/// <summary>
/// Cuts a hole in the drawn terrain under part of this building's footprint once it is built, so a model that
/// goes below ground shows. Clients only: the simulation's ground never changes.
/// </summary>
[AddComponentMenu(HyperRTSMenu.Look + "Effects/Terrain Hole")]
[Icon(HyperRTSIcons.TerrainHoleAuthoring)]
[HelpURL(HyperRTSDocs.Presentation)]
[DisallowMultipleComponent]
[RequiresAuthoring(typeof(Simulation.Buildings.BuildingAuthoring), "a Building")]
public class TerrainHoleAuthoring : AuthoringBehaviour
{
    [Tooltip("Hole centre in the building's local space, x and z metres.")]
    public Vector2 center;

    [Tooltip("Hole width and depth in metres, along the building's own axes.")]
    public Vector2 size = new(4f, 4f);

    public class Baker : Baker<TerrainHoleAuthoring>
    {
        public override void Bake(TerrainHoleAuthoring authoring) =>
            this.Writer().Add(new TerrainHole { Center = authoring.center, Size = authoring.size });
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(new Vector3(center.x, 0f, center.y), new Vector3(size.x, 0f, size.y));
    }
}
