using HyperRTS.Core;
using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Destruction
{
    /// <summary>The remains of a thrown turret (a <see cref="TurretTossAuthoring.turret"/>): the death fills in the throw clients show.</summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Tossed Turret")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    public class TossedTurretAuthoring : AuthoringBehaviour
    {
        public class Baker : Baker<TossedTurretAuthoring>
        {
            public override void Bake(TossedTurretAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                writer.Add<TossedTurret>();
            }
        }
    }
}
