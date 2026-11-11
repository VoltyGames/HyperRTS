using System.Collections.Generic;
using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Presentation.Animation.Figures;
using HyperRTS.Simulation.Common.Infantry;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Animation.Figures
{
    /// <summary>The clip tables, sets and ranges a fixture's figures play, and the assets it makes and frees.</summary>
    public abstract class SquadFigureClipFixture
    {
        protected readonly Dictionary<int, BlobAssetReference<FigureClips>> _clips = new();
        protected readonly List<Object> _made = new();

        [TearDown]
        public void DisposeClips()
        {
            foreach (var clips in _clips.Values)
            {
                clips.Dispose();
            }

            _clips.Clear();
            foreach (var made in _made)
            {
                Object.DestroyImmediate(made);
            }

            _made.Clear();
        }

        // The ranges the pipeline gives a figure whose table starts at the row offset; figures at one offset share them.
        protected BlobAssetReference<FigureClips> Clips(int offset)
        {
            if (!_clips.TryGetValue(offset, out var clips))
            {
                var table = Table(offset);
                var set = Set();
                _made.Add(table);
                _made.Add(set);
                clips = set.Build(table);
                _clips[offset] = clips;
            }

            return clips;
        }

        // Every clip the set names, ten rows apart from the offset; the falls play once and the rest loop.
        public static BoneClipTable Table(int offset)
        {
            var table = ScriptableObject.CreateInstance<BoneClipTable>();
            var names = new[]
            {
                "Idle", "Walk", "Run", "Fire", "Die", "DieBlast_Front", "DieBlast_Back", "DieBlast_Left", "DieBlast_Right",
                "Dig", "CrouchIdle", "CrouchWalk", "CrouchFire", "ProneIdle", "ProneCrawl", "ProneFire", "ProneDie", "CrouchDie",
            };
            var rows = new[] { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160, 170 };
            for (var i = 0; i < names.Length; i++)
            {
                table.clips.Add(new NamedClip
                {
                    name = names[i], startRow = offset + rows[i], frameCount = 10, fps = 10f, loop = !names[i].Contains("Die"),
                });
            }

            return table;
        }

        // Crouched and prone figures run with their walking and crawling clips, and a crouched fall is its own clip.
        public static FigureClipSet Set()
        {
            var set = ScriptableObject.CreateInstance<FigureClipSet>();
            set.idle = Postures("Idle", "CrouchIdle", "ProneIdle");
            set.walk = Postures("Walk", "CrouchWalk", "ProneCrawl");
            set.run = Postures("Run", "CrouchWalk", "ProneCrawl");
            set.fire = Postures("Fire", "CrouchFire", "ProneFire");
            set.actions.Add(new ActionClip { action = MemberAction.Dig, clip = "Dig" });
            set.shotDeath = Postures("Die", "CrouchDie", "ProneDie");
            set.blastFrontDeath = Postures("DieBlast_Front", "CrouchDie", "ProneDie");
            set.blastBackDeath = Postures("DieBlast_Back", "CrouchDie", "ProneDie");
            set.blastLeftDeath = Postures("DieBlast_Left", "CrouchDie", "ProneDie");
            set.blastRightDeath = Postures("DieBlast_Right", "CrouchDie", "ProneDie");
            return set;
        }

        private static PostureClips Postures(string stand, string crouch, string prone) =>
            new() { stand = stand, crouch = crouch, prone = prone };

        public static ClipRange Range(int startRow, bool loop) =>
            new() { StartRow = startRow, FrameCount = 10, Fps = 10f, Loop = loop };
    }
}
