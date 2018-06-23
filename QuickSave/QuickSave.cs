using Assets.Core.Interfaces;
using Assets.CS.TabletopUI;
using Assets.TabletopUi.Scripts.Infrastructure;
using Assets.TabletopUi.Scripts.Services;
using Harmony;
using IlilimModUtils;
using Partiality.Modloader;
using System;
using System.Reflection;
using UnityEngine;

/**
 * Mod that binds save and load to F5 and F9
 */

namespace QuickSave
{
    // Initialize the mod through Partiality
    class Mod : PartialityMod
    {
        public override void Init()
        {
            Patcher.Run(() =>
            {
                HarmonyInstance
                    .Create("ililim.cultistsimulatormods." + GetType().Namespace.ToLower())
                    .PatchAll(Assembly.GetExecutingAssembly());
            });
        }
    }

    class SaveState
    {
        public static bool HasSaved = true;
    }

    [HarmonyPatch(typeof(HotkeyWatcher))]
    [HarmonyPatch("WatchForGameplayHotkeys")]
    class Patch_HotkeyWatcher_WatchForGameplayHotkeys
    {
        static void Postfix()
        {
            Patcher.Run(() =>
            {
                TabletopManager manager = Registry.Retrieve<TabletopManager>();
                if (Input.GetKeyDown(KeyCode.F5))
                {
                    if (TabletopManager.IsSafeToAutosave())
                    {
                        manager.ForceAutosave();
                    }
                    else
                    {
                        Registry.Retrieve<INotifier>().ShowNotificationWindow("Not now, not yet -", "I can't save while exploring the Mansus or moving cards.");
                    }
                }
                else if (Input.GetKeyDown(KeyCode.F9))
                {
                    if (SaveState.HasSaved)
                    {
                        manager.LoadGame();
                    }
                    else
                    {
                        Registry.Retrieve<INotifier>().ShowNotificationWindow("Faint visions, but no memories", "Was that just a dream? Yet it felt so real. As it stands we have not saved the game yet so there is nothing to load.");
                    }
                }
            });
        }
    }

    [HarmonyPatch(typeof(TabletopManager), "BeginNewGame", new Type[] { typeof(SituationBuilder) })]
    class Patch_TabletopManager_BeginNewGame
    {
        static void Postfix()
        {
            Patcher.Run(() =>
            {
                SaveState.HasSaved = false;
            });
        }
    }

    [HarmonyPatch(typeof(TabletopManager), "SaveGame", new Type[] { typeof(bool) })]
    class Patch_TabletopManager_SaveGame
    {
        static void Postfix()
        {
            Patcher.Run(() =>
            {
                SaveState.HasSaved = true;
            });
        }
    }
}
