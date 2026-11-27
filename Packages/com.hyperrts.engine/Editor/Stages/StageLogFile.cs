using System;
using System.IO;
using System.Linq;

namespace HyperRTS.Editor.Stages;

/// <summary>A stage's log, a file from the project folder whose last line is <c>exit code N</c> once its run ends.</summary>
public sealed class StageLogFile(string path)
{
    public const string ExitLine = "exit code ";

    private const int TailBytes = 512;

    public string Path { get; } = path;

    public bool Exists => File.Exists(Path);

    public DateTime WrittenUtc => File.GetLastWriteTimeUtc(Path);

    /// <summary>True when the last run ended with an exit code other than 0.</summary>
    public bool Failed
    {
        get
        {
            if (!Exists)
            {
                return false;
            }

            var last = Text().TrimEnd().Split('\n').Last().Trim();
            return last.StartsWith(ExitLine, StringComparison.Ordinal) && last != ExitLine + "0";
        }
    }

    // Reads only the log's tail: an exit line is short, and a log can be long. A cut multi-byte character lands before the last line.
    private string LastLine()
    {
        using var stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.Seek(-Math.Min(stream.Length, TailBytes), SeekOrigin.End);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().TrimEnd().Split('\n').Last().Trim();
    }

    /// <summary>The log's text, read while a running stage may still be writing it.</summary>
    public string Text()
    {
        using var stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Writes a finished run's log: its text, then the exit line.</summary>
    public void Write(string text, int exitCode)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(Path, $"{text.TrimEnd()}\n{ExitLine}{exitCode}\n");
    }
}
