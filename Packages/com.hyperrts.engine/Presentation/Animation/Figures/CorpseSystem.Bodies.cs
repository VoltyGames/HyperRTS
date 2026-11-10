using System;
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
    public partial class CorpseSystem
    {
        private static readonly int BoneTextureId = Shader.PropertyToID("_BoneTex");
        private static readonly int FadeTimeId = Shader.PropertyToID("_BoneFadeTime");

        /// <summary>
        /// Hands a corpse playing its fall clip over to physics: <paramref name="slot"/> takes the pose its copies draw
        /// now, and the bodies start there, moving as the clip did over its last row.
        /// </summary>
        private void StartRagdoll(Entity root, int slot)
        {
            var corpse = EntityManager.GetComponentData<Corpse>(root);
            ref var ragdoll = ref corpse.Clips.Value.Ragdoll;
            var copy = EntityManager.GetBuffer<LinkedEntityGroup>(root)[1].Value;
            var meshInfo = EntityManager.GetComponentData<MaterialMeshInfo>(copy);
            var material = EntityManager.GetSharedComponent<RenderMeshArray>(copy).GetMaterial(meshInfo);
            if (material == null || material.GetTexture(BoneTextureId) is not Texture2D texture)
            {
                throw new InvalidOperationException(
                    $"Corpse copy '{EntityManager.GetName(copy)}' has no material with a bone texture to take its pose from.");
            }

            var step = EntityManager.GetComponentData<LowerClip>(copy).Clip.FrameStep;
            var bones = new BoneTexture(texture);
            var fadeTime = material.GetFloat(FadeTimeId);
            using var skins = Skins(copy, bones, fadeTime, ref ragdoll, _now);
            using var before = Skins(copy, bones, fadeTime, ref ragdoll, _now - step);
            var slots = SystemAPI.GetSingleton<PoseSlots>();
            var handover = EntityManager.GetBuffer<CorpseBone>(root);
            for (var bone = 0; bone < skins.Length; bone++)
            {
                slots.Write(slot, bone, skins[bone]);
                handover.Add(new CorpseBone { Handover = skins[bone] });
            }

            using var group = EntityManager.GetBuffer<LinkedEntityGroup>(root).ToNativeArray(Allocator.Temp);
            for (var i = 1; i < group.Length; i++)
            {
                EntityManager.SetComponentData(group[i].Value, new PoseSlot { Value = slot });
            }

            corpse.Slot = slot;
            corpse.SettleAt = _now + ragdoll.Tuning.settleTime;
            corpse.Slow = false;
            EntityManager.SetComponentData(root, corpse);
            SpawnBodies(root, corpse, skins, before, step);
        }

        private NativeArray<float4x4> Skins(Entity copy, in BoneTexture texture, float fadeTime, ref Ragdoll ragdoll, float time)
        {
            var lowerClip = EntityManager.GetComponentData<LowerClip>(copy).Clip;
            var upperClip = EntityManager.GetComponentData<UpperClip>(copy).Clip;
            var lower = BoneSide.At(lowerClip, EntityManager.GetComponentData<LowerFade>(copy).Fade, time, fadeTime);
            var upper = BoneSide.At(upperClip, EntityManager.GetComponentData<UpperFade>(copy).Fade, time, fadeTime);
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
                var body = EntityManager.CreateEntity(typeof(LocalTransform), typeof(PhysicsCollider), typeof(PhysicsVelocity),
                    typeof(PhysicsMass), typeof(PhysicsDamping));
                EntityManager.SetComponentData(body, LocalTransform.FromPositionRotation(pose.pos, pose.rot));
                EntityManager.SetComponentData(body, new PhysicsCollider { Value = colliders[b] });
                EntityManager.SetComponentData(body, velocity);
                EntityManager.SetComponentData(body, mass);
                EntityManager.SetComponentData(body, new PhysicsDamping { Linear = 0f, Angular = ragdoll.Tuning.angularDamping });
                EntityManager.AddSharedComponent(body, new PhysicsWorldIndex(PhysicsGroup.WorldIndex));
                made[b] = new CorpseBody { Body = body, Start = math.mul(math.inverse(new float4x4(pose)), corpse.World) };
            }

            var joints = Join(ref ragdoll, made);
            EntityManager.GetBuffer<CorpseBody>(root).AddRange(made);
            EntityManager.GetBuffer<CorpseJoint>(root).AddRange(joints.AsArray());
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
            var entity = EntityManager.CreateEntity(typeof(PhysicsConstrainedBodyPair), typeof(PhysicsJoint));
            EntityManager.SetComponentData(entity, pair);
            EntityManager.SetComponentData(entity, joint);
            EntityManager.AddSharedComponent(entity, new PhysicsWorldIndex(PhysicsGroup.WorldIndex));
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
    }
}
