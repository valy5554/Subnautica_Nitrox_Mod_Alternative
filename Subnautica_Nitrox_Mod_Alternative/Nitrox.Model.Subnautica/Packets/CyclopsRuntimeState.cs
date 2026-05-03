using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

[Serializable]
public class CyclopsRuntimeState : Packet
{
    public NitroxId CyclopsId { get; }
    public bool EngineOn { get; }
    public byte EngineModeIndex { get; }
    public float EngineHeat { get; }
    public bool IsOverheating { get; }
    public bool IsCriticalTemperature { get; }
    public bool HasActiveFire { get; }

    public CyclopsRuntimeState(
        NitroxId cyclopsId,
        bool engineOn,
        byte engineModeIndex,
        float engineHeat,
        bool isOverheating,
        bool isCriticalTemperature,
        bool hasActiveFire)
    {
        CyclopsId = cyclopsId;
        EngineOn = engineOn;
        EngineModeIndex = engineModeIndex;
        EngineHeat = engineHeat;
        IsOverheating = isOverheating;
        IsCriticalTemperature = isCriticalTemperature;
        HasActiveFire = hasActiveFire;
    }
}

