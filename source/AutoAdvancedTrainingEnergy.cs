using System;
using HarmonyLib;
using UnityEngine.UI;

namespace jshepler.ngu.mods
{
    [HarmonyPatch]
    internal static class AutoAdvancedTrainingEnergy
    {

        private static bool _enabled
        {
            get => Options.AdvancedTraining.AutoAllocateEnergy.Value;
            set => Options.AdvancedTraining.AutoAllocateEnergy.Value = value;
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
                    SetButtonColor(__instance.advancedTraining);
                });

            Plugin.OnUpdate += (o, e) => UpdateAllocation();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ButtonShower), "updateButtons")]
        private static void ButtonShower_updateButtons_postfix(ButtonShower __instance)
        {
            SetButtonColor(__instance.advancedTraining);
        }

        // The game's auto-advance would move completed AT energy to another AT bar.
        // Intercept it so completed training energy can be returned to NGU instead.
        [HarmonyPrefix, HarmonyPatch(typeof(AllAdvancedTraining), "advanceEnergy")]
        private static bool AllAdvancedTraining_advanceEnergy_prefix(AllAdvancedTraining __instance, int id)
        {
            if (!_enabled || !IsUsable(__instance))
                return true;

            ReturnTrainingEnergy(__instance.character, id);
            return false;
        }

        private static void UpdateAllocation()
        {
            if (!_enabled)
                return;

            var character = Plugin.Character;
            var allTraining = character?.advancedTrainingController;
            if (!IsUsable(allTraining))
                return;

            // Process every unlocked AT bar in the same update, rather than selecting one active bar.
            for (var id = 0; id < allTraining.size(); id++)
            {
                if (allTraining.reachedTarget(id))
                    ReturnTrainingEnergy(character, id);
                else
                    AllocateTrainingCap(character, allTraining, id);
            }
        }

        private static bool IsUsable(AllAdvancedTraining allTraining)
        {
            if (allTraining == null || allTraining.character == null || !allTraining.advancedTrainingUnlocked())
                return false;

            var character = allTraining.character;
            return character.NGUController != null
                && character.NGU != null
                && character.advancedTraining != null;
        }

        private static AdvancedTrainingController GetController(AllAdvancedTraining allTraining, int id)
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

        private static void AllocateTrainingCap(Character character, AllAdvancedTraining allTraining, int id)
        {
            var controller = GetController(allTraining, id);
            if (controller == null || controller.baseTime <= 0f)
                return;

            var currentEnergy = character.advancedTraining.energy[id];
            var cap = TrainingCapForNextLevel(character, controller);
            if (cap <= currentEnergy)
                return;

            var needed = cap - currentEnergy;
            var fromIdle = Math.Min(needed, Math.Max(character.idleEnergy, 0L));
            character.idleEnergy -= fromIdle;
            needed -= fromIdle;

            if (needed > 0)
                needed -= ReleaseFromNgu(character, needed);

            var allocated = cap - currentEnergy - needed;
            if (allocated <= 0)
                return;

            character.advancedTraining.energy[id] += allocated;
            controller.updateText();
        }

        private static long TrainingCapForNextLevel(Character character, AdvancedTrainingController controller)
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

        private static long ReleaseFromNgu(Character character, long amount)
        {
            var released = 0L;
            var nguControllers = character.NGUController.NGU;
            var count = Math.Min(character.NGU.skills.Count, nguControllers.Length);

            // Prefer NGUs that are still running; only drain reached targets as a last resort.
            for (var pass = 0; pass < 2 && released < amount; pass++)
            {
                for (var id = 0; id < count && released < amount; id++)
                {
                    if (nguControllers[id] == null || (pass == 0 && character.NGUController.reachedTarget(id)))
                        continue;

                    var available = character.NGU.skills[id].energy;
                    var take = Math.Min(amount - released, Math.Max(available, 0L));
                    if (take <= 0)
                        continue;

                    character.NGU.skills[id].energy -= take;
                    released += take;
                    nguControllers[id].refresh();
                }
            }

            return released;
        }

        private static void ReturnTrainingEnergy(Character character, int id)
        {
            if (id < 0 || id >= character.advancedTraining.energy.Length)
                return;

            var amount = character.advancedTraining.energy[id];
            if (amount <= 0)
                return;

            character.advancedTraining.energy[id] = 0L;
            character.idleEnergy += amount;

            var nguControllers = character.NGUController.NGU;
            var count = Math.Min(character.NGU.skills.Count, nguControllers.Length);
            var targetId = -1;
            for (var nguId = 0; nguId < count; nguId++)
            {
                if (nguControllers[nguId] != null && !character.NGUController.reachedTarget(nguId))
                {
                    targetId = nguId;
                    break;
                }
            }

            if (targetId >= 0)
            {
                character.NGU.skills[targetId].energy += amount;
                character.idleEnergy -= amount;
                nguControllers[targetId].refresh();
            }

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
