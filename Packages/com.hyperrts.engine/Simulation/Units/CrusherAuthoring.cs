using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Units
{
    /// <summary>Lets a vehicle flatten destructible scenery (fences, hay bales, small trees) it drives over.</summary>
    [AddComponentMenu(HyperRTSMenu.Units + "Crusher")]
    [Icon(HyperRTSIcons.Units)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(UnitAuthoring), "a Unit")]
    public class CrusherAuthoring : AuthoringBehaviour
    {
        [Tooltip("Flattens scenery whose Crushed By is 1 to this (e.g. 1 light vehicle, 2 APC, 3 tank).")]
        [Min(1)]
        public int size = 3;

        public class Baker : Baker<CrusherAuthoring>
        {
            public override void Bake(CrusherAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                writer.Add(new Crusher { Size = authoring.size });
            }
        }
    }
}
