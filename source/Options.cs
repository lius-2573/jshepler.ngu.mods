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

            MoneyPit.AutoToss = Config.Bind("MoneyPit", "AutoToss", false, "automatically toss gold into the money pit as soon as it's ready");
            DailySpin.AutoSpin = Config.Bind("DailySpin", "AutoSpin", false, "automatically spin the daily wheel as soon as it's ready");
            BloodMagic.AutoCast = Config.Bind("BloodMagic", "AutoCast", false, "automatically cast blood magic spells (Iron Pill, and enable game's auto gold/loot/rebirth spells) as soon as they're ready");
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

        internal static class BloodMagic
        {
            internal static ConfigEntry<bool> AutoCast;
        }
    }
}
