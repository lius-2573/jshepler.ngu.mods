using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace jshepler.ngu.mods
{
    [HarmonyPatch]
    internal class AutoBloodMagic
    {
        private static int _lastCheckFrame = -1;
        private static bool _enabled
        {
            get => Options.BloodMagic.AutoCast.Value;
            set => Options.BloodMagic.AutoCast.Value = value;
        }

        // Iron Pill has no native auto-cast in the game; the only confirmed cast method in the codebase.
        // gold/loot/rebirth spells have their own vanilla auto-cast checkboxes (goldAutoSpell etc.),
        // which are intentionally left alone - the player controls those manually.
        private static MethodInfo _castIP = typeof(RebirthPowerSpell).GetMethod("castAdventurePowerupSpell", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static Button _ironPillButton;
        private static Text _ironPillDisplay;
        private static GameObject _ironPillButtonTarget;
        private static GameObject _ironPillDisplayTarget;
        private static int _lastToggleFrame = -1;

        [HarmonyPostfix, HarmonyPatch(typeof(RebirthPowerSpell), "Start")]
        private static void RebirthPowerSpell_Start_postfix(RebirthPowerSpell __instance)
        {
            EnsureIronPillButton(__instance);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(RebirthPowerSpell), "updateMenu")]
        private static void RebirthPowerSpell_updateMenu_postfix(RebirthPowerSpell __instance)
        {
            EnsureIronPillButton(__instance);
        }

        private static void EnsureIronPillButton(RebirthPowerSpell spell)
        {
            var display = spell.adventureDisplay;
            var button = display?.GetComponent<Button>() ?? display?.GetComponentInParent<Button>();
            if (display != null)
            {
                display.raycastTarget = true;
                _ironPillDisplay = display;
            }

            _ironPillButton = button;
            AttachClickTarget(button?.gameObject);
            AttachClickTarget(display?.gameObject);
            SetVisual();
        }

        private static void AttachClickTarget(GameObject target)
        {
            if (target == null || target == _ironPillButtonTarget || target == _ironPillDisplayTarget)
                return;

            target.AddComponent<ClickHandlerComponent>()
                .OnRightClick(e => ToggleFromIronPill());

            if (_ironPillButtonTarget == null)
                _ironPillButtonTarget = target;
            else if (_ironPillDisplayTarget == null)
                _ironPillDisplayTarget = target;
        }

        private static void ToggleFromIronPill()
        {
            if (!Plugin.ShiftIsDown || Time.frameCount == _lastToggleFrame)
                return;

            _lastToggleFrame = Time.frameCount;
            _enabled = !_enabled;
            if (_enabled)
                AutomationThrottle.Reset(ref _lastCheckFrame);
            SetVisual();
        }

        private static void SetVisual()
        {
            var color = _enabled ? Plugin.ButtonColor_LightBlue : Color.white;
            if (_ironPillButton != null)
                _ironPillButton.image.color = color;
            else if (_ironPillDisplay != null)
                _ironPillDisplay.color = color;
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
