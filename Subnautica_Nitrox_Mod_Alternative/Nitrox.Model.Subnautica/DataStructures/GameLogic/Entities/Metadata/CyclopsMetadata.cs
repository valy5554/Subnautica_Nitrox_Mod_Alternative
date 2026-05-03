using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

[Serializable]
[DataContract]
public class CyclopsMetadata : EntityMetadata
{
    [DataMember(Order = 1)] public bool SilentRunningOn { get; set; }
    [DataMember(Order = 2)] public bool ShieldOn { get; set; }
    [DataMember(Order = 3)] public bool SonarOn { get; set; }
    [DataMember(Order = 4)] public bool EngineOn { get; set; }
    [DataMember(Order = 5)] public int EngineMode { get; set; }
    [DataMember(Order = 6)] public float Health { get; set; }
    [DataMember(Order = 7)] public bool IsDestroyed { get; set; }
    [DataMember(Order = 8)] public float EngineHeat { get; set; } // <--- Добавлено

    [IgnoreConstructor]
    protected CyclopsMetadata() { }

    public CyclopsMetadata(bool silentRunningOn, bool shieldOn, bool sonarOn, bool engineOn, int engineMode, float health, bool isDestroyed, float engineHeat)
    {
        SilentRunningOn = silentRunningOn;
        ShieldOn = shieldOn;
        SonarOn = sonarOn;
        EngineOn = engineOn;
        EngineMode = engineMode;
        Health = health;
        IsDestroyed = isDestroyed;
        EngineHeat = engineHeat;
    }
}
