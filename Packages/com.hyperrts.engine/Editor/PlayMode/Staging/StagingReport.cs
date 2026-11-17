using System;
using System.Collections.Generic;

namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>What a staging run did, written to <c>report.json</c> in its folder.</summary>
    [Serializable]
    public sealed class StagingReport
    {
        public string name;
        public string scene;

        /// <summary>"Done", or "Failed: " and why.</summary>
        public string ended;

        public List<Step> steps = new();
        public List<Group> groups = new();
        public List<Issue> consoleErrors = new();
        public List<string> shots = new();
        public List<string> frames = new();
        public List<string> clips = new();
        public List<Sample> samples = new();

        /// <summary>The contact sheet, empty when the run made no image.</summary>
        public string sheet;

        /// <summary>Source image of each sheet tile, left to right, top to bottom.</summary>
        public List<string> sheetTiles = new();

        [Serializable]
        public sealed class Step
        {
            public int index;
            public string kind;

            /// <summary>Game seconds the step was due.</summary>
            public float at;

            /// <summary>Game seconds it actually ran.</summary>
            public float ran;

            /// <summary>"ok", "running" while it works over frames, or "error: " and why.</summary>
            public string outcome;
        }

        [Serializable]
        public sealed class Group
        {
            public string name;
            public int alive;
            public int total;
        }

        [Serializable]
        public sealed class Issue
        {
            public string message;
            public string stackLine;
            public int count;
        }

        [Serializable]
        public sealed class Sample
        {
            public string what;
            public string path;

            /// <summary>Camera samples only: the farthest the camera got from its first position, in metres.</summary>
            public float maxMove;

            /// <summary>Camera samples only: the widest angle from its first rotation, in degrees.</summary>
            public float maxTurn;
        }
    }
}
