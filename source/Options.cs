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
            TimeMachine.AutoAllocateEnergy = Config.Bind("TimeMachine", "AutoAllocateEnergy", true, "automatically allocate 100% next-level energy to time machine speed and magic, taking resources from idle pools and NGUs");
            AutoAllocation.Priority = Config.Bind("AutoAllocation", "Priority", "Yggdrasil,Augment,BloodMagic,TimeMachine,AdvancedTraining,Wandoos,NGU,Hacks,Wishes", "allocation order; valid values: Yggdrasil, Augment, BloodMagic, TimeMachine, AdvancedTraining, Wandoos, NGU, Hacks, Wishes");
            AutoAllocation.Augment = Config.Bind("AutoAllocation", "Augment", true, "automatically fill unlocked augment energy to the next-level cap");
            AutoAllocation.BloodMagic = Config.Bind("AutoAllocation", "BloodMagic", true, "automatically fill unlocked blood ritual magic to the cap");
            AutoAllocation.Wandoos = Config.Bind("AutoAllocation", "Wandoos", true, "automatically fill Wandoos energy and magic to their caps");
            AutoAllocation.NGU = Config.Bind("AutoAllocation", "NGU", true, "automatically fill NGU energy and magic to their next-level caps");
            AutoAllocation.Hacks = Config.Bind("AutoAllocation", "Hacks", true, "automatically fill res3 into upgradeable hacks up to the next-level cap; when wishes are also auto-allocated and Hacks has higher priority than Wishes, hacks are limited to half the total res3 cap");
            Yggdrasil.AutoHarvest = Config.Bind("Yggdrasil", "AutoHarvest", true, "enable auto harvest/eat fruits when fully grown (max tier)");
            Yggdrasil.AutoActivate = Config.Bind("Yggdrasil", "AutoActivate", true, "automatically activate fruits whose permanent unlock was bought with EXP, paying the one-time activation cost (Yggdrasil is first in AutoAllocation.Priority by default)");
            Wishes.AutoAllocate = Config.Bind("Wishes", "AutoAllocate", false, "at the configured interval, split idle energy, magic, and res3 between running wishes and start the next wish when one completes");
            AutoMergeTransform.Enabled = Config.Bind("AutoMergeTransform", "Enabled", true, "enables/disables auto merging and transforming of pendants and looties");
            Questing.AutoButter = Config.Bind("Questing", "AutoButter", true, "If true, will automatically use butter when starting a major quest");

            MoneyPit.AutoToss = Config.Bind("MoneyPit", "AutoToss", true, "automatically toss gold into the money pit as soon as it's ready");
            DailySpin.AutoSpin = Config.Bind("DailySpin", "AutoSpin", true, "automatically spin the daily wheel as soon as it's ready");
            BloodMagic.AutoCast = Config.Bind("BloodMagic", "AutoCast", true, "automatically cast Iron Pill as soon as it's ready (gold/loot/rebirth spells have their own vanilla auto-cast checkboxes, which this leaves untouched)");
            GoldDiggers.AutoLoadSaved = Config.Bind("GoldDiggers", "AutoLoadSaved", true, "automatically apply the saved digger loadout after loading a save and after rebirth");
            Performance.FrameCheckInterval = Config.Bind("Performance", "FrameCheckInterval", 60, "number of rendered frames between checks for frame-based automation");
            Performance.WishCheckIntervalSeconds = Config.Bind("Performance", "WishCheckIntervalSeconds", 1f, "seconds between AutoWishes checks");
            Cards.AutoSortEnabled = Config.Bind("Cards", "AutoSort.Enabled", true, "if enabled, sorts cards as they are added");
            Cards.AutoSortBy = Config.Bind("Cards", "AutoSort.By", CardSortBy.RarityFirst, "RarityFirst: rarity, type, bonus; TypeFirst: type, rarity, bonus; Efficiency: bonus per mayo; Variance: bonus variance");
            Cards.AutoSortDirection = Config.Bind("Cards", "AutoSort.Direction", CardSortDirection.Ascending, "the order cards are sorted");
            Cards.AutoYeetMode = Config.Bind("Cards", "AutoYeet.Mode", CardYeetMode.Disabled, "what is used to determine when to auto yeet a card");
            Cards.MaxYeetRarity = Config.Bind("Cards", "AutoYeet.MaxYeetRarity", rarity.Crappy, "if AutoYeet.Mode is Rarity, this is a card's max rarity that will get yeeted");
            Cards.MaxYeetEfficiency = Config.Bind("Cards", "AutoYeet.MaxYeetEfficiency", 0f, "if AutoYeet.Mode is Efficiency, this is a card's max mayo efficiency that will get yeeted");
            Cards.MaxYeetVariance = Config.Bind("Cards", "AutoYeet.MaxYeetVariance", 0f, "if AutoYeet.Mode is Variance, this is a card's max variance that will get yeeted");
            Cards.AlwaysYeetCSV = Config.Bind("Cards", "AutoYeet.AlwaysYeet", "0,0,0,0,0,0,0,0,0,0,0,0,0,0,0", "card bonus flags, configured as a comma-separated list");
            Cards.AutoProtectChonkers = Config.Bind("Cards", "AutoProtectChonkers", true, "automatically protect Chonker cards when they spawn");
            Cards.AutoCastEnabled = Config.Bind("Cards", "AutoCast.Enabled", false, "automatically cast one highest-priority ordinary unprotected card at each automation check");

        }

        internal static class Yggdrasil
        {
            internal static ConfigEntry<bool> AutoHarvest;
            internal static ConfigEntry<bool> AutoActivate;
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

        internal static class TimeMachine
        {
            internal static ConfigEntry<bool> AutoAllocateEnergy;
        }

        internal static class AutoAllocation
        {
            internal static ConfigEntry<string> Priority;
            internal static ConfigEntry<bool> Augment;
            internal static ConfigEntry<bool> BloodMagic;
            internal static ConfigEntry<bool> Wandoos;
            internal static ConfigEntry<bool> NGU;
            internal static ConfigEntry<bool> Hacks;
        }

        internal static class GoldDiggers
        {
            internal static ConfigEntry<bool> AutoLoadSaved;
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
        internal static class Cards
        {
            internal static ConfigEntry<bool> AutoSortEnabled;
            internal static ConfigEntry<CardSortBy> AutoSortBy;
            internal static ConfigEntry<CardSortDirection> AutoSortDirection;
            internal static ConfigEntry<CardYeetMode> AutoYeetMode;
            internal static ConfigEntry<rarity> MaxYeetRarity;
            internal static ConfigEntry<float> MaxYeetEfficiency;
            internal static ConfigEntry<float> MaxYeetVariance;
            internal static ConfigEntry<string> AlwaysYeetCSV;
            internal static ConfigEntry<bool> AutoProtectChonkers;
            internal static ConfigEntry<bool> AutoCastEnabled;
        }
    }
}
