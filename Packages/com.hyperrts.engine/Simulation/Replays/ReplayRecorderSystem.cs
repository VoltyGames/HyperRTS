using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Transport;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Replays
{
    /// <summary>
    /// While a <see cref="ReplayRecording"/> exists, samples every gameplay entity at its rate after the frame's
    /// gameplay ran: a keyframe every few seconds, deltas of what changed in between. Starts one itself when
    /// <see cref="MatchRules.RecordReplay"/> is set.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderLast = true)]
    public partial struct ReplayRecorderSystem : ISystem
    {
        private bool _autoStarted;

        public void OnDestroy(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton(out ReplayRecording recording))
            {
                recording.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<ReplayRecording>(out var entity))
            {
                AutoStart(ref state);
                return;
            }

            var recording = SystemAPI.GetComponent<ReplayRecording>(entity);
            if (!TryBeginFrame(ref recording, SystemAPI.Time.ElapsedTime, out var frame))
            {
                return;
            }

            state.CompleteDependency();
            Sample(ref state, ref recording, frame.Keyframe);
            CollectRemoved(ref recording, frame.Keyframe);
            frame.EntityCount = recording.Stream.Entities.Length - frame.EntityStart;
            frame.RemovedCount = recording.Stream.Removed.Length - frame.RemovedStart;
            recording.Stream.Frames.Add(frame);
            SystemAPI.SetComponent(entity, recording);
        }

        /// <summary>Whether a sample is due; the first is taken at once and counts as time zero.</summary>
        private static bool TryBeginFrame(ref ReplayRecording recording, double elapsed, out ReplayFrame frame)
        {
            frame = default;
            if (recording.StartTime < 0d)
            {
                recording.StartTime = elapsed;
            }

            var time = (float)(elapsed - recording.StartTime);
            if (time < recording.NextSample)
            {
                return false;
            }

            // Fixed steps keep the rate exact; after a long frame, restart the steps rather than burst-sampling.
            recording.NextSample += recording.SampleInterval;
            if (recording.NextSample <= time)
            {
                recording.NextSample = time + recording.SampleInterval;
            }

            var first = recording.Stream.Frames.Length == 0;
            var keyframe = first || time - recording.LastKeyframe >= recording.KeyframeInterval;
            if (keyframe)
            {
                recording.LastKeyframe = time;
            }

            frame = new ReplayFrame
            {
                Time = time,
                Keyframe = keyframe,
                EntityStart = recording.Stream.Entities.Length,
                RemovedStart = recording.Stream.Removed.Length,
            };
            return true;
        }

        private void AutoStart(ref SystemState state)
        {
            if (_autoStarted || !SystemAPI.TryGetSingleton(out MatchRules rules))
            {
                return;
            }

            if (rules.RecordReplay)
            {
                _autoStarted = true;
                state.EntityManager.CreateSingleton(ReplayRecording.Create(ReplayRecorder.DefaultSampleRate,
                    ReplayRecorder.DefaultKeyframeInterval));
            }
        }

        private void Sample(ref SystemState state, ref ReplayRecording recording, bool keyframe)
        {
            var health = SystemAPI.GetComponentLookup<Health>(true);
            var construction = SystemAPI.GetComponentLookup<ConstructionProgress>(true);
            foreach (var (info, faction, transform, entity) in SystemAPI
                         .Query<RefRO<EntityInfo>, RefRO<Faction>, RefRO<LocalTransform>>()
                         .WithNone<Inside>().WithEntityAccess())
            {
                var known = recording.Last.TryGetValue(entity, out var last);
                var key = known ? last.Key : recording.NextKey++;
                var healthFraction = health.TryGetComponent(entity, out var hp) ? hp.Fraction : 1f;
                var building = construction.TryGetComponent(entity, out var progress) &&
                               construction.IsComponentEnabled(entity);
                var current = ReplayEntity.Capture(key, info.ValueRO.TypeId, faction.ValueRO.Value, transform.ValueRO,
                    healthFraction, progress.Value, building);

                recording.Seen.Add(entity);
                recording.Last[entity] = current;
                var changed = !known || !last.SameState(current);
                if (keyframe || changed)
                {
                    recording.Stream.Entities.Add(current);
                }
            }
        }

        private static void CollectRemoved(ref ReplayRecording recording, bool keyframe)
        {
            var stale = new NativeList<Entity>(Allocator.Temp);
            foreach (var pair in recording.Last)
            {
                if (recording.Seen.Contains(pair.Key))
                {
                    continue;
                }

                stale.Add(pair.Key);
                if (!keyframe)
                {
                    recording.Stream.Removed.Add(pair.Value.Key);
                }
            }

            foreach (var entity in stale)
            {
                recording.Last.Remove(entity);
            }

            recording.Seen.Clear();
        }
    }
}
