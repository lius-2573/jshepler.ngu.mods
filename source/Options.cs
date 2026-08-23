using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;

namespace jshepler.ngu.mods
{
    internal static class Options
    {
        internal static void Init(ConfigFile Config)
        {
            AdvancedTraining.AutoAllocateEnergy = Config.Bind("AdvancedTraining", "AutoAllocateEnergy", true, "automatically allocate 100% next-level energy to advanced training and return it to NGU when the target is reached");
            Yggdrasil.AutoHarvest = Config.Bind("Yggdrasil", "AutoHarvest", true, "enable auto harvest/eat fruits when fully grown (max tier)");
            Wishes.AutoAllocate = Config.Bind("Wishes", "AutoAllocate", false, "at the configured interval, split idle energy, magic, and res3 between running wishes and start the next wish when one completes");
            AutoMergeTransform.Enabled = Config.Bind("AutoMergeTransform", "Enabled", true, "enables/disables auto merging and transforming of pendants and looties");
            Questing.AutoButter = Config.Bind("Questing", "AutoButter", true, "If true, will automatically use butter when starting a major quest");

            MoneyPit.AutoToss = Config.Bind("MoneyPit", "AutoToss", true, "automatically toss gold into the money pit as soon as it's ready");
            DailySpin.AutoSpin = Config.Bind("DailySpin", "AutoSpin", true, "automatically spin the daily wheel as soon as it's ready");
            BloodMagic.AutoCast = Config.Bind("BloodMagic", "AutoCast", true, "automatically cast Iron Pill as soon as it's ready (gold/loot/rebirth spells have their own vanilla auto-cast checkboxes, which this leaves untouched)");
            Performance.FrameCheckInterval = Config.Bind("Performance", "FrameCheckInterval", 60, "number of rendered frames between checks for frame-based automation");
            Performance.WishCheckIntervalSeconds = Config.Bind("Performance", "WishCheckIntervalSeconds", 1f, "seconds between AutoWishes checks");
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

        internal static class MoneyPit
        {
            internal static ConfigEntry<bool> AutoToss;
        }

        internal static class DailySpin
        {
            internal static ConfigEntry<bool> AutoSpin;
        }

        internal static class Wishes
        {
            internal static ConfigEntry<bool> AutoAllocate;
        }

        internal static class AdvancedTraining
        {
            internal static ConfigEntry<bool> AutoAllocateEnergy;
        }

        internal static class BloodMagic
        {
            internal static ConfigEntry<bool> AutoCast;
        }
        internal static class Performance
        {
            internal static ConfigEntry<int> FrameCheckInterval;
            internal static ConfigEntry<float> WishCheckIntervalSeconds;
        }
    }
}
