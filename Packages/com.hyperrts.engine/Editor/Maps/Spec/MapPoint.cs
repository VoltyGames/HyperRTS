using System;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A point on the map plane in metres, X east and Z north, origin at the map centre.</summary>
    [Serializable]
    public struct MapPoint
    {
        public float x;
        public float z;

        public MapPoint(float x, float z)
        {
            this.x = x;
            this.z = z;
        }

        public Vector2 ToVector2() => new(x, z);

        public Vector3 ToVector3(float y = 0f) => new(x, y, z);

        public override string ToString() => $"({x:0.#}, {z:0.#})";
    }
}
