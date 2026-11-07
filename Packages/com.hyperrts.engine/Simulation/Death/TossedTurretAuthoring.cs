using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Death
{
    /// <summary>The remains of a thrown turret (a <see cref="TurretTossAuthoring.turret"/>): the death fills in the throw clients show.</summary>
    [AddComponentMenu(HyperRTSMenu.Entity + "Destruction/Tossed Turret")]
    [Icon(HyperRTSIcons.TossedTurretAuthoring)]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    public class TossedTurretAuthoring : AuthoringBehaviour
    {
        public class Baker : Baker<TossedTurretAuthoring>
        {
            public override void Bake(TossedTurretAuthoring authoring)
            {
                var writer = this.Writer();
                writer.Add<TossedTurret>();
            }
        }
    }
}
