using System;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Cues.Effects
{
    /// <summary>What a blast or a death leaves in the ground for the rest of the match: a crater, burnt earth and a stain.</summary>
    [Serializable]
    public struct GroundScar
    {
        [Tooltip("Metres deep the crater bowl dents the ground's lighting, half the mark's size across; 0 for none. " +
                 "Clipped at 2 m.")]
        [Min(0f)]
        public float craterDepth;

        [Tooltip("How black the ground is burnt, in a disc a little wider than the bowl: 0 none, 1 soot black.")]
        [Range(0f, 1f)]
        public float scorch;

        [Tooltip("Broken stone and brick left on the ground, over the scorch's disc: 0 none, 1 fully covered.")]
        [Range(0f, 1f)]
        public float rubble;

        [Tooltip("Grey ash left on the ground, over the scorch's disc: 0 none, 1 fully covered.")]
        [Range(0f, 1f)]
        public float ash;

        [Tooltip("Spilt fuel and oil left on the ground, over the scorch's disc: 0 none, 1 a dark, glossy slick.")]
        [Range(0f, 1f)]
        public float fuel;

        [Tooltip("Wet, churned mud left on the ground, over the scorch's disc: 0 none, 1 fully covered.")]
        [Range(0f, 1f)]
        public float mud;

        /// <summary>Rubble, ash, fuel and wet mud, 0 to 1 each.</summary>
        public float4 Stain => new(rubble, ash, fuel, mud);

        /// <summary>Leaves rubble, ash, fuel or mud on the ground.</summary>
        public bool LeavesStain => rubble > 0f || ash > 0f || fuel > 0f || mud > 0f;

        /// <summary>Leaves a crater, scorch or stain in the ground.</summary>
        public bool ScarsGround => craterDepth > 0f || scorch > 0f || LeavesStain;
    }
}
