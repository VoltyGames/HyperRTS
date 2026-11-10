using HyperRTS.Presentation.Quality;
using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Bones
{
    /// <summary>
    /// The live pose slots, as a singleton <see cref="PoseSlotSystem"/> keeps: per slot every bone's skinning matrix,
    /// absolute in object space, which a renderer with that <see cref="PoseSlot"/> draws. Only <see cref="Cap"/> slots
    /// may be taken at once.
    /// </summary>
    public struct PoseSlots : IComponentData
    {
        /// <summary>Bones one slot holds; LitTeamBones.hlsl's POSE_SLOT_BONES matches it.</summary>
        public const int MostBones = 128;

        /// <summary>Float4 rows one slot holds, three per bone.</summary>
        public const int SlotLength = MostBones * 3;

        /// <summary>Slots kept, enough for the highest effect level's cap.</summary>
        public static int Most => EffectDetail.RagdollsAt(DetailLevel.High);

        /// <summary>Every slot's rows, slot after slot; row r of a bone is row r of its 3x4 matrix.</summary>
        public NativeArray<float4> Skins;

        public NativeArray<bool> Taken;

        /// <summary>Per slot, whether it was written since the last upload.</summary>
        public NativeArray<bool> Changed;

        /// <summary>How many slots may be taken at once, from the effect level.</summary>
        public int Cap;

        public static PoseSlots Create() => new()
        {
            Skins = new NativeArray<float4>(Most * SlotLength, Allocator.Persistent),
            Taken = new NativeArray<bool>(Most, Allocator.Persistent),
            Changed = new NativeArray<bool>(Most, Allocator.Persistent),
            Cap = Most,
        };

        public void Dispose()
        {
            Skins.Dispose();
            Taken.Dispose();
            Changed.Dispose();
        }

        /// <summary>Whether a slot can be taken now: fewer than <see cref="Cap"/> are.</summary>
        public readonly bool HasFree => InUse < Cap;

        public readonly int InUse
        {
            get
            {
                var count = 0;
                foreach (var taken in Taken)
                {
                    count += taken ? 1 : 0;
                }

                return count;
            }
        }

        /// <summary>Takes a free slot, or returns false at the cap.</summary>
        public bool TryTake(out int slot)
        {
            slot = HasFree ? Taken.IndexOf(false) : -1;
            if (slot < 0)
            {
                return false;
            }

            Taken[slot] = true;
            return true;
        }

        public void Release(int slot) => Taken[slot] = false;

        public void Write(int slot, int bone, in float4x4 skin)
        {
            var at = slot * SlotLength + bone * 3;
            Skins[at] = new float4(skin.c0.x, skin.c1.x, skin.c2.x, skin.c3.x);
            Skins[at + 1] = new float4(skin.c0.y, skin.c1.y, skin.c2.y, skin.c3.y);
            Skins[at + 2] = new float4(skin.c0.z, skin.c1.z, skin.c2.z, skin.c3.z);
            Changed[slot] = true;
        }

        /// <summary>The skinning matrix last written to <paramref name="bone"/> of <paramref name="slot"/>.</summary>
        public readonly float4x4 Read(int slot, int bone)
        {
            var at = slot * SlotLength + bone * 3;
            return math.transpose(new float4x4(Skins[at], Skins[at + 1], Skins[at + 2], new float4(0f, 0f, 0f, 1f)));
        }

        /// <summary>Copies each slot written since the last upload into <paramref name="buffer"/>.</summary>
        public void Upload(GraphicsBuffer buffer)
        {
            for (var slot = 0; slot < Changed.Length; slot++)
            {
                if (!Changed[slot])
                {
                    continue;
                }

                var start = slot * SlotLength;
                buffer.SetData(Skins, start, start, SlotLength);
                Changed[slot] = false;
            }
        }
    }
}
