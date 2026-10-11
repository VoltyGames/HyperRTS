namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>
    /// Game code run after every map build (list the map in menus, sync its strings). The builder creates each
    /// implementation with its parameterless constructor.
    /// </summary>
    public interface IMapBuildHook
    {
        void OnBuilt(MapBuildResult result);
    }
}
