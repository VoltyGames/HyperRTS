using System;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>One piece of a side's start kit: a prop, metres right (x) and forward (y) of the spawn, degrees of yaw.</summary>
    [Serializable]
    public sealed class MapStartItem
    {
        [MapKey(typeof(MapProp))]
        public string key;
        public Vector2 offset;
        public float yaw;

        /// <summary>Laid out facing the match camera, as a placed building faces, instead of turned with the spawn.</summary>
        public bool facesCamera;

        /// <summary>The yaw its offset turns with, in degrees.</summary>
        public float Frame(float spawnYaw) => facesCamera ? math.degrees(PlacementMath.CameraYaw) : spawnYaw;
    }
}
