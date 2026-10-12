using System.Collections.Generic;
using System.Linq;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.GameEntities;
using Unity.NetCode;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>
    /// Multiplayer names map scenery by where it stands and how it is turned (<see cref="SceneryId"/>), so two pieces
    /// on the same spot would be mistaken for each other.
    /// </summary>
    public sealed class SceneryIdRule : ISceneRule
    {
        public void Check(SceneSet scenes, ValidationIssues issues)
        {
            var placed = new Dictionary<ulong, DestructibleAuthoring>();
            var scenery = scenes.Roots.Where(root => root.scene.isSubScene)
                .SelectMany(root => root.GetComponentsInChildren<DestructibleAuthoring>(true));
            foreach (var piece in scenery)
            {
                if (ReplicatesAsGhost(piece))
                {
                    continue;
                }

                var place = piece.transform;
                var id = SceneryId.At(place.position, place.rotation).Value;
                if (placed.TryGetValue(id, out var other))
                {
                    issues.Error(piece, $"Stands on the same spot, turned the same way, as '{other.name}': " +
                                        "multiplayer can't tell them apart. Move or turn one.");
                    continue;
                }

                placed[id] = piece;
            }
        }

        // Units, buildings and ghosts replicate as ghosts, not by id.
        private static bool ReplicatesAsGhost(DestructibleAuthoring piece) =>
            piece.TryGetComponent<GameEntityAuthoring>(out _) || piece.TryGetComponent<GhostAuthoringComponent>(out _);
    }
}
