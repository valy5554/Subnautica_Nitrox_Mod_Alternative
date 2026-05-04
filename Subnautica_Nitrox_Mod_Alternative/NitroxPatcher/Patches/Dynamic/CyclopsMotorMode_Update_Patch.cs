using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

[HarmonyPatch(typeof(CyclopsMotorMode))]
[HarmonyPatch(nameof(CyclopsMotorMode.Update))]
public static class CyclopsMotorMode_Update_Patch
{
    private static float lastSentHeat;
    private static float lastSentTime;

    public static void Postfix(CyclopsMotorMode __instance)
    {
        // Only the player currently piloting (or the "owner" of the sub logic) should broadcast
        if (__instance.subRoot == null || __instance.subRoot != Player.main.currentSub)
        {
            return;
        }

        // Optimization: Only send if heat changed significantly or every 0.5 seconds
        float currentHeat = __instance.engineOverheatValue;
        bool hasFire = false; // Add logic here to check for actual fire if needed

        if (Mathf.Abs(currentHeat - lastSentHeat) > 0.05f || Time.time > lastSentTime + 0.5f)
        {
            lastSentHeat = currentHeat;
            lastSentTime = Time.time;

            // This triggers the BroadcastRuntimeState method in your Cyclops.cs
            NitroxPatch.Resolve<Cyclops>().BroadcastRuntimeState(__instance.subRoot);

            // CRITICAL: If heat is high enough to cause fire, force a damage sync
            // Your Cyclops.cs GetActiveRoomFires() will now pick up the new fires
            if (currentHeat >= 0.8f) 
            {
                NitroxPatch.Resolve<Cyclops>().OnCreateDamagePoint(__instance.subRoot);
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