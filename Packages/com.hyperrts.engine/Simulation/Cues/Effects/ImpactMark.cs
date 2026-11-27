using HyperRTS.Simulation.Common.Authoring;
using UnityEngine;

namespace HyperRTS.Simulation.Cues.Effects;

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

    [Tooltip("Decal diameter as a multiple of the mark's, for a decal that also draws what the blast threw beyond its bowl.")]
    [Min(1f)]
    public float decalReach = 1f;

    [Tooltip("What the mark leaves in the ground for the rest of the match, over a disc a little wider than the mark.")]
    public GroundScar scar;

    [Tooltip("Debris the blast throws out of the bowl and leaves lying for the match, such as clods and stones.")]
    public DebrisSet[] debris = { };

    [Tooltip("Seconds the decal stays at full strength; 0 keeps it until newer marks of this kind reuse it. The " +
             "crater, scorch and stain outlast it.")]
    [Min(0f)]
    public float lifetime = 120f;

    [Tooltip("Seconds it then takes to fade out.")]
    [Min(0f)]
    public float fadeTime = 30f;

    [Tooltip("Most marks of this kind on the map at once; past it the oldest is reused.")]
    [Min(1)]
    public int maxCount = 64;
}
