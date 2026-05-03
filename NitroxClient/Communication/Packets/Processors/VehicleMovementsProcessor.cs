using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class VehicleMovementsProcessor : IClientPacketProcessor<VehicleMovements>
{
    public Task Process(ClientProcessorContext context, VehicleMovements packet)
    {
        if (!MovementBroadcaster.Instance)
        {
            return Task.CompletedTask;
        }

        // Используем локальное время получения пакета (Time.unscaledTime) вместо
        // времени отправителя (packet.RealTime). Это устраняет рассинхрон часов
        // между хостом и клиентом, который вызывал телепортации.
        float receiveTime = Time.unscaledTime;
        foreach (MovementData movementData in packet.Data)
        {
            if (MovementBroadcaster.Instance.Replicators.TryGetValue(movementData.Id, out MovementReplicator movementReplicator))
            {
                movementReplicator.AddSnapshot(movementData, receiveTime);
            }
        }
        return Task.CompletedTask;
    }
}
