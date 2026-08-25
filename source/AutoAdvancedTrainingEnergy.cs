using System;
using HarmonyLib;
using UnityEngine.UI;

namespace jshepler.ngu.mods
{
    [HarmonyPatch]
    internal static class AutoAdvancedTrainingEnergy
    {

        internal static bool Enabled
        {
            get => Options.AdvancedTraining.AutoAllocateEnergy.Value;
            set => Options.AdvancedTraining.AutoAllocateEnergy.Value = value;
        }

        private static bool _enabled
        {
            get => Enabled;
            set => Enabled = value;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ButtonShower), "Start")]
        private static void ButtonShower_Start_postfix(ButtonShower __instance)
        {
            __instance.advancedTraining.gameObject.AddComponent<ClickHandlerComponent>()
                .OnRightClick(e =>
                {
                    if (!Plugin.ShiftIsDown)
                        return;

                    _enabled = !_enabled;
                    if (_enabled)
                        AutoResourceAllocation.RequestCheck();
                    SetButtonColor(__instance.advancedTraining);
                });

            // Allocation is coordinated by AutoResourceAllocation so feature priorities apply globally.
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ButtonShower), "updateButtons")]
        private static void ButtonShower_updateButtons_postfix(ButtonShower __instance)
        {
            SetButtonColor(__instance.advancedTraining);
        }

        // Return completed training energy to the idle pool so the global scheduler
        // can give it to the configured next-priority feature.
        [HarmonyPrefix, HarmonyPatch(typeof(AllAdvancedTraining), "advanceEnergy")]
        private static bool AllAdvancedTraining_advanceEnergy_prefix(AllAdvancedTraining __instance, int id)
        {
            if (!_enabled || !IsUsable(__instance))
                return true;

            ReturnTrainingEnergy(__instance.character, id);
            return false;
        }


        internal static bool IsUsable(AllAdvancedTraining allTraining)
        {
            if (allTraining == null || allTraining.character == null || !allTraining.advancedTrainingUnlocked())
                return false;

            var character = allTraining.character;
            return character.NGUController != null
                && character.NGU != null
                && character.advancedTraining != null;
        }

        internal static AdvancedTrainingController GetController(AllAdvancedTraining allTraining, int id)
        {
            return id switch
            {
                0 => allTraining.defense,
                1 => allTraining.attack,
                2 => allTraining.block,
                3 => allTraining.wandoosEnergy,
                4 => allTraining.wandoosMagic,
                _ => null
            };
        }


        internal static long TrainingCapForNextLevel(Character character, AdvancedTrainingController controller)
        {
            var power = (double)character.totalEnergyPower();
            var speedBonus = (double)character.totalAdvancedTrainingSpeedBonus();
            var denominator = speedBonus * power;
            if (power <= 0d || speedBonus <= 0d || double.IsNaN(denominator) || double.IsInfinity(denominator))
                return 0L;

            var level = (double)character.advancedTraining.level[controller.id] + 1d;
            var cap = 50d * controller.baseTime * level * Math.Sqrt(power) / denominator * 1.000002d;
            if (cap <= 0d || double.IsNaN(cap))
                return 0L;
            if (cap >= long.MaxValue)
                return long.MaxValue;
            return (long)cap;
        }


        internal static void ReturnTrainingEnergy(Character character, int id)
        {
            if (id < 0 || id >= character.advancedTraining.energy.Length)
                return;

            var amount = character.advancedTraining.energy[id];
            if (amount <= 0)
                return;

            character.advancedTraining.energy[id] = 0L;
            character.idleEnergy += amount;


            var controller = GetController(character.advancedTrainingController, id);
            controller?.updateText();
        }

        private static void SetButtonColor(Button button)
        {
            if (button != null)
                button.image.color = _enabled ? Plugin.ButtonColor_LightBlue : UnityEngine.Color.white;
        }
    }
}
