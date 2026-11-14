using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Presentation.Animation.Figures;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Animation.Figures
{
    /// <summary>A squad fixture whose figures have two-bone ragdolls, a camera and the corpse systems.</summary>
    public abstract class SquadFigureRagdollFixture : SquadFigureFixture
    {
        private const int RigRows = 180;

        /// <summary>
        /// Gives every figure a two-bone ragdoll (lower hips, upper spine) on a readable bone texture of
        /// <see cref="RigSkin"/>, puts a main camera near the squad and adds the corpse and pose slot systems.
        /// </summary>
        protected void UseRagdolls()
        {
            var table = Table(0);
            table.bones.AddRange(new[] { "Hips", "Spine" });
            table.frames = RigRows;
            table.parents.AddRange(new[] { -1, 0 });
            table.bindPoses.AddRange(new[] { Matrix4x4.identity, Matrix4x4.Translate(new Vector3(0f, -0.5f, 0f)) });
            table.upperBones.AddRange(new[] { false, true });
            table.ragdoll.Add(Body(0, -1));
            table.ragdoll.Add(Body(1, 0));
            table.boneBodies.AddRange(new[] { 0, 1 });
            var set = Set();
            _made.Add(table);
            _made.Add(set);
            var clips = set.Build(table);
            _clips[-1] = clips;
            var figures = _em.GetBuffer<SquadFigure>(_squad);
            for (var i = 0; i < figures.Length; i++)
            {
                figures.ElementAt(i).Clips = clips;
            }

            DrawPartsWith(RigMaterial());
            var camera = new GameObject("Corpse Camera") { tag = "MainCamera" };
            camera.AddComponent<Camera>().transform.position = new Vector3(0f, 10f, -10f);
            _made.Add(camera);
            _later.Add(_kit.World.CreateSystem<CorpseSystem>());
            _later.Add(_kit.World.CreateSystem<CorpsePoseSystem>());
            _later.Add(_kit.World.CreateSystem<PoseSlotSystem>());
        }

        /// <summary>Gives every figure <paramref name="count"/> more parts, as a soldier's kit and weapon; call before <see cref="UseRagdolls"/>.</summary>
        protected void AddParts(int count)
        {
            for (var index = 0; index < _figures.Length; index++)
            {
                var figure = _figures[index];
                for (var i = 0; i < count; i++)
                {
                    var part = CreatePart();
                    Drive(part, _squad, figure, index);
                    _em.SetComponentData(part, new Parent { Value = figure });
                    _em.GetBuffer<FigureRenderer>(_squad).Add(new FigureRenderer { Model = figure, Part = part });
                    _em.GetBuffer<LinkedEntityGroup>(_squad).Add(part);
                }
            }
        }

        /// <summary>The skinning matrix the rig texture holds for a bone and row; the spine's is relative to the hips.</summary>
        private static float4x4 RigSkin(int bone, int row) => bone == 0
            ? float4x4.Translate(new float3(0f, 1f + row / 16f, row / 32f))
            : float4x4.TRS(new float3(0f, 0.5f, 0f), quaternion.RotateY(row * 0.03f), 1f);

        protected NativeArray<Entity> Corpses()
        {
            using var query = _em.CreateEntityQuery(typeof(Corpse));
            return query.ToEntityArray(Allocator.Temp);
        }

        private static RagdollBody Body(int bone, int parent) => new()
        {
            bone = bone,
            parent = parent,
            capsuleStart = Vector3.zero,
            capsuleEnd = new Vector3(0f, 0.4f, 0f),
            radius = 0.1f,
            massShare = 0.5f,
            joint = new RagdollJoint { perpendicularAxis = Vector3.right, coneAngle = 30f, perpendicularMin = -20f, perpendicularMax = 20f, twistMin = -10f, twistMax = 10f },
        };

        private Material RigMaterial()
        {
            var texture = new Texture2D(6, RigRows, TextureFormat.RGBAHalf, false, true);
            var texels = new half4[6 * RigRows];
            for (var row = 0; row < RigRows; row++)
            {
                for (var bone = 0; bone < 2; bone++)
                {
                    var skin = math.transpose(RigSkin(bone, row));
                    for (var r = 0; r < 3; r++)
                    {
                        texels[row * 6 + bone * 3 + r] = (half4)skin[r];
                    }
                }
            }

            texture.SetPixelData(texels, 0);
            texture.Apply(false, false);
            var material = new Material(Shader.Find("HyperRTS/LitTeam"));
            material.SetTexture("_BoneTex", texture);
            material.SetFloat("_BoneFadeTime", 0.01f);
            _made.Add(texture);
            _made.Add(material);
            return material;
        }

        private void DrawPartsWith(Material material)
        {
            var mesh = new Mesh();
            _made.Add(mesh);
            var array = new RenderMeshArray(new[] { material }, new[] { mesh });
            foreach (var renderer in _em.GetBuffer<FigureRenderer>(_squad).ToNativeArray(Allocator.Temp))
            {
                _em.AddSharedComponent(renderer.Part, array);
                _em.SetComponentData(renderer.Part, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
            }
        }
    }
}
