using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>A holed variant of a section's wall or roof, and where its hole is, in the building's space.</summary>
    public struct SectionHole
    {
        public Entity Model;
        public float3 Center;
    }

    /// <summary>
    /// The models that show a <see cref="Section"/>, at the same index: standing, holed, breaking and broken. A section
    /// without pieces swaps straight from <see cref="Intact"/> to <see cref="Broken"/>; one with them breaks as a clip of
    /// <see cref="Pieces"/> beside its <see cref="Stub"/>, then leaves the <see cref="Heap"/>.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct SectionModel : IBufferElementData
    {
        public const int MostHoles = 3;

        public Entity Intact;
        public Entity Broken;
        public Entity Pieces;
        public Entity Stub;
        public Entity Heap;
        public SectionHole FirstHole;
        public SectionHole SecondHole;
        public SectionHole ThirdHole;

        public readonly bool HasPieces => Pieces != Entity.Null;

        public readonly SectionHole Hole(int index) => index switch
        {
            0 => FirstHole,
            1 => SecondHole,
            _ => ThirdHole,
        };

        /// <summary>The hole variant whose hole is nearest <paramref name="local"/>, or -1 when the section has none.</summary>
        public readonly int NearestHole(float3 local)
        {
            var nearest = -1;
            var best = float.MaxValue;
            for (var i = 0; i < MostHoles; i++)
            {
                var hole = Hole(i);
                var distance = math.distancesq(hole.Center, local);
                if (hole.Model != Entity.Null && distance < best)
                {
                    best = distance;
                    nearest = i;
                }
            }

            return nearest;
        }

        /// <summary>Calls <paramref name="add"/> with every model that starts hidden, everything but <see cref="Intact"/>.</summary>
        public readonly void ForEachHidden(Action<Entity> add)
        {
            Entity[] hidden = { Broken, Pieces, Stub, Heap, FirstHole.Model, SecondHole.Model, ThirdHole.Model };
            foreach (var model in hidden)
            {
                if (model != Entity.Null)
                {
                    add(model);
                }
            }
        }
    }

    /// <summary>One break clip's rows in the building's vertex animation textures.</summary>
    public struct SectionClip
    {
        public int StartRow;
        public int FrameCount;
        public float Fps;

        public readonly float Seconds => FrameCount / Fps;
    }

    /// <summary>The three ways a section's pieces break, shared by every section of the building.</summary>
    public struct SectionClips : IComponentData
    {
        /// <summary>Blown inward by a hit from outside.</summary>
        public SectionClip BlownIn;

        /// <summary>Blown outward by a hit from inside.</summary>
        public SectionClip BlownOut;

        /// <summary>Falling apart with nothing hitting it, once its supports are gone.</summary>
        public SectionClip Collapse;
    }

    /// <summary>What this client has seen happen to a <see cref="Section"/>, at the same index; presentation keeps it.</summary>
    [InternalBufferCapacity(0)]
    public struct SectionLook : IBufferElementData
    {
        /// <summary>The holed variant showing plus one, 0 for the standing model.</summary>
        public byte Hole;

        /// <summary>Where the latest seen impact landed, in the building's space.</summary>
        public float3 Impact;

        /// <summary>When it landed on the world clock, or minus infinity for none.</summary>
        public float ImpactTime;

        /// <summary>When its break clip ends on the world clock, while it is breaking.</summary>
        public float BreakEnd;

        public static SectionLook Untouched => new() { ImpactTime = float.NegativeInfinity };
    }

    /// <summary>The sections on screen; presentation keeps it, and scenery sync resets it for changes nobody saw.</summary>
    public struct ShownSections : IComponentData
    {
        /// <summary>Effect rig played at a section as it breaks, +Z pointing out of the building.</summary>
        public UnityObjectRef<GameObject> Effect;

        /// <summary>Dust wave played at the foot of a ground floor section as it breaks.</summary>
        public UnityObjectRef<GameObject> DustWave;

        /// <summary>The broken mask on screen.</summary>
        public ulong Broken;

        /// <summary>The sections whose break clip is still playing.</summary>
        public ulong Breaking;

        /// <summary>False until shown, so a loaded or unseen building shows its state without effects.</summary>
        public bool Seen;
    }
}
