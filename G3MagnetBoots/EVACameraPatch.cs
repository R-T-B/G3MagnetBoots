using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace G3MagnetBoots
{
    [HarmonyPatch(typeof(FlightCamera))]
    public static class Patch_FlightCamera_SetMode_EvaLocked
    {
        [HarmonyPrefix]
        [HarmonyPatch("setMode")]
        public static bool Prefix(FlightCamera __instance, ref FlightCamera.Modes m)
        {
            var active = FlightGlobals.ActiveVessel;
            if (active == null) return true;

            bool evaOnHull = G3MagnetBootsModule.ActiveEvaOnHull();
            
            if (active.isEVA && m == FlightCamera.Modes.LOCKED && !evaOnHull)
            {
                return false;
            }

            if (active.isEVA &&
                __instance.mode == FlightCamera.Modes.LOCKED &&
                m == FlightCamera.Modes.FREE &&
                Patch_FlightCamera_LateUpdate_EvaLocked.InLateUpdate)
            {
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(FlightCamera), "LateUpdate")]
    internal static class Patch_FlightCamera_LateUpdate_EvaLocked
    {
        private static readonly MethodInfo miUpdateFoR =
            AccessTools.Method(typeof(FlightCamera), "updateFoR");

        private static readonly FieldInfo fiFoRlerp =
            AccessTools.Field(typeof(FlightCamera), "FoRlerp");

        private static readonly MethodInfo miSetMode =
            AccessTools.Method(typeof(FlightCamera), "setMode", new[] { typeof(FlightCamera.Modes) });

        internal static bool InLateUpdate;

        // Number of consecutive frames the EVA has been off the hull
        private static int framesOffHull = 0;

        // Tune this if needed:
        private const int OffHullFramesToUnlock = 5;

        [HarmonyPrefix]
        private static void Prefix()
        {
            InLateUpdate = true;
        }

        [HarmonyPostfix]
        private static void Postfix(FlightCamera __instance)
        {
            try
            {
                var active = FlightGlobals.ActiveVessel;
                if (active == null || !active.isEVA)
                    return;

                bool evaOnHull = G3MagnetBootsModule.ActiveEvaOnHull();

                if (evaOnHull)
                {
                    framesOffHull = 0;
                }
                else
                {
                    framesOffHull++;
                }

                // Only force unlock if we've been *reliably* off hull for a few frames
                if (!evaOnHull &&
                    framesOffHull >= OffHullFramesToUnlock &&
                    __instance.mode == FlightCamera.Modes.LOCKED)
                {
                    // Temporarily drop the LateUpdate guard so our own setMode() is allowed
                    InLateUpdate = false;
                    miSetMode.Invoke(__instance, new object[] { FlightCamera.Modes.FREE });
                    return;
                }

                // If not on hull, do not apply custom LOCKED orientation logic
                if (!evaOnHull)
                    return;

                if (__instance.mode != FlightCamera.Modes.LOCKED)
                    return;

                // Existing orientation logic, only while firmly on hull
                Quaternion shpRel = __instance.GetCameraFoR(FoRModes.SHP_REL);
                Vector3 up = FlightGlobals.ActiveVessel.transform.up.normalized;
                Vector3 kerbalFwd = shpRel * Vector3.forward;

                Vector3 flatFwd = Vector3.ProjectOnPlane(kerbalFwd, up);
                if (flatFwd.sqrMagnitude < 1e-6f)
                {
                    flatFwd = Vector3.ProjectOnPlane(shpRel * Vector3.right, up);
                }
                flatFwd.Normalize();

                miUpdateFoR.Invoke(
                    __instance,
                    new object[]
                    {
                    Quaternion.LookRotation(flatFwd, up),
                    fiFoRlerp.GetValue(__instance)
                    });
            }
            finally
            {
                InLateUpdate = false;
            }
        }
    }

}
