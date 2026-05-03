using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class CyclopsRuntimeStateProcessor : IAuthPacketProcessor<CyclopsRuntimeState>
{
    public async Task Process(AuthProcessorContext context, CyclopsRuntimeState packet)
    {
        await context.SendToOthersAsync(packet);
    }
}

