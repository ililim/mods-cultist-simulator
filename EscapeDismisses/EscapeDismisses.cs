﻿using Assets.Core.Interfaces;
using Assets.CS.TabletopUI;
using Assets.TabletopUi.Scripts.Infrastructure;
using Harmony;
using IlilimModUtils;
using Partiality.Modloader;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/**
 * Mod that rebinds escape to dismiss all visible panels. Settings are rebound to shif+escape
 * Will hide notifications and information panels before situation windows.
 */

namespace EscapeDismisses
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

    // Utility class to keep track of all NotificationWindows spawned so we can hide them later
    class WindowController
    {
        // References to the windows, could be a dereferenced null
        public static List<NotificationWindow> pastNotifications = new List<NotificationWindow>();
        public static bool tokenDetailsVisible = true;
        public static bool aspectDetailsVisible = false;
        private static DateTime lastClosed = DateTime.MinValue;

        public static bool AnyTopPanelsVisible()
        {
            // Our state hijack does take into account animations, 0.35 seconds what I measured that animation takes
            // If player double taps esc that quickly it's reasonable to think that they want to close all windows
            if ((DateTime.Now - lastClosed).TotalSeconds < 0.35)
            {
                return false;
            }
            Registry.Retrieve<INotifier>().PushTextToLog("__GetDetailWindowsActivityState()"); // hijacked method
            return tokenDetailsVisible || aspectDetailsVisible || AnyNotificationsVisible();
        }

        public static void CloseAll(bool excludeSituations = false)
        {
            var prevLastClosed = lastClosed;
            lastClosed = DateTime.Now;
            WindowController.HideAllNotifications();

            // We do a little throttling here because spamming esc can give some minor visual errors
            if ((prevLastClosed - lastClosed).TotalSeconds < 0.35)
            {
                Registry.Retrieve<INotifier>().PushTextToLog("__HideAllNotifications()"); // hijacked method
            }
            if (!excludeSituations)
            {
                Registry.Retrieve<TabletopManager>().CloseAllSituationWindowsExcept(null);
            }
        }

        public static bool AnyNotificationsVisible()
        {
            for (int i = 0; i < pastNotifications.Count; i++)
            {
                if (pastNotifications[i] != null)
                    return true;
            }
            return false;
        }

        public static void HideAllNotifications()
        {
            for (int i = 0; i < pastNotifications.Count; i++)
            {
                if (pastNotifications[i] == null)
                {
                    pastNotifications.RemoveAt(i--); // If we have an old reference remove it and adjust the counter
                }
                else
                {
                    pastNotifications[i].Hide();
                }
            }
        }
    }

    // We store all NotificationWindows when they are built
    [HarmonyPatch(typeof(Notifier), "BuildNotificationWindow", new Type[] { typeof(float) })]
    class Patch_Notifier_BuildNotificationWindow
    {
        static List<NotificationWindow> pastNotifications = new List<NotificationWindow>();

        static void Postfix(NotificationWindow __result)
        {
            Patcher.Run(() =>
            {
                WindowController.pastNotifications.Add(__result);
            });
        }
    }

    // Hijack method to access internal visibility state
    [HarmonyPatch(typeof(TokenDetailsWindow), "ShowElementDetails", new Type[] { typeof(Element), typeof(ElementStackToken) })]
    class Patch_TokenDetailsWindow_ShowElementDetails
    {
        static bool Prefix(Element element, TokenDetailsWindow __instance)
        {
            return Patcher.Run(() =>
            {
                if (element.Id == "__GetActivityState()")
                {
                    WindowController.tokenDetailsVisible = __instance.gameObject.activeInHierarchy;
                    return false;
                }
                return true;
            });
        }
    }

    // Hijack method to access internal visibility state
    [HarmonyPatch(typeof(AspectDetailsWindow), "ShowAspectDetails", new Type[] { typeof(Element), typeof(bool) })]
    class Patch_AspectDetailsWindow_ShowAspectDetails
    {
        static bool Prefix(Element element, AspectDetailsWindow __instance)
        {
            return Patcher.Run(() =>
            {
                if (element.Id == "__GetActivityState()")
                {
                    WindowController.aspectDetailsVisible = __instance.gameObject.activeInHierarchy;
                    return false;
                }
                return true;
            });
        }
    }

    // We want to hide the TabletopUI Notifier's private tokenDetails and aspectDetails, so we hijack
    // Notifier's unused PushTextToLog debugging method to avoid having to work around access restrictions
    [HarmonyPatch(typeof(Notifier), "PushTextToLog", new Type[] { typeof(string) })]
    class Patch_Notifier_PushTextToLog {
        static bool Prefix(TokenDetailsWindow ___tokenDetails, AspectDetailsWindow ___aspectDetails, string text)
        {
            return Patcher.Run(() =>
            {
                if (text == "__HideAllNotifications()")
                {
                    ___aspectDetails.Hide();
                    ___tokenDetails.Hide();
                    return false;
                }
                else if (text == "__GetDetailWindowsActivityState()")
                {
                    ___tokenDetails.ShowElementDetails(new Element("__GetActivityState()", null, null, 0, null), null);
                    ___aspectDetails.ShowAspectDetails(new Element("__GetActivityState()", null, null, 0, null), false);
                    return false;
                }

                return true;
            });
        }
    }

    [HarmonyPatch(typeof(HotkeyWatcher))]
    [HarmonyPatch("WatchForGameplayHotkeys")]
    class Patch_HotkeyWatcher_WatchForGameplayHotkeys
    {
        static bool Prefix()
        {
            return Patcher.Run(() =>
            {
                // If escape was not pressed or if it was pressed with either shift key we run the game's default function
                if (!Input.GetKeyDown(KeyCode.Escape) || (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
                {
                    return true;
                }
                WindowController.CloseAll(excludeSituations: WindowController.AnyTopPanelsVisible());
                return false;
            });
        }
    }
}
