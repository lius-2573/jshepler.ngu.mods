using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace jshepler.ngu.mods
{
    [HarmonyPatch]
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        // https://www.schemecolor.com/blue-red-yellow-green.php
        internal static readonly Color ButtonColor_Green = new Color32(40, 204, 45, 255); // #28cc2d
        internal static readonly Color ButtonColor_Yellow = new Color32(255, 244, 79, 255); // #fff44f
        internal static readonly Color ButtonColor_LightBlue = new Color32(99, 202, 216, 255); // #63cad8


        private readonly Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        private static ManualLogSource Log;
        internal static void LogInfo(string text) => Log.LogInfo(text);

        internal static event EventHandler OnUpdate;

        internal static event EventHandler OnSaveLoaded;
        internal static event EventHandler OnOfflineProgressionComplete;
        internal static event EventHandler OnGameStart;

        internal static Character Character = null;

        internal static bool AltIsDown => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        internal static bool ControlIsDown => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        internal static bool ShiftIsDown => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        

        private void Awake()
        {
            // prevents the bepinex manager object (i.e. this plugin instance) from being destroyed after Awake()
            // https://github.com/aedenthorn/PlanetCrafterMods/issues/7
            // not needed for all games, but I'm not currently aware of anything that it would hurt
            this.gameObject.hideFlags = HideFlags.HideAndDontSave;

            // enables virtual terminal mode so that ANSI escape sequences are rendered correctly
            EnableVT.Init();
            
            Log = base.Logger;
            Options.Init(base.Config);

            harmony.PatchAll();
            LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }

        private void Update()
        {
            if (Character == null)
                return;

            OnUpdate?.Invoke(null, EventArgs.Empty);
        }





        [HarmonyPostfix, HarmonyPatch(typeof(Character), "Start")]
        private static void Character_Start_postfix(Character __instance)
        {
            Character = __instance;

            OnGameStart?.Invoke(null, EventArgs.Empty);
        }



        // this is now called from ModSave/Patches.cs::LoadModData()
        // doing the postfix on finalTriggers would execute before the modData was loaded
        internal static void ImportExport_finalTriggers_postfix()
        {
            OnSaveLoaded?.Invoke(null, EventArgs.Empty);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Character), "addOfflineProgress")]
        private static void Character_addOfflineProgress_postfix()
        {
            OnOfflineProgressionComplete?.Invoke(null, EventArgs.Empty);
        }

        // when starting a new game, there is no offline progress and mods that rely on this event
        // won't be called and could have bad side-effects
        [HarmonyPostfix, HarmonyPatch(typeof(MainMenuController), "startNewGame")]
        private static void MainMenuController_startNewGame_postfix()
        {
            OnSaveLoaded?.Invoke(null, EventArgs.Empty);
            OnOfflineProgressionComplete?.Invoke(null, EventArgs.Empty);
        }

        [HarmonyFinalizer, HarmonyPatch(typeof(Character), "addOfflineProgress")]
        private static void Character_addOfflineProgress_finalizer(Exception __exception)
        {
            if(__exception != null)
                LogInfo($"Character.addOfflineProgress threw exception:\n{__exception}");
        }



        internal static void ShowNotification(string text, float seconds = 3f)
        {
            Character?.tooltip.showTooltip(text, seconds);
        }


        private static Text _tooltipText;
        [HarmonyPostfix, HarmonyPatch(typeof(HoverTooltip), "Start")]
        private static void HoverTooltip_Start_postfix(Text ___tooltipText)
        {
            _tooltipText = ___tooltipText;
        }

        // some of the mods change the font to fixed-width to show a table in the tooltip
        // and sometimes doesn't get reset when that tooltip gets hidden - this ensures all
        // the other regular tooltips are in the default font
        [HarmonyPostfix, HarmonyPatch(typeof(HoverTooltip), "hideTooltip")]
        private static void HoverTooltip_hideTooltip_postfix()
        {
            ResetTooltipFont();
        }


        internal static void ResetTooltipFont()
        {
            if(_tooltipText != null)
                _tooltipText.font = Fonts.LiberationSans_Regular;
        }




    }
}

/*
notes:
    the load local save button on the startup screen calls:
        MainMenuController.loadFileSave()
        ->  MainMenuController.loadFileKartridge()
            ->  OpenFileDialog.loadFileMainMenuStandalone()
                ->  OpenFileDialog.loadintoGame()
                    ->  ImportExport.loadData()
                    ->  Character.addOfflineProgress()

    the load autosave button on the startup screen calls:
        MainMenuController.loadAutosave()
        -> MainMenuController.loadAutosaveSteam()
            -> OpenFileDialog.loadintoGame()
                ->  ImportExport.loadData()
                ->  Character.addOfflineProgress()
                    ->  ImportExport.loadData()
                    ->  Character.addOfflineProgress()

    the load cloud save button on the startup screen calls:
        MainMenuController.loadCloudSave()
        -> MainMenuController.loadCloudSaveSteam()
            -> openFileDialog.loadintoGame()

    the load save button bottom-left of game screen calls:
        OpenFileDialog.startLoadStandalone()
        ->  OpenFileDialog.quickLoad()
            ->  importExport.loadBase64ToData()
                ->  ImportExport.loadData()
        ->  Character.addOfflineProgress()

    both eventually call ImportExport.loadData(SaveData) which updates the game from SaveData -> PlayerData

    reg key: HKEY_CURRENT_USER\SOFTWARE\NGU Industries\NGU Idle
 */
