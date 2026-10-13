using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Meshes
{
    /// <summary>One unbroken stretch of a road ribbon: its rows and which of its ends fade out.</summary>
    internal readonly struct MapRoadRun
    {
        public MapRoadRun(List<float> along, int first, int last, bool fadeIn, bool fadeOut)
        {
            Along = along;
            First = first;
            Last = last;
            FadeIn = fadeIn;
            FadeOut = fadeOut;
        }

        /// <summary>Metres along the whole road at each row.</summary>
        public List<float> Along { get; }

        public int First { get; }

        public int Last { get; }

        public bool FadeIn { get; }

        public bool FadeOut { get; }

        /// <summary>Vertex alpha at a row: clear at a fading end, solid from <see cref="MapRoadMesh.FadeLength"/> in.</summary>
        public float Alpha(int row)
        {
            var fromStart = FadeIn ? (Along[row] - Along[First]) / MapRoadMesh.FadeLength : 1f;
            var fromEnd = FadeOut ? (Along[Last] - Along[row]) / MapRoadMesh.FadeLength : 1f;
            return Mathf.Clamp01(Mathf.Min(fromStart, fromEnd));
        }
    }
}
