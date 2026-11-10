using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Simulation.Common.Infantry;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Presentation.Animation.Figures
{
    public partial class FigureClipSet
    {
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
