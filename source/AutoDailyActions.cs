using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace jshepler.ngu.mods
{
    [HarmonyPatch]
    internal class AutoDailyActions
    {
        private static bool _autoToss
        {
            get => Options.MoneyPit.AutoToss.Value;
            set => Options.MoneyPit.AutoToss.Value = value;
        }

        private static bool _autoSpin
        {
            get => Options.DailySpin.AutoSpin.Value;
            set => Options.DailySpin.AutoSpin.Value = value;
        }

        private static MethodInfo _tossGoldMethod = typeof(PitController).GetMethod("engage", BindingFlags.NonPublic | BindingFlags.Instance);

        [HarmonyPostfix, HarmonyPatch(typeof(ButtonShower), "Start")]
        private static void ButtonShower_Start_postfix(ButtonShower __instance)
        {
            // 每日转盘没有自己的按钮,与原 RIghtClickMoneyPit 一致,绑定在钱坑按钮上:
            // shift+右键 = 切换钱坑自动投;alt+右键 = 切换转盘自动转
            var button = __instance.pit;
            button.gameObject.AddComponent<ClickHandlerComponent>()
                .OnRightClick(e =>
                {
                    if (Plugin.ShiftIsDown)
                    {
                        _autoToss = !_autoToss;
                        setColor(button);
                    }

                    else if (Plugin.AltIsDown)
                    {
                        _autoSpin = !_autoSpin;
                        setColor(button);
                    }
                });

            Plugin.OnUpdate += (o, e) =>
            {
                if (_autoToss)
                    tossGold();

                if (_autoSpin)
                    doSpin();
            };
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ButtonShower), "updateButtons")]
        private static void ButtonShower_updateButtons_postfix(ButtonShower __instance)
        {
            setColor(__instance.pit);
        }

        private static void setColor(Button button)
        {
            if (Plugin.Character == null)
                return;

            button.image.color = _autoToss && _autoSpin ? Plugin.ButtonColor_Yellow
                : _autoToss ? Plugin.ButtonColor_LightBlue
                : _autoSpin ? Plugin.ButtonColor_Green
                : Color.white;
        }

        private static void tossGold()
        {
            var character = Plugin.Character;
            if (character == null)
                return;

            var pitController = character.pitController;
            if (!pitController.canToss())
                return;

            _tossGoldMethod.Invoke(pitController, new object[0]);
            Plugin.ShowNotification(pitController.pitText.text);
        }

        private static void doSpin()
        {
            var character = Plugin.Character;
            if (character == null)
                return;

            var dailyController = character.dailyController;
            if (!dailyController.canSpin())
                return;

            dailyController.startNoBullshitSpin();
            Plugin.ShowNotification(dailyController.outcomeText.text);
        }
    }
}
