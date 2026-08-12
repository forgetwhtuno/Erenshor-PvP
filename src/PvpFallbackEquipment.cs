using System;
using System.Collections.Generic;
using System.Linq;

namespace ErenshorPvP
{
    // Encounter-local visuals for off-map profiles whose native tracking/save record has no
    // equipment yet. Items are never written back to the Sim, inventory, or Erenshor save.
    internal static class PvpFallbackEquipment
    {
        private static readonly Item.SlotType[] ArmorSlots =
        {
            Item.SlotType.Head, Item.SlotType.Chest, Item.SlotType.Shoulder,
            Item.SlotType.Arm, Item.SlotType.Bracer, Item.SlotType.Hand,
            Item.SlotType.Leg, Item.SlotType.Foot, Item.SlotType.Back
        };

        internal static List<Item> Build(PvpOpponentProfile profile, Class profileClass)
        {
            List<Item> result = new List<Item>();
            if (profile == null || profileClass == null || GameData.ItemDB == null) return result;
            // Awake may leave ItemDBList as a non-null but empty staging list while the native
            // array/dictionary is already usable. Merge every verified native collection instead
            // of treating an empty list as authoritative.
            List<Item> database = new List<Item>();
            if (GameData.ItemDB.ItemDBList != null) database.AddRange(GameData.ItemDB.ItemDBList);
            if (GameData.ItemDB.ItemDB != null) database.AddRange(GameData.ItemDB.ItemDB);
            if (GameData.ItemDB.GenericItems != null) database.AddRange(GameData.ItemDB.GenericItems);
            database = database.Where(x => x != null).Distinct().ToList();
            if (database.Count == 0)
            {
                PvpDiagnostics.Warning("fallback_equipment_candidates profile=" + profile.Name + "; database=0");
                return result;
            }

            List<Item> eligible = database.Where(item => Eligible(item, profile.Level, profileClass)).ToList();
            for (int i = 0; i < ArmorSlots.Length; i++)
            {
                Item.SlotType slot = ArmorSlots[i];
                Item selected = Select(eligible.Where(x => x.RequiredSlot == slot), profile.Name, slot.ToString(), null);
                if (selected != null) result.Add(selected);
            }

            Item primary = Select(eligible.Where(x => x.RequiredSlot == Item.SlotType.Primary ||
                x.RequiredSlot == Item.SlotType.PrimaryOrSecondary), profile.Name, "primary", null);
            if (primary != null) result.Add(primary);

            Item secondary = Select(eligible.Where(x => x.RequiredSlot == Item.SlotType.Secondary),
                profile.Name, "secondary", primary);
            if (secondary == null)
                secondary = Select(eligible.Where(x => x.RequiredSlot == Item.SlotType.PrimaryOrSecondary),
                    profile.Name, "secondary-flex", primary);
            if (secondary != null) result.Add(secondary);
            int visible = database.Count(HasVisibleEquipment);
            int classCompatible = database.Count(item => HasVisibleEquipment(item) && ClassCompatible(item, profileClass));
            int levelCompatible = database.Count(item => HasVisibleEquipment(item) && ClassCompatible(item, profileClass) && item.ItemLevel >= 0 && item.ItemLevel <= Math.Max(1, profile.Level));
            PvpDiagnostics.Log("fallback_equipment_candidates profile=" + profile.Name +
                "; database=" + database.Count + "; visible=" + visible + "; class=" + classCompatible +
                "; level=" + levelCompatible + "; eligible=" + eligible.Count + "; selected=" + result.Count);
            return result;
        }

        private static bool Eligible(Item item, int level, Class profileClass)
        {
            if (!HasVisibleEquipment(item) || item == GameData.PlayerInv.Empty || item.Template || item.FurnitureSet ||
                item.SimPlayersCantGet || item.ItemLevel < 0 || item.ItemLevel > Math.Max(1, level)) return false;
            if (!ClassCompatible(item, profileClass)) return false;
            Item.SlotType slot = item.RequiredSlot;
            return ArmorSlots.Contains(slot) || slot == Item.SlotType.Primary ||
                slot == Item.SlotType.Secondary || slot == Item.SlotType.PrimaryOrSecondary;
        }

        private static bool HasVisibleEquipment(Item item)
        {
            return item != null && !string.IsNullOrWhiteSpace(item.Id) &&
                   !string.IsNullOrWhiteSpace(item.EquipmentToActivate);
        }

        private static bool ClassCompatible(Item item, Class profileClass)
        {
            if (item == null || profileClass == null || item.Classes == null) return false;
            foreach (Class allowed in item.Classes)
            {
                if (allowed == null) continue;
                if (allowed == profileClass) return true;
                if (!string.IsNullOrWhiteSpace(allowed.ClassName) && !string.IsNullOrWhiteSpace(profileClass.ClassName) &&
                    string.Equals(allowed.ClassName, profileClass.ClassName, StringComparison.OrdinalIgnoreCase)) return true;
                if (!string.IsNullOrWhiteSpace(allowed.DisplayName) && !string.IsNullOrWhiteSpace(profileClass.DisplayName) &&
                    string.Equals(allowed.DisplayName, profileClass.DisplayName, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static Item Select(IEnumerable<Item> source, string profileName, string slotKey, Item excluded)
        {
            List<Item> candidates = source.Where(x => x != null && x != excluded)
                .OrderByDescending(x => x.ItemLevel)
                .ThenBy(x => x.Id, StringComparer.Ordinal)
                .ToList();
            if (candidates.Count == 0) return null;
            int best = candidates[0].ItemLevel;
            List<Item> band = candidates.Where(x => x.ItemLevel >= best - 2).ToList();
            uint hash = StableHash((profileName ?? string.Empty) + "|" + slotKey);
            return band[(int)(hash % (uint)band.Count)];
        }

        private static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++) { hash ^= value[i]; hash *= 16777619u; }
                return hash;
            }
        }
    }
}
