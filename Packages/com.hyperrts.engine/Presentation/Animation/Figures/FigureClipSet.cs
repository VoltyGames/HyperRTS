using HyperRTS.Core;
using HyperRTS.Simulation.Common.Infantry;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>Names of one bone clip for each posture.</summary>
    [Serializable]
    public struct PostureClips
    {
        public string stand;
        public string crouch;
        public string prone;

        public readonly string For(Posture posture) => posture switch
        {
            Posture.Crouch => crouch,
            Posture.Prone => prone,
            _ => stand,
        };
    }

    /// <summary>The whole-body clip a member plays while it does an action.</summary>
    [Serializable]
    public struct ActionClip
    {
        public MemberAction action;

        [Tooltip("Name of the bone clip.")]
        public string clip;
    }

    /// <summary>
    /// Which bone clip a squad figure plays for each posture, motion, action and death. Every entry is required: a
    /// missing one or a name the figure's bone clip table lacks fails the bake naming this set and the entry, so a
    /// crouched figure never quietly plays the standing clip. A rifleman and a launcher carrier use different sets
    /// over the same table.
    /// </summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Animation + "Figure Clip Set", fileName = "FigureClipSet")]
    [HelpURL(HyperRTSDocs.Presentation)]
    public partial class FigureClipSet : ScriptableObject
    {
        [Header("Legs")]
        [Tooltip("Clips the legs play standing still, by posture.")]
        public PostureClips idle;

        [Tooltip("Clips the legs play moving slower than the squad's run speed, by posture.")]
        public PostureClips walk;

        [Tooltip("Clips the legs play at or above the run speed, by posture.")]
        public PostureClips run;

        [Header("Upper body")]
        [Tooltip("Clips the upper body plays over the legs after a shot, by posture. Each plays once per shot and sets how long the figure keeps firing.")]
        public PostureClips fire;

        [Header("Whole body")]
        [Tooltip("The clip for each member action except None.")]
        public List<ActionClip> actions = new();

        [Header("Deaths")]
        [Tooltip("Clips played once when a shot drops the figure, by posture.")]
        public PostureClips shotDeath;

        [Tooltip("Clips played once when a blast ahead of the figure drops it, by posture.")]
        public PostureClips blastFrontDeath;

        [Tooltip("Clips played once when a blast behind the figure drops it, by posture.")]
        public PostureClips blastBackDeath;

        [Tooltip("Clips played once when a blast on the figure's left drops it, by posture.")]
        public PostureClips blastLeftDeath;

        [Tooltip("Clips played once when a blast on the figure's right drops it, by posture.")]
        public PostureClips blastRightDeath;

        /// <summary>The death clips of one blast side.</summary>
        public PostureClips BlastDeath(BlastSide side) => side switch
        {
            BlastSide.Front => blastFrontDeath,
            BlastSide.Back => blastBackDeath,
            BlastSide.Left => blastLeftDeath,
            _ => blastRightDeath,
        };
    }
}
