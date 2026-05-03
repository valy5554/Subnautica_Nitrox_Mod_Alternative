using System.Reflection;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;
using UnityEngine;
using Nitrox.Model.DataStructures;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

public class CyclopsMetadataExtractor : EntityMetadataExtractor<CyclopsMetadataExtractor.CyclopsGameObject, CyclopsMetadata>
{
    private static readonly FieldInfo heatField = typeof(CyclopsMotorMode).GetField("engineOverheatValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    public override CyclopsMetadata Extract(CyclopsGameObject cyclops)
    {
        GameObject gameObject = cyclops.GameObject;
        CyclopsSilentRunningAbilityButton silentRunning = gameObject.RequireComponentInChildren<CyclopsSilentRunningAbilityButton>(true);
        CyclopsEngineChangeState engineState = gameObject.RequireComponentInChildren<CyclopsEngineChangeState>(true);

        bool engineShuttingDown = (engineState.motorMode.engineOn && engineState.invalidButton);
        bool engineOn = (engineState.startEngine || engineState.motorMode.engineOn) && !engineShuttingDown;

        CyclopsShieldButton shield = gameObject.GetComponentInChildren<CyclopsShieldButton>(true);
        bool shieldOn = (shield) ? shield.active : false;

        CyclopsSonarButton sonarButton = gameObject.GetComponentInChildren<CyclopsSonarButton>(true);
        bool sonarOn = (sonarButton) ? sonarButton._sonarActive : false;

        CyclopsMotorMode.CyclopsMotorModes motorMode = engineState.motorMode.cyclopsMotorMode;

        LiveMixin liveMixin = gameObject.RequireComponentInChildren<LiveMixin>();
        float health = liveMixin.health;

        SubRoot subRoot = gameObject.RequireComponentInChildren<SubRoot>();
        bool isDestroyed = subRoot.subDestroyed || health <= 0f;

        // Достаем температуру
        float heat = 0f;
        if (heatField != null)
            heat = (float)heatField.GetValue(engineState.motorMode);

        return new CyclopsMetadata(silentRunning.active, shieldOn, sonarOn, engineOn, (int)motorMode, health, isDestroyed, heat);
    }

    public struct CyclopsGameObject { public GameObject GameObject { get; set; } }
}
