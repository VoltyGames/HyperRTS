namespace HyperRTS.Editor.Stages
{
    /// <summary>Where a step of a tool page stands, from its files or its last log; later values are worse.</summary>
    public enum StageState
    {
        Done,
        OutOfDate,
        Missing,
        Failed,
    }
}
