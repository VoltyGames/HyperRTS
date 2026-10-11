using System.Collections.Generic;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Resources;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>
    /// Instantiates every placement: gameplay prefabs at the SubScene root with their owner and resource amount,
    /// scenery under the main scene's generated root. Everything stands on the finished ground.
    /// </summary>
    public static class MapPlacementWriter
    {
        /// <summary>Heights are terrain heights; <paramref name="datum"/> is the terrain height of world y = 0.</summary>
        public static void Write(List<MapPlacement> placements, MapHeights heights, float datum, Scene entities,
            Transform root)
        {
            var scenery = new GameObject("Scenery").transform;
            scenery.SetParent(root, false);
            var counts = new Dictionary<string, int>();
            foreach (var placement in placements)
            {
                var key = placement.Entry.key;
                counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
                var name = $"{key} {counts[key]}";
                var instance = placement.IsEntity
                    ? (GameObject)PrefabUtility.InstantiatePrefab(placement.Entry.prefab, entities)
                    : (GameObject)PrefabUtility.InstantiatePrefab(placement.Entry.prefab, scenery);
                if (placement.IsEntity)
                {
                    MapSceneWriter.Rename(instance, name);
                    Configure(instance, placement);
                }
                else
                {
                    instance.name = name;
                }

                var position = placement.Position;
                var y = (placement.Height ?? heights.Height(position)) - datum;
                instance.transform.SetPositionAndRotation(new Vector3(position.x, y, position.y),
                    Quaternion.Euler(0f, placement.Yaw, 0f));
                instance.transform.localScale = Vector3.one * placement.Scale;
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

            if (placement.Amount <= 0)
            {
                return;
            }

            foreach (var node in instance.GetComponentsInChildren<ResourceNodeAuthoring>(true))
            {
                node.amount = placement.Amount;
                PrefabUtility.RecordPrefabInstancePropertyModifications(node);
            }
        }
    }
}
