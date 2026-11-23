using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Presentation.Effects.Streams;
using HyperRTS.Simulation.Cues.Effects;
using HyperRTS.Simulation.Destruction;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules.Effects
{
    /// <summary>
    /// Each effect field of an authoring component holds a valid rig or a surface table, and has the socket it plays from.
    /// One rule reads a table of authoring types, since the validator makes one instance per rule type.
    /// </summary>
    public sealed class EffectReferencesRule : IAuthoringRule
    {
        private readonly (Type Authoring, Func<Component, IEnumerable<EffectReference>> References)[] _table =
        {
            For<SpawnEffectAuthoring>(spawn => new[] { EffectReference.ForRig("Spawn effect", spawn.effect, true) }),
            For<EffectTrailAuthoring>(trail => new[] { EffectReference.ForRig("Trail rig", trail.rig, true) }),
            For<ImpactAuthoring>(ImpactReferences),
            For<MotionDustAuthoring>(dust => new[]
                { EffectReference.ForSurface("Motion dust", dust.effects, true).WithSocket("socket", dust.socket) }),
            For<DamageStagesAuthoring>(DamageStageReferences),
            For<LoopEffectsAuthoring>(LoopReferences),
            For<EntityEffectsAuthoring>(EntityReferences),
        };

        public bool AppliesTo(Component component) => _table.Any(entry => entry.Authoring.IsInstanceOfType(component));

        public void Check(Component component, ValidationIssues issues)
        {
            var entry = _table.First(row => row.Authoring.IsInstanceOfType(component));
            foreach (var reference in entry.References(component))
            {
                CheckReference(component, reference, issues);
            }
        }

        private static (Type, Func<Component, IEnumerable<EffectReference>>) For<T>(Func<T, IEnumerable<EffectReference>> references)
            where T : Component => (typeof(T), component => references((T)component));

        private static void CheckReference(Component owner, EffectReference reference, ValidationIssues issues)
        {
            if (!reference.IsSurface)
            {
                EffectRigCheck.Check(owner, reference.Rig, reference.Label, reference.Required, issues);
            }
            else if (reference.Surface == null && reference.Required)
            {
                issues.Error(owner, $"{reference.Label} has no surface effects asset.");
            }

            if (reference.SocketName == null || reference.Socket != null || !reference.InUse)
            {
                return;
            }

            var label = reference.Rig != null ? $"{reference.Label} '{reference.Rig.name}'" : reference.Label;
            issues.Error(owner, $"{label} has no {reference.SocketName}.");
        }

        // An impact has marks, effects, or both.
        private static IEnumerable<EffectReference> ImpactReferences(ImpactAuthoring impacts) => new[]
        {
            EffectReference.ForSurface("Impact effects", impacts.effects, !impacts.marks.Any(mark => mark != null)),
        };

        private static IEnumerable<EffectReference> DamageStageReferences(DamageStagesAuthoring damage) =>
            damage.stages.Select((stage, i) => EffectReference.ForRig($"Damage stage {i} effect", stage.effect, false));

        private static IEnumerable<EffectReference> LoopReferences(LoopEffectsAuthoring effects) =>
            effects.loops.Select((loop, i) => EffectReference.ForRig($"Loop {i}", loop.rig, true).WithSocket("socket", loop.socket));

        private static IEnumerable<EffectReference> EntityReferences(EntityEffectsAuthoring effects)
        {
            yield return EffectReference.ForRig("Fire", effects.fire, false).WithSocket("muzzle socket", effects.muzzle);
            yield return EffectReference.ForRig("Death", effects.death, false);
            yield return EffectReference.ForRig("Ability", effects.ability, false);
            for (var i = 0; i < effects.mountFire.Count; i++)
            {
                var mount = effects.mountFire[i];
                yield return EffectReference.ForRig($"Mount fire {i + 1}", mount.rig, false).WithSocket("muzzle socket", mount.muzzle);
            }
        }
    }
}
