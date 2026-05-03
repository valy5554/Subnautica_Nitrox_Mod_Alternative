using System.Reflection;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Prevents Cyclops propulsion when the engine is off.
/// This runs for local and remote instances and is a hard safety net against stale throttle state.
/// </summary>
public sealed partial class SubControl_Update_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((SubControl t) => t.Update());

    public static void Prefix(SubControl __instance)
    {
        if (__instance == null || __instance.cyclopsMotorMode == null)
        {
            return;
        }

        if (!__instance.cyclopsMotorMode.engineOn)
        {
            __instance.throttle = Vector3.zero;
            __instance.useThrottleIndex = 0;
        }
    }
}

