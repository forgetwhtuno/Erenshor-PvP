using System;
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ErenshorPvP
{
    // Read-only first gate for temporary PvP opponents. Native spawning is only considered after
    // this reports the exact current-game factory, template, and collection shape as available.
    internal static class PvpSpawnCapability
    {
        internal static string InspectLiveState()
        {
            try
            {
                object manager = GameData.SimMngr;
                if (manager == null) return "[Erenshor PvP] spawnprobe: SimPlayerMngr unavailable; no spawn attempted.";
                Type managerType = manager.GetType();
                MethodInfo spawn = managerType.GetMethod("SpawnMeInPlayerZone", BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(SimPlayerTracking), typeof(string), typeof(Vector3), typeof(bool) }, null);
                MethodInfo trackingSpawn = typeof(SimPlayerTracking).GetMethod("SpawnMeInGame", BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(Vector3), typeof(SimPlayerTracking) }, null);
                object blankTemplate = ReadField(manager, "BlankSPTemplate");
                object templates = ReadField(manager, "SPTemplate");
                object active = ReadField(manager, "ActiveSimInstances");

                StringBuilder text = new StringBuilder("[Erenshor PvP] spawnprobe: ");
                text.Append("manager=ok; native_zone_factory=").Append(spawn != null ? "available" : "missing");
                text.Append("; tracking_factory=").Append(trackingSpawn != null ? "available" : "missing");
                text.Append("; blank_template=").Append(blankTemplate is GameObject ? "available" : "missing");
                text.Append("; templates=").Append(Count(templates));
                text.Append("; active_instances=").Append(Count(active));
                text.Append("; action=read_only_no_spawn");
                return text.ToString();
            }
            catch (Exception ex)
            {
                return "[Erenshor PvP] spawnprobe: failed safely (" + ex.GetType().Name + "); no spawn attempted.";
            }
        }

        private static object ReadField(object instance, string name)
        {
            if (instance == null) return null;
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return field == null ? null : field.GetValue(instance);
        }

        private static int Count(object value)
        {
            ICollection collection = value as ICollection;
            return collection == null ? -1 : collection.Count;
        }
    }
}
