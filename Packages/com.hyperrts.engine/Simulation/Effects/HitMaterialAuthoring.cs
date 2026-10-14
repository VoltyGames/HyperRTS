using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Effects
{
    /// <summary>What this unit, building or tree is made of, so shots that hit it throw up the matching debris.</summary>
    [AddComponentMenu(HyperRTSMenu.Effects + "Hit Material")]
    [Icon(HyperRTSIcons.Effects)]
    [HelpURL(HyperRTSDocs.Effects)]
    [DisallowMultipleComponent]
    public class HitMaterialAuthoring : AuthoringBehaviour
    {
        [Tooltip("Material struck by hits on this entity (metal for vehicles, masonry for houses, wood for trees).")]
        public SurfaceMaterial material = SurfaceMaterial.Metal;

        public class Baker : Baker<HitMaterialAuthoring>
        {
            public override void Bake(HitMaterialAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                EffectSetup.AddHitMaterial(ref writer, authoring.material);
            }
        }
    }
}
