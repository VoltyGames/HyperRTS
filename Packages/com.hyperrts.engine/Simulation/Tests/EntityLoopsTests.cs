using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Entity loops built the way their baker builds them.</summary>
    public class EntityLoopsTests
    {
        [Test]
        public void OnlySetCues_GetALoop_WithTheTuning()
        {
            using var world = new World("Loops");
            var entity = world.EntityManager.CreateEntity();
            var cue = ScriptableObject.CreateInstance<SoundCue>();
            var writer = new EntityManagerWriter(world.EntityManager, entity);

            var loops = EntityLoopsAuthoring.Add(ref writer, 8f, 0.15f, 4f);
            EntityLoopsAuthoring.Set(loops, LoopSlot.EngineIdle, cue);
            EntityLoopsAuthoring.Set(loops, LoopSlot.Rotor, null);
            EntityLoopsAuthoring.Set(loops, LoopSlot.Ambient, cue);

            loops = world.EntityManager.GetBuffer<EntityLoopSound>(entity);
            Assert.AreEqual(2, loops.Length);
            Assert.AreEqual(LoopSlot.EngineIdle, loops[0].Slot);
            Assert.AreEqual(LoopSlot.Ambient, loops[1].Slot);
            Assert.AreEqual(4f, world.EntityManager.GetComponentData<LoopSoundTuning>(entity).SpinUpTime);
            Object.DestroyImmediate(cue);
        }
    }
}
