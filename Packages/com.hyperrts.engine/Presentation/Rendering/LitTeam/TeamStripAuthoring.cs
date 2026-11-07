using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Rendering.LitTeam
{
    /// <summary>Limits the owner's colour to a height band, for models painted in team-colour strips (map buildings).</summary>
    [AddComponentMenu(HyperRTSMenu.Buildings + "Team Strip")]
    [Icon(HyperRTSIcons.Buildings)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a Unit or Building")]
    public class TeamStripAuthoring : AuthoringBehaviour
    {
        [Tooltip("Bottom of the band, above the model's origin (its own space).")]
        [Unit("m")]
        public float bottom = 2f;

        [Tooltip("Top of the band, above the model's origin (its own space).")]
        [Unit("m")]
        public float top = 2.4f;

        public class Baker : Baker<TeamStripAuthoring>
        {
            public override void Bake(TeamStripAuthoring authoring)
            {
                var writer = this.Writer();
                writer.Add(new TeamStripBand { Bottom = authoring.bottom, Top = authoring.top });
            }
        }
    }
}
