using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using Unity.Entities;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Cue queue readers on top of <see cref="TestWorld"/>.</summary>
    public static class CueTestKit
    {
        /// <summary>This frame's cues.</summary>
        public static CueEvent[] Queued(this TestWorld world)
        {
            using var query = world.EntityManager.CreateEntityQuery(typeof(CueQueue));
            return world.EntityManager.GetBuffer<CueEvent>(query.GetSingletonEntity()).AsNativeArray().ToArray();
        }

        /// <summary>This frame's cues of one slot.</summary>
        public static CueEvent[] Queued(this TestWorld world, CueSlot slot) =>
            Array.FindAll(world.Queued(), cue => cue.Slot == slot);

        /// <summary>Every cue of the next frames; the queue itself only holds the last frame's.</summary>
        public static List<CueEvent> QueuedOver(this TestWorld world, float seconds)
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
        public static List<CueEvent> QueuedOver(this TestWorld world, float seconds, CueSlot slot) =>
            world.QueuedOver(seconds).FindAll(cue => cue.Slot == slot);

        public static int GetTypeId(this TestWorld world, Entity entity) => world.Get<EntityInfo>(entity).TypeId;
    }
}
