using System;
using System.Collections.Generic;
using UnityEngine;

namespace ErenshorPvP
{
    // Immutable snapshot of an off-map Sim. This is profile data only; it is never registered
    // as a live SimPlayer and never mutates the source tracking record.
    internal sealed class PvpOpponentProfile
    {
        internal readonly string Name;
        internal readonly int Level;
        internal readonly string ClassName;
        internal readonly string GuildId;
        internal readonly string Gender;
        internal readonly int SimIndex;
        internal readonly int GearScore;
        internal readonly string HairName;
        internal readonly int HairColorIndex;
        internal readonly int SkinColorIndex;
        internal readonly Color HairColor;
        internal readonly Color SkinColor;
        internal readonly List<string> EquippedItemIds;
        internal readonly List<string> AcquiredSpellIds;

        private PvpOpponentProfile(string name, int level, string className, string guildId, string gender, int simIndex, int gearScore,
            string hairName, int hairColorIndex, int skinColorIndex, Color hairColor, Color skinColor,
            List<string> equippedItemIds, List<string> acquiredSpellIds)
        {
            Name = name; Level = Math.Max(1, level); ClassName = className ?? string.Empty;
            GuildId = guildId ?? string.Empty; Gender = gender ?? string.Empty;
            SimIndex = simIndex; GearScore = Math.Max(0, gearScore);
            HairName = hairName ?? string.Empty; HairColorIndex = Math.Max(0, hairColorIndex); SkinColorIndex = Math.Max(0, skinColorIndex);
            HairColor = hairColor; SkinColor = skinColor;
            EquippedItemIds = equippedItemIds ?? new List<string>();
            AcquiredSpellIds = acquiredSpellIds ?? new List<string>();
        }

        internal static PvpOpponentProfile FromTracking(SimPlayerTracking tracking)
        {
            if (tracking == null || string.IsNullOrWhiteSpace(tracking.SimName)) return null;
            Color hair = Color.white; Color skin = new Color(.72f, .55f, .43f, 1f);
            try
            {
                if (GameData.SimMngr != null && GameData.SimMngr.HairColors != null && tracking.HairColor >= 0 && tracking.HairColor < GameData.SimMngr.HairColors.Count)
                    hair = GameData.SimMngr.HairColors[tracking.HairColor];
                if (GameData.SimMngr != null && GameData.SimMngr.SkinColors != null && tracking.SkinColor >= 0 && tracking.SkinColor < GameData.SimMngr.SkinColors.Count)
                    skin = GameData.SimMngr.SkinColors[tracking.SkinColor];
            }
            catch { }
            List<string> equipment = tracking.NewEquippedItems == null ? new List<string>() : new List<string>(tracking.NewEquippedItems);
            List<string> spells = new List<string>();
            try
            {
                SimPlayerSaveData saved = SimPlayerDataManager.FindMyDataInList(tracking.SimName);
                if (saved != null)
                {
                    if (saved.MyEquippedItems != null && saved.MyEquippedItems.Count > 0) equipment = new List<string>(saved.MyEquippedItems);
                    if (saved.AcquiredSpells != null) spells = new List<string>(saved.AcquiredSpells);
                }
            }
            catch { }
            return new PvpOpponentProfile(tracking.SimName, tracking.Level, tracking.ClassName, tracking.GuildID, tracking.Gender,
                tracking.simIndex, tracking.GearScore, tracking.HairIndex, tracking.HairColor, tracking.SkinColor, hair, skin, equipment, spells);
        }

        internal static PvpOpponentProfile ForTest(string name, int level, string className, string guild)
        {
            return new PvpOpponentProfile(name, level, className, guild, "", -1, 0, "", 0, 0, Color.white, Color.white,
                new List<string>(), new List<string>());
        }

        internal string Describe()
        {
            return Name + " level=" + Level + "; class=" + (string.IsNullOrEmpty(ClassName) ? "unknown" : ClassName) +
                   "; guild=" + (string.IsNullOrEmpty(GuildId) ? "none" : GuildId) + "; gear=" + GearScore + "; spells=" + AcquiredSpellIds.Count;
        }
    }
}
