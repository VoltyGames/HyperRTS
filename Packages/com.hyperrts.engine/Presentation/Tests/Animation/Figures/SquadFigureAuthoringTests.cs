using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Presentation.Animation.Figures;
using HyperRTS.Presentation.Animation.VAT;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Animation.Figures
{
    /// <summary>Resolving each figure's clips from its own table when the squad is baked.</summary>
    public class SquadFigureAuthoringTests
    {
        [Test]
        public void Authoring_ResolvesClipsFromEachFiguresOwnTable()
        {
            var root = new GameObject("Squad");
            try
            {
                var squad = root.AddComponent<SquadFiguresAuthoring>();
                squad.figures.Add(Figure(root, 0));
                squad.figures.Add(Figure(root, 100));
                squad.digClip = "";

                var clips = squad.Clips();

                Assert.AreEqual(SquadFigureFixture.Range(20, true), clips[0].Run);
                Assert.AreEqual(SquadFigureFixture.Range(120, true), clips[1].Run);
                Assert.AreEqual(SquadFigureFixture.Range(140, false), clips[1].Die);

                squad.figures[1].root.GetComponentInChildren<BoneAnimatedAuthoring>().clips.clips.RemoveAt(4);
                Assert.Throws<KeyNotFoundException>(() => squad.Clips());
            }
            finally
            {
                foreach (var figure in root.GetComponentsInChildren<BoneAnimatedAuthoring>())
                {
                    Object.DestroyImmediate(figure.clips);
                }

                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Authoring_FillsOnlyEmptyPostureClipsWithTheStandingOnes()
        {
            var root = new GameObject("Squad");
            try
            {
                var squad = root.AddComponent<SquadFiguresAuthoring>();
                squad.figures.Add(Figure(root, 0));
                squad.crouchWalkClip = "Run";
                squad.digClip = "";

                var clips = squad.Clips()[0];

                Assert.AreEqual(clips.Idle, clips.CrouchIdle);
                Assert.AreEqual(clips.Run, clips.CrouchWalk);
                Assert.AreEqual(clips.Fire, clips.ProneFire);
                Assert.AreEqual(clips.Idle, clips.Dig);

                squad.proneCrawlClip = "Missing";
                Assert.Throws<KeyNotFoundException>(() => squad.Clips());
            }
            finally
            {
                foreach (var figure in root.GetComponentsInChildren<BoneAnimatedAuthoring>())
                {
                    Object.DestroyImmediate(figure.clips);
                }

                Object.DestroyImmediate(root);
            }
        }

        // A figure baked on its own, with a body and a weapon sharing its table; its clip names match the squad's.
        private static SquadFiguresAuthoring.Figure Figure(GameObject root, int offset, string prefix = "")
        {
            var table = ScriptableObject.CreateInstance<BoneClipTable>();
            string[] names = { "Idle", "Walk", "Run", "Fire", "Die" };
            for (var i = 0; i < names.Length; i++)
            {
                table.clips.Add(new VATClipTable.Clip
                {
                    name = prefix + names[i], startRow = offset + i * 10, frameCount = 10, fps = 10f, loop = i < 4,
                });
            }

            var figure = new GameObject($"Figure {offset}");
            figure.transform.SetParent(root.transform);
            foreach (var part in new[] { "Body", "Weapon" })
            {
                var child = new GameObject(part);
                child.transform.SetParent(figure.transform);
                child.AddComponent<BoneAnimatedAuthoring>().clips = table;
            }

            return new SquadFiguresAuthoring.Figure { root = figure.transform, clipPrefix = prefix };
        }
    }
}
