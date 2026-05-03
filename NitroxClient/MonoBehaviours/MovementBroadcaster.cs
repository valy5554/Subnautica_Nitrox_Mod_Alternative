using System.Collections.Generic;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours.Vehicles;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;

namespace NitroxClient.MonoBehaviours;

public class MovementBroadcaster : MonoBehaviour
{
    // СНИЖАЕМ ГЛОБАЛЬНЫЙ СПАМ С 30 ДО 15 КАДРОВ В СЕКУНДУ
    public const int BROADCAST_FREQUENCY = 50;
    public const float BROADCAST_PERIOD = 1f / BROADCAST_FREQUENCY;

    public static MovementBroadcaster Instance;

    public Dictionary<NitroxId, MovementReplicator> Replicators = [];
    private readonly Dictionary<NitroxId, WatchedEntry> watchedEntries = [];
    private float latestBroadcastTime;

    public void Start()
    {
        if (Instance)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    public void OnDestroy()
    {
        Instance = null;
    }

    public void Update()
    {
        // Use unscaled local time for stable broadcast cadence.
        float currentTime = Time.unscaledTime;
        if (currentTime < latestBroadcastTime + BROADCAST_PERIOD)
        {
            return;
        }
        latestBroadcastTime = currentTime;
        BroadcastLocalData(currentTime);
    }

    private float lastBroadcastLog;

    public void BroadcastLocalData(float time)
    {
        SimulationOwnership simulationOwnership = this.Resolve<SimulationOwnership>();
        List<MovementData> data = [];
        List<NitroxId> watchedIds = [.. watchedEntries.Keys];

        for (int i = watchedIds.Count - 1; i >= 0; i--)
        {
            NitroxId entryId = watchedIds[i];
            WatchedEntry entry = watchedEntries[entryId];

            // If we hold EXCLUSIVE lock, always broadcast at high rate.
            // This prevents "1-2 updates/sec" when IsDriven* checks fail due to hierarchy differences.
            bool forceHighRate = simulationOwnership != null && simulationOwnership.HasExclusiveLock(entryId);
            if (entry.ShouldBroadcastMovement(forceHighRate))
            {
                data.Add(entry.GetMovementData(entryId));
                entry.OnBroadcastPosition();
            }
        }

        if (data.Count > 0)
        {
            this.Resolve<IPacketSender>().Send(new VehicleMovements(data, time));
        }

        // Диагностика: раз в 2 секунды логируем состояние broadcaster'а
        if (Time.unscaledTime - lastBroadcastLog >= 2f)
        {
            lastBroadcastLog = Time.unscaledTime;
            Log.Debug($"[MB] watched={watchedEntries.Count} replicators={Replicators.Count} sent={data.Count} packets this tick");
        }
    }

    public static void RegisterWatched(GameObject gameObject, NitroxId entityId)
    {
        if (!Instance)
            return;
        if (!Instance.watchedEntries.ContainsKey(entityId))
        {
            Instance.watchedEntries.Add(entityId, new(entityId, gameObject.transform));
        }
    }

    public static void UnregisterWatched(NitroxId entityId)
    {
        if (Instance)
            Instance.watchedEntries.Remove(entityId);
    }

    public static void RegisterReplicator(MovementReplicator movementReplicator)
    {
        if (Instance)
            Instance.Replicators.Add(movementReplicator.objectId, movementReplicator);
    }

    public static void UnregisterReplicator(MovementReplicator movementReplicator)
    {
        if (Instance)
            Instance.Replicators.Remove(movementReplicator.objectId);
    }
}
