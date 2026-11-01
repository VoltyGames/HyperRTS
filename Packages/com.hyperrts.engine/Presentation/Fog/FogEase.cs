using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Fog
{
    /// <summary>Moves the fog's shown opacity toward its target, so cells fade in and out instead of popping.</summary>
    public static class FogEase
    {
        /// <summary>
        /// Steps every shown cell toward its target by the share of a full fade that <paramref name="deltaTime"/> is of
        /// <paramref name="fadeTime"/>; true once every cell has arrived.
        /// </summary>
        public static bool Step(NativeArray<byte> shown, NativeArray<byte> target, float deltaTime, float fadeTime)
        {
            using var moving = new NativeReference<int>(Allocator.TempJob);
            var step = (int)math.ceil(byte.MaxValue * deltaTime / math.max(fadeTime, 1e-3f));
            new StepJob { Shown = shown, Target = target, Step = math.max(step, 1), Moving = moving }.Run();
            return moving.Value == 0;
        }

        [BurstCompile]
        private struct StepJob : IJob
        {
            public NativeArray<byte> Shown;
            [ReadOnly] public NativeArray<byte> Target;
            public int Step;
            public NativeReference<int> Moving;

            public void Execute()
            {
                var moving = 0;
                for (var i = 0; i < Shown.Length; i++)
                {
                    int shown = Shown[i];
                    int target = Target[i];
                    if (shown == target)
                    {
                        continue;
                    }

                    var next = shown < target ? math.min(shown + Step, target) : math.max(shown - Step, target);
                    Shown[i] = (byte)next;
                    moving += next != target ? 1 : 0;
                }

                Moving.Value = moving;
            }
        }
    }
}
