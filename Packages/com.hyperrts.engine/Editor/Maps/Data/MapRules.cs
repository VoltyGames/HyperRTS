using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Library;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>
    /// The thresholds a map is measured against and the match settings it is built with (the built scene's match
    /// takes its nav cell size and max slope from here). Exactly one <c>*.rules.json</c> per project.
    /// </summary>
    [Serializable]
    [MapLibraryItem("rules")]
    public sealed class MapRules : IMapLibraryItem
    {
        [NonSerialized]
        public string name;

        [Tooltip("Pathfinding cell size the built match uses (metres).")]
        public float navCellSize = 1f;

        [Tooltip("Steepest drivable ground (degrees); steeper terrain blocks units.")]
        public float maxSlope = 30f;

        [Tooltip("Light vehicle speed (m/s); rush times are path lengths at this speed.")]
        public float vehicleSpeed = 8f;

        [Tooltip("Width of the corridor closed along each found path when counting attack paths (metres).")]
        public float corridorWidth = 16f;

        [Tooltip("Smallest share of a base's radius that must stay buildable.")]
        [Range(0f, 1f)]
        public float minBuildableShare = 0.8f;

        [Tooltip("Most ground decals a map may lay before the report warns; each is a draw the GPU pays for.")]
        [Min(0)]
        public int maxDecals = 600;

        [Tooltip("Thresholds per mode.")]
        public List<MapModeRules> modes = new();

        [Header("Views")]
        [Tooltip("Pitch of the match camera (degrees down from the horizon); map screenshots use it.")]
        public float viewPitch = 52f;

        [Tooltip("Usual match camera height above the ground (metres).")]
        public float viewHeight = 48f;

        [Tooltip("Closest match camera height (metres), for close-up screenshots.")]
        public float viewCloseHeight = 18f;

        [Tooltip("Vertical field of view of the match camera (degrees).")]
        public float viewFieldOfView = 45f;

        public MapModeRules Mode(string mode) => MapKeys.Get(modes, mode, name, "mode");

        void IMapLibraryItem.Load(string key, MapLibraryRefs refs) => name = key;
    }
}
