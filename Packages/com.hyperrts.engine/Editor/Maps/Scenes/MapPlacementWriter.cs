using System.Collections.Generic;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Resources;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Scenes
{
    /// <summary>
    /// Instantiates every placement: gameplay prefabs at the SubScene root with their owner and resource amount,
    /// scenery under the main scene's generated root. Everything stands on the finished ground (<see cref="MapPlacementPose"/>).
    /// </summary>
    public static class MapPlacementWriter
    {
        public static void Write(MapScene scene, MapGeneration generation)
        {
            var (heights, groundLevel) = (generation.Heights, generation.Layout.GroundLevel);
            var scenery = scene.CreateChild("Scenery");
            var counts = new Dictionary<string, int>();
            foreach (var placement in generation.Placements.All)
            {
                var key = placement.Entry.key;
                counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
                var name = $"{key} {counts[key]}";
                var instance = placement.IsEntity
                    ? (GameObject)PrefabUtility.InstantiatePrefab(placement.Entry.prefab, scene.Entities)
                    : (GameObject)PrefabUtility.InstantiatePrefab(placement.Entry.prefab, scenery);
                if (placement.IsEntity)
                {
                    MapScene.NameGenerated(instance, name);
                    Configure(instance, placement);
                }
                else
                {
                    instance.name = name;
                }

                var pose = MapPlacementPose.OnGround(placement, heights, groundLevel);
                instance.transform.SetPositionAndRotation(pose.position, pose.rotation);
                MapPlacementScale.Apply(instance, placement);
            }
        }

        private static void Configure(GameObject instance, MapPlacement placement)
        {
            foreach (var entity in instance.GetComponentsInChildren<GameEntityAuthoring>(true))
            {
                entity.owner = placement.Owner;
                PrefabUtility.RecordPrefabInstancePropertyModifications(entity);
            }

            if (placement.Deck is { } deck)
            {
                var area = instance.GetComponent<NavAreaAuthoring>();
                if (area == null)
                {
                    area = instance.AddComponent<NavAreaAuthoring>();
                }

                area.kind = NavAreaKind.Deck;
                area.size = deck;
            }

            foreach (var node in instance.GetComponentsInChildren<ResourceNodeAuthoring>(true))
            {
                if (placement.Amount <= 0)
                {
                    throw new System.InvalidOperationException(
                        $"HyperRTS: '{placement.Entry.key}' is a resource node placed without an amount; place it through the spec's resources.");
                }

                node.amount = placement.Amount;
                PrefabUtility.RecordPrefabInstancePropertyModifications(node);
            }
        }
    }
}
