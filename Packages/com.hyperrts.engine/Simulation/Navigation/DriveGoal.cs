using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Where a drive search must bring the hull centre, and which way it must face there if it matters.</summary>
    public readonly struct DriveGoal
    {
        public readonly float2 Center;

        /// <summary>The facing it must end with; zero for a point goal.</summary>
        public readonly float2 Facing;

        private DriveGoal(float2 center, float2 facing)
        {
            Center = center;
            Facing = facing;
        }

        public bool HasFacing => math.any(Facing != 0f);

        public static DriveGoal ToPoint(float2 center) => new(center, float2.zero);

        public static DriveGoal ToPose(float2 center, float2 facing) => new(center, facing);
    }
}
