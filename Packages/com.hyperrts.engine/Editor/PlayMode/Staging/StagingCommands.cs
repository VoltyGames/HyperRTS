using Unity.Pipeline.Commands;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>Scripted Play checks as pipeline commands.</summary>
    public static class StagingCommands
    {
        [CliCommand("play", "stage", "Run a staging file (JSON with a scene and timed steps: spawn, order, camera, shot, frames, sample, destroy, damage, speed, fog) as one scripted Play check. Returns at once with the output folder; poll play stage_status until it says Done or Failed. Writes report.json and sheet.png there.", Tags = new[] { "hyperrts/play" })]
        public static object Stage(
            [CliArg("file", "Staging JSON path relative to the project root", Required = true)] string file,
            [CliArg("width", "Capture width in pixels, with height; 0 keeps the staging's own resolution")] int width = 0,
            [CliArg("height", "Capture height in pixels, with width")] int height = 0) =>
            new { folder = StagingJob.Start(file, new Vector2Int(width, height)) };

        [CliCommand("play", "stage_status", "Progress of the running staging, or how the last one ended, with its report path.", Tags = new[] { "hyperrts/play" })]
        public static object StageStatus()
        {
            StagingJob.EndIfStalled();
            return new { running = StagingJob.IsRunning, status = StagingJob.Status };
        }
    }
}
