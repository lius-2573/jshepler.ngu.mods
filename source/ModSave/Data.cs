using System.Collections.Generic;

namespace jshepler.ngu.mods.ModSave
{
    internal static class Data
    {
        internal static Dictionary<string, object> Values = new();

        internal static T Get<T>(string key, T defaultValue = default)
        {
            if (!Values.TryGetValue(key, out var value))
            {
                Values[key] = defaultValue;
                return defaultValue;
            }

            return (T)value;
        }

        internal static void Set(string key, object value)
        {
            Values[key] = value;
        }

        internal static bool AutoQuestingEnabled
        {
            get => Get<bool>("AutoQuestingEnabled");
            set => Set("AutoQuestingEnabled", value);
        }

        internal static int AutoQuestingStartThreshold
        {
            // 0 = follow current maxBankedQuests() (start as soon as the bank is full);
            // >0 = explicit threshold to start at
            get => Get("AutoQuestingStartThreshold", 0);
            set => Set("AutoQuestingStartThreshold", value);
        }

        internal static bool AutoAdventureEnabled
        {
            get => Get<bool>("AutoAdventureEnabled");
            set => Set("AutoAdventureEnabled", value);
        }

        internal static int AutoAdventureZone
        {
            // target adventure zone id to return to after being defeated and healed
            // in the safe zone; -1 = none
            get => Get("AutoAdventureZone", -1);
            set => Set("AutoAdventureZone", value);
        }

        internal static string[] LastYggRewards
        {
            get => Get("LastYggRewards", new string[21]);
            set => Set("LastYggRewards", value);
        }


        // REMINDER: NO TYPES DEFINED IN MODS ELSE NOT LOADABLE IN VANILLA
    }
}
