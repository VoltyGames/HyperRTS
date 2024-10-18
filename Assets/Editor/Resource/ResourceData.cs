using HyperRTS.Core.Resources;
using UnityEngine;

namespace HyperRTS.Editor.Resource
{
    [CreateAssetMenu(fileName = "NewResource", menuName = "RTS Engine/Resource Data", order = 0)]
    public class ResourceData : ScriptableObject
    {
        public int resourceId;
        public string resourceName;
        public Sprite resourceIcon;
    }
}
