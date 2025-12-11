using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace G3MagnetBoots
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    internal sealed class HarmonyLoader : MonoBehaviour
    {
        private void Awake()
        {
            try
            {
                Harmony h2 = new("EVAMagBoots");
                h2.PatchAll(typeof(HarmonyLoader).Assembly);
                AccessTools.Method(typeof(KerbalEVA), "SetupFSM");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
            }
        }
    }

    // Patch into KerbalEVA.cs SetupFSM method to initialize custom hull states and events
    [HarmonyPatch(typeof(KerbalEVA), "SetupFSM")]
    internal static class Patch_KerbalEVA_SetupFSM
    {
        static void Postfix(KerbalEVA __instance)
        {
            if (__instance == null) return;
            var module = __instance.part?.FindModuleImplementing<G3MagnetBootsModule>();
            module?.HookIntoEva(__instance);
        }
    }
}
