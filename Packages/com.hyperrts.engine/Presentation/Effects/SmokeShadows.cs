using HyperRTS.Simulation.Common;
using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>
    /// The smoke columns nearest the camera, from streamed loops whose rigs carry a <see cref="SmokeShadow"/>, written
    /// each frame as shader globals that <see cref="Environment.CloudShadows"/> draws into the sun's cookie. Medium
    /// detail and up.
    /// </summary>
    public sealed class SmokeShadows
    {
        public const int Capacity = 32;

        /// <summary>Each column's foot in world space (xyz) and radius in metres (w).</summary>
        public static readonly int StartsId = Shader.PropertyToID("_HyperRTSSmokeStarts");

        /// <summary>Each column's top in world space (xyz) and opacity (w).</summary>
        public static readonly int EndsId = Shader.PropertyToID("_HyperRTSSmokeEnds");

        public static readonly int CountId = Shader.PropertyToID("_HyperRTSSmokeCount");

        private readonly Vector4[] _starts = new Vector4[Capacity];
        private readonly Vector4[] _ends = new Vector4[Capacity];

        /// <summary>The loops offered this frame; cleared by <see cref="Show"/>.</summary>
        public LoopOffers<SmokeShadow> Offers { get; } = new();

        /// <summary>Columns written by the last <see cref="Show"/>.</summary>
        public int Count { get; private set; }

        /// <summary>Writes the nearest offers up to the cap as globals (none on Low) and clears the offers.</summary>
        public void Show(Vector3 eye, DetailLevel level)
        {
            Count = 0;
            if (level != DetailLevel.Low)
            {
                Offers.SortFrom(eye);
                Count = Mathf.Min(Capacity, Offers.Count);
            }

            for (var i = 0; i < Count; i++)
            {
                var offer = Offers[i];
                offer.Source.Column(offer.Position, offer.Age, offer.Strength, out _starts[i], out _ends[i]);
            }

            // The arrays always go at full size: Unity fixes a global array's length the first time it is set.
            Shader.SetGlobalVectorArray(StartsId, _starts);
            Shader.SetGlobalVectorArray(EndsId, _ends);
            Shader.SetGlobalInt(CountId, Count);
            Offers.Clear();
        }
    }
}
