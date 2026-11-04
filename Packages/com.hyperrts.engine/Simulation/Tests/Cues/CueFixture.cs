using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Cues.Effects;
using HyperRTS.Simulation.Cues.Sounds;
using HyperRTS.Simulation.Cues.Voice;
using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
namespace HyperRTS.Simulation.Tests.Cues
{
    /// <summary>A world with a sound cue and an effect rig to hang on entities, shared by the cue tests.</summary>
    public abstract class CueFixture : SimulationFixture
    {
        protected SoundCue _cue;
        protected GameObject _rig;

        [SetUp]
        public void Prepare()
        {
            _cue = World.Own(ScriptableObject.CreateInstance<SoundCue>());
            _rig = World.Own(new GameObject("Rig"));
        }

        protected Entity Voiced(Entity entity, params CueSlot[] slots)
        {
            var writer = World.Writer(entity);
            var sounds = EntitySoundsAuthoring.Add(writer);
            foreach (var slot in slots)
            {
                EntitySoundsAuthoring.Set(sounds, slot, _cue);
            }

            return entity;
        }

        protected Entity Rigged(Entity entity, CueSlot slot, float3 socket)
        {
            var writer = World.Writer(entity);
            EntityEffectsAuthoring.Set(EntityEffectsAuthoring.Add(writer), slot, _rig, socket);
            return entity;
        }

        protected static CueEvent Find(List<CueEvent> heard, CueSlot slot) => heard.Find(cue => cue.Slot == slot);

        protected T Single<T>() where T : unmanaged, IComponentData
        {
            using var query = World.Query(typeof(T));
            return query.GetSingleton<T>();
        }
    }
}
