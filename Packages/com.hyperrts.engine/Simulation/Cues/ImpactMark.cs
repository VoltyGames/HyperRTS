using HyperRTS.Core;
using UnityEngine;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>
    /// A mark shots leave where they land on open ground (a crater, a scorch): decal variants, size and lifetime.
    /// Presentation draws it; the simulation only reports where shots landed.
    /// </summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Combat + "Impact Mark", fileName = "ImpactMark")]
    [HelpURL(HyperRTSDocs.Combat)]
    public class ImpactMark : ScriptableObject
    {
        [Tooltip("Decal materials (URP decal shader); each impact picks one at random.")]
        public Material[] decals = { };

        [Tooltip("Random diameter in metres between x and y, for a hit without splash.")]
        public Vector2 size = new(2f, 3f);

        [Tooltip("Metres added to the diameter per metre of the hit's splash radius, so bigger blasts leave bigger marks.")]
        [Min(0f)]
        public float sizePerSplashRadius = 0.5f;

        [Tooltip("How far the decal reaches above and below the ground, in metres. Keep it shallow so units standing " +
                 "in the mark stay clean.")]
        [Min(0.1f)]
        public float depth = 1f;

        [Tooltip("Metres deep the crater bowl dents the ground's lighting for the rest of the match, half the mark's size " +
                 "across; 0 for none. Clipped at 2 m.")]
        [Min(0f)]
        public float craterDepth;

        [Tooltip("How black the ground is burnt for the rest of the match, in a disc a little wider than the bowl: 0 none, " +
                 "1 soot black.")]
        [Range(0f, 1f)]
        public float scorch;

        [Tooltip("Broken stone and brick left on the ground for the rest of the match, over the scorch's disc: 0 none, " +
                 "1 fully covered.")]
        [Range(0f, 1f)]
        public float rubble;

        [Tooltip("Grey ash left on the ground for the rest of the match, over the scorch's disc: 0 none, 1 fully covered.")]
        [Range(0f, 1f)]
        public float ash;

        [Tooltip("Spilt fuel left on the ground for the rest of the match, over the scorch's disc: 0 none, 1 a dark, " +
                 "glossy slick.")]
        [Range(0f, 1f)]
        public float fuel;

        [Tooltip("Seconds the decal stays at full strength; 0 keeps it until newer marks of this kind reuse it. The " +
                 "crater, scorch and remains outlast it.")]
        [Min(0f)]
        public float lifetime = 120f;

        [Tooltip("Seconds it then takes to fade out.")]
        [Min(0f)]
        public float fadeTime = 30f;

        [Tooltip("Most marks of this kind on the map at once; past it the oldest is reused.")]
        [Min(1)]
        public int maxCount = 64;

        /// <summary>Leaves rubble, ash or fuel on the ground for the rest of the match.</summary>
        public bool LeavesRemains => rubble > 0f || ash > 0f || fuel > 0f;

        /// <summary>Leaves a crater, scorch or remains in the ground for the rest of the match.</summary>
        public bool ScarsGround => craterDepth > 0f || scorch > 0f || LeavesRemains;
    }
}
