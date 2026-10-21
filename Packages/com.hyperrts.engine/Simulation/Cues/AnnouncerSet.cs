using System;
using System.Collections.Generic;
using HyperRTS.Core;
using UnityEngine;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>
    /// One announcer's lines (a side's or a language's): a 2D sound cue per <see cref="AnnouncementKind"/>, each with
    /// its own cooldown. The cue's priority orders the queue when alerts pile up.
    /// </summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Audio + "Announcer Set", fileName = "AnnouncerSet")]
    [HelpURL(HyperRTSDocs.Audio)]
    public class AnnouncerSet : ScriptableObject
    {
        [Serializable]
        public struct Line
        {
            [Tooltip("The alert this line answers.")]
            public AnnouncementKind kind;

            [Tooltip("What the announcer says; empty stays silent.")]
            public SoundCue cue;

            [Tooltip("Seconds before this alert may play again.")]
            [Min(0f)]
            public float cooldown;
        }

        [Tooltip("One line per alert kind; a kind without a line stays silent.")]
        public List<Line> lines = new()
        {
            new Line { kind = AnnouncementKind.UnitLost, cooldown = 10f },
            new Line { kind = AnnouncementKind.BaseUnderAttack, cooldown = 30f },
            new Line { kind = AnnouncementKind.LowPower, cooldown = 20f },
            new Line { kind = AnnouncementKind.ConstructionComplete, cooldown = 2f },
            new Line { kind = AnnouncementKind.UnitReady, cooldown = 2f },
            new Line { kind = AnnouncementKind.UpgradeComplete, cooldown = 2f },
        };

        [Tooltip("Seconds an alert may wait behind others before it is dropped as stale.")]
        [Min(0f)]
        public float maxWait = 4f;

        /// <summary>The first line for the kind; false when the set has none.</summary>
        public bool TryGetLine(AnnouncementKind kind, out Line line)
        {
            foreach (var entry in lines)
            {
                if (entry.kind == kind)
                {
                    line = entry;
                    return true;
                }
            }

            line = default;
            return false;
        }
    }
}
