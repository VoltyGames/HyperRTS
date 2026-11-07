using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Movement.Crushing
{
    /// <summary>Lets a vehicle flatten destructible scenery (fences, hay bales, small trees) it drives over.</summary>
    [AddComponentMenu(HyperRTSMenu.Units + "Crusher")]
    [Icon(HyperRTSIcons.Units)]
    [HelpURL(HyperRTSDocs.Movement)]
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
                var writer = this.Writer();
                writer.Add(new Crusher { Size = authoring.size });
            }
        }
    }
}
