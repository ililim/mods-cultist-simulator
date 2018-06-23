using Harmony;
using IlilimModUtils;
using Partiality.Modloader;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

/**
 * Mod that allows the player to change the game speed
 */

namespace SpeedChanger
{
    // Initialize the mod through Partiality
    class Mod : PartialityMod
    {
        public static float? SpeedMultiplier = null;

        public override void Init()
        {
            Patcher.Run(() =>
            {
                string path = Application.persistentDataPath + "/config.ini";
                if (File.Exists(path))
                {
                    string text = File.ReadAllText(path);
                    Regex configPattern = new Regex(@"gamespeed=([0-9]*[.]?[0-9]+)");
                    Match match = configPattern.Match(text);
                    if (match.Success)
                    {
                        SpeedMultiplier = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture.NumberFormat);
                    }
                }

                HarmonyInstance
                    .Create("ililim.cultistsimulatormods." + GetType().Namespace.ToLower())
                    .PatchAll(Assembly.GetExecutingAssembly());
            });
        }
    }

    [HarmonyPatch(typeof(Heart), "AdvanceTime", new Type[] { typeof(float) })]
    class Patch_Heart_AdvanceTime
    {
        static void Prefix(ref float intervalThisBeat)
        {
            try
            {
                if (Mod.SpeedMultiplier != null)
                    intervalThisBeat = intervalThisBeat * (float)Mod.SpeedMultiplier;

            }
            catch (Exception e)
            {
                Patcher.HandleException(e);
            }
        }
    }
}

