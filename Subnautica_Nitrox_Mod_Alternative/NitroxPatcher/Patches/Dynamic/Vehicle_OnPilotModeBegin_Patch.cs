using System.Reflection;
using HarmonyLib;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class Vehicle_OnPilotModeBegin_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Vehicle t) => t.OnPilotModeBegin());

    public static void Prefix(Vehicle __instance)
    {
        Resolve<Vehicles>().BroadcastOnPilotModeChanged(__instance.gameObject, true);

        // Критично: если за руль сел клиент, ему нужен EXCLUSIVE lock на транспорт,
        // иначе владельцем симуляции остаётся хост и позиция будет рассылаться редко (1 раз / 2 сек)
        // + появятся телепорты/рывки на другой стороне.
        if (__instance.TryGetIdOrWarn(out NitroxId id))
        {
            Resolve<SimulationOwnership>().RequestSimulationLock(id, SimulationLockType.EXCLUSIVE);
        }
    }

    public override void Patch(Harmony harmony)
    {
        PatchPrefix(harmony, TARGET_METHOD, Reflect.Method(() => Prefix(default)));
    }
}