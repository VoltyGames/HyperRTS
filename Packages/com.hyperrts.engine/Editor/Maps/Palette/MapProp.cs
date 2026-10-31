using HyperRTS.Core;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Resources;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Palette
{
    /// <summary>One placeable prefab.</summary>
    [MapLibraryItem("prop")]
    [CreateAssetMenu(menuName = HyperRTSMenu.MapLibrary + "Prop", fileName = "New.prop")]
    public sealed class MapProp : MapLibraryItem
    {
        public GameObject prefab;

        /// <summary>Visual-only model drawn on the terrain where the prop falls in the backdrop; null leaves it out there.</summary>
        public GameObject backdropModel;

        [Tooltip("Footprint (width across, depth along the prefab's forward) in metres; spacing and levelling use it.")]
        public Vector2 size = new(4f, 4f);

        [Tooltip("Level the ground under the footprint before placing.")]
        public bool flatten;

        [Tooltip("Random yaw added on top of the spec's (degrees, either way).")]
        public float yawJitter;

        [Tooltip("In scatters, stand on the nearest road's verge facing along it (yaw jitter on top), like a wreck pushed aside.")]
        public bool besideRoad;

        [Tooltip("Tilt with the ground slope up to this many degrees; 0 stands upright.")]
        public float maxTilt;

        [Tooltip("Metres of the prop buried below the ground, so it sits in it rather than on it.")]
        public float buriedDepth;

        [Tooltip("Random uniform scale range.")]
        public Vector2 scale = Vector2.one;

        [Tooltip("Climate surface painted under and around the footprint (a gravel pad); empty for none.")]
        [MapKey(typeof(MapClimateSurface))]
        public string pad;

        [Tooltip("How far the pad reaches past the footprint (metres).")]
        public float padMargin = 3f;

        [Tooltip("How far the pad's edge wanders in and out (metres).")]
        public float padWander = 0.8f;

        [Tooltip("Lay the pad as an oval inside its rectangle (trampled ground) rather than a squared yard.")]
        public bool padRound;

        [Tooltip("Road class of an access track run from the pad to the nearest road; empty for none.")]
        [MapKey(typeof(MapRoadClass))]
        public string access;

        [Tooltip("Props this close (metres) to another asking for the same access road share its track; 0 for a track of their own.")]
        public float sharedAccessDistance;

        [Tooltip("Decal laid on the ground under the prop, turned with it (oil under a derrick, mud at a gate); empty for none.")]
        [MapKey(typeof(MapClimateDecal))]
        public string groundDecal;

        [Tooltip("Diameter of the ground decal (metres), scaled with the prop; required above 0 when groundDecal is set.")]
        public float groundDecalSize;

        public float Radius => Mathf.Max(size.x, size.y) * 0.5f;

        /// <summary>Prefabs with HyperRTS authoring bake into the SubScene; the rest are scenery in the main scene.</summary>
        public bool IsEntity => prefab.GetComponentInChildren<AuthoringBehaviour>(true) != null;

        /// <summary>A resource node. Looked up on the live prefab each time, so edits to it show on the next build.</summary>
        public bool IsResource => prefab.GetComponentInChildren<ResourceNodeAuthoring>() != null;

        /// <summary>A resource node that regrows once tapped (its authoring has a regrowth rate above 0).</summary>
        public bool IsRenewable
        {
            get
            {
                var node = prefab.GetComponentInChildren<ResourceNodeAuthoring>();
                return node != null && node.regrowthPerSecond > 0f;
            }
        }

        /// <summary>Something players use (a building, unit or resource node), as opposed to scenery.</summary>
        public bool IsGameplay =>
            prefab.GetComponentInChildren<GameEntityAuthoring>(true) != null
            || prefab.GetComponentInChildren<ResourceNodeAuthoring>(true) != null;
    }
}
