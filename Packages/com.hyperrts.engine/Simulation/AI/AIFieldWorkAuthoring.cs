using HyperRTS.Core;
using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.AI
{
    /// <summary>
    /// Lets bots dig this line-placed building in at their defence points, as cover or as a barrier in front of it.
    /// Builders must list it among their build options.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Buildings + "AI Field Work")]
    [HelpURL(HyperRTSDocs.AI)]
    [DisallowMultipleComponent]
    public class AIFieldWorkAuthoring : AuthoringBehaviour
    {
        [Tooltip("Cover goes across the lane's approach; a barrier goes a few metres in front of it.")]
        public FieldWorkRole role;

        public class Baker : Baker<AIFieldWorkAuthoring>
        {
            public override void Bake(AIFieldWorkAuthoring authoring) =>
                this.Writer().Add(new AIFieldWork { Role = authoring.role });
        }
    }
}
