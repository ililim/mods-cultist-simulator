using Assets.Core.Entities;
using Assets.Core.Interfaces;
using Assets.CS.TabletopUI;
using Assets.TabletopUi;
using Assets.TabletopUi.Scripts.Infrastructure;
using Assets.TabletopUi.SlotsContainers;
using Harmony;
using Partiality.Modloader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

/**
 * Various utilities for controllering the game for Ililim's other mods
 */

namespace IlilimModUtils
{
    // Initialize the mod through Partiality
    class Mod : PartialityMod
    {
        private static bool? shouldDebug = null;
        public static bool ShouldDebug
        {
            get {
                if (shouldDebug == null)
                {
                    string path = Application.persistentDataPath + "/config.ini";
                    shouldDebug = File.Exists(path) ? File.ReadAllText(path).Contains("debug=1") : false;
                }
                return (bool)shouldDebug;
            }
        }

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
    
    // Wrapper for running patched methods with optional debug logging
    public class Patcher
    {
        public static T Run<T>(Func<T> patch, T defaultReturn)
        {
            try
            {
                return patch();
            }
            catch (Exception e)
            {
                HandleException(e);
                return defaultReturn;
            }
        }

        // By default a patch returns true on error (causing it to run the original method)
        public static bool Run(Func<bool> patch, bool defaultReturn = true)
        {
            return Run<bool>(patch, defaultReturn);
        }

        public static void Run(Action patch)
        {
            Run<object>(() => {
                patch();
                return null;
            }, null);
        }


        public static void HandleException(Exception e)
        {
            if (Mod.ShouldDebug)
                FileLog.Log("Error occurred: \n" + e.ToString());
        }
    }

    // Validates whether elements (for our purposes) are active and unused by other game controllers
    class Validator
    {
        public static bool Available(SlotToFill slotToFill)
        {
            return slotToFill != null && Available(slotToFill.TokenAndSlot) && Available(slotToFill.ElementStackToken);
        }

        public static bool Available(TokenAndSlot tokenAndSlot)
        {
            return tokenAndSlot != null && Available(tokenAndSlot.Token) && Available(tokenAndSlot.RecipeSlot);
        }

        public static bool Available(ElementStackToken stack)
        {
            return !(
                stack == null ||
                stack.Equals(null) ||
                stack.Defunct ||
                stack.IsBeingAnimated ||
                DraggableToken.itemBeingDragged == stack
            );
        }

        public static bool Available(RecipeSlot slot)
        {
            return !(
                slot == null ||
                slot.Equals(null) ||
                slot.Defunct ||
                slot.IsGreedy ||
                slot.IsBeingAnimated ||
                slot.GetElementStackInSlot() != null
            );
        }

        public static bool Available(SituationToken token)
        {
            return !(
                token == null ||
                token.Equals(null) ||
                token.Defunct ||
                token.IsBeingAnimated
            );
        }
    }

    public class Positions
    {
        public static double GetDistanceBetween(float[] left, float[] right)
        {
            // Pythagorean distance between two points
            var distance = Math.Sqrt(Math.Pow(left[0] - right[0], 2) + Math.Pow(left[1] - right[1], 2));
            return distance;
        }

        // The registry of all tokens returns interfaces which do not directly expose their
        // location data, so we have to decode their SaveLocationInfo to get their position
        public static float[] GetPosition(string locationInfo)
        {
            // Structure: x_y_GUID
            string[] PositionParts = locationInfo.Split('_');
            float x = float.Parse(PositionParts[0]);
            float y = float.Parse(PositionParts[1]);
            return new[] { x, y };
        }

        public static float[] GetPosition(IElementStack element)
        {
            return GetPosition(element.SaveLocationInfo);
        }

        public static float[] GetPosition(SituationController situation)
        {
            return GetPosition(situation.situationToken.SaveLocationInfo);
        }

        public static List<IElementStack> GetAdjacentStacks(SituationController situation, float maxDistance = float.MaxValue)
        {
            return GameBoard.GetAllStacks()
                .Where(stack => GetDistanceBetween(GetPosition(stack), GetPosition(situation)) < maxDistance)
                .OrderByDescending(o => GetPosition(o)[1]) // Order by y (higher first)
                .ThenBy(o => GetPosition(o)[0])            // Order by x (lower first)
                .ToList();
        }

        public static List<SituationController> GetSituationsRelativeTo(ElementStackToken stack)
        {
            return GameBoard.GetAllSituations()
                .OrderBy(situ => GetDistanceBetween(GetPosition(situ), GetPosition(stack)))
                .ThenByDescending(o => GetPosition(o)[1])  // Order by y (higher first)
                .ThenBy(o => GetPosition(o)[0])            // Order by x (lower first)
                .ToList();
        }

        public static List<IElementStack> GetStacksRelativeTo(SituationController situation)
        {
            return GameBoard.GetAllStacks()
                .OrderBy(stack => GetDistanceBetween(GetPosition(stack), GetPosition(situation)))
                .ThenByDescending(o => GetPosition(o)[1])  // Order by y (higher first)
                .ThenBy(o => GetPosition(o)[0])            // Order by x (lower first)
                .ToList();
        }
    }

    // Data structure to keep track of which slots to fill with what
    public class SlotToFill
    {
        public TokenAndSlot TokenAndSlot { get; set; }
        public ElementStackToken ElementStackToken { get; set; }
    }

    public class GameBoard
    {
        public static List<IElementStack> GetAllStacks()
        {
            var tabletopManager = Registry.Retrieve<TabletopManager>();
            return tabletopManager._tabletop.GetElementStacksManager().GetStacks().ToList();
        }

        public static List<SituationController> GetAllSituations()
        {
            return Registry.Retrieve<SituationsCatalogue>().GetRegisteredSituations();
        }

        public static SituationController GetOpenSituation()
        {
            return Registry.Retrieve<SituationsCatalogue>().GetOpenSituation();
        }
    }

    public class SituSlotController
    {
        public static List<RecipeSlot> _lastFoundSlots = new List<RecipeSlot>();

        private static HashSet<SlotToFill> SlotsToFill = new HashSet<SlotToFill>();

        public static List<RecipeSlot> GetAllSlots(SituationController situation)
        {
            // Call hijacked method with the correct type of slot given the current situation state
            _lastFoundSlots = new List<RecipeSlot>();

            if (situation.SituationClock.State == SituationState.Unstarted)
            {
                situation.situationWindow.GetStartingSlotBySaveLocationInfoPath("__GetStartingSlots()");
            }
            else if (
                situation.SituationClock.State == SituationState.Ongoing ||
                situation.SituationClock.State == SituationState.RequiringExecution
            ) {
                situation.situationWindow.GetStartingSlotBySaveLocationInfoPath("__GetOngoingSlots()");
            }

            return _lastFoundSlots;
        }

        public static List<RecipeSlot> GetAllEmptySlots(SituationController situation)
        {
            return GetAllSlots(situation).Where(s => s.GetElementStackInSlot() == null).ToList();
        }

        public static RecipeSlot GetFirstEmptyRecipeSlot(SituationController situation)
        {
            foreach (RecipeSlot slot in GetAllSlots(situation))
            if (slot.GetElementStackInSlot() == null)
            {
                return slot;
            }
            return null;
        }

        // Instantly moves card into slot
        public static void MoveStackIntoSlot(ElementStackToken elementStack, RecipeSlot slot)
        {
            // Make sure slot and stack are valid and empty and we have a match
            // Abort with feedback if we don't
            if (!(Validator.Available(elementStack) && Validator.Available(slot) && StackMatchesSlot(elementStack, slot)))
            {
                SoundManager.PlaySfx("CardDragFail");
                return;
            }

            // Remove glow so that it won't flicker when moved
            elementStack.ShowGlow(false, true);

            // Force stack to remember its last position
            elementStack.lastTablePos = new Vector2?(elementStack.RectTransform.anchoredPosition);

            if (elementStack.Quantity != 1)
            {
                IElementStack newStack = elementStack.SplitAllButNCardsToNewStack(elementStack.Quantity - 1, new Context(Context.ActionSource.PlayerDrag));
                slot.AcceptStack(newStack, new Context(Context.ActionSource.PlayerDrag));
            }
            else
            {
                slot.AcceptStack(elementStack, new Context(Context.ActionSource.PlayerDrag));
            }
        }

        // Add a slot to fill to the queue of animations to perform
        public static void FillSlotEventually(TokenAndSlot tokenAndSlot, ElementStackToken elementStackToken)
        {
            SlotToFill slotToFill = new SlotToFill
            {
                TokenAndSlot = tokenAndSlot,
                ElementStackToken = elementStackToken
            };

            if (Validator.Available(slotToFill) && !AlreadyHandlingSlotToFill(slotToFill))
            {
                SlotsToFill.Add(slotToFill);
            }
        }

        // Batch animate all outstanding slots to fill
        public static void DoFillSlots()
        {
            Choreographer choreographer = Registry.Retrieve<Choreographer>();

            foreach (var slotToFill in SlotsToFill)
                if (Validator.Available(slotToFill) && StackMatchesSlot(slotToFill.ElementStackToken, slotToFill.TokenAndSlot.RecipeSlot))
                {
                    var stack = slotToFill.ElementStackToken;
                    // Force the stack to remember its last position as this param is not updated by the game reliably
                    stack.lastTablePos = new Vector2?(stack.RectTransform.anchoredPosition);
                    stack.SplitAllButNCardsToNewStack(1, new Context(Context.ActionSource.PlayerDrag));
                    choreographer.MoveElementToSituationSlot(stack, slotToFill.TokenAndSlot, new Action<ElementStackToken, TokenAndSlot>(choreographer.ElementGreedyAnimDone));
                }

            SlotsToFill.Clear();
        }

        private static bool AlreadyHandlingSlotToFill(SlotToFill newSlot)
        {
            // We do not want to add the SlotToFill queue if 1) we already are using that token 2) the slot is already going to be filled
            foreach (var existingSlot in SlotsToFill)
            if (
                existingSlot.ElementStackToken == newSlot.ElementStackToken ||
                (existingSlot.TokenAndSlot.Token == newSlot.TokenAndSlot.Token && existingSlot.TokenAndSlot.RecipeSlot == newSlot.TokenAndSlot.RecipeSlot)
            ) return true;

            return false;
        }

        public static bool StackMatchesSlot(IElementStack elementStack, RecipeSlot slot)
        {
            return slot.GetSlotMatchForStack(elementStack).MatchType == SlotMatchForAspectsType.Okay;
        }

        public static void Clear()
        {
            SlotsToFill.Clear();
        }
    }

    [HarmonyPatch(typeof(Heart))]
    [HarmonyPatch("Beat")]
    class Patch_Heart_Beat
    {
        public static void Prefix(int ___beatCounter)
        {
            Patcher.Run(() =>
            {
                if (___beatCounter >= 19) // It will turn into 20 this beat
                    SituSlotController.DoFillSlots();
            });
        }
    }

    [HarmonyPatch(typeof(Heart))]
    [HarmonyPatch("Clear")]
    class Patch_Heart_Clear
    {
        public static void PostFix()
        {
            Patcher.Run(() =>
            {
                SituSlotController.Clear();
            });
        }
    }

    // We will need to fetch all starting and ongoing slots for the currently open window. These are hidden behind a few private
    // properties and restrictive interfaces. To work around this we hijack one of the methods on SituationWindow
    [HarmonyPatch(typeof(SituationWindow), "GetStartingSlotBySaveLocationInfoPath", new Type[] { typeof(string) })]
    class Patch_SituationWindow_GetStartingSlotBySaveLocationInfoPath
    {
        public static bool Prefix(string locationInfo, StartingSlotsManager ___startingSlots, OngoingSlotManager ___ongoing)
        {
            return Patcher.Run(() =>
            {
                if (!(locationInfo == "__GetAllSlots()" || locationInfo == "__GetStartingSlots()" || locationInfo == "__GetOngoingSlots()"))
                    return true;

                SituSlotController._lastFoundSlots = new List<RecipeSlot>();

                if (locationInfo == "__GetAllSlots()" || locationInfo == "__GetStartingSlots()")
                {
                    SituSlotController._lastFoundSlots.AddRange(
                        new List<RecipeSlot>(___startingSlots.GetAllSlots())
                    );
                }
                if (locationInfo == "__GetAllSlots()" || locationInfo == "__GetOngoingSlots()")
                {
                    SituSlotController._lastFoundSlots.AddRange(
                        new List<RecipeSlot>(___ongoing.GetAllSlots())
                    );
                }

                return false;
            });
        }
    }
}
