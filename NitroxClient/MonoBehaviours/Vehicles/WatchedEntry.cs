using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;
using System.Reflection;

namespace NitroxClient.MonoBehaviours.Vehicles;

public class WatchedEntry
{
    private const float MAX_TIME_WITHOUT_BROADCAST = 2f;
    private const float SEND_INTERVAL = 0.066f; // ~15 пакетов в сек. Для 30 ставь 0.033f

    private readonly NitroxId Id;
    private readonly Transform transform;
    private readonly Vehicle vehicle;
    private readonly SubControl subControl;
    private readonly CyclopsEngineChangeState cyclopsEngineChangeState;
    private readonly CyclopsMotorMode cyclopsMotorMode;
    private readonly CyclopsNoiseManager cyclopsNoiseManager;

    private static readonly FieldInfo cyclopsHeatField =
        typeof(CyclopsMotorMode).GetField("engineOverheatValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    private float latestBroadcastTime;

    public WatchedEntry(NitroxId Id, Transform transform)
    {
        this.Id = Id;
        this.transform = transform;
        // Важно: entity root для транспорта часто НЕ совпадает с объектом, где висит Vehicle/SubControl.
        // Если искать компоненты только на transform, то IsDriven* всегда false и мы будем слать 1 пакет / 2 секунды.
        Vehicle foundVehicle = transform.GetComponentInChildren<Vehicle>(true);
        if (!foundVehicle)
        {
            foundVehicle = transform.GetComponentInParent<Vehicle>();
        }
        vehicle = foundVehicle;

        SubControl foundSubControl = transform.GetComponentInChildren<SubControl>(true);
        if (!foundSubControl)
        {
            foundSubControl = transform.GetComponentInParent<SubControl>();
        }
        subControl = foundSubControl;

        // Cyclops специфичные компоненты (если это он)
        CyclopsEngineChangeState foundEngineState = transform.GetComponentInChildren<CyclopsEngineChangeState>(true);
        if (!foundEngineState)
        {
            foundEngineState = transform.GetComponentInParent<CyclopsEngineChangeState>();
        }
        cyclopsEngineChangeState = foundEngineState;

        CyclopsMotorMode foundMotorMode = transform.GetComponentInChildren<CyclopsMotorMode>(true);
        if (!foundMotorMode)
        {
            foundMotorMode = transform.GetComponentInParent<CyclopsMotorMode>();
        }
        cyclopsMotorMode = foundMotorMode;

        CyclopsNoiseManager foundNoise = transform.GetComponentInChildren<CyclopsNoiseManager>(true);
        if (!foundNoise)
        {
            foundNoise = transform.GetComponentInParent<CyclopsNoiseManager>();
        }
        cyclopsNoiseManager = foundNoise;
        latestBroadcastTime = Time.unscaledTime;
    }

    private bool IsDrivenVehicle()
    {
        if (vehicle == null || Player.main == null)
            return false;

        Vehicle current = Player.main.GetVehicle();
        if (current == null)
            return false;

        // Сравниваем по иерархии, т.к. network-entity может быть родителем/дочерним объектом реального Vehicle.
        Transform a = transform;
        Transform b = current.transform;
        return a == b || a.IsChildOf(b) || b.IsChildOf(a);
    }

    private bool IsDrivenCyclops()
    {
        if (subControl == null || Player.main == null)
            return false;

        SubRoot currentSub = Player.main.GetCurrentSub();
        if (currentSub == null || currentSub.isBase)
            return false;

        Transform a = transform;
        Transform b = currentSub.transform;
        return a == b || a.IsChildOf(b) || b.IsChildOf(a);
    }

    public bool ShouldBroadcastMovement(bool forceHighRate = false)
    {
        if (!transform)
        {
            MovementBroadcaster.UnregisterWatched(Id);
            return false;
        }

        float currentTime = Time.unscaledTime;
        float deltaTime = currentTime - latestBroadcastTime;

        // Если кто-то за рулем — шлем пакеты постоянно по таймеру
        if (forceHighRate || IsDrivenVehicle() || IsDrivenCyclops())
        {
            if (deltaTime >= SEND_INTERVAL)
            {
                latestBroadcastTime = currentTime;
                return true;
            }
            return false;
        }

        // Если стоит — шлем раз в 2 секунды
        if (deltaTime >= MAX_TIME_WITHOUT_BROADCAST)
        {
            latestBroadcastTime = currentTime;
            return true;
        }

        return false;
    }

    public MovementData GetMovementData(NitroxId id)
    {
        if (IsDrivenVehicle())
        {
            sbyte steeringWheelYaw = (sbyte)(Mathf.Clamp(vehicle.steeringWheelYaw, -1, 1) * 70f);
            sbyte steeringWheelPitch = (sbyte)(Mathf.Clamp(vehicle.steeringWheelPitch, -1, 1) * 45f);
            bool throttleApplied = false;

            if (AvatarInputHandler.main != null && AvatarInputHandler.main.IsEnabled())
            {
                Vector3 input = GameInput.GetMoveDirection();
                if (vehicle is SeaMoth)
                    throttleApplied = input.magnitude > 0f;
                else if (vehicle is Exosuit)
                    throttleApplied = input.y > 0f;
            }

            if (vehicle is Exosuit exosuit)
            {
                return new ExosuitMovementData(id, transform.position.ToDto(), transform.rotation.ToDto(),
                    exosuit.aimTargetLeft.transform.localPosition.ToDto(),
                    exosuit.aimTargetRight.transform.localPosition.ToDto(),
                    steeringWheelYaw, steeringWheelPitch, throttleApplied, exosuit.IKenabled);
            }

            return new DrivenVehicleMovementData(id, transform.position.ToDto(), transform.rotation.ToDto(), steeringWheelYaw, steeringWheelPitch, throttleApplied);
        }

        if (IsDrivenCyclops())
        {
            sbyte steeringWheelYaw = (sbyte)Mathf.Clamp(subControl.steeringWheelYaw, -90, 90);
            sbyte steeringWheelPitch = (sbyte)Mathf.Clamp(subControl.steeringWheelPitch, -90, 90);
            bool engineOn = cyclopsEngineChangeState != null && cyclopsEngineChangeState.motorMode != null && cyclopsEngineChangeState.motorMode.engineOn;
            float engineHeat = 0f;
            if (cyclopsMotorMode != null && cyclopsHeatField != null)
            {
                engineHeat = (float)cyclopsHeatField.GetValue(cyclopsMotorMode);
            }

            float noisePercent = 0f;
            if (cyclopsNoiseManager != null && engineOn)
            {
                noisePercent = cyclopsNoiseManager.GetNoisePercent();
            }

            // Если двигатель выключен (например, хост выключил удалённо), запрещаем посылать тягу.
            // Иначе водитель будет продолжать слать movement и Cyclops "едет на выключенном двигателе".
            Vector3 throttle = engineOn ? subControl.throttle : Vector3.zero;
            bool throttleApplied = engineOn && throttle.magnitude > 0.0001f;
            int useThrottleIndex = engineOn ? subControl.useThrottleIndex : 0;
            // Source of truth for speed mode is SubControl.useThrottleIndex (0..2).
            int engineMode = useThrottleIndex;

            // Важно: subControl.throttle и useThrottleIndex реально двигают механику/шум/возможный перегрев.
            return new CyclopsMovementData(
                id,
                transform.position.ToDto(),
                transform.rotation.ToDto(),
                steeringWheelYaw,
                steeringWheelPitch,
                throttleApplied,
                throttle.ToDto(),
                (byte)Mathf.Clamp(useThrottleIndex, 0, 255),
                engineOn,
                engineMode,
                engineHeat,
                noisePercent);
        }

        return new SimpleMovementData(id, transform.position.ToDto(), transform.rotation.ToDto());
    }

    public void OnBroadcastPosition() { }
}
