using System.Reflection;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class PilotingChair_OnSteeringStart_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PilotingChair t) => t.OnSteeringStart(default));

    public static void Postfix(PilotingChair __instance)
    {
        if (Player.main.currChair == __instance && __instance.subRoot)
        {
            Resolve<Vehicles>().BroadcastOnPilotModeChanged(__instance.subRoot.gameObject, true);

            // Если игрок появился уже в циклопе, OnHandClick может не вызываться => lock не запрашивается.
            // Без EXCLUSIVE lock'а управление/газ может "не работать" и движение будет слаться редко.
            if (__instance.subRoot.gameObject.TryGetIdOrWarn(out NitroxId id))
            {
                Resolve<SimulationOwnership>().RequestSimulationLock(id, SimulationLockType.EXCLUSIVE);
            }
        }
    }
}
