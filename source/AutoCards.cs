using System;
using HarmonyLib;
using UnityEngine;

namespace jshepler.ngu.mods
{
    internal enum CardSortBy
    {
        RarityFirst,
        TypeFirst,
        Efficiency,
        Variance
    }

    internal enum CardSortDirection
    {
        Descending = -1,
        Ascending = 1
    }

    internal enum CardYeetMode
    {
        Disabled,
        Efficiency,
        Variance,
        Rarity
    }

    [HarmonyPatch]
    internal static class AutoCards
    {
        private const int ManaTypeCount = 6;
        private const int AlwaysYeetFlagCount = 15;

        private static readonly bool[] AlwaysYeetFlags = new bool[AlwaysYeetFlagCount];
        private static string _cachedAlwaysYeetCsv;

        private static bool _autoYeetInProgress;
        private static bool _eventsRegistered;
        private static int _lastCheckFrame = -1;
        private static CardsController _controller;
        private static Card _targetCard;
        private static bool _hasGeneratorSnapshot;
        private static readonly bool[] GeneratorSnapshot = new bool[ManaTypeCount];
        private static readonly bool[] SelectedGenerators = new bool[ManaTypeCount];
        private static readonly float[] GeneratorDeficits = new float[ManaTypeCount];

        private static bool AutoSortEnabled => Options.Cards.AutoSortEnabled.Value;
        private static CardSortBy AutoSortBy => Options.Cards.AutoSortBy.Value;
        private static CardSortDirection AutoSortDirection => Options.Cards.AutoSortDirection.Value;
        private static CardYeetMode AutoYeetMode => Options.Cards.AutoYeetMode.Value;
        private static rarity MaxYeetRarity => Options.Cards.MaxYeetRarity.Value;
        private static float MaxYeetEfficiency => Options.Cards.MaxYeetEfficiency.Value;
        private static float MaxYeetVariance => Options.Cards.MaxYeetVariance.Value;
        private static bool AutoProtectChonkers => Options.Cards.AutoProtectChonkers.Value;
        private static bool AutoCastEnabled => Options.Cards.AutoCastEnabled.Value;

        [HarmonyPostfix, HarmonyPatch(typeof(CardsController), "Start")]
        private static void CardsController_Start_postfix(CardsController __instance)
        {
            _controller = __instance;

            if (_eventsRegistered)
                return;

            _eventsRegistered = true;
            Plugin.OnUpdate += (o, e) => UpdateAutoCast();
            Plugin.OnGameStart += (o, e) => ResetState();
            Plugin.OnSaveLoaded += (o, e) => ResetState();
            Plugin.OnOfflineProgressionComplete += (o, e) => ResetState();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ButtonShower), "Start")]
        private static void ButtonShower_Start_postfix(ButtonShower __instance)
        {
            if (__instance.cards == null)
                return;

            __instance.cards.gameObject.AddComponent<ClickHandlerComponent>()
                .OnRightClick(e =>
                {
                    if (Plugin.ShiftIsDown)
                        ToggleAutoCast();
                });
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ButtonShower), "updateButtons"), HarmonyPriority(Priority.LowerThanNormal)]
        private static void ButtonShower_updateButtons_postfix(ButtonShower __instance)
        {
            if (__instance.cards != null)
                __instance.cards.image.color = AutoCastEnabled ? Plugin.ButtonColor_LightBlue : Color.white;
        }

        [HarmonyPostfix
            , HarmonyPatch(typeof(CardsController), "addCard")
            , HarmonyPatch(typeof(CardsController), "addChonkerCard")]
        private static void CardsController_addCards_postfix()
        {
            if (_autoYeetInProgress)
                return;

            if (AutoYeetMode != CardYeetMode.Disabled)
                YeetCards();

            if (AutoSortEnabled)
                SortCards();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(CardsController), "generateCard")]
        private static void CardsController_generateCard_postfix(bool isChonker, Card __result)
        {
            if (isChonker && __result != null)
                __result.isProtected = AutoProtectChonkers;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(CardsController), "Update")]
        private static void CardsController_Update_postfix()
        {
            if (Plugin.Character == null || (Menu)Plugin.Character.menuID != Menu.Cards)
                return;

            if (Input.GetKeyDown(KeyCode.S))
                SortCards();

            if (Input.GetKeyDown(KeyCode.Y))
                YeetCards();
        }

        internal static void SortCards()
        {
            var controller = GetController();
            var cards = controller?.character?.cards?.cards;
            if (cards == null)
                return;

            cards.Sort(CompareCardsByConfiguredOrder);
            controller.updateDeckPods();
            controller.updateDeckButtons();
        }

        private static int CompareCardsByConfiguredOrder(Card left, Card right)
        {
            return CompareCards(left, right, AutoSortBy, AutoSortDirection);
        }

        private static int CompareCards(Card left, Card right, CardSortBy sortBy, CardSortDirection direction)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;

            var multiplier = direction == CardSortDirection.Descending ? -1 : 1;
            var controller = GetController();
            if (controller == null)
                return 0;

            switch (sortBy)
            {
                case CardSortBy.RarityFirst:
                    if (left.cardRarity != right.cardRarity)
                        return left.cardRarity.CompareTo(right.cardRarity) * multiplier;

                    if (left.bonusType != right.bonusType)
                        return left.bonusType.CompareTo(right.bonusType) * multiplier;

                    return left.effectAmount.CompareTo(right.effectAmount) * multiplier;

                case CardSortBy.TypeFirst:
                    if (left.bonusType != right.bonusType)
                        return left.bonusType.CompareTo(right.bonusType) * multiplier;

                    if (left.cardRarity != right.cardRarity)
                        return left.cardRarity.CompareTo(right.cardRarity) * multiplier;

                    return left.effectAmount.CompareTo(right.effectAmount) * multiplier;

                case CardSortBy.Efficiency:
                    return CardMetrics.GetEfficiency(left, controller)
                        .CompareTo(CardMetrics.GetEfficiency(right, controller)) * multiplier;

                case CardSortBy.Variance:
                    return CardMetrics.GetVariance(left)
                        .CompareTo(CardMetrics.GetVariance(right)) * multiplier;

                default:
                    return CardMetrics.GetEfficiency(left, controller)
                        .CompareTo(CardMetrics.GetEfficiency(right, controller)) * multiplier;
            }
        }

        private static void YeetCards()
        {
            var controller = GetController();
            var cards = controller?.character?.cards?.cards;
            if (cards == null || _autoYeetInProgress)
                return;

            RefreshAlwaysYeetFlags();

            _autoYeetInProgress = true;
            try
            {
                var doAnotherPass = true;
                while (doAnotherPass)
                {
                    doAnotherPass = false;

                    var index = cards.Count;
                    while (index > 0)
                    {
                        var card = cards[--index];
                        if (card == null || card.isProtected)
                            continue;

                        if (!ShouldYeet(card))
                            continue;

                        controller.trashCard(index);
                        doAnotherPass = true;
                    }
                }
            }
            finally
            {
                _autoYeetInProgress = false;
            }
        }

        private static bool ShouldYeet(Card card)
        {
            if (IsAlwaysYeet(card))
                return true;

            if (card.type == cardType.end)
                return false;

            return AutoYeetMode switch
            {
                CardYeetMode.Efficiency => CardMetrics.GetEfficiency(card, GetController()) <= MaxYeetEfficiency,
                CardYeetMode.Variance => CardMetrics.GetVariance(card) <= MaxYeetVariance,
                CardYeetMode.Rarity => card.cardRarity <= MaxYeetRarity,
                _ => false
            };
        }

        private static void RefreshAlwaysYeetFlags()
        {
            var csv = Options.Cards.AlwaysYeetCSV.Value ?? string.Empty;
            if (string.Equals(_cachedAlwaysYeetCsv, csv, StringComparison.Ordinal))
                return;

            _cachedAlwaysYeetCsv = csv;
            Array.Clear(AlwaysYeetFlags, 0, AlwaysYeetFlags.Length);

            var values = csv.Split(',');
            var count = Math.Min(values.Length, AlwaysYeetFlagCount);
            for (var index = 0; index < count; index++)
                AlwaysYeetFlags[index] = values[index].Trim() == "1";
        }

        private static bool IsAlwaysYeet(Card card)
        {
            var index = card.type == cardType.end ? 0 : (int)card.bonusType;
            if (index < 0 || index >= AlwaysYeetFlagCount)
                return false;

            return AlwaysYeetFlags[index];
        }

        private static void UpdateAutoCast()
        {
            if (!AutoCastEnabled)
            {
                ReleaseGeneratorControl();
                return;
            }

            if (!AutomationThrottle.ShouldRunEveryFrames(ref _lastCheckFrame))
                return;

            var controller = GetController();
            if (!IsUsable(controller) || !controller.character.cards.cardsOn)
            {
                ReleaseGeneratorControl();
                return;
            }

            var cards = controller.character.cards.cards;
            if (cards == null || cards.Count == 0)
            {
                ReleaseGeneratorControl();
                return;
            }

            var targetIndex = FindAutoCastTargetIndex(controller);
            if (targetIndex < 0)
            {
                ReleaseGeneratorControl();
                return;
            }
            var target = cards[targetIndex];


            if (HasEnoughMayo(controller, target))
            {
                ReleaseGeneratorControl();

                var temporarilyUnprotected = target.cardRarity == rarity.BigChonker && target.isProtected;
                if (temporarilyUnprotected)
                    target.isProtected = false;

                controller.tryConsumeCard(targetIndex);

                // tryConsumeCard deletes a successful card. Restore protection only if it refused.
                if (temporarilyUnprotected && cards.IndexOf(target) >= 0)
                    target.isProtected = true;

                return;
            }

            EnsureGeneratorControl(controller, target);
            AllocateGeneratorsForTarget(controller, target);
        }

        private static int FindAutoCastTargetIndex(CardsController controller)
        {
            var cards = controller.character.cards.cards;
            var sortBy = AutoSortBy;
            var useVariance = sortBy == CardSortBy.Variance;
            var useMetric = useVariance
                || (sortBy != CardSortBy.RarityFirst && sortBy != CardSortBy.TypeFirst);
            var bestIndex = -1;
            var bestMetric = 0f;

            for (var index = 0; index < cards.Count; index++)
            {
                var card = cards[index];
                if (!IsAutoCastCandidate(card))
                    continue;

                if (useMetric)
                {
                    var metric = useVariance
                        ? CardMetrics.GetVariance(card)
                        : CardMetrics.GetEfficiency(card, controller);

                    if (bestIndex < 0 || metric.CompareTo(bestMetric) > 0)
                    {
                        bestIndex = index;
                        bestMetric = metric;
                    }
                }
                else if (bestIndex < 0
                    || CompareCards(card, cards[bestIndex], sortBy, CardSortDirection.Descending) < 0)
                {
                    bestIndex = index;
                }
            }

            return bestIndex;
        }

        private static bool IsAutoCastCandidate(Card card)
        {
            if (card == null || card.type == cardType.end)
                return false;

            // Chonkers stay protected until this policy explicitly decides to cast them.
            return card.cardRarity == rarity.BigChonker || !card.isProtected;
        }

        private static bool HasEnoughMayo(CardsController controller, Card card)
        {
            var manas = controller.character.cards.manas;
            if (card.manaCosts == null || manas == null || card.manaCosts.Count < manas.Count)
                return false;

            for (var index = 0; index < manas.Count; index++)
            {
                if (card.manaCosts[index] > manas[index].amount)
                    return false;
            }

            return true;
        }

        private static void EnsureGeneratorControl(CardsController controller, Card target)
        {
            if (_hasGeneratorSnapshot && ReferenceEquals(_targetCard, target))
                return;

            ReleaseGeneratorControl();

            _controller = controller;
            var manas = controller.character.cards.manas;
            for (var index = 0; index < GeneratorSnapshot.Length; index++)
                GeneratorSnapshot[index] = index < manas.Count && manas[index].running;

            _targetCard = target;
            _hasGeneratorSnapshot = true;
        }

        private static void AllocateGeneratorsForTarget(CardsController controller, Card target)
        {
            var manas = controller.character.cards.manas;
            if (manas == null || target.manaCosts == null || target.manaCosts.Count < manas.Count)
                return;

            var manaCount = Math.Min(manas.Count, ManaTypeCount);
            Array.Clear(SelectedGenerators, 0, SelectedGenerators.Length);
            Array.Clear(GeneratorDeficits, 0, GeneratorDeficits.Length);

            for (var index = 0; index < manaCount; index++)
            {
                var unitsNeeded = target.manaCosts[index] - manas[index].amount;
                if (unitsNeeded > 0)
                {
                    // Include fractional progress so a nearly-complete generator wins a tie.
                    GeneratorDeficits[index] = Mathf.Max(0.000001f, unitsNeeded - manas[index].progress);
                }
            }

            var maxActive = Mathf.Clamp(controller.maxManaGenSize(), 1, manaCount);
            for (var slot = 0; slot < maxActive; slot++)
            {
                var selected = SelectNextGenerator(manas, manaCount);
                if (selected < 0)
                    break;

                SelectedGenerators[selected] = true;
            }

            var changed = false;
            for (var index = 0; index < manaCount; index++)
            {
                var shouldRun = SelectedGenerators[index];
                if (manas[index].running != shouldRun)
                {
                    manas[index].running = shouldRun;
                    changed = true;
                }
            }

            if (changed)
            {
                controller.updateManaPods();
                controller.updateManaGenText();
            }
        }

        private static int SelectNextGenerator(System.Collections.Generic.List<Mana> manas, int manaCount)
        {
            var selected = -1;
            var selectedDeficit = 0f;

            for (var index = 0; index < manaCount; index++)
            {
                if (SelectedGenerators[index] || GeneratorDeficits[index] <= 0f)
                    continue;

                if (selected < 0
                    || GeneratorDeficits[index] > selectedDeficit
                    || (Mathf.Approximately(GeneratorDeficits[index], selectedDeficit)
                        && manas[index].running
                        && !manas[selected].running)
                    || (Mathf.Approximately(GeneratorDeficits[index], selectedDeficit)
                        && manas[index].running == manas[selected].running
                        && index < selected))
                {
                    selected = index;
                    selectedDeficit = GeneratorDeficits[index];
                }
            }

            return selected;
        }

        private static void ReleaseGeneratorControl()
        {
            if (!_hasGeneratorSnapshot)
            {
                _targetCard = null;
                return;
            }

            var controller = GetController();
            var manas = controller?.character?.cards?.manas;
            if (manas != null)
            {
                var changed = false;
                for (var index = 0; index < GeneratorSnapshot.Length && index < manas.Count; index++)
                {
                    if (manas[index].running != GeneratorSnapshot[index])
                    {
                        manas[index].running = GeneratorSnapshot[index];
                        changed = true;
                    }
                }

                if (changed)
                {
                    controller.updateManaPods();
                    controller.updateManaGenText();
                }
            }

            _hasGeneratorSnapshot = false;
            _targetCard = null;
        }

        private static void ResetState()
        {
            // A save/load event replaces game state; never restore running flags from the old save.
            _hasGeneratorSnapshot = false;
            _targetCard = null;
            AutomationThrottle.Reset(ref _lastCheckFrame);
        }

        private static void ToggleAutoCast()
        {
            if (AutoCastEnabled)
            {
                Options.Cards.AutoCastEnabled.Value = false;
                ReleaseGeneratorControl();
                Plugin.ShowNotification("自动出牌：已关闭");
                return;
            }

            Options.Cards.AutoCastEnabled.Value = true;
            AutomationThrottle.Reset(ref _lastCheckFrame);
            Plugin.ShowNotification("自动出牌：已开启（高优先级普通卡，单次一张）");
        }

        private static CardsController GetController()
        {
            if (Plugin.Character?.cardsController != null)
                return Plugin.Character.cardsController;

            return _controller;
        }

        private static bool IsUsable(CardsController controller)
        {
            return controller != null
                && controller.character != null
                && controller.character.cards != null
                && controller.character.cards.cards != null
                && controller.character.cards.manas != null;
        }
    }
}
