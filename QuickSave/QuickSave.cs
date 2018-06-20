﻿using Assets.Core.Interfaces;
using Assets.Core.Entities;
using Assets.CS.TabletopUI;
using Assets.TabletopUi.Scripts.Services;
using Assets.TabletopUi;
using Assets.TabletopUi.SlotsContainers;
using Assets.TabletopUi.Scripts.Infrastructure;
using Harmony;
using Partiality.Modloader;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using IlilimModUtils;

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
            HarmonyInstance
                .Create("ililim.cultistsimulatormods." + GetType().Namespace.ToLower())
                .PatchAll(Assembly.GetExecutingAssembly());
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
        }
    }

    [HarmonyPatch(typeof(TabletopManager), "BeginNewGame", new Type[] { typeof(SituationBuilder) })]
    class Patch_TabletopManager_BeginNewGame
    {
        static void Postfix()
        {
            SaveState.HasSaved = false;
        }
    }

    [HarmonyPatch(typeof(TabletopManager), "SaveGame", new Type[] { typeof(bool) })]
    class Patch_TabletopManager_SaveGame
    {
        static void Postfix()
        {
            SaveState.HasSaved = true;
        }
    }
}