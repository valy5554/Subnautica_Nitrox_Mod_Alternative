using System.Collections.Generic;
using System;
using System.Reflection;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.MonoBehaviours;
using NitroxClient.MonoBehaviours.Cyclops;
using UnityEngine;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class CyclopsRuntimeStateProcessor : IClientPacketProcessor<CyclopsRuntimeState>
{
    private static readonly FieldInfo heatField =
        typeof(CyclopsMotorMode).GetField("engineOverheatValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly Dictionary<NitroxId, (bool overheat, bool critical, bool fire)> lastAlertByCyclops = new();

    public Task Process(ClientProcessorContext context, CyclopsRuntimeState packet)
    {
        if (!NitroxEntity.TryGetObjectFrom(packet.CyclopsId, out GameObject cyclops))
        {
            return Task.CompletedTask;
        }

        SubControl subControl = cyclops.GetComponentInChildren<SubControl>(true);
        CyclopsEngineChangeState engineState = cyclops.GetComponentInChildren<CyclopsEngineChangeState>(true);
        CyclopsMotorMode motorMode = cyclops.GetComponentInChildren<CyclopsMotorMode>(true);
        NitroxCyclops nitroxCyclops = cyclops.GetComponentInChildren<NitroxCyclops>(true);

        if (subControl != null)
        {
            subControl.useThrottleIndex = Mathf.Clamp(packet.EngineModeIndex, 0, 2);
            if (!packet.EngineOn)
            {
                subControl.throttle = Vector3.zero;
            }
        }

        if (engineState != null && engineState.motorMode != null)
        {
            engineState.motorMode.engineOn = packet.EngineOn;
            if (!packet.EngineOn)
            {
                engineState.startEngine = false;
            }
            engineState.motorMode.cyclopsMotorMode = (CyclopsMotorMode.CyclopsMotorModes)Mathf.Clamp(packet.EngineModeIndex, 0, 2);
        }

        if (motorMode != null && heatField != null)
        {
            heatField.SetValue(motorMode, packet.EngineHeat);
        }

        if (nitroxCyclops != null)
        {
            // Keep AI/HUD noise on remote side alive while engine mode changes.
            nitroxCyclops.SetRemoteNoisePercent(packet.EngineOn ? Mathf.Clamp01(packet.EngineModeIndex / 2f) : 0f);
        }

        HandleVoiceAlerts(packet, cyclops.GetComponentInChildren<SubRoot>(true));
        return Task.CompletedTask;
    }

    private static void HandleVoiceAlerts(CyclopsRuntimeState packet, SubRoot subRoot)
    {
        if (subRoot == null || subRoot.voiceNotificationManager == null)
        {
            return;
        }

        lastAlertByCyclops.TryGetValue(packet.CyclopsId, out (bool overheat, bool critical, bool fire) prev);
        if (packet.IsOverheating && !prev.overheat)
        {
            TryPlayByKeywords(subRoot, new[] { "overheat", "overheating" });
        }
        if (packet.IsCriticalTemperature && !prev.critical)
        {
            TryPlayByKeywords(subRoot, new[] { "critical", "temperature", "temp" });
        }
        if (packet.HasActiveFire && !prev.fire)
        {
            TryPlayByKeywords(subRoot, new[] { "fire" });
        }

        lastAlertByCyclops[packet.CyclopsId] = (packet.IsOverheating, packet.IsCriticalTemperature, packet.HasActiveFire);
    }

    private static void TryPlayByKeywords(SubRoot subRoot, string[] keywords)
    {
        object notification = FindNotificationByKeywords(subRoot, keywords);
        if (notification == null)
        {
            return;
        }

        MethodInfo method = subRoot.voiceNotificationManager.GetType().GetMethod("PlayVoiceNotification", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
        {
            return;
        }

        ParameterInfo[] p = method.GetParameters();
        if (p.Length == 3)
        {
            method.Invoke(subRoot.voiceNotificationManager, new object[] { notification, false, true });
        }
        else if (p.Length == 2)
        {
            method.Invoke(subRoot.voiceNotificationManager, new object[] { notification, false });
        }
        else if (p.Length == 1)
        {
            method.Invoke(subRoot.voiceNotificationManager, new object[] { notification });
        }
    }

    private static object FindNotificationByKeywords(SubRoot subRoot, string[] keywords)
    {
        Type t = subRoot.GetType();
        FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (FieldInfo f in fields)
        {
            string name = f.Name.ToLowerInvariant();
            // Avoid false positives: "fire" matches suppression/extinguish notifications too.
            if (name.Contains("suppress") || name.Contains("supression") || name.Contains("suppression") || name.Contains("extinguish"))
            {
                continue;
            }
            bool allMatch = true;
            for (int i = 0; i < keywords.Length; i++)
            {
                if (!name.Contains(keywords[i]))
                {
                    allMatch = false;
                    break;
                }
            }
            if (!allMatch)
            {
                continue;
            }

            object value = f.GetValue(subRoot);
            if (value != null)
            {
                return value;
            }
        }
        return null;
    }
}

