using System.Threading.Tasks;
using Unity.Pipeline.Commands;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>Game view frame capture as a pipeline command.</summary>
    public static class FrameCaptureCommands
    {
        [CliCommand("play", "capture", "Record the Game view in Play as a PNG sequence at a fixed frame rate into Logs/FrameCaptures/<name>/; returns the PNG paths.", Tags = new[] { "hyperrts/play" })]
        public static async Task<string[]> Capture(
            [CliArg("name", "Folder name under Logs/FrameCaptures", Required = true)] string name,
            [CliArg("seconds", "Seconds of game time to record")] float seconds = 2f,
            [CliArg("fps", "Frames per second of game time")] int fps = 60) =>
            await new FrameCapture(name, seconds, fps).Record();
    }
}
