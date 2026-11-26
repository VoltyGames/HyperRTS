using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Destruction
{
    /// <summary>One break clip's rows in the building's vertex animation textures.</summary>
    [System.Serializable]
    public struct SectionClip
    {
        public int StartRow;
        public int FrameCount;
        public float Fps;

        public readonly float Seconds => FrameCount / Fps;
    }

    /// <summary>The three ways a section's pieces break, shared by every section of the building.</summary>
    [System.Serializable]
    public struct SectionClips : IComponentData
    {
        /// <summary>Seconds an impact still decides how a section breaks.</summary>
        public const float ImpactMemory = 2f;

        /// <summary>Blown inward by a hit from outside.</summary>
        public SectionClip BlownIn;

        /// <summary>Blown outward by a hit from inside.</summary>
        public SectionClip BlownOut;

        /// <summary>Falling apart with nothing hitting it, once its supports are gone.</summary>
        public SectionClip Collapse;

        /// <summary>Blown in by a hit from outside, out by one from inside, else falling apart on its own.</summary>
        public readonly SectionClip Pick(in Section section, in SectionLook look, float now)
        {
            if (now - look.ImpactTime > ImpactMemory)
            {
                return Collapse;
            }

            var fromOutside = math.dot(look.Impact - section.Center, section.Outward) > 0f;
            return fromOutside ? BlownIn : BlownOut;
        }

        /// <summary>Seconds the longest clip plays: how long a building whose last section broke stays before it dies.</summary>
        public readonly float Longest => math.max(Collapse.Seconds, math.max(BlownIn.Seconds, BlownOut.Seconds));

        /// <summary>Whether all three clips have frames, which pieces need to play.</summary>
        public readonly bool IsSet => BlownIn.FrameCount > 0 && BlownOut.FrameCount > 0 && Collapse.FrameCount > 0;
    }
}
