using System.Reflection;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class CyclopsMotorMode_Update_Patch : NitroxPatch, IDynamicPatch
{
    // Use the Nitrox Reflection helper to target the private Update method
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((CyclopsMotorMode t) => t.Update());

    private static float lastSentHeat;
    private static float lastSentTime;

    public static void Postfix(CyclopsMotorMode __instance)
    {
        // 1. Ownership check: Only sync if the player is actually in/piloting this sub
        if (__instance.subRoot == null || __instance.subRoot != Player.main.currentSub)
        {
            return;
        }

        float currentHeat = __instance.engineOverheatValue;

        // 2. Optimization: Throttle updates to every 0.5s or significant heat change
        if (Mathf.Abs(currentHeat - lastSentHeat) > 0.05f || Time.time > lastSentTime + 0.5f)
        {
            lastSentHeat = currentHeat;
            lastSentTime = Time.time;

            // 3. Resolve the Cyclops logic instance using the inherited Resolve method
            Cyclops cyclopsLogic = Resolve<Cyclops>();

            // 4. Sync engine state (Speed, Heat, Engine On/Off)
            cyclopsLogic.BroadcastRuntimeState(__instance.subRoot);

            // 5. If overheating (heat >= 80%), force a broadcast of active fires
            if (currentHeat >= 0.8f) 
            {
                cyclopsLogic.BroadcastFireState(__instance.subRoot);
            }
        }
    }
}


// using HarmonyLib;
// using NitroxClient.GameLogic;
// using UnityEngine;

// namespace NitroxPatcher.Patches.Dynamic;

// [HarmonyPatch(typeof(CyclopsMotorMode))]
// [HarmonyPatch(nameof(CyclopsMotorMode.Update))]
// public static class CyclopsMotorMode_Update_FirePatch
// {
//     private static float lastSentTime;

//     public static void Postfix(CyclopsMotorMode __instance)
//     {
//         // 1. Only sync if this is the Cyclops the player is currently inside/piloting
//         if (__instance.subRoot == null || __instance.subRoot != Player.main.currentSub)
//         {
//             return;
//         }

//         // 2. Optimization: Don't spam the network. Check every 0.5 seconds.
//         if (Time.time < lastSentTime + 0.5f)
//         {
//             return;
//         }
//         lastSentTime = Time.time;

//         // 3. Get the heat value
//         float heat = __instance.engineOverheatValue;

//         // 4. Trigger the broadcast
//         // We use BroadcastRuntimeState to sync the Heat Bar UI
//         NitroxPatch.Resolve<Cyclops>().BroadcastRuntimeState(__instance.subRoot);

//         // 5. If engine is critical (usually > 0.8), force the fire position sync
//         if (heat >= 0.8f)
//         {
//             // This calls the method you just added to Cyclops.cs!
//             NitroxPatch.Resolve<Cyclops>().BroadcastFireState(__instance.subRoot);
//         }
//     }
// }