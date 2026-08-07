using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace jshepler.ngu.mods
{
    [HarmonyPatch]
    internal class AutoBloodMagic
    {
        private static bool _enabled
        {
            get => Options.BloodMagic.AutoCast.Value;
            set => Options.BloodMagic.AutoCast.Value = value;
        }

        // Iron Pill: RebirthPowerSpell.castAdventurePowerupSpell (confirmed via TrackBaseAdvPowerGained.cs)
        private static MethodInfo _castIP = typeof(RebirthPowerSpell).GetMethod("castAdventurePowerupSpell", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        [HarmonyPostfix, HarmonyPatch(typeof(ButtonShower), "Start")]
        private static void ButtonShower_Start_postfix(ButtonShower __instance)
        {
            var button = __instance.bloodMagic;
            button.gameObject.AddComponent<ClickHandlerComponent>()
                .OnRightClick(e =>
                {
                    if (!Plugin.ShiftIsDown)
                        return;

                    _enabled = !_enabled;
                    setColor(button);
                });

            Plugin.OnUpdate += (o, e) =>
            {
                if (Plugin.Character == null)
                    return;

                if (_enabled)
                {
                    ensureAutoSpells();
                    castReadySpells();
                }

                else
                    restoreAutoSpells();
            };
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ButtonShower), "updateButtons")]
        private static void ButtonShower_updateButtons_postfix(ButtonShower __instance)
        {
            setColor(__instance.bloodMagic);
        }

        private static void setColor(Button button)
        {
            if (Plugin.Character == null)
                return;

            button.image.color = _enabled ? Plugin.ButtonColor_LightBlue : Color.white;
        }

        // the game has native auto-cast for gold/loot/rebirth spells (goldAutoSpell etc.);
        // when enabled, force those on so the game casts them as soon as they're ready;
        // remember the original state and restore it when disabled
        private static bool _restoreAuto = false;
        private static bool _origGold, _origLoot, _origRebirth;

        private static void ensureAutoSpells()
        {
            var bm = Plugin.Character.bloodMagic;

            if (!_restoreAuto)
            {
                _origGold = bm.goldAutoSpell;
                _origLoot = bm.lootAutoSpell;
                _origRebirth = bm.rebirthAutoSpell;
                _restoreAuto = true;
            }

            bm.goldAutoSpell = true;
            bm.lootAutoSpell = true;
            bm.rebirthAutoSpell = true;
        }

        private static void restoreAutoSpells()
        {
            if (!_restoreAuto)
                return;

            var bm = Plugin.Character.bloodMagic;
            bm.goldAutoSpell = _origGold;
            bm.lootAutoSpell = _origLoot;
            bm.rebirthAutoSpell = _origRebirth;
            _restoreAuto = false;
        }

        // Iron Pill has no native auto-cast, so cast it ourselves when ready
        private static void castReadySpells()
        {
            var character = Plugin.Character;
            if (character == null || character.bossID < 37)
                return;

            var bm = character.bloodMagic;
            if (bm.adventureSpellTime.totalseconds < character.bloodMagicController.spells.adventureSpellCooldown)
                return;

            // spells is the RebirthPowerSpell instance (has adventureSpellCooldown, castingAutoSpells() etc.)
            if (_castIP == null || _castIP.GetParameters().Length > 0)
                return;

            if (character.bloodMagicController.spells is RebirthPowerSpell spell)
                _castIP.Invoke(_castIP.IsStatic ? null : spell, null);
        }
    }
}
