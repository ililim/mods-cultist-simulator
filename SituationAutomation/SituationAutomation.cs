using Assets.Core.Interfaces;
using Assets.CS.TabletopUI;
using Assets.TabletopUi;
using Harmony;
using IlilimModUtils;
using Partiality.Modloader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/**
 * Mod that enables automation for situations/verbs
 */

namespace SituationAutomation
{
    // Initialize the mod through Partiality
    class Mod : PartialityMod
    {
        public static float AutomationDistance = 190f;

        public override void Init()
        {
            Patcher.Run(() =>
            {
                string path = Application.persistentDataPath + "/config.ini";
                if (File.Exists(path))
                {
                    string text = File.ReadAllText(path);
                    Regex configPattern = new Regex(@"automationdistance=([0-9]*[.]?[0-9]+)");
                    Match match = configPattern.Match(text);
                    if (match.Success)
                    {
                        float.TryParse(match.Groups[1].Value, out float maybeDistance);
                        if (maybeDistance > 0) AutomationDistance = maybeDistance;
                    }
                }
                HarmonyInstance
                    .Create("ililim.cultistsimulatormods." + GetType().Namespace.ToLower())
                    .PatchAll(Assembly.GetExecutingAssembly());
            });
        }
    }

    class SituationAutomator
    {

        static readonly Sprite hammerIconSprite = Resources.Load<Sprite>("icons100/verbs/auction");

        static List<SituationController> automatedSituations = new List<SituationController>();

        static Dictionary<SituationController, DateTime> situationLastCompletionTimes = new Dictionary<SituationController, DateTime>();

        public static bool ToggleSituationAutomation(
            SituationController situation,
            Image slotImage,
            Image slotImageArt,
            GameObject greedyIcon,
            ParticleSystem slotAppearFx
        ) {
            string[] allowedSituationIds = { "work", "study", "dream", "explore", "talk" };

            // Check if we are allowed to automated this situation
            if (!Array.Exists<string>(allowedSituationIds, s => s == situation.GetTokenId()))
                return false;

            if (automatedSituations.Contains(situation))
            {
                automatedSituations.Remove(situation);
                slotImage.gameObject.SetActive(false);
                greedyIcon.gameObject.SetActive(false);
            }
            else
            {
                automatedSituations.Add(situation);
                SoundManager.PlaySfx("SituationTokenShowOngoingSlot");
                SetAutomatedSituationIcon(slotImage, slotImageArt, greedyIcon);
                slotAppearFx.Play();
            }
            return true;
        }

        public static void SetAutomatedSituationIcon(Image slotImage, Image slotImageArt, GameObject greedyIcon)
        {
            slotImage.gameObject.SetActive(true);
            slotImageArt.sprite = hammerIconSprite;
            slotImageArt.color = Color.white;
            greedyIcon.gameObject.SetActive(false);
        }

        public static bool IsAutomated(SituationController situation)
        {
            return automatedSituations.Contains(situation);
        }

        public static bool PopulateSlotWithNearbyStacks(SituationController situation, RecipeSlot slotToFill)
        {
            // Trying to fill all the slots
            var candidateStacks = Positions.GetAdjacentStacks(situation, Mod.AutomationDistance);
            foreach (var stack in candidateStacks)
            if (SituSlotController.StackMatchesSlot(stack, slotToFill))
            {
                var tokenAndSlot = new TokenAndSlot
                {
                    RecipeSlot = slotToFill,
                    Token = situation.situationToken as SituationToken
                };
                SituSlotController.FillSlotEventually(tokenAndSlot, stack as ElementStackToken);
                return true; // Successfully found a token for this slot
            }
            return false; // Failed to find a token for this slot
        }

        public static void DoAutomatedAction(SituationController situation)
        {
            // We do not do any automated actions while the player has its window open or is dragging the token
            if (situation.IsOpen || situation.situationToken as DraggableToken == DraggableToken.itemBeingDragged)
                return;

            switch (situation.SituationClock.State)
            {
                case SituationState.Unstarted:
                    HandleUnstarted(situation);
                    break;
                case SituationState.FreshlyStarted:
                    HandleFreshlyStarted(situation);
                    break;
                case SituationState.Ongoing:
                    HandleOngoing(situation);
                    break;
                case SituationState.RequiringExecution:
                    HandleRequiringExecution(situation);
                    break;
                case SituationState.Complete:
                    HandleComplete(situation);
                    break;
                default:
                    break;
            }
        }

        private static void HandleUnstarted(SituationController situation)
        {
            // Do nothing if we are paused
            if (Registry.Retrieve<TabletopManager>().GetPausedState()) return;

            // Do nothing until at least a second has passed since last completion for smoother UX
            situationLastCompletionTimes.TryGetValue(situation, out DateTime lastCompletionTime);
            if ((lastCompletionTime != null) && ((DateTime.Now - lastCompletionTime).TotalSeconds < 1.2))
                return;

            var emptySlot = SituSlotController.GetFirstEmptyRecipeSlot(situation);

            // If no more slots can be filled we can try to activate the recipe
            if (emptySlot == null)
            {
                situation.AttemptActivateRecipe();
                return;
            }

            // Try to fill the slot and return if successful
            if (PopulateSlotWithNearbyStacks(situation, emptySlot))
                return;

            // We have slots to fill but nothing to fill them with. Possibly we already filled our primary slot
            // and just cannot find any "extra" cards. If that's the case we can activate the recipe with partially filled slots.
            var primarySlot = (situation.situationWindow as SituationWindow).GetPrimarySlot();
            if (primarySlot != null && primarySlot.GetElementStackInSlot() != null)
            {
                situation.AttemptActivateRecipe();
            }
        }

        private static void HandleFreshlyStarted(SituationController situation)
        {
            return;
        }

        private static void HandleOngoing(SituationController situation)
        {
            var emptySlot = SituSlotController.GetFirstEmptyRecipeSlot(situation);
            if (emptySlot != null)
            {
                PopulateSlotWithNearbyStacks(situation, emptySlot);
            }
        }

        private static void HandleRequiringExecution(SituationController situation)
        {
            return;
        }

        private static void HandleComplete(SituationController situation)
        {
            situationLastCompletionTimes[situation] = DateTime.Now;
            situation.DumpAllResults();
        }
    }

    [HarmonyPatch(typeof(SituationController), "ExecuteHeartbeat", new Type[] { typeof(float) })]
    class Patch_SituationController_ExecuteHeartBeat
    {
        public static void Postfix(SituationController __instance, float interval)
        {
            Patcher.Run(() =>
            {
                if (SituationAutomator.IsAutomated(__instance))
                    SituationAutomator.DoAutomatedAction(__instance);
            });
        }
    }

    [HarmonyPatch(typeof(SituationToken), "OnPointerClick", new Type[] { typeof(PointerEventData) })]
    class Patch_SituationToken_OnPointerClick
    {
        public static bool Prefix(
            SituationToken __instance,
            Image ___ongoingSlotImage,
            Image ___ongoingSlotArtImage,
            GameObject ___ongoingSlotGreedyIcon,
            ParticleSystem ___ongoingSlotAppearFX
        ) {
            return Patcher.Run(() =>
            {
                if (!(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
                    return true;

                bool success = SituationAutomator.ToggleSituationAutomation(
                    __instance.SituationController,
                    ___ongoingSlotImage,
                    ___ongoingSlotArtImage,
                    ___ongoingSlotGreedyIcon,
                    ___ongoingSlotAppearFX
                );

                if (!success)
                    Registry.Retrieve<INotifier>().ShowNotificationWindow("I can't automate that -", "Only everyday tasks can be automated.");

                return false;
            });
        }
    }

    // We disable the automated token's ability to render their minislot because we handle that already
    [HarmonyPatch(typeof(SituationToken))]
    [HarmonyPatch("DisplayMiniSlot")]
    class Patch_SituationToken_DisplayMiniSlot
    {
        public static bool Prefix(SituationToken __instance) {
            return Patcher.Run(() =>
            {
                return !SituationAutomator.IsAutomated(__instance.SituationController);
            });
        }
    }

    [HarmonyPatch(typeof(SituationToken))]
    [HarmonyPatch("DisplayStackInMiniSlot")]
    class Patch_SituationToken_DisplayStackInMiniSlot
    {
        public static bool Prefix(SituationToken __instance)
        {
            return Patcher.Run(() =>
            {
                return !SituationAutomator.IsAutomated(__instance.SituationController);
            });
        }
    }
}
