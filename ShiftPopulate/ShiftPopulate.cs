using Assets.CS.TabletopUI;
using Assets.TabletopUi;
using Assets.TabletopUi.Scripts.Infrastructure;
using Harmony;
using IlilimModUtils;
using Partiality.Modloader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

/*
 * Mod that allows players to populate situation slots with shift+click instead of dragging
 */

namespace ShiftPopulate
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

    [HarmonyPatch(typeof(ElementStackToken), "OnPointerClick", new Type[] { typeof(PointerEventData) })]
    class Patch_ElementStackToken_OnPointerClick
    {
        static bool Prefix(ElementStackToken __instance)
        {
            return Patcher.Run(() =>
            {
                // If neither shift is down give back control to the game immediately
                if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
                    return true;

                // Do not move cards in Mansus. Supporting this could be a future addition
                if (TabletopManager.IsInMansus()) return false;

                var stack = __instance;
                var situation = GameBoard.GetOpenSituation();
                List<RecipeSlot> slots = null;

                if (situation == null)
                {
                    foreach (var closedSituation in Positions.GetSituationsRelativeTo(stack))
                        if (closedSituation.CanAcceptStackWhenClosed(stack))
                        {
                            closedSituation.OpenWindow();
                            situation = closedSituation;
                            break;
                        }
                }

                if (situation == null)
                {
                    // Let controller handle the fail state
                    SituSlotController.MoveStackIntoSlot(stack, null);
                }
                else
                {
                    var populatedSlot = false;
                    slots = SituSlotController.GetAllEmptySlots(situation);
                    for (int i = 0; i < slots.Count; i++)
                    {
                        if (SituSlotController.StackMatchesSlot(stack, slots[i]))
                        {
                            SituSlotController.MoveStackIntoSlot(stack, slots[i]);
                            populatedSlot = true;
                            break;
                        }
                    }

                    // There is no slot available for us, allow the controller to handle the fail state
                    if (!populatedSlot)
                    {
                        SituSlotController.MoveStackIntoSlot(stack, null);
                    }
                }

                return false;
            });
        }
    }

    [HarmonyPatch(typeof(RecipeSlot), "OnPointerClick", new Type[] { typeof(PointerEventData) })]
    class Patch_RecipeSlot_OnPointerClick
    {
        static bool Prefix(RecipeSlot __instance)
        {
            return Patcher.Run(() =>
            {
                // If neither shift is down give back control to the game immediately
                if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
                    return true;

                var slot = __instance;
                var situation = GameBoard.GetOpenSituation();

                if (situation == null || slot.GetElementStackInSlot() != null || !SituSlotController.GetAllEmptySlots(situation).Contains(slot))
                    return true;

                foreach (var stack in Positions.GetStacksRelativeTo(situation))
                if (SituSlotController.StackMatchesSlot(stack, slot))
                {
                    SituSlotController.MoveStackIntoSlot(stack as ElementStackToken, slot);
                    break;
                }

                return false;
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
                if (!Input.GetKeyDown(KeyCode.E))
                    return true;

                SituationController situation = GameBoard.GetOpenSituation();

                if (situation != null)
                    foreach (var slot in SituSlotController.GetAllSlots(situation).AsEnumerable().Reverse())
                    {
                        var stack = slot.GetElementStackInSlot() as ElementStackToken;
                        if (stack != null)
                        {
                            stack.ReturnToTabletop(new Context(Context.ActionSource.PlayerDrag));
                            break;
                        }
                    }

                return false;
            });
        }
    }
}
