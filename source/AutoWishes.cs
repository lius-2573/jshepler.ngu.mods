using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace jshepler.ngu.mods
{
    [HarmonyPatch]
    internal static class AutoWishes
    {
        private static readonly List<int> _runningWishIds = new();
        private static int _pendingWishSlots;
        private static float _lastCheckTime = float.NegativeInfinity;

        private static bool _enabled
        {
            get => Options.Wishes.AutoAllocate.Value;
            set => Options.Wishes.AutoAllocate.Value = value;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ButtonShower), "Start")]
        private static void ButtonShower_Start_postfix(ButtonShower __instance)
        {
            var button = __instance.wishes;
            if (button == null)
                return;

            button.gameObject.AddComponent<ClickHandlerComponent>()
                .OnRightClick(e =>
                {
                    if (!Plugin.ShiftIsDown)
                        return;

                    _enabled = !_enabled;
                    SetButtonColor(button);

                    if (_enabled)
                    {
                        AutomationThrottle.Reset(ref _lastCheckTime);
                        UpdateAllocation();
                    }
                    else
                    {
                        _pendingWishSlots = 0;
                        AutomationThrottle.Reset(ref _lastCheckTime);
                    }
                });

            Plugin.OnUpdate += (o, e) => UpdateAllocation();
            Plugin.OnGameStart += (o, e) => ResetState();
            Plugin.OnSaveLoaded += (o, e) => ResetState();
            SetButtonColor(button);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ButtonShower), "updateButtons")]
        private static void ButtonShower_updateButtons_postfix(ButtonShower __instance)
        {
            SetButtonColor(__instance.wishes);
        }
        [HarmonyPostfix, HarmonyPatch(typeof(WishesController), "updateAllWishes")]
        private static void WishesController_updateAllWishes_postfix(WishesController __instance)
        {
            if (_enabled)
                UpdateAllocation(__instance);
        }


        [HarmonyPostfix, HarmonyPatch(typeof(WishesController), "doLevelupEffect")]
        private static void WishesController_doLevelupEffect_postfix(WishesController __instance, int id, int level)
        {
            if (!_enabled || !IsUsable(__instance) || !__instance.character.wishes.wishesOn)
                return;

            if (level >= __instance.maxWishLevel(id)
                && IsRunning(__instance.character.wishes.wishes[id]))
                _pendingWishSlots++;
        }

        private static void UpdateAllocation()
        {
            if (!_enabled)
                return;

            UpdateAllocation(Plugin.Character?.wishesController);
        }

        private static void UpdateAllocation(WishesController controller)
        {
            if (!IsUsable(controller) || !controller.character.wishes.wishesOn)
                return;

            if (!AutomationThrottle.ShouldRunEverySeconds(ref _lastCheckTime))
                return;

            var started = TryStartPendingWishes(controller);
            var changed = AllocateIdleResources(controller);
            if (started)
                controller.updateMenu();
            else if (changed)
                controller.updateText();
        }

        private static bool IsUsable(WishesController controller)
        {
            return controller != null
                && controller.character != null
                && controller.character.wishes != null
                && controller.character.wishes.wishes != null
                && controller.character.settings != null
                && controller.properties != null;
        }

        private static bool IsRunning(Wish wish)
        {
            return wish != null && (wish.energy > 0 || wish.magic > 0 || wish.res3 > 0);
        }

        private static void CollectRunningWishes(WishesController controller)
        {
            _runningWishIds.Clear();

            var wishes = controller.character.wishes.wishes;
            for (var id = 0; id < wishes.Count; id++)
            {
                if (IsRunning(wishes[id]) && wishes[id].level < controller.maxWishLevel(id))
                    _runningWishIds.Add(id);
            }
        }

        private static bool AllocateIdleResources(WishesController controller)
        {
            CollectRunningWishes(controller);
            if (_runningWishIds.Count == 0)
                return false;

            var changed = false;
            changed |= DistributeEnergy(controller.character, _runningWishIds);
            changed |= DistributeMagic(controller.character, _runningWishIds);
            changed |= DistributeRes3(controller.character, _runningWishIds);
            return changed;
        }

        private static bool DistributeEnergy(Character character, List<int> wishIds)
        {
            var idle = character.idleEnergy;
            if (idle <= 0)
                return false;

            var remaining = idle;
            for (var index = 0; index < wishIds.Count; index++)
            {
                var slotsLeft = wishIds.Count - index;
                var share = remaining / slotsLeft;
                if (remaining % slotsLeft > 0)
                    share++;

                var wish = character.wishes.wishes[wishIds[index]];
                var amount = Math.Min(share, long.MaxValue - wish.energy);
                wish.energy += amount;
                remaining -= amount;
            }

            character.idleEnergy = remaining;
            return remaining != idle;
        }

        private static bool DistributeMagic(Character character, List<int> wishIds)
        {
            var idle = character.magic.idleMagic;
            if (idle <= 0)
                return false;

            var remaining = idle;
            for (var index = 0; index < wishIds.Count; index++)
            {
                var slotsLeft = wishIds.Count - index;
                var share = remaining / slotsLeft;
                if (remaining % slotsLeft > 0)
                    share++;

                var wish = character.wishes.wishes[wishIds[index]];
                var amount = Math.Min(share, long.MaxValue - wish.magic);
                wish.magic += amount;
                remaining -= amount;
            }

            character.magic.idleMagic = remaining;
            return remaining != idle;
        }

        private static bool DistributeRes3(Character character, List<int> wishIds)
        {
            var idle = character.res3.idleRes3;
            if (idle <= 0)
                return false;

            var remaining = idle;
            for (var index = 0; index < wishIds.Count; index++)
            {
                var slotsLeft = wishIds.Count - index;
                var share = remaining / slotsLeft;
                if (remaining % slotsLeft > 0)
                    share++;

                var wish = character.wishes.wishes[wishIds[index]];
                var amount = Math.Min(share, long.MaxValue - wish.res3);
                wish.res3 += amount;
                remaining -= amount;
            }

            character.res3.idleRes3 = remaining;
            return remaining != idle;
        }

        private static bool TryStartPendingWishes(WishesController controller)
        {
            var started = false;
            while (_pendingWishSlots > 0 && CountRunningWishes(controller) < controller.curWishSlots())
            {
                var nextId = FindNextWish(controller);
                if (nextId < 0 || !SeedWish(controller, nextId))
                    break;

                _pendingWishSlots--;
                started = true;
            }

            return started;
        }

        private static int CountRunningWishes(WishesController controller)
        {
            var count = 0;
            var wishes = controller.character.wishes.wishes;
            for (var id = 0; id < wishes.Count; id++)
            {
                if (IsRunning(wishes[id]))
                    count++;
            }

            return count;
        }

        private static int FindNextWish(WishesController controller)
        {
            controller.constructList();

            for (var index = 0; index < controller.curValidUpgradesList.Count; index++)
            {
                var id = controller.curValidUpgradesList[index];
                if (controller.invalidID(id)
                    || controller.wishLocked(id)
                    || controller.properties[id].difficultyRequirement > controller.character.settings.rebirthDifficulty)
                    continue;

                var wish = controller.character.wishes.wishes[id];
                if (!IsRunning(wish) && wish.level < controller.maxWishLevel(id))
                    return id;
            }

            return -1;
        }

        private static bool SeedWish(WishesController controller, int id)
        {
            var character = controller.character;
            var wish = character.wishes.wishes[id];
            var started = false;

            if (character.idleEnergy > 0)
            {
                wish.energy++;
                character.idleEnergy--;
                started = true;
            }

            if (character.magic.idleMagic > 0)
            {
                wish.magic++;
                character.magic.idleMagic--;
                started = true;
            }

            if (character.res3.idleRes3 > 0)
            {
                wish.res3++;
                character.res3.idleRes3--;
                started = true;
            }

            return started;
        }

        private static void ResetState()
        {
            _pendingWishSlots = 0;
            AutomationThrottle.Reset(ref _lastCheckTime);
            _runningWishIds.Clear();
        }

        private static void SetButtonColor(Button button)
        {
            if (button != null)
                button.image.color = _enabled ? Plugin.ButtonColor_LightBlue : Color.white;
        }
    }
}
