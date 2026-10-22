using UnityEngine;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>One debris draw instance with last frame's matrix, so pieces write motion vectors.</summary>
    public struct DebrisInstance
    {
        // Unity finds these by name for instanced motion vectors, so they keep its casing.
        public Matrix4x4 objectToWorld;
        public Matrix4x4 prevObjectToWorld;
    }
}
