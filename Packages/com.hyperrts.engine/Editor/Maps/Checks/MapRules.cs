using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Checks
{
    /// <summary>
    /// The thresholds a map is measured against and the match settings it is built with (the built scene's match
    /// takes its nav cell size, building gap, max slope and infantry tuning from here). Exactly one <c>*.rules.asset</c> per
    /// project.
    /// </summary>
    [MapLibraryItem("rules")]
    [CreateAssetMenu(menuName = HyperRTSMenu.MapLibrary + "Rules", fileName = "New.rules")]
    public sealed class MapRules : MapLibraryItem
    {
        [Tooltip("Pathfinding cell size the built match uses (metres).")]
        public float navCellSize = 1f;

        [Tooltip("Least clear gap between buildings that don't touch (metres), so vehicles fit between them.")]
        [Min(0f)]
        public float buildingGap;

        [Tooltip("Steepest drivable ground (degrees); steeper terrain blocks units.")]
        public float maxSlope = 30f;

        [Tooltip("The infantry tuning (cover, squads, voice, craters) the built match references.")]
        public InfantryTuning infantry;

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

        [Tooltip("Fewest props per hectare per surface before the report warns it looks bare.")]
        public List<MapPropTarget> propTargets = new();

        [Header("Composition")]
        [Tooltip("Ground the match camera sees at its middle zoom (metres, across and deep); the report tiles the play area with frames this size.")]
        public Vector2 compositionFrame = new(90f, 75f);

        [Tooltip("Fewest of the five kinds of interest (wood edge, water, field pattern, built place, relief) a frame should hold.")]
        [Range(1, 5)]
        public int compositionMinKinds = 3;

        [Tooltip("Height range inside a frame that counts as a change in relief (metres).")]
        [Min(0f)]
        public float compositionMinRelief = 3f;

        [Tooltip("Share of frames that should hold the fewest kinds before the report warns.")]
        [Range(0f, 1f)]
        public float compositionTargetShare = 0.85f;

        [Header("Modes")]
        [Tooltip("Thresholds per mode.")]
        public List<MapModeRules> modes = new();

        [Header("Views")]
        [Tooltip("Usual match camera height above the ground (metres).")]
        public float viewHeight = 48f;

        [Tooltip("Closest match camera height (metres), for close-up screenshots.")]
        public float viewCloseHeight = 18f;

        public MapModeRules Mode(string mode) => MapKeys.Get(modes, mode, key, "mode");
    }
}
