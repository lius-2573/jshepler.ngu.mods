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
        private enum ResourceKind
        {
            Energy,
            Magic,
            Res3
        }

        private static int _energyRemainderStart;
        private static int _magicRemainderStart;
        private static int _res3RemainderStart;


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
                        AutoResourceAllocation.RequestCheck();
                    }
                    else
                    {
                        _pendingWishSlots = 0;
                        AutomationThrottle.Reset(ref _lastCheckTime);
                    }
                });

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
                AutomationThrottle.Reset(ref _lastCheckTime);
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

        internal static void UpdateAllocationForPriority()
        {
            if (_enabled)
                UpdateAllocation(Plugin.Character?.wishesController);
        }

        private static void UpdateAllocation(WishesController controller)
        {
            if (!IsUsable(controller) || !controller.character.wishes.wishesOn)
                return;

            if (!AutomationThrottle.ShouldRunEverySeconds(ref _lastCheckTime))
                return;

            var started = TryStartPendingWishes(controller);
            var changed = RebalanceResources(controller);
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

        private static bool RebalanceResources(WishesController controller)
        {
            CollectRunningWishes(controller);
            if (_runningWishIds.Count == 0)
                return false;

            var character = controller.character;
            var changed = false;
            changed |= RebalanceResource(character, _runningWishIds, ResourceKind.Energy, ref _energyRemainderStart);
            changed |= RebalanceResource(character, _runningWishIds, ResourceKind.Magic, ref _magicRemainderStart);
            changed |= RebalanceResource(character, _runningWishIds, ResourceKind.Res3, ref _res3RemainderStart);
            return changed;
        }

        private static bool RebalanceResource(Character character, List<int> wishIds, ResourceKind kind, ref int remainderStart)
        {
            var total = GetIdleResource(character, kind);
            for (var index = 0; index < wishIds.Count; index++)
            {
                var wish = character.wishes.wishes[wishIds[index]];
                total = SaturatingAdd(total, GetWishResource(wish, kind));
                SetWishResource(wish, kind, 0L);
            }

            if (total <= 0)
                return false;

            SetIdleResource(character, kind, 0L);

            var share = total / wishIds.Count;
            var remainder = (int)(total % wishIds.Count);
            var start = remainderStart % wishIds.Count;
            if (start < 0)
                start += wishIds.Count;

            for (var index = 0; index < wishIds.Count; index++)
            {
                var distance = (index - start + wishIds.Count) % wishIds.Count;
                var amount = share + (distance < remainder ? 1L : 0L);
                var wish = character.wishes.wishes[wishIds[index]];
                SetWishResource(wish, kind, amount);
            }

            remainderStart = (start + remainder) % wishIds.Count;
            return true;
        }

        private static long GetIdleResource(Character character, ResourceKind kind)
        {
            return kind switch
            {
                ResourceKind.Energy => character.idleEnergy,
                ResourceKind.Magic => character.magic.idleMagic,
                ResourceKind.Res3 => character.res3.idleRes3,
                _ => 0L
            };
        }

        private static void SetIdleResource(Character character, ResourceKind kind, long amount)
        {
            switch (kind)
            {
                case ResourceKind.Energy:
                    character.idleEnergy = amount;
                    break;
                case ResourceKind.Magic:
                    character.magic.idleMagic = amount;
                    break;
                case ResourceKind.Res3:
                    character.res3.idleRes3 = amount;
                    break;
            }
        }

        private static long GetWishResource(Wish wish, ResourceKind kind)
        {
            return kind switch
            {
                ResourceKind.Energy => wish.energy,
                ResourceKind.Magic => wish.magic,
                ResourceKind.Res3 => wish.res3,
                _ => 0L
            };
        }

        private static void SetWishResource(Wish wish, ResourceKind kind, long amount)
        {
            switch (kind)
            {
                case ResourceKind.Energy:
                    wish.energy = amount;
                    break;
                case ResourceKind.Magic:
                    wish.magic = amount;
                    break;
                case ResourceKind.Res3:
                    wish.res3 = amount;
                    break;
            }
        }

        private static long SaturatingAdd(long left, long right)
        {
            if (right <= 0)
                return left;

            if (left >= long.MaxValue - right)
                return long.MaxValue;

            return left + right;
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
            _energyRemainderStart = 0;
            _magicRemainderStart = 0;
            _res3RemainderStart = 0;
            _runningWishIds.Clear();
        }

        private static void SetButtonColor(Button button)
        {
            if (button != null)
                button.image.color = _enabled ? Plugin.ButtonColor_LightBlue : Color.white;
        }
    }
}
