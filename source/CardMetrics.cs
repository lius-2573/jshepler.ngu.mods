using System.Collections.Generic;
using UnityEngine;

namespace jshepler.ngu.mods
{
    internal static class CardMetrics
    {
        private readonly struct Constants
        {
            internal readonly float QuadFactor;
            internal readonly float ExpFactor;
            internal readonly float FixedCoefficient;
            internal readonly float ScalingCoefficient;

            internal Constants(float quadFactor, float expFactor, float fixedCoefficient, float scalingCoefficient)
            {
                QuadFactor = quadFactor;
                ExpFactor = expFactor;
                FixedCoefficient = fixedCoefficient;
                ScalingCoefficient = scalingCoefficient;
            }
        }

        private static readonly Dictionary<cardBonus, Constants> BonusConstants = new()
        {
            { cardBonus.energyNGUSpeed, new Constants(1.2f, 1.03f, 0.0003f, 0.001f) },
            { cardBonus.magicNGUSpeed, new Constants(0.8f, 1.08f, 0.0002f, 0.001f) },
            { cardBonus.wandoosSpeed, new Constants(0.8f, 1.1f, 0.0002f, 0.001f) },
            { cardBonus.augSpeed, new Constants(0.8f, 1.1f, 0.0002f, 0.001f) },
            { cardBonus.TMSpeed, new Constants(0.8f, 1.15f, 0.0002f, 0.001f) },
            { cardBonus.hackSpeed, new Constants(0.4f, 1.05f, 0.0002f, 0.001f) },
            { cardBonus.wishSpeed, new Constants(0.5f, 1.05f, 0.0002f, 0.001f) },
            { cardBonus.atkDefStats, new Constants(1.5f, 2f, 0.05f, 0.01f) },
            { cardBonus.adventureStat, new Constants(0.4f, 1.07f, 0.0005f, 0.001f) },
            { cardBonus.dropChance, new Constants(1f, 1.15f, 0.0002f, 0.001f) },
            { cardBonus.goldDrop, new Constants(0.8f, 1.15f, 0.001f, 0.005f) },
            { cardBonus.dayCareSpeed, new Constants(0.4f, 1.04f, 0.00005f, 0.0002f) },
            { cardBonus.PP, new Constants(0.6f, 1.11f, 0.0001f, 0.0002f) },
            { cardBonus.QP, new Constants(0.6f, 1.08f, 0.0001f, 0.0002f) }
        };

        internal static float GetEfficiency(Card card, CardsController controller)
        {
            if (card == null || controller == null || card.manaCosts == null)
                return 0f;

            var totalCost = TotalCost(card);
            if (totalCost <= 0)
                return 0f;

            var isChonker = card.cardRarity == rarity.BigChonker;
            var maxMayo = isChonker ? controller.maxChonkerMana() : controller.maxCardMana();
            if (maxMayo <= 0)
                return 0f;

            var maxBonus = controller.generateCardEffect(
                card.bonusType,
                card.tier,
                maxMayo,
                controller.getMaxVariance(),
                isChonker);

            var bestBonusPerMayo = maxBonus / maxMayo;
            if (bestBonusPerMayo <= 0f)
                return 0f;

            return (card.effectAmount / totalCost) / bestBonusPerMayo;
        }

        internal static float GetVariance(Card card)
        {
            if (card == null || card.manaCosts == null || !BonusConstants.TryGetValue(card.bonusType, out var constants))
                return 0f;

            var totalCost = TotalCost(card);
            if (totalCost <= 0)
                return 0f;

            var denominator = constants.ScalingCoefficient
                * Mathf.Pow(card.tier, constants.QuadFactor)
                * Mathf.Pow(constants.ExpFactor, card.tier);
            if (denominator == 0f)
                return 0f;

            return ((card.effectAmount / totalCost) - constants.FixedCoefficient) / denominator;
        }

        private static int TotalCost(Card card)
        {
            var total = 0;
            for (var index = 0; index < card.manaCosts.Count; index++)
                total += card.manaCosts[index];

            return total;
        }
    }
}
