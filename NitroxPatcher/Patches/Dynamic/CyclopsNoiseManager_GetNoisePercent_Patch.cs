using System.Reflection;
using HarmonyLib;
using NitroxClient.MonoBehaviours.Cyclops;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Forces Cyclops noise percent to match the authoritative pilot.
/// This fixes Cyclops HUD noise meter and any systems that read CyclopsNoiseManager.GetNoisePercent().
/// </summary>
public sealed partial class CyclopsNoiseManager_GetNoisePercent_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((CyclopsNoiseManager t) => t.GetNoisePercent());
    private static float dummyResult;

    public static void Postfix(CyclopsNoiseManager __instance, ref float __result)
    {
        // Engine off => noise must be 0 even for local pilot.
        CyclopsMotorMode motorMode = __instance.GetComponentInParent<CyclopsMotorMode>();
        if (motorMode != null && !motorMode.engineOn)
        {
            __result = 0f;
            return;
        }

        // If we are locally piloting THIS cyclops, keep the game's real value.
        if (Player.main != null && Player.main.isPiloting && Player.main.currentSub != null)
        {
            Transform a = Player.main.currentSub.transform;
            Transform b = __instance.transform;
            if (a == b || a.IsChildOf(b) || b.IsChildOf(a))
            {
                return;
            }
        }

        NitroxCyclops nitroxCyclops = __instance.GetComponentInParent<NitroxCyclops>();
        if (nitroxCyclops == null)
        {
            return;
        }

        // If we have fresh authoritative data, override noise percent.
        if (Time.unscaledTime - nitroxCyclops.RemoteNoiseUpdatedAt <= 1.0f)
        {
            __result = nitroxCyclops.RemoteNoisePercent;
        }
    }

    public override void Patch(Harmony harmony)
    {
        PatchPostfix(harmony, TARGET_METHOD, Reflect.Method(() => Postfix(default, ref dummyResult)));
    }
}

