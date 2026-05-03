using FMOD.Studio;
using Nitrox.Model.GameLogic.FMOD;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxClient.MonoBehaviours.Vehicles;

public class SeamothMovementReplicator : VehicleMovementReplicator
{
    private SeaMoth seaMoth;
    private FMOD_CustomLoopingEmitter rpmSound;
    private FMOD_CustomEmitter revSound;
    private FMOD_CustomEmitter enterSeamoth;

    private float radiusRpmSound;
    private float radiusRevSound;
    private float radiusEnterSound;

    private RemotePlayer? drivingPlayer;
    private bool throttleApplied;
    private Vector3 smoothVelocity = Vector3.zero;

    public void Awake()
    {
        seaMoth = GetComponent<SeaMoth>();
        SetupSound();
    }

    public new void Update()
    {
        if (Player.main != null && Player.main.GetVehicle() == seaMoth)
        {
            smoothVelocity = Vector3.zero;
            return;
        }

        // Для удалённой техники используем общий путь MovementReplicator:
        // он интерполирует между снапшотами и использует единый источник времени.
        base.Update();

        if (seaMoth.transform.position.y < 0f)
        {
            seaMoth.bubbles.transform.rotation = Quaternion.LookRotation(Vector3.up);
            if (!seaMoth.bubbles.isPlaying)
                seaMoth.bubbles.Play();
        }
        else
        {
            seaMoth.bubbles.Stop();
        }

        if (throttleApplied && seaMoth.IsPowered())
        {
            seaMoth.engineSound.AccelerateInput(1);
        }
    }

    public override void ApplyNewMovementData(MovementData newMovementData)
    {
        if (newMovementData is not DrivenVehicleMovementData vehicleMovementData)
            return;

        throttleApplied = vehicleMovementData.ThrottleApplied;
    }

    private void SetupSound()
    {
        rpmSound = seaMoth.engineSound.engineRpmSFX;
        revSound = seaMoth.engineSound.engineRevUp;
        enterSeamoth = seaMoth.enterSeamoth;

        this.Resolve<FMODWhitelist>().IsWhitelisted(rpmSound.asset.path, out radiusRpmSound);
        this.Resolve<FMODWhitelist>().IsWhitelisted(revSound.asset.path, out radiusRevSound);
        this.Resolve<FMODWhitelist>().IsWhitelisted(enterSeamoth.asset.path, out radiusEnterSound);
    }

    public override void Enter(RemotePlayer remotePlayer)
    {
        drivingPlayer = remotePlayer;
        seaMoth.bubbles.Play();
        if (enterSeamoth)
        {
            enterSeamoth.Stop();
            enterSeamoth.ReleaseEvent();
            enterSeamoth.CacheEventInstance();
            enterSeamoth.Play();
        }
    }

    public override void Exit()
    {
        drivingPlayer = null;
        throttleApplied = false;
        seaMoth.bubbles.Stop();
    }
}
