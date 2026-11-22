using System.Linq;
using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Editor.Validation.Sets;
using HyperRTS.Presentation.Rendering.LitTeam;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Capture;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Squads;
using HyperRTS.Simulation.Transport;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules.Units
{
    /// <summary>
    /// Owner colour reaches a unit or building only through a material with <c>_TeamColor</c> (HyperRTS/LitTeam), and a
    /// vehicle or building a player can own carries enough team markers for its owner to show on the model.
    /// </summary>
    public sealed class TeamColorRule : AuthoringRule<GameEntityAuthoring>
    {
        private const string TeamColor = "_TeamColor";

        protected override void Check(GameEntityAuthoring entity, ValidationIssues issues)
        {
            var materials = entity.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .ToList();
            if (materials.Count == 0)
            {
                return;
            }

            if (!materials.Any(material => material.HasProperty(TeamColor)))
            {
                issues.Warn(entity, "No material has _TeamColor (use HyperRTS/LitTeam): it won't show its owner's colour.");
                return;
            }

            // TeamColorSettingsRule reports a project without exactly one asset, once.
            var settings = AssetSet.InProject().All<TeamColorSettings>();
            if (settings.Count == 1)
            {
                CheckMarkers(entity, settings[0].minMarkers, PrefabSet.InProject(), issues);
            }
        }

        /// <summary>Base prefabs in <paramref name="prefabs"/> are skipped: their variants are placed, and inherit their markers.</summary>
        internal static void CheckMarkers(GameEntityAuthoring entity, int minimum, PrefabSet prefabs, ValidationIssues issues)
        {
            if (!CarriesMarkers(entity) || prefabs.IsBase(entity.gameObject))
            {
                return;
            }

            var markers = entity.GetComponentsInChildren<TeamMarkerAuthoring>(true);
            foreach (var marker in markers.Where(marker => !Painted(marker)))
            {
                issues.Warn(marker, "This team marker has no _TeamColor material (use HyperRTS/LitTeam): it won't take its owner's colour.");
            }

            if (markers.Length < minimum)
            {
                issues.Warn(entity, $"Only {markers.Length} team markers, fewer than {minimum}: add Team Marker props " +
                    "(a roof panel, pennant, flag or placard) so its owner shows on the model.");
            }
        }

        // Vehicles and aircraft (units that aren't squads) and buildings a player can own. A building painted in a team
        // strip, as map buildings are, shows its holder through the strip instead.
        private static bool CarriesMarkers(GameEntityAuthoring entity)
        {
            if (!TakesAnOwner(entity))
            {
                return false;
            }

            if (entity is UnitAuthoring)
            {
                return !entity.TryGetComponent<SquadAuthoring>(out _);
            }

            return entity is BuildingAuthoring && !entity.TryGetComponent<TeamStripAuthoring>(out _);
        }

        // A neutral entity takes an owner only by capture or by a garrison moving in.
        private static bool TakesAnOwner(GameEntityAuthoring entity)
        {
            if (entity.owner != Owner.Neutral || entity.TryGetComponent<CapturableAuthoring>(out _))
            {
                return true;
            }

            return entity.TryGetComponent<ContainerAuthoring>(out var container) && container.neutralGarrison;
        }

        private static bool Painted(TeamMarkerAuthoring marker) =>
            marker.GetComponent<Renderer>().sharedMaterials.Any(material => material != null && material.HasProperty(TeamColor));
    }
}
