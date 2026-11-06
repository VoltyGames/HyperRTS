using HyperRTS.Core;
using HyperRTS.Simulation.Cues.Voice;
using HyperRTS.Simulation.Squads;
using UnityEngine;

namespace HyperRTS.Simulation.Match
{
    /// <summary>The numbers that shape infantry in a match: cover, squads, voice pacing and craters. The Match references one.</summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Match + "Infantry Tuning", fileName = "InfantryTuning")]
    [HelpURL(HyperRTSDocs.Match)]
    public class InfantryTuning : ScriptableObject
    {
        [Tooltip("What cover takes off a hit and how far a halted squad looks for it.")]
        public CoverSettings cover;

        [Tooltip("How suppression, posture, retreat and reinforcing change a squad.")]
        public SquadSettings squads;

        [Tooltip("How often units acknowledge orders, call out under fire and bark.")]
        public VoiceSettings voice;

        [Tooltip("Which blasts dig a crater that gives soldiers light cover.")]
        public CraterSettings craters;
    }
}
