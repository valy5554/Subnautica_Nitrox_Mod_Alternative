using System.Collections.Generic;
using System.Reflection;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Physics tick safety net: when Cyclops engine is off, propulsion must stay at zero.
/// </summary>
public sealed partial class SubControl_FixedUpdate_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((SubControl t) => t.FixedUpdate());
    private static readonly Dictionary<NitroxId, float> nextRuntimeBroadcastAtById = new();
    private static readonly Dictionary<NitroxId, bool> lastHasFireById = new();
    private static readonly Dictionary<NitroxId, float> nextDamageBroadcastAtById = new();

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

    public static void Postfix(SubControl __instance)
    {
        if (Player.main == null || !Player.main.isPiloting || Player.main.currentSub == null)
        {
            return;
        }

        SubRoot subRoot = __instance.GetComponentInParent<SubRoot>();
        if (subRoot == null || subRoot != Player.main.currentSub)
        {
            return;
        }

        if (!subRoot.TryGetIdOrWarn(out NitroxId id))
        {
            return;
        }

        float now = Time.unscaledTime;
        if (!nextRuntimeBroadcastAtById.TryGetValue(id, out float nextRuntimeAt) || now >= nextRuntimeAt)
        {
            nextRuntimeBroadcastAtById[id] = now + 0.1f; // 10Hz explicit runtime packet
            Resolve<Cyclops>().BroadcastRuntimeState(subRoot);
        }

        bool hasFire = false;
        SubFire subFire = subRoot.GetComponent<SubFire>();
        if (subFire != null && subFire.roomFires != null)
        {
            foreach (KeyValuePair<CyclopsRooms, SubFire.RoomFire> roomFire in subFire.roomFires)
            {
                if (roomFire.Value?.spawnNodes == null)
                {
                    continue;
                }
                for (int i = 0; i < roomFire.Value.spawnNodes.Length; i++)
                {
                    if (roomFire.Value.spawnNodes[i].childCount > 0)
                    {
                        hasFire = true;
                        break;
                    }
                }
                if (hasFire)
                {
                    break;
                }
            }
        }

        if (!lastHasFireById.TryGetValue(id, out bool lastHasFire) || hasFire != lastHasFire)
        {
            lastHasFireById[id] = hasFire;
            // Reuse existing packet path that contains full fire-node state.
            Resolve<Cyclops>().OnCreateDamagePoint(subRoot);
        }

        // Also periodically push full CyclopsDamage state while piloting to guarantee fire sync.
        if (!nextDamageBroadcastAtById.TryGetValue(id, out float nextDamageAt) || now >= nextDamageAt)
        {
            nextDamageBroadcastAtById[id] = now + 0.5f;
            Resolve<Cyclops>().OnCreateDamagePoint(subRoot);
        }
    }
}

