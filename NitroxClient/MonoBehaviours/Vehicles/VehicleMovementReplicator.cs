using NitroxClient.GameLogic;
using NitroxClient.GameLogic.Simulation;
using Nitrox.Model.DataStructures;
using UnityEngine;

namespace NitroxClient.MonoBehaviours.Vehicles;

public abstract class VehicleMovementReplicator : MovementReplicator
{
    protected static readonly int VIEW_YAW = Animator.StringToHash("view_yaw");
    protected static readonly int VIEW_PITCH = Animator.StringToHash("view_pitch");

    public new void Update()
    {
        // 1. Стандартная интерполяция
        base.Update();

        // 2. Логика физики (isKinematic) остается здесь, так как она нужна локально
        bool isWeDrivingThis = false;
        if (Player.main != null)
        {
            var currentVehicle = Player.main.GetVehicle();
            var currentSub = Player.main.GetCurrentSub();
            isWeDrivingThis = (currentVehicle != null && currentVehicle.gameObject == gameObject) ||
                              (currentSub != null && currentSub.gameObject == gameObject);
        }

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            bool shouldBeKinematic = !isWeDrivingThis;
            if (rb.isKinematic != shouldBeKinematic)
            {
                rb.isKinematic = shouldBeKinematic;
            }
        }

        // ВНИМАНИЕ: Мы удалили BroadcastOnPilotModeChanged отсюда. 
        // Теперь это делают только патчи при реальном нажатии кнопок.
    }

    public abstract void Enter(RemotePlayer remotePlayer);
    public abstract void Exit();
}
