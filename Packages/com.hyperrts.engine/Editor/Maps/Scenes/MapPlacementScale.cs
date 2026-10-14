using System;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Simulation.Combat;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Scenes
{
    /// <summary>
    /// Scales a placed instance. A prefab with <see cref="StretchPartsAuthoring"/> keeps a uniform scale and stretches
    /// only its span between its caps; any other prefab stretches whole along its long side.
    /// </summary>
    public static class MapPlacementScale
    {
        public static void Apply(GameObject instance, MapPlacement placement)
        {
            if (!instance.TryGetComponent<StretchPartsAuthoring>(out var parts))
            {
                instance.transform.localScale = placement.LocalScale;
                return;
            }

            instance.transform.localScale = Vector3.one * placement.Scale;
            if (Mathf.Approximately(placement.Stretch, 1f))
            {
                return;
            }

            RequireSameAxis(parts, placement);
            parts.Extend(placement.PartsExtension);
            Record(parts);
        }

        /// <summary>The planners stretch along the palette size's long side, so the parts must lie along it too.</summary>
        private static void RequireSameAxis(StretchPartsAuthoring parts, MapPlacement placement)
        {
            var axis = parts.transform.InverseTransformDirection(parts.startCap.parent.TransformDirection(parts.Layout().Axis));
            var partsAlongZ = Mathf.Abs(axis.z) > Mathf.Abs(axis.x);
            if (partsAlongZ == placement.LongSideAlongZ)
            {
                return;
            }

            throw new InvalidOperationException(
                $"HyperRTS: '{placement.Entry.key}' stretch parts lie along {(partsAlongZ ? "Z" : "X")}, but its size " +
                $"{placement.Entry.size} puts the long side along {(placement.LongSideAlongZ ? "Z" : "X")}.");
        }

        private static void Record(StretchPartsAuthoring parts)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(parts);
            PrefabUtility.RecordPrefabInstancePropertyModifications(parts.startCap);
            PrefabUtility.RecordPrefabInstancePropertyModifications(parts.endCap);
            PrefabUtility.RecordPrefabInstancePropertyModifications(parts.span);
        }
    }
}
