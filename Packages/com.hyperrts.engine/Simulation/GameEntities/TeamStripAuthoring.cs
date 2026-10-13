using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.GameEntities
{
    /// <summary>Limits the owner's colour to a height band, for models painted in team-colour strips (map buildings).</summary>
    [AddComponentMenu(HyperRTSMenu.Buildings + "Team Strip")]
    [Icon(HyperRTSIcons.Buildings)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a Unit or Building")]
    public class TeamStripAuthoring : AuthoringBehaviour
    {
        [Tooltip("Bottom of the band, in metres above the model's origin (its own space).")]
        public float bottom = 2f;

        [Tooltip("Top of the band, in metres above the model's origin (its own space).")]
        public float top = 2.4f;

        public class Baker : Baker<TeamStripAuthoring>
        {
            public override void Bake(TeamStripAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                writer.Add(new TeamStripBand { Bottom = authoring.bottom, Top = authoring.top });
            }
        }
    }
}
