using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

// БЫЛО: internal sealed class CyclopsFireCreatedProcessor...
// СТАЛО:
public sealed class CyclopsFireCreatedProcessor(Fires fires) : IClientPacketProcessor<CyclopsFireCreated>
{
    public static bool IsSpawningFromNetwork = false;
    private readonly Fires fires = fires;

    public Task Process(ClientProcessorContext context, CyclopsFireCreated packet)
    {
        IsSpawningFromNetwork = true;
        fires.Create(packet.FireCreatedData);
        IsSpawningFromNetwork = false;
        return Task.CompletedTask;
    }
}
