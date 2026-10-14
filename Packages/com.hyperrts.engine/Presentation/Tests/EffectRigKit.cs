using HyperRTS.Presentation.Effects;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>Tiny effect rigs built in code: still, shapeless, long-lived particle systems.</summary>
    public static class EffectRigKit
    {
        public static EffectRig Rig(string name,
            ParticleSystemSimulationSpace space = ParticleSystemSimulationSpace.World)
        {
            var root = new GameObject(name);
            var rig = root.AddComponent<EffectRig>();
            AddSystem(rig, "Particles", space);
            return rig;
        }

        public static ParticleSystem AddSystem(EffectRig rig, string name,
            ParticleSystemSimulationSpace space = ParticleSystemSimulationSpace.World)
        {
            var child = new GameObject(name);
            child.transform.SetParent(rig.transform);
            var system = child.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.simulationSpace = space;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.playOnAwake = false;
            main.startLifetime = 100f;
            main.startSpeed = 0f;
            main.maxParticles = 10000;
            var shape = system.shape;
            shape.enabled = false;
            var emission = system.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            return system;
        }

        public static ParticleSystem System(EffectRig rig) => rig.GetComponentInChildren<ParticleSystem>();

        public static void AddBurst(EffectRig rig, float time, short count)
        {
            var emission = System(rig).emission;
            var bursts = new ParticleSystem.Burst[emission.burstCount + 1];
            emission.GetBursts(bursts);
            bursts[^1] = new ParticleSystem.Burst(time, count);
            emission.SetBursts(bursts);
        }

        public static void SetRates(EffectRig rig, float perSecond, float perMetre) =>
            SetRates(System(rig), perSecond, perMetre);

        public static void SetRates(ParticleSystem system, float perSecond, float perMetre)
        {
            var emission = system.emission;
            emission.rateOverTime = perSecond;
            emission.rateOverDistance = perMetre;
        }
    }
}
