using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>One stream's path, picked rig and leftover particle fraction between frames.</summary>
    public sealed class StreamState
    {
        /// <summary>Last frame's point; this frame's on the first frame, so the first segment is empty.</summary>
        public Vector3 From;

        public Vector3 To;
        public float Carry;
        public int Frame;

        /// <summary>The variant this stream plays, picked by <see cref="Key"/>.</summary>
        public EffectRig Rig;

        public uint Key;

        /// <summary>The rig <see cref="Rig"/> was picked from.</summary>
        public EffectRig Root;

        /// <summary>Plays the root's variant for <see cref="Key"/>, picked again only when the root changes.</summary>
        public void Use(EffectRig root)
        {
            if (Root != root)
            {
                Root = root;
                Rig = root.Pick(Key);
            }
        }

        /// <summary>Streams the rig's authored rates along this frame's path at the effect detail's density.</summary>
        public void Emit(Quaternion rotation, float deltaTime, float strength, float poseScale = 1f) =>
            Rig.Stream(From, To, rotation, deltaTime, strength, EffectDetail.Density, ref Carry, poseScale);
    }
}
