using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;

namespace HyperRTS.Input.Commands
{
    /// <summary>
    /// The placement ghost's yaw: a drag aims it, holding RotateBuilding turns it with the mouse, a tap adds 45° and
    /// the snap modifier rounds every turn to 15°.
    /// </summary>
    public struct PlacementRotation
    {
        // Pointer pixels are per frame, so the turn follows the hand rather than the frame rate.
        private const float DegreesPerPixel = 0.5f;

        // Less pointer travel than this between press and release counts as a tap.
        private const float TapPixels = 3f;

        private const float TapDegrees = 45f;
        private const float SnapDegrees = 15f;

        private float _yaw;
        private float _pressYaw;
        private float _pressX;
        private float _lastX;
        private bool _moved;

        /// <summary>RotateBuilding is held: the ghost stays pinned while the mouse turns it.</summary>
        public bool Turning { get; private set; }

        /// <summary>Facing the camera, as a new placement starts.</summary>
        public static PlacementRotation Start() => new() { _yaw = PlacementMath.CameraYaw };

        public readonly float Yaw(bool snap) => snap ? Snapped(_yaw) : _yaw;

        /// <summary>Points the front along <paramref name="pull"/> on XZ.</summary>
        public void Aim(float2 pull) => _yaw = math.atan2(pull.x, pull.y);

        public void Press(float pointerX)
        {
            Turning = true;
            _moved = false;
            _pressYaw = _yaw;
            _pressX = pointerX;
            _lastX = pointerX;
        }

        public void Turn(float pointerX)
        {
            _yaw += math.radians((pointerX - _lastX) * DegreesPerPixel);
            _lastX = pointerX;
            _moved |= math.abs(pointerX - _pressX) >= TapPixels;
        }

        public void Release()
        {
            Turning = false;
            if (!_moved)
            {
                _yaw = _pressYaw + math.radians(TapDegrees);
            }
        }

        /// <summary>The drag end moved so the line from <paramref name="start"/> runs at a multiple of 15°.</summary>
        public static float3 SnapEnd(float3 start, float3 end)
        {
            var delta = end.xz - start.xz;
            var angle = Snapped(math.atan2(delta.y, delta.x));
            math.sincos(angle, out var sin, out var cos);
            var snapped = start.xz + new float2(cos, sin) * math.length(delta);
            return new float3(snapped.x, end.y, snapped.y);
        }

        private static float Snapped(float radians)
        {
            var step = math.radians(SnapDegrees);
            return math.round(radians / step) * step;
        }
    }
}
