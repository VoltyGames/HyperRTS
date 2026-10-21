using System;
using HyperRTS.Core;
using UnityEngine;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>
    /// One announcer's lines (a side's or a language's): a 2D sound cue per alert, each with its own cooldown. The
    /// cue's priority orders the queue when alerts pile up.
    /// </summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Audio + "Announcer Set", fileName = "AnnouncerSet")]
    [HelpURL(HyperRTSDocs.Audio)]
    public class AnnouncerSet : ScriptableObject
    {
        [Serializable]
        public struct Line
        {
            [Tooltip("What the announcer says; empty stays silent.")]
            public SoundCue cue;

            [Tooltip("Seconds before this alert may play again.")]
            [Min(0f)]
            public float cooldown;
        }

        [Tooltip("One of the player's units died.")]
        public Line unitLost = new() { cooldown = 10f };

        [Tooltip("One of the player's buildings took damage.")]
        public Line baseUnderAttack = new() { cooldown = 30f };

        [Tooltip("The player's power grid draws more than it produces.")]
        public Line lowPower = new() { cooldown = 20f };

        [Tooltip("A building site finished.")]
        public Line constructionComplete = new() { cooldown = 2f };

        [Tooltip("A unit left the production queue.")]
        public Line unitReady = new() { cooldown = 2f };

        [Tooltip("A research item finished.")]
        public Line upgradeComplete = new() { cooldown = 2f };

        [Tooltip("Seconds an alert may wait behind others before it is dropped as stale.")]
        [Min(0f)]
        public float maxWait = 4f;

        /// <summary>The line of an announcement slot (see <see cref="CueRules.IsAnnouncement"/>).</summary>
        public Line LineFor(CueSlot slot) => slot switch
        {
            CueSlot.AnnounceUnitLost => unitLost,
            CueSlot.AnnounceBaseUnderAttack => baseUnderAttack,
            CueSlot.AnnounceLowPower => lowPower,
            CueSlot.AnnounceConstructionComplete => constructionComplete,
            CueSlot.AnnounceUnitReady => unitReady,
            CueSlot.AnnounceUpgradeComplete => upgradeComplete,
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Not an announcement slot."),
        };
    }
}
