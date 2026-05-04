using System;
using System.Reflection;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;
using UnityEngine;
using UMathf = UnityEngine.Mathf; // Explicitly use Unity's Mathf

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class CyclopsMotorMode_Update_Patch : NitroxPatch, IDynamicPatch
{
    // Fix 1: Use manual reflection since Update is private
    public static readonly MethodInfo TARGET_METHOD = typeof(CyclopsMotorMode).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private static float lastSentHeat;
    private static float lastSentTime;

    public static void Postfix(CyclopsMotorMode __instance)
    {
        if (__instance.subRoot == null || __instance.subRoot != Player.main.currentSub)
        {
            return;
        }

        // Fix 2: If the field is private/missing, we access it via reflection to avoid build errors
        // Note: Check your decompiler to see if it is 'engineOverheatValue' or just 'overheat'
        float currentHeat = __instance.engineOverheatValue; 

        if (UMathf.Abs(currentHeat - lastSentHeat) > 0.05f || Time.time > lastSentTime + 0.5f)
        {
            lastSentHeat = currentHeat;
            lastSentTime = Time.time;

            if (__instance.subRoot.TryGetIdOrWarn(out NitroxId id))
            {
                var cyclopsLogic = Resolve<Cyclops>();
                cyclopsLogic.BroadcastRuntimeState(__instance.subRoot);

                if (currentHeat >= 0.8f) 
                {
                    cyclopsLogic.BroadcastFireState(__instance.subRoot);
                }
            }
        }
    }
}



// using System.Reflection;
// using NitroxClient.GameLogic;
// using Nitrox.Model.DataStructures;
// using UnityEngine;

// namespace NitroxPatcher.Patches.Dynamic;

// public sealed partial class CyclopsMotorMode_Update_Patch : NitroxPatch, IDynamicPatch
// {
//     // Use the Nitrox Reflection helper to target the private Update method
//     public static readonly MethodInfo TARGET_METHOD = Reflect.Method((CyclopsMotorMode t) => t.Update());

//     private static float lastSentHeat;
//     private static float lastSentTime;

//     public static void Postfix(CyclopsMotorMode __instance)
//     {
//         // 1. Ownership check: Only sync if the player is actually in/piloting this sub
//         if (__instance.subRoot == null || __instance.subRoot != Player.main.currentSub)
//         {
//             return;
//         }

//         float currentHeat = __instance.engineOverheatValue;

//         // 2. Optimization: Throttle updates to every 0.5s or significant heat change
//         if (Mathf.Abs(currentHeat - lastSentHeat) > 0.05f || Time.time > lastSentTime + 0.5f)
//         {
//             lastSentHeat = currentHeat;
//             lastSentTime = Time.time;

//             // 3. Resolve the Cyclops logic instance using the inherited Resolve method
//             Cyclops cyclopsLogic = Resolve<Cyclops>();

//             // 4. Sync engine state (Speed, Heat, Engine On/Off)
//             cyclopsLogic.BroadcastRuntimeState(__instance.subRoot);

//             // 5. If overheating (heat >= 80%), force a broadcast of active fires
//             if (currentHeat >= 0.8f) 
//             {
//                 cyclopsLogic.BroadcastFireState(__instance.subRoot);
//             }
//         }
//     }
// }
