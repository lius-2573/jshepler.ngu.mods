using System;
using HarmonyLib;
using UnityEngine.UI;

namespace jshepler.ngu.mods
{
    [HarmonyPatch]
    internal static class AutoTimeMachineEnergy
    {

        internal static bool Enabled
        {
            get => Options.TimeMachine.AutoAllocateEnergy.Value;
            set => Options.TimeMachine.AutoAllocateEnergy.Value = value;
        }

        private static bool _enabled
        {
            get => Enabled;
            set => Enabled = value;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ButtonShower), "Start")]
        private static void ButtonShower_Start_postfix(ButtonShower __instance)
        {
            var button = __instance.brokenTimeMachine;
            if (button != null)
            {
                button.gameObject.AddComponent<ClickHandlerComponent>()
                    .OnRightClick(e =>
                    {
                        if (!Plugin.ShiftIsDown)
                            return;

                        _enabled = !_enabled;
                        if (_enabled)
                            AutoResourceAllocation.RequestCheck();
                        SetButtonColor(button);
                    });
            }

        }

        [HarmonyPostfix, HarmonyPatch(typeof(ButtonShower), "updateButtons")]
        private static void ButtonShower_updateButtons_postfix(ButtonShower __instance)
        {
            SetButtonColor(__instance.brokenTimeMachine);
        }


        internal static bool IsUsable(TimeMachineController controller)
        {
            if (controller == null || controller.character == null || controller.character.machine == null)
                return false;

            var character = controller.character;
            return character.bossID >= 30
                && character.magic != null;
        }


        internal static long SpeedCapForNextLevel(Character character, TimeMachineController controller)
        {
            var power = (double)character.totalEnergyPower();
            var speedBonus = (double)character.hacksController.totalTMSpeedBonus()
                * character.allChallenges.timeMachineChallenge.TMSpeedBonus()
                * character.cardsController.getBonus(cardBonus.TMSpeed);
            var divider = (double)controller.baseSpeedDivider();
            var level = (double)character.machine.levelSpeed + 1d;
            var denominator = power * speedBonus;

            if (power <= 0d || speedBonus <= 0d || divider <= 0d || level <= 0d
                || double.IsNaN(denominator) || double.IsInfinity(denominator))
                return 0L;

            var cap = 50000d * level * divider / denominator * 1.000002d;
            if (character.settings.rebirthDifficulty >= difficulty.sadistic)
                cap /= controller.sadisticDivider();

            if (cap <= 0d || double.IsNaN(cap))
                return 0L;
            if (double.IsInfinity(cap) || cap >= long.MaxValue)
                return long.MaxValue;

            var result = (long)cap;
            return result < long.MaxValue ? result + 1L : long.MaxValue;
        }
        internal static long MagicCapForNextLevel(Character character, TimeMachineController controller)
        {
            var power = (double)character.totalMagicPower();
            var speedBonus = (double)character.hacksController.totalTMSpeedBonus()
                * character.allChallenges.timeMachineChallenge.TMSpeedBonus()
                * character.cardsController.getBonus(cardBonus.TMSpeed);
            var divider = (double)controller.baseGoldMultiDivider();
            var level = (double)character.machine.levelGoldMulti + 1d;
            var denominator = power * speedBonus;

            if (power <= 0d || speedBonus <= 0d || divider <= 0d || level <= 0d
                || double.IsNaN(denominator) || double.IsInfinity(denominator))
                return 0L;

            var cap = 50000d * level * divider / denominator * 1.000002d;
            if (character.settings.rebirthDifficulty >= difficulty.sadistic)
                cap /= controller.sadisticDivider();

            if (cap <= 0d || double.IsNaN(cap))
                return 0L;
            if (double.IsInfinity(cap) || cap >= long.MaxValue)
                return long.MaxValue;

            var result = (long)cap;
            return result < long.MaxValue ? result + 1L : long.MaxValue;
        }


        private static void SetButtonColor(Button button)
        {
            if (button != null)
                button.image.color = _enabled ? Plugin.ButtonColor_LightBlue : UnityEngine.Color.white;
        }
    }
}
