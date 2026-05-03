using FMOD.Studio;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours.Cyclops;
using Nitrox.Model.GameLogic.FMOD;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;
using System.Reflection;

namespace NitroxClient.MonoBehaviours.Vehicles;

public class CyclopsMovementReplicator : VehicleMovementReplicator
{
    protected static readonly int CYCLOPS_YAW = Animator.StringToHash("cyclops_yaw");
    protected static readonly int CYCLOPS_PITCH = Animator.StringToHash("cyclops_pitch");

    private SubControl subControl;
    private FMOD_CustomLoopingEmitter rpmSound;
    private float radiusRpmSound;
    private RemotePlayer? drivingPlayer;
    private bool throttleApplied;
    private float steeringWheelYaw;
    private Vector3 smoothVelocity = Vector3.zero;

    private CyclopsEngineChangeState cyclopsEngineChangeState;
    private CyclopsMotorMode cyclopsMotorMode;
    private static readonly FieldInfo cyclopsHeatField =
        typeof(CyclopsMotorMode).GetField("engineOverheatValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    private bool isLocalPlayerPiloting =>
        Player.main != null &&
        Player.main.isPiloting &&
        ((Player.main.GetCurrentSub() != null && (Player.main.GetCurrentSub().transform == transform || Player.main.GetCurrentSub().transform.IsChildOf(transform) || transform.IsChildOf(Player.main.GetCurrentSub().transform))) ||
         (Player.main.GetVehicle() != null && (Player.main.GetVehicle().transform == transform || Player.main.GetVehicle().transform.IsChildOf(transform) || transform.IsChildOf(Player.main.GetVehicle().transform))));

    public void Awake()
    {
        SubControl found = GetComponentInChildren<SubControl>(true);
        if (!found)
        {
            found = GetComponentInParent<SubControl>();
        }
        subControl = found;

        CyclopsEngineChangeState foundEngineState = GetComponentInChildren<CyclopsEngineChangeState>(true);
        if (!foundEngineState)
        {
            foundEngineState = GetComponentInParent<CyclopsEngineChangeState>();
        }
        cyclopsEngineChangeState = foundEngineState;

        CyclopsMotorMode foundMotorMode = GetComponentInChildren<CyclopsMotorMode>(true);
        if (!foundMotorMode)
        {
            foundMotorMode = GetComponentInParent<CyclopsMotorMode>();
        }
        cyclopsMotorMode = foundMotorMode;
        SetupSound();
    }

    public new void Update()
    {
        if (isLocalPlayerPiloting)
        {
            // Hard safety: if engine is off, never allow local thrust to persist.
            if (cyclopsEngineChangeState != null && cyclopsEngineChangeState.motorMode != null && !cyclopsEngineChangeState.motorMode.engineOn)
            {
                subControl.throttle = Vector3.zero;
                subControl.useThrottleIndex = 0;
                throttleApplied = false;
            }
            UpdateSound();
            smoothVelocity = Vector3.zero;
            return;
        }

        // Для удалённого Cyclops используем общий интерполятор.
        base.Update();

        if (subControl.canAccel && throttleApplied)
        {
            float topClamp = subControl.useThrottleIndex switch
            {
                1 => 0.66f,
                2 => 1f,
                _ => 0.33f,
            };
            subControl.engineRPMManager.AccelerateInput(topClamp);
            for (int i = 0; i < subControl.throttleHandlers.Length; i++)
            {
                subControl.throttleHandlers[i].OnSubAppliedThrottle();
            }
        }

        if (Mathf.Abs(steeringWheelYaw) > 0.1f)
        {
            subControl.mainAnimator.SetFloat(VIEW_YAW, subControl.steeringWheelYaw);
            subControl.mainAnimator.SetFloat(VIEW_PITCH, subControl.steeringWheelPitch);

            if (drivingPlayer != null && drivingPlayer.AnimationController != null)
            {
                Animator anim = drivingPlayer.AnimationController.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    anim.SetFloat(CYCLOPS_YAW, subControl.steeringWheelYaw);
                    anim.SetFloat(CYCLOPS_PITCH, subControl.steeringWheelPitch);
                }
            }
        }

        UpdateSound();
    }

    private void UpdateSound()
    {
        float distanceToPlayer = Vector3.Distance(Player.main.transform.position, transform.position);
        float volumeRpmSound = SoundHelper.CalculateVolume(distanceToPlayer, radiusRpmSound, 1f);
        rpmSound.GetEventInstance().setVolume(volumeRpmSound);
    }

    public override void ApplyNewMovementData(MovementData newMovementData)
    {
        if (newMovementData is not DrivenVehicleMovementData vehicleMovementData)
        {
            return;
        }

        steeringWheelYaw = vehicleMovementData.SteeringWheelYaw / 127f;
        subControl.steeringWheelYaw = steeringWheelYaw;
        subControl.steeringWheelPitch = vehicleMovementData.SteeringWheelPitch / 127f;
        throttleApplied = vehicleMovementData.ThrottleApplied && drivingPlayer != null;

        // Cyclops-specific state that affects noise/heat/voice lines.
        if (newMovementData is CyclopsMovementData cyclopsData)
        {
            // Если двигатель выключен, принудительно обнуляем тягу на принимающей стороне тоже.
            subControl.throttle = cyclopsData.EngineOn ? cyclopsData.Throttle.ToUnity() : Vector3.zero;
            subControl.useThrottleIndex = cyclopsData.EngineOn ? cyclopsData.UseThrottleIndex : 0;
            if (!cyclopsData.EngineOn)
            {
                throttleApplied = false;
            }

            if (cyclopsEngineChangeState != null && cyclopsEngineChangeState.motorMode != null)
            {
                cyclopsEngineChangeState.motorMode.engineOn = cyclopsData.EngineOn;
                if (!cyclopsData.EngineOn)
                {
                    cyclopsEngineChangeState.startEngine = false;
                }
                // Keep mode consistent with actual throttle index that drives movement.
                int modeIndex = Mathf.Clamp(cyclopsData.UseThrottleIndex, 0, 2);
                cyclopsEngineChangeState.motorMode.cyclopsMotorMode = (CyclopsMotorMode.CyclopsMotorModes)modeIndex;
            }

            if (cyclopsMotorMode != null && cyclopsHeatField != null)
            {
                cyclopsHeatField.SetValue(cyclopsMotorMode, cyclopsData.EngineHeat);
            }

            NitroxCyclops nitroxCyclops = GetComponentInChildren<NitroxCyclops>(true);
            if (!nitroxCyclops)
            {
                nitroxCyclops = GetComponentInParent<NitroxCyclops>();
            }
            if (nitroxCyclops != null)
            {
                nitroxCyclops.SetRemoteNoisePercent(cyclopsData.NoisePercent);
            }
        }
    }

    public override void Enter(RemotePlayer remotePlayer)
    {
        if (isLocalPlayerPiloting)
        {
            return;
        }
        drivingPlayer = remotePlayer;
        if (drivingPlayer.AnimationController != null)
        {
            Animator anim = drivingPlayer.AnimationController.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.SetBool("piloting", true);
            }
        }
    }

    public override void Exit()
    {
        if (drivingPlayer != null && drivingPlayer.AnimationController != null)
        {
            Animator anim = drivingPlayer.AnimationController.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.SetBool("piloting", false);
                anim.SetFloat(CYCLOPS_YAW, 0f);
                anim.SetFloat(CYCLOPS_PITCH, 0f);
            }
        }
        drivingPlayer = null;
        throttleApplied = false;
        steeringWheelYaw = 0f;
    }

    private void SetupSound()
    {
        rpmSound = subControl.engineRPMManager.engineRpmSFX;
        rpmSound.followParent = true;
        this.Resolve<FMODWhitelist>().IsWhitelisted(rpmSound.asset.path, out radiusRpmSound);
    }
}
