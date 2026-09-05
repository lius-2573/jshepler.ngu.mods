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
            Yggdrasil,
            Augment,
            BloodMagic,
            TimeMachine,
            AdvancedTraining,
            Wandoos,
            NGU,
            Hacks,
            Wishes
        }

        private static readonly Feature[] DefaultPriority =
        {
            Feature.Yggdrasil,
            Feature.Augment,
            Feature.BloodMagic,
            Feature.TimeMachine,
            Feature.AdvancedTraining,
            Feature.Wandoos,
            Feature.NGU,
            Feature.Hacks,
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
            AttachToggle(__instance.bloodMagic, Options.AutoAllocation.BloodMagic, false);
            AttachToggle(__instance.wandoos, Options.AutoAllocation.Wandoos);
            AttachToggle(__instance.ngu, Options.AutoAllocation.NGU);
            AttachToggle(__instance.hacks, Options.AutoAllocation.Hacks);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ButtonShower), "updateButtons")]
        private static void ButtonShower_updateButtons_postfix(ButtonShower __instance)
        {
            SetButtonColor(__instance.augmentation, Options.AutoAllocation.Augment.Value);
            SetButtonColor(__instance.wandoos, Options.AutoAllocation.Wandoos.Value);
            SetButtonColor(__instance.ngu, Options.AutoAllocation.NGU.Value);
            SetButtonColor(__instance.hacks, Options.AutoAllocation.Hacks.Value);
        }

        private static void AttachToggle(Button button, ConfigEntry<bool> option, bool showColor = true)
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
                    if (showColor)
                        SetButtonColor(button, option.Value);
                });

            if (showColor)
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

            AutoWishes.PrepareForPriority();
            ReleaseManagedResources(character);
            // wandoos gets a guaranteed top-up before the priority loop: its progress
            // stalls completely at wandoosEnergy == 0 (vanilla advance*Progress early-
            // returns), and during bootup / high OS levels its cap exceeds the whole
            // pool, so a starved wandoos could never recover on its own. The reclaim
            // above keeps priority-based yielding intact - higher-priority modules
            // still drain the pool first in the loop below (which skips Wandoos).
            AllocateWandoos(character);
            var order = GetPriorityOrder();
            for (var index = 0; index < order.Length; index++)
            {
                switch (order[index])
                {
                    case Feature.Yggdrasil:
                        ActivateYggdrasilFruits(character);
                        break;
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
                        break; // topped up before the loop; Priority position no longer starves it
                    case Feature.NGU:
                        AllocateNgu(character);
                        break;
                    case Feature.Hacks:
                        AllocateHacks(character);
                        break;
                    case Feature.Wishes:
                        AutoWishes.UpdateAllocationForPriority();
                        break;
                }
            }
        }
        private static void ReleaseManagedResources(Character character)
        {
            if (Options.AutoAllocation.Augment.Value)
                ReleaseAugmentResources(character);
            if (Options.AutoAllocation.BloodMagic.Value)
                ReleaseBloodMagicResources(character);
            if (AutoTimeMachineEnergy.Enabled)
                ReleaseTimeMachineResources(character);
            if (AutoAdvancedTrainingEnergy.Enabled)
                ReleaseAdvancedTrainingResources(character);
            if (Options.AutoAllocation.Wandoos.Value)
                ReleaseWandoosResources(character);
            if (Options.AutoAllocation.NGU.Value)
                ReleaseNguResources(character);
        }

        private static void ReleaseAugmentResources(Character character)
        {
            if (character.augmentsController == null
                || character.augmentsController.augments == null
                || character.augments == null
                || character.augments.augs == null)
                return;

            var controllers = character.augmentsController.augments;
            var count = Math.Min(character.augments.augs.Length, controllers.Length);
            for (var id = 0; id < count; id++)
            {
                var augment = character.augments.augs[id];
                if (augment == null)
                    continue;

                var energy = augment.augEnergy;
                var upgradeEnergy = augment.upgradeEnergy;
                augment.augEnergy = 0L;
                augment.upgradeEnergy = 0L;
                AddIdleEnergy(character, energy);
                AddIdleEnergy(character, upgradeEnergy);

                var controller = controllers[id];
                if (controller != null && (energy > 0L || upgradeEnergy > 0L))
                {
                    controller.updateAugTexts();
                    controller.updateUpgradeTexts();
                }
            }
        }

        private static void ReleaseBloodMagicResources(Character character)
        {
            var controller = character.bloodMagicController;
            if (controller == null || character.bloodMagic == null || character.bloodMagic.ritual == null)
                return;

            for (var id = 0; id < character.bloodMagic.ritual.Count; id++)
            {
                var ritual = character.bloodMagic.ritual[id];
                if (ritual == null)
                    continue;

                var magic = ritual.magic;
                ritual.magic = 0L;
                AddIdleMagic(character, magic);

                if (magic > 0L && controller.bloodMagics != null && id < controller.bloodMagics.Length
                    && controller.bloodMagics[id] != null)
                    controller.bloodMagics[id].updateBloodMagicText();
            }
        }

        private static void ReleaseTimeMachineResources(Character character)
        {
            var controller = character.timeMachineController;
            if (!AutoTimeMachineEnergy.IsUsable(controller))
                return;

            var speedEnergy = character.machine.speedEnergy;
            var goldMultiMagic = character.machine.goldMultiMagic;
            character.machine.speedEnergy = 0L;
            character.machine.goldMultiMagic = 0L;
            AddIdleEnergy(character, speedEnergy);
            AddIdleMagic(character, goldMultiMagic);

            if (speedEnergy > 0L)
                controller.updateSpeedText();
            if (goldMultiMagic > 0L)
                controller.updateGoldMultiText();
        }

        private static void ReleaseAdvancedTrainingResources(Character character)
        {
            var allTraining = character.advancedTrainingController;
            if (!AutoAdvancedTrainingEnergy.IsUsable(allTraining))
                return;

            for (var id = 0; id < allTraining.size(); id++)
            {
                var amount = character.advancedTraining.energy[id];
                if (amount <= 0L)
                    continue;

                character.advancedTraining.energy[id] = 0L;
                AddIdleEnergy(character, amount);
                AutoAdvancedTrainingEnergy.GetController(allTraining, id)?.updateText();
            }
        }

        private static void ReleaseWandoosResources(Character character)
        {
            if (character.wandoos98 == null)
                return;

            var energy = character.wandoos98.wandoosEnergy;
            var magic = character.wandoos98.wandoosMagic;
            character.wandoos98.wandoosEnergy = 0L;
            character.wandoos98.wandoosMagic = 0L;
            AddIdleEnergy(character, energy);
            AddIdleMagic(character, magic);

            if ((energy > 0L || magic > 0L) && character.wandoos98Controller != null)
                character.wandoos98Controller.updateText();
        }

        private static void ReleaseNguResources(Character character)
        {
            var allNgu = character.NGUController;
            if (allNgu == null || character.NGU == null)
                return;

            if (character.NGU.skills != null && allNgu.NGU != null)
            {
                var count = Math.Min(character.NGU.skills.Count, allNgu.NGU.Length);
                for (var id = 0; id < count; id++)
                {
                    var skill = character.NGU.skills[id];
                    if (skill == null)
                        continue;

                    var energy = skill.energy;
                    skill.energy = 0L;
                    AddIdleEnergy(character, energy);
                    if (energy > 0L && allNgu.NGU[id] != null)
                        allNgu.NGU[id].updateText();
                }
            }

            if (character.NGU.magicSkills != null && allNgu.NGUMagic != null)
            {
                var count = Math.Min(character.NGU.magicSkills.Count, allNgu.NGUMagic.Length);
                for (var id = 0; id < count; id++)
                {
                    var skill = character.NGU.magicSkills[id];
                    if (skill == null)
                        continue;

                    var magic = skill.magic;
                    skill.magic = 0L;
                    AddIdleMagic(character, magic);
                    if (magic > 0L && allNgu.NGUMagic[id] != null)
                        allNgu.NGUMagic[id].updateText();
                }
            }
        }

        private static void AddIdleEnergy(Character character, long amount)
        {
            if (amount <= 0L)
                return;
            character.idleEnergy = SaturatingAdd(character.idleEnergy, amount);
        }

        private static void AddIdleMagic(Character character, long amount)
        {
            if (amount <= 0L || character.magic == null)
                return;
            character.magic.idleMagic = SaturatingAdd(character.magic.idleMagic, amount);
        }

        private static long SaturatingAdd(long left, long right)
        {
            if (right <= 0L)
                return left;
            if (left >= long.MaxValue - right)
                return long.MaxValue;
            return left + right;
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

        // fruits whose permanent auto-activation was NOT bought with EXP must be activated manually
        // by the player, paying the one-time activation cost (FruitController.activate) after every
        // harvest/rebirth; this mirrors that activation from the idle pool (no tooltips, no partial
        // payment - same invariant as vanilla: idle covers the full cost, idle and cur both drop)
        private static void ActivateYggdrasilFruits(Character character)
        {
            if (!Options.Yggdrasil.AutoActivate.Value
                || character.yggdrasil == null
                || character.yggdrasil.fruits == null
                || character.yggdrasilController == null
                || character.yggdrasilController.activationCost == null
                || character.yggdrasilController.usesEnergy == null
                || character.magic == null)
                return;

            var count = Math.Min(
                Math.Min(character.yggdrasil.fruits.Count, character.yggdrasilController.activationCost.Count),
                character.yggdrasilController.usesEnergy.Count);
            for (var id = 0; id < count; id++)
            {
                var fruit = character.yggdrasil.fruits[id];

                // permCostPaid fruits are auto-activated for free by AllYggdrasil.updateFruitTimers
                if (fruit == null || fruit.activated || fruit.permCostPaid || fruit.maxTier <= 0L)
                    continue;

                var cost = character.yggdrasilController.activationCost[id];
                if (character.yggdrasilController.usesEnergy[id])
                {
                    if (character.idleEnergy < cost)
                        continue;

                    character.idleEnergy -= cost;
                    character.curEnergy -= cost;
                }
                else
                {
                    if (character.magic.idleMagic < cost)
                        continue;

                    character.magic.idleMagic -= cost;
                    character.magic.curMagic -= cost;
                }

                fruit.activate();
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
                var augment = character.augments.augs[id];
                if (controller == null || augment == null)
                    continue;

                var changed = false;
                if (!controller.augLocked() && !controller.hitAugmentTarget())
                {
                    var amount = TakeEnergy(
                        character,
                        augment.augEnergy,
                        AugmentCapForNextLevel(character, controller, false));
                    if (amount > 0L)
                    {
                        augment.augEnergy += amount;
                        changed = true;
                    }
                }

                if (!controller.upgradeLocked() && !controller.hitUpgradeTarget())
                {
                    var amount = TakeEnergy(
                        character,
                        augment.upgradeEnergy,
                        AugmentCapForNextLevel(character, controller, true));
                    if (amount > 0L)
                    {
                        augment.upgradeEnergy += amount;
                        changed = true;
                    }
                }

                if (changed)
                {
                    controller.updateAugTexts();
                    controller.updateUpgradeTexts();
                }
            }
        }

        private static long AugmentCapForNextLevel(Character character, AugmentController controller, bool upgrade)
        {
            var id = controller.id;
            var augment = character.augments.augs[id];
            var level = (upgrade ? augment.upgradeLevel : augment.augLevel) + 1d;
            var rebirthDifficulty = character.settings.rebirthDifficulty;
            var divider = rebirthDifficulty switch
            {
                difficulty.normal => upgrade
                    ? character.augmentsController.normalUpgradeSpeedDividers[id]
                    : character.augmentsController.normalAugSpeedDividers[id],
                difficulty.evil => upgrade
                    ? character.augmentsController.evilUpgradeSpeedDividers[id]
                    : character.augmentsController.evilAugSpeedDividers[id],
                difficulty.sadistic => upgrade
                    ? character.augmentsController.sadisticUpgradeSpeedDividers[id]
                    : character.augmentsController.sadisticAugSpeedDividers[id],
                _ => 0f
            };

            var power = (double)character.totalEnergyPower();
            var speedBonus = (double)(1f + character.inventoryController.bonuses[specType.Augs])
                * character.inventory.macguffinBonuses[12]
                * character.hacksController.totalAugSpeedBonus()
                * character.adventureController.itopod.totalAugSpeedBonus()
                * character.cardsController.getBonus(cardBonus.augSpeed)
                * (1d + character.allChallenges.noAugsChallenge.evilCompletions() * 0.05d);
            if (character.allChallenges.noAugsChallenge.completions() >= 1)
                speedBonus *= 1.100000023841858d;
            if (character.allChallenges.noAugsChallenge.evilCompletions()
                >= character.allChallenges.noAugsChallenge.maxCompletions)
                speedBonus *= 1.25d;

            if (power <= 0d || speedBonus <= 0d || divider <= 0f || level <= 0d)
                return 0L;

            var scale = rebirthDifficulty >= difficulty.sadistic
                ? (double)controller.sadisticDivider()
                : 50000d;
            var cap = scale * divider * level / (power * speedBonus) * 1.000002d;
            return CapFromDouble(cap);
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

                var amount = TakeMagic(character, ritual.magic, ritualController.capValue());
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
                    AutoTimeMachineEnergy.SpeedCapForNextLevel(character, controller));
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
                    AutoTimeMachineEnergy.MagicCapForNextLevel(character, controller));
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
                    AutoAdvancedTrainingEnergy.TrainingCapForNextLevel(character, controller));
                if (amount <= 0)
                    continue;

                character.advancedTraining.energy[id] += amount;
                controller.updateText();
            }
        }

        // wandoos semantics: progress per tick is wandoosEnergy * speed / baseTime and
        // vanilla's advance*Progress returns immediately while wandoosEnergy == 0; the
        // per-tick level gain caps at +1, so the optimal holding is exactly
        // capAmountEnergy (= baseTime / speed). During bootup (or with high OS levels)
        // that cap can far exceed the player's total energy - in that case inject every
        // free scrap so the bar progresses as fast as the pool allows. settings.wandoos98On
        // ("power on", ctor default false, only set by eating the Wandoos 98 startup disc,
        // no UI toggle) gates vanilla progress; auto power-on once installed, otherwise the
        // allocator would silently stall forever.
        private static void AllocateWandoos(Character character)
        {
            var controller = character.wandoos98Controller;
            if (!Options.AutoAllocation.Wandoos.Value)
                return;
            if (controller == null || character.wandoos98 == null)
            {
                Plugin.LogInfo("[wandoos-diag] skip: controller/wandoos98 null");
                return;
            }
            if (!character.wandoos98.installed)
            {
                Plugin.LogInfo("[wandoos-diag] skip: not installed");
                return;
            }

            if (!character.settings.wandoos98On)
            {
                character.settings.wandoos98On = true;
                Plugin.LogInfo("[wandoos-diag] auto power-on (wandoos98On was false)");
            }

            var capEnergy = controller.capAmountEnergy();
            var capMagic = controller.capAmountMagic();
            var idleE = Math.Max(character.idleEnergy, 0L);
            var idleM = character.magic == null ? 0L : Math.Max(character.magic.idleMagic, 0L);
            // capAmount = (long)(base/speed) + 1; during MEH bootup vanilla's speed
            // chain evaluates to NaN, and (long)NaN == long.MinValue - a garbage cap
            // that makes every consumer bail (cap <= current) and nothing allocate.
            // Any cap <= 0 is garbage (real caps are >= 1): treat as unbounded and
            // inject the whole free pool instead.
            var fillE = capEnergy <= 0L || capEnergy > character.totalCapEnergy();
            var fillM = capMagic <= 0L || capMagic > character.totalCapMagic();

            var energy = TakeEnergy(
                character,
                character.wandoos98.wandoosEnergy,
                capEnergy,
                fillE);
            if (energy > 0)
                character.wandoos98.wandoosEnergy += energy;

            var magic = TakeMagic(
                character,
                character.wandoos98.wandoosMagic,
                capMagic,
                fillM);
            if (magic > 0)
                character.wandoos98.wandoosMagic += magic;

            Plugin.LogInfo($"[wandoos-diag] capE={capEnergy} idleE={idleE} takenE={energy} fillE={fillE} " +
                           $"capM={capMagic} idleM={idleM} takenM={magic} fillM={fillM} " +
                           $"haveE={character.wandoos98.wandoosEnergy} haveM={character.wandoos98.wandoosMagic}");

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

                var amount = TakeEnergy(character, skill.energy, allNgu.energyNGUCapAmount(id));
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

                var amount = TakeMagic(character, skill.magic, allNgu.magicNGUCapAmount(id));
                if (amount <= 0)
                    continue;

                skill.magic += amount;
                controller.updateText();
            }
        }

        // tops up idle res3 into upgradeable hacks (id 0..14, THE END hack id 15 takes no direct
        // allocation) up to the amount needed for the next level; res3 already sitting in a hack
        // is never reclaimed, only the shortfall is topped up from the idle pool on each check
        // (same control pattern as wishes, which keep their res3 resident); when wishes are also
        // being auto-allocated and Hacks has higher priority than Wishes in AutoAllocation.Priority,
        // the total amount allocated across all hacks is limited to half the res3 cap so wishes
        // keep the other half
        private static void AllocateHacks(Character character)
        {
            var controller = character.hacksController;
            if (!Options.AutoAllocation.Hacks.Value
                || controller == null
                || character.hacks == null
                || character.hacks.hacks == null
                || character.hacks.hacks.Count == 0
                || character.res3 == null
                || !character.hacks.hacksOn)
                return;

            var budget = long.MaxValue;
            if (Options.Wishes.AutoAllocate.Value && HacksBeforeWishes())
                budget = Math.Max(character.totalCapRes3() / 2L, 0L);

            var count = Math.Min(character.hacks.hacks.Count, controller.properties?.Count ?? 0);
            if (count <= 0)
                return;

            var last = count - 1; // skip THE END hack
            var used = 0L;
            for (var id = 0; id < last; id++)
            {
                var hack = character.hacks.hacks[id];
                if (hack != null)
                    used += Math.Max(hack.res3, 0L);
            }

            var changed = false;
            for (var id = 0; id < last; id++)
            {
                var hack = character.hacks.hacks[id];
                if (hack == null
                    || controller.hitTarget(id)
                    || hack.level >= controller.hardCapLevel(id))
                    continue;

                var cap = HackCapForNextLevel(character, controller, id);
                if (cap <= hack.res3)
                    continue;

                var needed = cap - Math.Max(hack.res3, 0L);
                var allow = budget == long.MaxValue ? needed : Math.Min(needed, Math.Max(budget - used, 0L));
                if (allow <= 0L)
                    continue;

                var amount = TakeRes3(character, allow);
                if (amount <= 0)
                    continue;

                hack.res3 += amount;
                used += amount;
                changed = true;
            }

            if (changed)
                controller.refreshMenu();
        }

        // res3 needed for the next hack level: progress per tick is
        // res3 * res3Power * hackSpeedBonus / (baseDivider * 1.0078^level * (level + 1)),
        // so reaching 1 progress requires baseDivider * 1.0078^level * (level + 1) / (power * speed)
        private static long HackCapForNextLevel(Character character, HacksController controller, int id)
        {
            var properties = controller.properties;
            if (properties == null || id >= properties.Count)
                return 0L;

            var power = (double)character.totalRes3Power();
            var speed = (double)controller.totalHackSpeedBonus();
            var divider = (double)properties[id].baseDivider;
            if (power <= 0d || speed <= 0d || divider <= 0d)
                return 0L;

            var level = (double)character.hacks.hacks[id].level;
            var cap = divider * Math.Pow(1.0078d, level) * (level + 1d) / (power * speed);
            return CapFromDouble(cap);
        }

        private static long TakeRes3(Character character, long amount)
        {
            if (amount <= 0L || character.res3 == null)
                return 0L;

            var taken = Math.Min(amount, Math.Max(character.res3.idleRes3, 0L));
            character.res3.idleRes3 -= taken;
            return taken;
        }
        private static long TakeEnergy(Character character, long current, long cap, bool fillAll = false)
        {
            if (character.idleEnergy <= 0L)
                return 0L;
            if (cap <= 0L)
                fillAll = true; // garbage cap (NaN-cast): unbounded

            if (cap <= current && !fillAll)
                return 0L;

            var needed = fillAll ? long.MaxValue : cap - Math.Max(current, 0L);
            var taken = Math.Min(needed, Math.Max(character.idleEnergy, 0L));
            character.idleEnergy -= taken;
            return taken;
        }
        private static long TakeMagic(Character character, long current, long cap, bool fillAll = false)
        {
            if (character.magic == null || character.magic.idleMagic <= 0L)
                return 0L;
            if (cap <= 0L)
                fillAll = true;

            if (cap <= current && !fillAll)
                return 0L;

            var needed = fillAll ? long.MaxValue : cap - Math.Max(current, 0L);
            var taken = Math.Min(needed, Math.Max(character.magic.idleMagic, 0L));
            character.magic.idleMagic -= taken;
            return taken;
        }
        private static long CapFromDouble(double cap)
        {
            if (cap <= 0d || double.IsNaN(cap))
                return 0L;
            if (double.IsInfinity(cap) || cap >= long.MaxValue)
                return long.MaxValue;

            var result = (long)cap;
            return result < long.MaxValue ? result + 1L : long.MaxValue;
        }
        private static bool HacksBeforeWishes()
        {
            var order = GetPriorityOrder();
            var hacksIndex = Array.IndexOf(order, Feature.Hacks);
            var wishesIndex = Array.IndexOf(order, Feature.Wishes);
            return hacksIndex >= 0 && wishesIndex > hacksIndex;
        }


    }
}
