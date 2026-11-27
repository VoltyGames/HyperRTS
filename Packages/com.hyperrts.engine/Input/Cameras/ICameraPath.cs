using UnityEngine;

namespace HyperRTS.Input.Cameras;

/// <summary>A scripted camera move, such as a cutscene's pan or orbit, that <see cref="CinematicCamera"/> plays.</summary>
public interface ICameraPath
{
    /// <summary>The camera's pose at <paramref name="time"/>, in seconds of game time.</summary>
    Pose ViewAt(float time);
}
