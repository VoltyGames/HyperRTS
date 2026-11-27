using System;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Cues.Effects;

/// <summary>What a blast or a death leaves in the ground for the rest of the match: a crater, burnt earth and a stain.</summary>
[Serializable]
public struct GroundScar
{
    [Tooltip("Metres deep the crater bowl dents the ground, half the mark's size across; 0 for none. Clipped at 8 m.")]
    [Range(0f, 8f)]
    public float craterDepth;

    [Tooltip("Metres the lip of earth round the bowl stands above the ground, peaking just outside the bowl radius; 0 for none. Clipped at 1.5 m.")]
    [Range(0f, 1.5f)]
    public float rimHeight;

    [Tooltip("How far scorch and the stain reach, as a multiple of the bowl radius, at least 1.3 so the lip fits inside. " +
             "A small deep bowl in a wide burn takes a large value.")]
    [Min(1.3f)]
    public float burnReach;

    [Tooltip("Burns out everything to the burn reach at once, so no field fire lights or spreads there afterwards: a strike, not a shell.")]
    public bool burnsOut;

    [Tooltip("How black the ground is burnt, out to the burn reach: 0 none, 1 soot black.")]
    [Range(0f, 1f)]
    public float scorch;

    [Tooltip("Broken stone and brick left on the ground, over the burn reach: 0 none, 1 fully covered.")]
    [Range(0f, 1f)]
    public float rubble;

    [Tooltip("Grey ash left on the ground, over the burn reach: 0 none, 1 fully covered.")]
    [Range(0f, 1f)]
    public float ash;

    [Tooltip("Spilt fuel and oil left on the ground, over the burn reach: 0 none, 1 a dark, glossy slick.")]
    [Range(0f, 1f)]
    public float fuel;

    [Tooltip("Wet, churned mud left on the ground, over the burn reach: 0 none, 1 fully covered.")]
    [Range(0f, 1f)]
    public float mud;

    /// <summary>Rubble, ash, fuel and wet mud, 0 to 1 each.</summary>
    public float4 Stain => new(rubble, ash, fuel, mud);

    /// <summary>Leaves rubble, ash, fuel or mud on the ground.</summary>
    public bool LeavesStain => rubble > 0f || ash > 0f || fuel > 0f || mud > 0f;
}
