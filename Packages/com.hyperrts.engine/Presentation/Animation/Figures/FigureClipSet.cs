using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Infantry;
using Unity.Collections;
using Unity.Entities;
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
    /// Which bone clip a squad figure plays for each posture, motion, action and death. Every entry is required, so a
    /// missing one fails the bake naming this set and the entry.
    /// </summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Animation + "Figure Clip Set", fileName = "FigureClipSet")]
    [HelpURL(HyperRTSDocs.Presentation)]
    public class FigureClipSet : ScriptableObject
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

        private static readonly Posture[] Postures = { Posture.Stand, Posture.Crouch, Posture.Prone };

        /// <summary>
        /// The ranges of this set's clips in <paramref name="table"/>, owned by the caller. Fails naming this set and the
        /// entry when one is unset or the table lacks its clip.
        /// </summary>
        public BlobAssetReference<FigureClips> Build(BoneClipTable table)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<FigureClips>();
                var legRanges = builder.Allocate(ref root.Legs, Postures.Length * FigureClips.Motions);
                var fireRanges = builder.Allocate(ref root.Fire, Postures.Length);
                var fallRanges = builder.Allocate(ref root.Falls, Postures.Length * FigureClips.FallKinds);
                foreach (var posture in Postures)
                {
                    var at = (int)posture;
                    legRanges[at * FigureClips.Motions] = Find(table, "idle", posture, idle);
                    legRanges[at * FigureClips.Motions + 1] = Find(table, "walk", posture, walk);
                    legRanges[at * FigureClips.Motions + 2] = Find(table, "run", posture, run);
                    fireRanges[at] = Find(table, "fire", posture, fire);
                    fireRanges[at].Loop = false;
                    fallRanges[at * FigureClips.FallKinds] = Find(table, "shotDeath", posture, shotDeath);
                    foreach (BlastSide side in Enum.GetValues(typeof(BlastSide)))
                    {
                        fallRanges[at * FigureClips.FallKinds + 1 + (int)side] = Find(table, $"blast{side}Death", posture, BlastDeath(side));
                    }
                }

                FillActions(table, builder.Allocate(ref root.Actions, Enum.GetValues(typeof(MemberAction)).Length));
                Ragdoll.Build(builder, ref root.Ragdoll, table);
                return builder.CreateBlobAssetReference<FigureClips>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        private void FillActions(BoneClipTable table, BlobBuilderArray<ClipRange> actionClips)
        {
            foreach (MemberAction action in Enum.GetValues(typeof(MemberAction)))
            {
                if (action == MemberAction.None)
                {
                    continue;
                }

                var entry = actions.FindIndex(candidate => candidate.action == action);
                if (entry < 0)
                {
                    throw new InvalidOperationException($"Figure clip set '{name}' has no action {action}.");
                }

                actionClips[(int)action] = Find(table, $"action {action}", actions[entry].clip);
            }
        }

        private ClipRange Find(BoneClipTable table, string entry, Posture posture, PostureClips clips) =>
            Find(table, $"{entry} {posture}", clips.For(posture));

        private ClipRange Find(BoneClipTable table, string entry, string clipName)
        {
            if (string.IsNullOrEmpty(clipName))
            {
                throw new InvalidOperationException($"Figure clip set '{name}' names no clip for {entry}.");
            }

            return table.TryRange(clipName, out var range)
                ? range
                : throw new KeyNotFoundException($"Figure clip set '{name}' {entry}: bone clip table '{table.name}' has no clip '{clipName}'.");
        }
    }
}
