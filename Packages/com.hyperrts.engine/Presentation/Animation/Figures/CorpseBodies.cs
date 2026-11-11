using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Presentation.Physics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Collider = Unity.Physics.Collider;
using FloatRange = Unity.Physics.Math.FloatRange;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>Hands a corpse's fall over to physics: capsule bodies and joints made from its ragdoll, which freeze once it rests.</summary>
    internal sealed class CorpseBodies
    {
        private readonly EntityManager _entities;
        private readonly Dictionary<BlobAssetReference<FigureClips>, BlobAssetReference<Collider>[]> _colliders = new();

        private static readonly int BoneTextureId = Shader.PropertyToID("_BoneTex");
        private static readonly int FadeTimeId = Shader.PropertyToID("_BoneFadeTime");

        public CorpseBodies(EntityManager entities)
        {
            _entities = entities;
        }

        /// <summary>
        /// Hands a corpse playing its fall clip over to physics: <paramref name="slot"/> takes the pose its copies draw
        /// now, and the bodies start there, moving as the clip did over its last row.
        /// </summary>
        public void StartRagdoll(Entity root, int slot, PoseSlots slots, float now)
        {
            var corpse = _entities.GetComponentData<Corpse>(root);
            ref var ragdoll = ref corpse.Clips.Value.Ragdoll;
            var copy = _entities.GetBuffer<LinkedEntityGroup>(root)[1].Value;
            var meshInfo = _entities.GetComponentData<MaterialMeshInfo>(copy);
            var material = _entities.GetSharedComponent<RenderMeshArray>(copy).GetMaterial(meshInfo);
            if (material == null || material.GetTexture(BoneTextureId) is not Texture2D texture)
            {
                throw new InvalidOperationException(
                    $"Corpse copy '{_entities.GetName(copy)}' has no material with a bone texture to take its pose from.");
            }

            var step = _entities.GetComponentData<LowerClip>(copy).Clip.FrameStep;
            var bones = new BoneTexture(texture);
            var fadeTime = material.GetFloat(FadeTimeId);
            using var skins = Skins(copy, bones, fadeTime, ref ragdoll, now);
            using var before = Skins(copy, bones, fadeTime, ref ragdoll, now - step);
                        var handover = _entities.GetBuffer<CorpseBone>(root);
            for (var bone = 0; bone < skins.Length; bone++)
            {
                slots.Write(slot, bone, skins[bone]);
                handover.Add(new CorpseBone { Handover = skins[bone] });
            }

            using var group = _entities.GetBuffer<LinkedEntityGroup>(root).ToNativeArray(Allocator.Temp);
            for (var i = 1; i < group.Length; i++)
            {
                _entities.SetComponentData(group[i].Value, new PoseSlot { Value = slot });
            }

            corpse.Slot = slot;
            corpse.SettleAt = now + ragdoll.Tuning.settleTime;
            corpse.Slow = false;
            _entities.SetComponentData(root, corpse);
            SpawnBodies(root, corpse, skins, before, step);
        }

        private NativeArray<float4x4> Skins(Entity copy, in BoneTexture texture, float fadeTime, ref Ragdoll ragdoll, float time)
        {
            var lowerClip = _entities.GetComponentData<LowerClip>(copy).Clip;
            var upperClip = _entities.GetComponentData<UpperClip>(copy).Clip;
            var lower = BoneSide.At(lowerClip, _entities.GetComponentData<LowerFade>(copy).Fade, time, fadeTime);
            var upper = BoneSide.At(upperClip, _entities.GetComponentData<UpperFade>(copy).Fade, time, fadeTime);
            var skins = new NativeArray<float4x4>(ragdoll.BoneCount, Allocator.Temp);
            ragdoll.Skins(texture, lower, upper, skins);
            return skins;
        }

        private void SpawnBodies(Entity root, in Corpse corpse, NativeArray<float4x4> skins, NativeArray<float4x4> before, float step)
        {
            ref var ragdoll = ref corpse.Clips.Value.Ragdoll;
            var colliders = Colliders(corpse.Clips);
            var made = new NativeArray<CorpseBody>(ragdoll.Bodies.Length, Allocator.Temp);
            for (var b = 0; b < made.Length; b++)
            {
                var pose = ragdoll.BodyWorld(b, skins, corpse.World);
                var was = ragdoll.BodyWorld(b, before, corpse.World);
                var mass = PhysicsMass.CreateDynamic(colliders[b].Value.MassProperties, ragdoll.Bodies[b].massShare * ragdoll.Tuning.mass);
                var velocity = PhysicsVelocity.CalculateVelocityToTarget(mass, was.pos, was.rot, pose, 1f / step);
                velocity.Linear += corpse.Throw;
                var body = _entities.CreateEntity(typeof(LocalTransform), typeof(PhysicsCollider), typeof(PhysicsVelocity),
                    typeof(PhysicsMass), typeof(PhysicsDamping));
                _entities.SetComponentData(body, LocalTransform.FromPositionRotation(pose.pos, pose.rot));
                _entities.SetComponentData(body, new PhysicsCollider { Value = colliders[b] });
                _entities.SetComponentData(body, velocity);
                _entities.SetComponentData(body, mass);
                _entities.SetComponentData(body, new PhysicsDamping { Linear = 0f, Angular = ragdoll.Tuning.angularDamping });
                _entities.AddSharedComponent(body, new PhysicsWorldIndex(PhysicsGroup.WorldIndex));
                made[b] = new CorpseBody { Body = body, Start = math.mul(math.inverse(new float4x4(pose)), corpse.World) };
            }

            var joints = Join(ref ragdoll, made);
            _entities.GetBuffer<CorpseBody>(root).AddRange(made);
            _entities.GetBuffer<CorpseJoint>(root).AddRange(joints.AsArray());
        }

        // Each joint is two: a cone with twist about the capsule, and the perpendicular range trimming the cone.
        private NativeList<CorpseJoint> Join(ref Ragdoll ragdoll, NativeArray<CorpseBody> bodies)
        {
            var joints = new NativeList<CorpseJoint>(Allocator.Temp);
            for (var b = 0; b < bodies.Length; b++)
            {
                ref var entry = ref ragdoll.Bodies[b];
                if (entry.parent < 0)
                {
                    continue;
                }

                ragdoll.JointFrames(b, out var child, out var parent);
                var limits = entry.joint;
                PhysicsJoint.CreateRagdoll(child, parent, math.radians(limits.coneAngle),
                    new FloatRange(math.radians(limits.perpendicularMin), math.radians(limits.perpendicularMax)),
                    new FloatRange(math.radians(limits.twistMin), math.radians(limits.twistMax)), out var cone, out var plane);
                var pair = new PhysicsConstrainedBodyPair(bodies[b].Body, bodies[entry.parent].Body, false);
                joints.Add(new CorpseJoint { Joint = CreateJoint(pair, cone) });
                joints.Add(new CorpseJoint { Joint = CreateJoint(pair, plane) });
            }

            return joints;
        }

        private Entity CreateJoint(in PhysicsConstrainedBodyPair pair, in PhysicsJoint joint)
        {
            var entity = _entities.CreateEntity(typeof(PhysicsConstrainedBodyPair), typeof(PhysicsJoint));
            _entities.SetComponentData(entity, pair);
            _entities.SetComponentData(entity, joint);
            _entities.AddSharedComponent(entity, new PhysicsWorldIndex(PhysicsGroup.WorldIndex));
            return entity;
        }

        // Every corpse of the same clips shares its capsules.
        private BlobAssetReference<Collider>[] Colliders(BlobAssetReference<FigureClips> clips)
        {
            if (_colliders.TryGetValue(clips, out var made))
            {
                return made;
            }

            ref var ragdoll = ref clips.Value.Ragdoll;
            made = new BlobAssetReference<Collider>[ragdoll.Bodies.Length];
            for (var b = 0; b < made.Length; b++)
            {
                made[b] = Unity.Physics.CapsuleCollider.Create(ragdoll.Capsule(b), PhysicsGroup.BodyFilter);
            }

            _colliders.Add(clips, made);
            return made;
        }

        public void DestroyBodies(Entity root)
        {
            var made = new NativeList<Entity>(Allocator.Temp);
            foreach (var body in _entities.GetBuffer<CorpseBody>(root))
            {
                made.Add(body.Body);
            }

            foreach (var joint in _entities.GetBuffer<CorpseJoint>(root))
            {
                made.Add(joint.Joint);
            }

            _entities.GetBuffer<CorpseBody>(root).Clear();
            _entities.GetBuffer<CorpseJoint>(root).Clear();
            _entities.DestroyEntity(made.AsArray());
        }

        public void Dispose()
        {
            foreach (var colliders in _colliders.Values)
            {
                foreach (var collider in colliders)
                {
                    collider.Dispose();
                }
            }

            _colliders.Clear();
        }
    }
}
