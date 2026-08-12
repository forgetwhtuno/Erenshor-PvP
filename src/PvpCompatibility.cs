using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ErenshorPvP
{
    internal static class PvpCompatibility
    {
        internal static bool IsCoopSession()
        {
            try
            {
                Type networked = FindType("NetworkedPlayer");
                if (networked == null) return false;
                return UnityEngine.Object.FindObjectsOfType(networked).Length > 0;
            }
            catch { return true; }
        }

        internal static bool IsVerifiedHuntCampActive()
        {
            try
            {
                Type api = FindType("ErenshorCampmaster.CampmasterApi");
                PropertyInfo property = api == null ? null : api.GetProperty("IsHuntCampActive", BindingFlags.Public | BindingFlags.Static);
                return property != null && property.PropertyType == typeof(bool) && (bool)property.GetValue(null, null);
            }
            catch { return false; }
        }

        internal static bool IsRemoteHuman(SimPlayer sim)
        {
            if (sim == null) return true;
            try
            {
                Type networked = FindType("NetworkedPlayer");
                if (networked != null && sim.GetComponent(networked) != null) return true;
                Type networkedSim = FindType("NetworkedSim");
                if (networkedSim != null && sim.GetComponent(networkedSim) != null) return true;
            }
            catch { return true; }
            return false;
        }

        internal static bool IsPartyMember(SimPlayer sim)
        {
            try { return sim != null && sim.InGroup && GameData.SimPlayerGrouping != null && GameData.SimPlayerGrouping.IsSimInPlayerGroup(sim); }
            catch { return true; }
        }

        internal static bool IsSameScene(UnityEngine.Object value, Character player)
        {
            try
            {
                if (value == null || player == null || player.gameObject == null) return false;
                GameObject go = value as GameObject;
                if (go == null)
                {
                    Component component = value as Component;
                    go = component == null ? null : component.gameObject;
                }
                return go != null && go.scene.IsValid() && go.scene.isLoaded &&
                    go.scene.handle == SceneManager.GetActiveScene().handle;
            }
            catch { return false; }
        }

        internal static string ReadName(SimPlayer sim)
        {
            if (sim == null) return string.Empty;
            foreach (string name in new[] { "PlayerName", "MyName", "CharacterName", "CharName", "SimName", "Name" })
            {
                object value = ReadMember(sim, name);
                if (value is string && !string.IsNullOrWhiteSpace((string)value)) return ((string)value).Trim();
            }
            try { return sim.gameObject == null ? string.Empty : sim.gameObject.name; } catch { return string.Empty; }
        }

        internal static object ReadMember(object instance, string name)
        {
            if (instance == null) return null;
            try
            {
                Type type = instance.GetType();
                FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) return field.GetValue(instance);
                PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return property != null && property.CanRead ? property.GetValue(instance, null) : null;
            }
            catch { return null; }
        }

        internal static bool TryInt(object instance, string name, out int value)
        {
            value = 0; object raw = ReadMember(instance, name);
            if (raw == null) return false;
            try { value = Convert.ToInt32(raw); return true; } catch { return false; }
        }

        private static Type FindType(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                try { Type type = assembly.GetType(name, false); if (type != null) return type; } catch { }
            return null;
        }
    }
}
