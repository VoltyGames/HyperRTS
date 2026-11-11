using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Presentation.Animation.Rigs
{
    /// <summary>The rig that poses this part, tilt root or tilt follower, so the part pulls its pose and writes only itself.</summary>
    public struct MotionRigLink : IComponentData
    {
        public Entity Rig;

        /// <summary>Index of the first <see cref="MotionRigPart"/> on this entity, or -1 for a tilt root with none.</summary>
        public int FirstPart;

        /// <summary>Links every part and the tilt entities of <paramref name="rig"/> and chains its entries per part.</summary>
        public static void AddAll(EntityManager manager, Entity rig)
        {
            var body = manager.GetComponentData<MotionRig>(rig);
            var parts = manager.GetBuffer<MotionRigPart>(rig);
            var firsts = new NativeList<int>(Allocator.Temp);
            for (var i = 0; i < parts.Length; i++)
            {
                var entry = parts[i];
                entry.Following = 0;
                parts[i] = entry;
                var last = -1;
                for (var before = 0; before < i; before++)
                {
                    last = parts[before].Part == entry.Part ? before : last;
                }

                if (last < 0)
                {
                    firsts.Add(i);
                    continue;
                }

                var previous = parts[last];
                previous.Following = i + 1;
                parts[last] = previous;
            }

            var placed = parts.ToNativeArray(Allocator.Temp);
            foreach (var first in firsts)
            {
                Set(manager, placed[first].Part, rig, first);
            }

            AddTilt(manager, rig, body.TiltRoot);
            AddTilt(manager, rig, body.TiltFollower);
        }

        private static void AddTilt(EntityManager manager, Entity rig, Entity tilted)
        {
            if (tilted != Entity.Null && !manager.HasComponent<MotionRigLink>(tilted))
            {
                Set(manager, tilted, rig, -1);
            }
        }

        private static void Set(EntityManager manager, Entity entity, Entity rig, int first)
        {
            manager.AddComponent<MotionRigLink>(entity);
            manager.SetComponentData(entity, new MotionRigLink { Rig = rig, FirstPart = first });
        }
    }
}
