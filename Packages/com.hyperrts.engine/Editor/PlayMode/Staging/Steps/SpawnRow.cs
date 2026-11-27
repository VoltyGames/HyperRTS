using Unity.Mathematics;

namespace HyperRTS.Editor.PlayMode.Staging.Steps;

/// <summary>Where a spawn step's units stand: abreast in a line across their facing, centred on a point.</summary>
internal readonly struct SpawnRow
{
    private readonly float2 _centre;
    private readonly float2 _across;
    private readonly float _spacing;
    private readonly int _count;

    /// <summary>The facing <paramref name="yawDegrees"/> turns clockwise from +Z, as the units spawn.</summary>
    public SpawnRow(float2 centre, float yawDegrees, float spacing, int count)
    {
        var yaw = math.radians(yawDegrees);
        _centre = centre;
        _across = new float2(math.cos(yaw), -math.sin(yaw));
        _spacing = spacing;
        _count = count;
    }

    public float2 Position(int index) => _centre + _across * ((index - (_count - 1) * 0.5f) * _spacing);
}
