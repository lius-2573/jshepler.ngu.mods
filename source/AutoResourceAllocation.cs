using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine.UI;

namespace jshepler.ngu.mods
{
    [HarmonyPatch]
    internal static class AutoResourceAllocation
    {
        private enum Feature
        {
            Augment,
            BloodMagic,
            TimeMachine,
            AdvancedTraining,
            Wandoos,
            NGU,
            Wishes
        }

        private static readonly Feature[] DefaultPriority =
        {
            Feature.Augment,
            Feature.BloodMagic,
            Feature.TimeMachine,
            Feature.AdvancedTraining,
            Feature.Wandoos,
            Feature.NGU,
            Feature.Wishes
        };

        private static int _lastCheckFrame = -1;
        private static bool _updateRegistered;
        private static string _cachedPriority;
        private static Feature[] _cachedOrder;

        [HarmonyPostfix, HarmonyPatch(typeof(Character), "Start")]
        private static void Character_Start_postfix()
        {
            if (_updateRegistered)
                return;

            Plugin.OnUpdate += (o, e) => UpdateAllocation();
            _updateRegistered = true;
            RequestCheck();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ButtonShower), "Start")]
        private static void ButtonShower_Start_postfix(ButtonShower __instance)
        {
            AttachToggle(__instance.augmentation, Options.AutoAllocation.Augment);
            AttachToggle(__instance.bloodMagic, Options.AutoAllocation.BloodMagic);
            AttachToggle(__instance.wandoos, Options.AutoAllocation.Wandoos);
            AttachToggle(__instance.ngu, Options.AutoAllocation.NGU);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ButtonShower), "updateButtons")]
        private static void ButtonShower_updateButtons_postfix(ButtonShower __instance)
        {
            SetButtonColor(__instance.augmentation, Options.AutoAllocation.Augment.Value);
            SetButtonColor(__instance.bloodMagic, Options.AutoAllocation.BloodMagic.Value);
            SetButtonColor(__instance.wandoos, Options.AutoAllocation.Wandoos.Value);
            SetButtonColor(__instance.ngu, Options.AutoAllocation.NGU.Value);
        }

        private static void AttachToggle(Button button, ConfigEntry<bool> option)
        {
            if (button == null || option == null)
                return;

            button.gameObject.AddComponent<ClickHandlerComponent>()
                .OnRightClick(e =>
                {
                    if (!Plugin.ShiftIsDown)
                        return;

                    option.Value = !option.Value;
                    if (option.Value)
                        RequestCheck();
                    SetButtonColor(button, option.Value);
                });

            SetButtonColor(button, option.Value);
        }

        private static void SetButtonColor(Button button, bool enabled)
        {
            if (button != null)
                button.image.color = enabled ? Plugin.ButtonColor_LightBlue : UnityEngine.Color.white;
        }

        internal static void RequestCheck()
        {
            AutomationThrottle.Reset(ref _lastCheckFrame);
        }

        private static void UpdateAllocation()
        {
            if (!AutomationThrottle.ShouldRunEveryFrames(ref _lastCheckFrame))
                return;

            var character = Plugin.Character;
            if (character == null)
                return;

            var order = GetPriorityOrder();
            for (var index = 0; index < order.Length; index++)
            {
                switch (order[index])
                {
                    case Feature.Augment:
                        AllocateAugmentEnergy(character);
                        break;
                    case Feature.BloodMagic:
                        AllocateBloodMagic(character);
                        break;
                    case Feature.TimeMachine:
                        AllocateTimeMachine(character);
                        break;
                    case Feature.AdvancedTraining:
                        AllocateAdvancedTraining(character);
                        break;
                    case Feature.Wandoos:
                        AllocateWandoos(character);
                        break;
                    case Feature.NGU:
                        AllocateNgu(character);
                        break;
                    case Feature.Wishes:
                        AutoWishes.UpdateAllocationForPriority();
                        break;
                }
            }
        }

        private static Feature[] GetPriorityOrder()
        {
            var configured = Options.AutoAllocation.Priority?.Value ?? string.Empty;
            if (_cachedOrder != null && string.Equals(_cachedPriority, configured, StringComparison.Ordinal))
                return _cachedOrder;

            var order = new List<Feature>(DefaultPriority.Length);
            var tokens = configured.Split(new[] { ',', ';', '|', '>', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < tokens.Length; index++)
            {
                if (!TryParseFeature(tokens[index], out var feature) || order.Contains(feature))
                    continue;

                order.Add(feature);
            }

            for (var index = 0; index < DefaultPriority.Length; index++)
            {
                if (!order.Contains(DefaultPriority[index]))
                    order.Add(DefaultPriority[index]);
            }

            _cachedPriority = configured;
            _cachedOrder = order.ToArray();
            return _cachedOrder;
        }

        private static bool TryParseFeature(string token, out Feature feature)
        {
            if (Enum.TryParse(token, true, out feature))
                return true;

            switch (token.Trim().ToUpperInvariant())
            {
                case "AUGMENTS":
                    feature = Feature.Augment;
                    return true;
                case "BLOOD":
                case "RITUAL":
                case "RITUALS":
                    feature = Feature.BloodMagic;
                    return true;
                case "TM":
                    feature = Feature.TimeMachine;
                    return true;
                case "AT":
                    feature = Feature.AdvancedTraining;
                    return true;
                default:
                    feature = default;
                    return false;
            }
        }

        private static void AllocateAugmentEnergy(Character character)
        {
            if (!Options.AutoAllocation.Augment.Value
                || character.augmentsController == null
                || character.augmentsController.augments == null
                || character.augments == null
                || character.augments.augs == null)
                return;

            var controllers = character.augmentsController.augments;
            var count = Math.Min(character.augments.augs.Length, controllers.Length);
            for (var id = 0; id < count; id++)
            {
                var controller = controllers[id];
                if (controller == null || controller.augLocked() || controller.hitAugmentTarget())
                    continue;

                var cap = CapFromProgress(controller.getAugProgressPerTick(1L));
                var augment = character.augments.augs[id];
                if (augment == null)
                    continue;

                var amount = TakeEnergy(character, augment.augEnergy, cap, true);
                if (amount <= 0)
                    continue;

                augment.augEnergy += amount;
                controller.updateAugTexts();
            }
        }

        private static void AllocateBloodMagic(Character character)
        {
            var controller = character.bloodMagicController;
            if (!Options.AutoAllocation.BloodMagic.Value
                || controller == null
                || character.bloodMagic == null
                || controller.bloodMagics == null
                || character.bloodMagic.ritual == null)
                return;

            var count = Math.Min(controller.ritualsUnlocked(), controller.bloodMagics.Length);
            count = Math.Min(count, character.bloodMagic.ritual.Count);
            for (var id = 0; id < count; id++)
            {
                var ritualController = controller.bloodMagics[id];
                var ritual = character.bloodMagic.ritual[id];
                if (ritualController == null || ritual == null)
                    continue;

                var amount = TakeMagic(character, ritual.magic, ritualController.capValue(), true);
                if (amount <= 0)
                    continue;

                ritual.magic += amount;
                ritualController.updateBloodMagicText();
            }
        }

        private static void AllocateTimeMachine(Character character)
        {
            if (!AutoTimeMachineEnergy.Enabled)
                return;

            var controller = character.timeMachineController;
            if (!AutoTimeMachineEnergy.IsUsable(controller))
                return;

            var machine = character.machine;
            if (!controller.hitSpeedLevelTarget())
            {
                var amount = TakeEnergy(
                    character,
                    machine.speedEnergy,
                    AutoTimeMachineEnergy.SpeedCapForNextLevel(character, controller),
                    true);
                if (amount > 0)
                {
                    machine.speedEnergy += amount;
                    controller.updateSpeedText();
                }
            }

            if (!controller.hitMultiLevelTarget())
            {
                var amount = TakeMagic(
                    character,
                    machine.goldMultiMagic,
                    AutoTimeMachineEnergy.MagicCapForNextLevel(character, controller),
                    true);
                if (amount > 0)
                {
                    machine.goldMultiMagic += amount;
                    controller.updateGoldMultiText();
                }
            }
        }

        private static void AllocateAdvancedTraining(Character character)
        {
            if (!AutoAdvancedTrainingEnergy.Enabled)
                return;

            var allTraining = character.advancedTrainingController;
            if (!AutoAdvancedTrainingEnergy.IsUsable(allTraining))
                return;

            for (var id = 0; id < allTraining.size(); id++)
            {
                if (allTraining.reachedTarget(id))
                {
                    AutoAdvancedTrainingEnergy.ReturnTrainingEnergy(character, id);
                    continue;
                }

                var controller = AutoAdvancedTrainingEnergy.GetController(allTraining, id);
                if (controller == null || controller.baseTime <= 0f)
                    continue;

                var amount = TakeEnergy(
                    character,
                    character.advancedTraining.energy[id],
                    AutoAdvancedTrainingEnergy.TrainingCapForNextLevel(character, controller),
                    true);
                if (amount <= 0)
                    continue;

                character.advancedTraining.energy[id] += amount;
                controller.updateText();
            }
        }

        private static void AllocateWandoos(Character character)
        {
            var controller = character.wandoos98Controller;
            if (!Options.AutoAllocation.Wandoos.Value
                || controller == null
                || character.wandoos98 == null
                || !character.settings.wandoos98On
                || !character.wandoos98.installed)
                return;

            var energy = TakeEnergy(
                character,
                character.wandoos98.wandoosEnergy,
                controller.capAmountEnergy(),
                true);
            if (energy > 0)
                character.wandoos98.wandoosEnergy += energy;

            var magic = TakeMagic(
                character,
                character.wandoos98.wandoosMagic,
                controller.capAmountMagic(),
                true);
            if (magic > 0)
                character.wandoos98.wandoosMagic += magic;

            if (energy > 0 || magic > 0)
                controller.updateText();
        }

        private static void AllocateNgu(Character character)
        {
            var allNgu = character.NGUController;
            if (!Options.AutoAllocation.NGU.Value
                || allNgu == null
                || character.NGU == null
                || allNgu.NGU == null
                || allNgu.NGUMagic == null
                || character.NGU.skills == null
                || character.NGU.magicSkills == null)
                return;

            var energyCount = Math.Min(character.NGU.skills.Count, allNgu.NGU.Length);
            for (var id = 0; id < energyCount; id++)
            {
                var controller = allNgu.NGU[id];
                var skill = character.NGU.skills[id];
                if (controller == null || skill == null || allNgu.reachedTarget(id))
                    continue;

                var amount = TakeEnergy(character, skill.energy, allNgu.energyNGUCapAmount(id), false);
                if (amount <= 0)
                    continue;

                skill.energy += amount;
                controller.updateText();
            }

            var magicCount = Math.Min(character.NGU.magicSkills.Count, allNgu.NGUMagic.Length);
            for (var id = 0; id < magicCount; id++)
            {
                var controller = allNgu.NGUMagic[id];
                var skill = character.NGU.magicSkills[id];
                if (controller == null || skill == null || allNgu.reachedMagicTarget(id))
                    continue;

                var amount = TakeMagic(character, skill.magic, allNgu.magicNGUCapAmount(id), false);
                if (amount <= 0)
                    continue;

                skill.magic += amount;
                controller.updateText();
            }
        }

        private static long CapFromProgress(float progressPerUnit)
        {
            if (float.IsNaN(progressPerUnit) || progressPerUnit <= 0f)
                return 0L;
            if (float.IsInfinity(progressPerUnit))
                return 1L;

            var cap = Math.Ceiling(1.000002d / progressPerUnit);
            if (cap >= long.MaxValue)
                return long.MaxValue;
            if (cap < 1d)
                return 1L;
            return (long)cap;
        }

        private static long TakeEnergy(Character character, long current, long cap, bool fromNgu)
        {
            if (cap <= current)
                return 0L;

            var needed = cap - Math.Max(current, 0L);
            var taken = Math.Min(needed, Math.Max(character.idleEnergy, 0L));
            character.idleEnergy -= taken;
            needed -= taken;

            if (fromNgu && needed > 0)
                taken += ReleaseFromNguEnergy(character, needed);

            return taken;
        }
        private static long TakeMagic(Character character, long current, long cap, bool fromNgu)
        {
            if (cap <= current || character.magic == null)
                return 0L;

            var needed = cap - Math.Max(current, 0L);
            var taken = Math.Min(needed, Math.Max(character.magic.idleMagic, 0L));
            character.magic.idleMagic -= taken;
            needed -= taken;

            if (fromNgu && needed > 0)
                taken += ReleaseFromNguMagic(character, needed);

            return taken;
        }

        private static long ReleaseFromNguEnergy(Character character, long amount)
        {
            var nguController = character.NGUController;
            if (nguController == null || character.NGU == null || nguController.NGU == null || character.NGU.skills == null)
                return 0L;

            var released = 0L;
            var count = Math.Min(character.NGU.skills.Count, nguController.NGU.Length);
            for (var pass = 0; pass < 2 && released < amount; pass++)
            {
                for (var id = 0; id < count && released < amount; id++)
                {
                    if (nguController.NGU[id] == null || (pass == 0 && nguController.reachedTarget(id)))
                        continue;

                    var skill = character.NGU.skills[id];
                    if (skill == null)
                        continue;

                    var take = Math.Min(amount - released, Math.Max(skill.energy, 0L));
                    if (take <= 0)
                        continue;

                    skill.energy -= take;
                    released += take;
                    nguController.NGU[id].refresh();
                }
            }

            return released;
        }

        private static long ReleaseFromNguMagic(Character character, long amount)
        {
            var nguController = character.NGUController;
            if (nguController == null || character.NGU == null || nguController.NGUMagic == null || character.NGU.magicSkills == null)
                return 0L;

            var released = 0L;
            var count = Math.Min(character.NGU.magicSkills.Count, nguController.NGUMagic.Length);
            for (var pass = 0; pass < 2 && released < amount; pass++)
            {
                for (var id = 0; id < count && released < amount; id++)
                {
                    if (nguController.NGUMagic[id] == null || (pass == 0 && nguController.reachedMagicTarget(id)))
                        continue;

                    var skill = character.NGU.magicSkills[id];
                    if (skill == null)
                        continue;

                    var take = Math.Min(amount - released, Math.Max(skill.magic, 0L));
                    if (take <= 0)
                        continue;

                    skill.magic -= take;
                    released += take;
                    nguController.NGUMagic[id].refresh();
                }
            }

            return released;
        }
    }
}
