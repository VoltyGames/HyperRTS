using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using Unity.Entities;

namespace HyperRTS.Simulation.Tests;

/// <summary>Cue queue readers on top of <see cref="TestWorld"/>.</summary>
public static class CueTestKit
{
    extension(TestWorld world)
    {
        /// <summary>This frame's cues.</summary>
        public CueEvent[] Queued()
        {
            using var query = world.Query(typeof(CueQueue));
            return world.EntityManager.GetBuffer<CueEvent>(query.GetSingletonEntity()).AsNativeArray().ToArray();
        }

        /// <summary>This frame's cues of one slot.</summary>
        public CueEvent[] Queued(CueSlot slot) =>
            Array.FindAll(world.Queued(), cue => cue.Slot == slot);

        /// <summary>Every cue of the next frames; the queue itself only holds the last frame's.</summary>
        public List<CueEvent> QueuedOver(float seconds)
        {
            var queued = new List<CueEvent>();
            for (var t = 0f; t < seconds; t += TestWorld.FrameTime)
            {
                world.Tick();
                queued.AddRange(world.Queued());
            }

            return queued;
        }

        /// <summary>Every cue of one slot over the next frames.</summary>
        public List<CueEvent> QueuedOver(float seconds, CueSlot slot) =>
            world.QueuedOver(seconds).FindAll(cue => cue.Slot == slot);

        public int GetTypeId(Entity entity) => world.Get<EntityInfo>(entity).TypeId;
    }
}
