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

        // Iron Pill has no native auto-cast in the game; the only confirmed cast method in the codebase.
        // gold/loot/rebirth spells have their own vanilla auto-cast checkboxes (goldAutoSpell etc.),
        // which are intentionally left alone - the player controls those manually.
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
                if (_enabled)
                    castReadySpells();
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

        // Iron Pill is ready when the boss that unlocks it (37) is defeated and its cooldown has elapsed
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
