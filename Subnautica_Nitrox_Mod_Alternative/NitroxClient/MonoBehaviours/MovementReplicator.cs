using System.Collections.Generic;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.Settings;
using NitroxClient.MonoBehaviours.Cyclops;
using NitroxClient.MonoBehaviours.Vehicles;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;

namespace NitroxClient.MonoBehaviours;

public abstract class MovementReplicator : MonoBehaviour
{
    // WatchedEntry шлёт пакеты каждые ~0.066 сек (15/сек).
    // INTERPOLATION_TIME — буфер ожидания: 4 пакета в запасе = 0.066 * 4 ≈ 0.27 сек.
    public const float INTERPOLATION_TIME = 0.27f;
    public const float SNAPSHOT_EXPIRATION_TIME = 5f * INTERPOLATION_TIME;

    protected readonly LinkedList<Snapshot> buffer = new();
    /// <summary>
    /// To ensure a smooth experience, we need a max allowed latency value which should top the incoming latencies at all times.
    /// Big increments and any decrements of this value will likely cause stutter, so we try to avoid changing this value too much.
    /// But it is required that after a lag spike, we eventually lower down that value, which is done periodically <see cref="NitroxPrefs.LatencyUpdatePeriod"/>.
    /// </summary>
    private Rigidbody rigidbody;
    public NitroxId objectId { get; private set; }

    /// <summary>
    /// Current time must be based on real time to avoid effects from time changes/speed.
    /// Используем Time.unscaledTime — тот же источник что WatchedEntry на отправителе,
    /// это минимизирует рассинхрон временных меток пакетов.
    /// </summary>
    private float CurrentTime => Time.unscaledTime;

    public void AddSnapshot(MovementData movementData, float time)
    {
        // Используем локальное время получения пакета вместо времени отправителя.
        // Это полностью устраняет проблему рассинхрона часов между хостом и клиентом,
        // которая приводила к отрицательной latency и протухшим снапшотам.
        float receiveTime = CurrentTime;
        float occurrenceTime = receiveTime + INTERPOLATION_TIME;

        // Cleaning any previous value change that would occur later than the newly received snapshot
        while (buffer.Last != null && buffer.Last.Value.IsSnapshotNewer(occurrenceTime))
        {
            buffer.RemoveLast();
        }

        buffer.AddLast(new Snapshot(movementData, occurrenceTime));
    }

    public void ClearBuffer() => buffer.Clear();

    public void Start()
    {
        if (!gameObject.TryGetNitroxId(out NitroxId _objectId))
        {
            Log.Error($"Can't start a {nameof(MovementReplicator)} on {name} because it doesn't have an attached: {nameof(NitroxEntity)}");
            Destroy(this);
            return;
        }
        objectId = _objectId;

        rigidbody = GetComponent<Rigidbody>();
        if (gameObject.TryGetComponent(out NitroxCyclops nitroxCyclops))
        {
            nitroxCyclops.SetReceiving();
        }
        else
        {
            if (gameObject.TryGetComponent(out WorldForces worldForces))
            {
                worldForces.enabled = false;
            }
            rigidbody.isKinematic = false;
        }

        MovementBroadcaster.RegisterReplicator(this);
    }

    public void OnDestroy()
    {
        if (gameObject.TryGetComponent(out NitroxCyclops nitroxCyclops))
        {
            nitroxCyclops.SetBroadcasting();
        }
        else
        {
            if (gameObject.TryGetComponent(out WorldForces worldForces))
            {
                worldForces.enabled = true;
            }
        }

        MovementBroadcaster.UnregisterReplicator(this);
    }

    // Максимальная скорость «догона» при рывке буфера — 20 м/с.
    // Если разрыв больше этого — всё равно телепортируем (игрок далеко улетел).
    private const float TELEPORT_THRESHOLD = 50f;
    // Скорость сглаживания позиции через SmoothDamp — чем меньше, тем плавнее но с задержкой
    // SMOOTH_TIME: сколько секунд SmoothDamp тратит на догон цели.
    // 0.15f = плавно, без рывков. Меньше = быстрее но дёрганее.
    private const float SMOOTH_TIME = 0.15f;

    // Текущая скорость SmoothDamp (состояние между кадрами)
    private Vector3 smoothDampVelocity;

    // Логирование: раз в секунду выводим диагностику в консоль
    private float lastLogTime;
    private int framesSinceLog;
    private float maxDistSinceLog;
    private int teleportsSinceLog;
    private string lastMode = "";

    public void Update()
    {
        if (buffer.Count == 0)
        {
            return;
        }

        float currentTime = CurrentTime;

        // Удаляем протухшие снапшоты
        while (buffer.First != null && buffer.First.Value.IsExpired(currentTime))
        {
            buffer.RemoveFirst();
        }

        LinkedListNode<Snapshot> firstNode = buffer.First;
        if (firstNode == null)
            return;

        if (firstNode.Value.IsSnapshotNewer(currentTime))
            return;

        while (firstNode.Next != null && !firstNode.Next.Value.IsSnapshotNewer(currentTime))
        {
            firstNode = firstNode.Next;
            buffer.RemoveFirst();
        }

        LinkedListNode<Snapshot> nextNode = firstNode.Next;

        Vector3 targetPos;
        Quaternion targetRot;
        MovementData latestData;
        float debugT = -1f;

        if (nextNode == null)
        {
            lastMode = "SINGLE";
            latestData = firstNode.Value.Data;
            targetPos = latestData.Position.ToUnity();
            targetRot = latestData.Rotation.ToUnity();
        }
        else
        {
            lastMode = "LERP";
            MovementData prevData = firstNode.Value.Data;
            latestData = nextNode.Value.Data;

            float timeDiff = nextNode.Value.Time - firstNode.Value.Time;
            debugT = timeDiff > 0 ? (currentTime - firstNode.Value.Time) / timeDiff : 1f;
            debugT = Mathf.Clamp01(debugT);

            targetPos = Vector3.Lerp(prevData.Position.ToUnity(), latestData.Position.ToUnity(), debugT);
            targetRot = Quaternion.Lerp(prevData.Rotation.ToUnity(), latestData.Rotation.ToUnity(), debugT);
        }

        float dist = Vector3.Distance(transform.position, targetPos);
        maxDistSinceLog = Mathf.Max(maxDistSinceLog, dist);
        framesSinceLog++;

        if (dist > TELEPORT_THRESHOLD)
        {
            teleportsSinceLog++;
            transform.position = targetPos;
            transform.rotation = targetRot;
            smoothDampVelocity = Vector3.zero;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref smoothDampVelocity, SMOOTH_TIME, float.MaxValue, Time.unscaledDeltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.unscaledDeltaTime / SMOOTH_TIME);
        }

        // Лог раз в секунду только для транспорта (VehicleMovementReplicator)
        if (this is VehicleMovementReplicator && Time.unscaledTime - lastLogTime >= 1f)
        {
            lastLogTime = Time.unscaledTime;
            Log.Debug($"[MR] buf={buffer.Count} mode={lastMode} t={debugT:F2} dist={maxDistSinceLog:F2} teleports={teleportsSinceLog} frames={framesSinceLog}");
            maxDistSinceLog = 0;
            teleportsSinceLog = 0;
            framesSinceLog = 0;
        }

        ApplyNewMovementData(latestData);
    }

    public abstract void ApplyNewMovementData(MovementData newMovementData);

    public record struct Snapshot(MovementData Data, float Time)
    {
        public bool IsSnapshotNewer(float currentTime) => currentTime < Time;

        public bool IsExpired(float currentTime) => currentTime > Time + SNAPSHOT_EXPIRATION_TIME;
    }

    public static MovementReplicator AddReplicatorToObject(GameObject gameObject)
    {
        // IMPORTANT:
        // Network entity roots (NitroxEntity) are often NOT the same GameObject that holds
        // the game's control components (Vehicle/SubControl). Use child checks.
        if (gameObject.GetComponentInChildren<SeaMoth>(true))
        {
            return gameObject.AddComponent<SeamothMovementReplicator>();
        }
        if (gameObject.GetComponentInChildren<Exosuit>(true))
        {
            return gameObject.AddComponent<ExosuitMovementReplicator>();
        }
        // Cyclops: SubRoot is the entity root, SubControl is usually on a child object.
        if (gameObject.GetComponentInChildren<SubControl>(true) || (gameObject.TryGetComponent(out SubRoot sr) && !sr.isBase))
        {
            return gameObject.AddComponent<CyclopsMovementReplicator>();
        }
        return gameObject.AddComponent<MovementReplicator>();
    }
}
