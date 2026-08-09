using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace jshepler.ngu.mods
{
    [HarmonyPatch]
    internal class AutoAdventure
    {
        private static Character _character;
        private static AdventureController _controller;

        // set when the player is defeated in an adventure zone (other than ITOPOD, which
        // doesn't send you to the safe zone) and is now recovering in the safe zone;
        // cleared once fully healed and sent back to the target zone
        private static bool _waitingForRespawn;

        private static bool _enabled
        {
            get => ModSave.Data.AutoAdventureEnabled;
            set => ModSave.Data.AutoAdventureEnabled = value;
        }

        private static int _targetZone
        {
            get => ModSave.Data.AutoAdventureZone;
            set => ModSave.Data.AutoAdventureZone = value;
        }

        [HarmonyPrepare]
        private static void prep(MethodBase original)
        {
            if (original != null)
                return;

            // after offline progression / loading a save the character is wherever the
            // game left them; don't yank them into an adventure zone right after that
            Plugin.OnOfflineProgressionComplete += (o, e) => _waitingForRespawn = false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ButtonShower), "Start")]
        private static void ButtonShower_start_postfix(ButtonShower __instance)
        {
            _character = __instance.character;
            _controller = _character.adventureController;

            __instance.adventure.gameObject.AddComponent<ClickHandlerComponent>()
                .OnRightClick(e =>
                {
                    if (!Plugin.ShiftIsDown)
                        return;

                    Toggle();
                });
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ButtonShower), "updateButtons"), HarmonyPriority(Priority.LowerThanNormal)]
        private static void ButtonShower_updateButtons_postifx(ButtonShower __instance)
        {
            __instance.adventure.image.color = _enabled ? Plugin.ButtonColor_LightBlue : Color.white;
        }

        // player was defeated in an adventure zone; mark that we owe them a return trip.
        // (ITOPOD deaths never send the player to the safe zone, so they're skipped)
        [HarmonyPostfix, HarmonyPatch(typeof(AdventureController), "playerDeath")]
        private static void AdventureController_playerDeath_postfix(AdventureController __instance)
        {
            if (!_enabled || __instance.zone == 1000)
                return;

            _waitingForRespawn = true;
        }

        // once fully healed in the safe zone, return to the target zone; the game's own
        // manageFight logic then resumes auto-combat there on the next frame
        [HarmonyPostfix, HarmonyPatch(typeof(AdventureController), "Update")]
        private static void AdventureController_Update_postfix(AdventureController __instance)
        {
            if (!_waitingForRespawn || !_enabled)
                return;

            var character = __instance.character;
            if (__instance.zone != -1 || character.adventure.curHP < character.totalAdvHP())
                return;

            _waitingForRespawn = false;

            if (_targetZone < 0 || _targetZone == 1000)
                return;

            __instance.zoneSelector.changeZone(_targetZone);
        }

        // while enabled, following the zone dropdown keeps the target zone in sync with
        // the player's manual choice (a real adventure zone only; safe zone and ITOPOD
        // are not valid targets)
        [HarmonyPostfix, HarmonyPatch(typeof(ZoneSelector), "changeZone")]
        private static void ZoneSelector_changeZone_postfix(ZoneSelector __instance)
        {
            if (!_enabled)
                return;

            var zone = __instance.character.adventure.zone;
            if (zone < 0 || zone == 1000)
                return;

            _targetZone = zone;
        }

        // rebirth resets adventure state; don't carry a pending respawn across it
        [HarmonyPostfix, HarmonyPatch(typeof(AdventureController), "reset")]
        private static void AdventureController_reset_postfix()
        {
            _waitingForRespawn = false;
        }

        private static void Toggle()
        {
            var zone = _character.adventure.zone;

            if (_enabled)
            {
                _enabled = false;
                Plugin.ShowNotification("自动回到冒险区域：已关闭");
                return;
            }

            // enable locks in the current zone as the target
            if (zone < 0 || zone == 1000)
            {
                // not in a usable zone: fall back to the previously stored target if any
                if (_targetZone >= 0 && _targetZone != 1000)
                {
                    _enabled = true;
                    Plugin.ShowNotification($"自动回到冒险区域：已开启（目标：{_controller.zoneName(_targetZone)}）");
                    return;
                }

                Plugin.ShowNotification("自动回到冒险区域：请先进入目标冒险区域");
                return;
            }

            _targetZone = zone;
            _enabled = true;
            Plugin.ShowNotification($"自动回到冒险区域：已开启（目标：{_controller.zoneName(zone)}）");
        }
    }
}
