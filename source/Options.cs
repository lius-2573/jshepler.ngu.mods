using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;

namespace jshepler.ngu.mods
{
    internal static class Options
    {
        internal static void Init(ConfigFile Config)
        {
            Yggdrasil.AutoHarvest = Config.Bind("Yggdrasil", "AutoHarvest", true, "enable auto harvest/eat fruits when fully grown (max tier)");
            AutoMergeTransform.Enabled = Config.Bind("AutoMergeTransform", "Enabled", false, "enables/disables auto merging and transforming of pendants and looties");
            Questing.AutoButter = Config.Bind("Questing", "AutoButter", false, "If true, will automatically use butter when starting a major quest");
        }

        internal static class Yggdrasil
        {
            internal static ConfigEntry<bool> AutoHarvest;
        }

        internal static class AutoMergeTransform
        {
            internal static ConfigEntry<bool> Enabled;
        }

        internal static class Questing
        {
            internal static ConfigEntry<bool> AutoButter;
        }
    }
}
