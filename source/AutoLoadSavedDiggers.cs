using System;
using HarmonyLib;
using UnityEngine.UI;
namespace jshepler.ngu.mods
{
    [HarmonyPatch]
    internal static class AutoLoadSavedDiggers
    {
        private static AllGoldDiggerController _controller;
        private static int _lastCheckFrame = -1;
        private static bool _pending;
        private static bool _eventsRegistered;
        private static Button _button;

        [HarmonyPostfix, HarmonyPatch(typeof(ButtonShower), "Start")]
        private static void ButtonShower_Start_postfix(ButtonShower __instance)
        {
            _button = __instance.diggers;
            if (_button == null)
                return;

            _button.gameObject.AddComponent<ClickHandlerComponent>()
                .OnRightClick(e =>
                {
                    if (!Plugin.ShiftIsDown)
                        return;

                    Options.GoldDiggers.AutoLoadSaved.Value = !Options.GoldDiggers.AutoLoadSaved.Value;
                    if (Options.GoldDiggers.AutoLoadSaved.Value)
                    {
                        _pending = true;
                        RequestCheck();
                    }
                    SetButtonColor();
                });

            SetButtonColor();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ButtonShower), "updateButtons")]
        private static void ButtonShower_updateButtons_postfix()
        {
            SetButtonColor();
        }

        private static void SetButtonColor()
        {
            if (_button != null)
                _button.image.color = Options.GoldDiggers.AutoLoadSaved.Value
                    ? Plugin.ButtonColor_LightBlue
                    : UnityEngine.Color.white;
        }
        [HarmonyPostfix, HarmonyPatch(typeof(AllGoldDiggerController), "Start")]
        private static void AllGoldDiggerController_Start_postfix(AllGoldDiggerController __instance)
        {
            _controller = __instance;
            _pending = true;
            RegisterEvents();
            RequestCheck();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Rebirth), "engage", typeof(bool))]
        private static void Rebirth_engage_postfix()
        {
            _pending = true;
            RequestCheck();
        }

        private static void RegisterEvents()
        {
            if (_eventsRegistered)
                return;

            Plugin.OnUpdate += (o, e) => Update();
            Plugin.OnSaveLoaded += (o, e) => MarkPending();
            Plugin.OnOfflineProgressionComplete += (o, e) => MarkPending();
            _eventsRegistered = true;
        }

        private static void MarkPending()
        {
            _pending = true;
            RequestCheck();
        }

        private static void RequestCheck()
        {
            AutomationThrottle.Reset(ref _lastCheckFrame);
        }

        private static void Update()
        {
            if (!Options.GoldDiggers.AutoLoadSaved.Value || !_pending || _controller == null)
                return;

            if (!AutomationThrottle.ShouldRunEveryFrames(ref _lastCheckFrame))
                return;

            var character = _controller.character;
            if (character == null || character.diggers == null || character.diggers.loadoutDiggers == null
                || character.diggers.loadoutDiggers.Count == 0 || character.diggers.diggers == null)
            {
                _pending = false;
                return;
            }

            if (HasSavedLoadoutActive(character))
            {
                _pending = false;
                return;
            }

            if (character.grossGoldPerSecond() <= 0d)
                return;

            _controller.clearAllActiveDiggers();
            _controller.applyDiggerLoadout();
            _controller.refreshMenu();

            if (HasSavedLoadoutActive(character))
                _pending = false;
        }

        private static bool HasSavedLoadoutActive(Character character)
        {
            var diggers = character.diggers.diggers;
            var saved = character.diggers.loadoutDiggers;

            for (var id = 0; id < diggers.Count; id++)
            {
                var shouldBeActive = saved.Contains(id);
                if (shouldBeActive && (!diggers[id].active || diggers[id].curLevel <= 0))
                    return false;
                if (!shouldBeActive && diggers[id].active)
                    return false;
            }

            return true;
        }
    }
}
