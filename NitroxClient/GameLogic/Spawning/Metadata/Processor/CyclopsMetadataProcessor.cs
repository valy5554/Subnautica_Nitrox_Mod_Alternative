using System.Reflection;
using NitroxClient.Communication;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor
{
    public class CyclopsMetadataProcessor : EntityMetadataProcessor<CyclopsMetadata>
    {
        private readonly IPacketSender packetSender;
        private readonly LiveMixinManager liveMixinManager;

        private static readonly FieldInfo heatField = typeof(CyclopsMotorMode).GetField("engineOverheatValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        public CyclopsMetadataProcessor(IPacketSender packetSender, LiveMixinManager liveMixinManager)
        {
            this.packetSender = packetSender;
            this.liveMixinManager = liveMixinManager;
        }

        public override void ProcessMetadata(GameObject cyclops, CyclopsMetadata metadata)
        {
            using (PacketSuppressor<EntityMetadataUpdate>.Suppress())
            {
                SetEngineMode(cyclops, (CyclopsMotorMode.CyclopsMotorModes)metadata.EngineMode);
                ChangeSilentRunning(cyclops, metadata.SilentRunningOn);
                ChangeShieldMode(cyclops, metadata.ShieldOn);
                ChangeSonarMode(cyclops, metadata.SonarOn);
                SetEngineState(cyclops, metadata.EngineOn);
                SetHealth(cyclops, metadata.Health);
                SetDestroyed(cyclops, metadata.IsDestroyed);
                SetEngineHeat(cyclops, metadata.EngineHeat);
            }
        }

        private void SetEngineState(GameObject cyclops, bool isOn)
        {
            CyclopsEngineChangeState engineState = cyclops.GetComponentInChildren<CyclopsEngineChangeState>(true);
            if (engineState != null && engineState.motorMode != null)
            {
                if (engineState.motorMode.engineOn == isOn)
                {
                    // Keep this flag consistent too; otherwise extractor can still treat engine as on.
                    if (!isOn)
                    {
                        engineState.startEngine = false;
                    }
                    return;
                }
                engineState.motorMode.engineOn = isOn;
                if (!isOn)
                {
                    engineState.startEngine = false;
                }
            }
        }

        private void SetEngineHeat(GameObject cyclops, float heat)
        {
            CyclopsMotorMode motorMode = cyclops.GetComponentInChildren<CyclopsMotorMode>(true);
            if (motorMode != null && heatField != null)
            {
                heatField.SetValue(motorMode, heat);
            }
        }

        private void SetEngineMode(GameObject cyclops, CyclopsMotorMode.CyclopsMotorModes mode)
        {
            CyclopsMotorMode motorMode = cyclops.GetComponentInChildren<CyclopsMotorMode>(true);
            if (motorMode)
            {
                motorMode.cyclopsMotorMode = mode;
            }

            // Keep SubControl throttle index aligned with engine mode for remote clients.
            SubControl subControl = cyclops.GetComponentInChildren<SubControl>(true);
            if (subControl)
            {
                subControl.useThrottleIndex = Mathf.Clamp((int)mode, 0, 2);
            }
        }

        private void ChangeSilentRunning(GameObject cyclops, bool isOn)
        {
            CyclopsSilentRunningAbilityButton button = cyclops.GetComponentInChildren<CyclopsSilentRunningAbilityButton>(true);
            if (button && button.active != isOn)
            {
                button.OnClick();
            }
        }

        private void ChangeShieldMode(GameObject cyclops, bool isOn)
        {
            CyclopsShieldButton shieldButton = cyclops.GetComponentInChildren<CyclopsShieldButton>(true);
            if (shieldButton && shieldButton.active != isOn)
            {
                shieldButton.OnClick();
            }
        }

        private void ChangeSonarMode(GameObject cyclops, bool isOn)
        {
            CyclopsSonarButton sonarButton = cyclops.GetComponentInChildren<CyclopsSonarButton>(true);
            if (sonarButton)
            {
                var activeField = typeof(CyclopsSonarButton).GetField("sonarActive", BindingFlags.NonPublic | BindingFlags.Instance)
                               ?? typeof(CyclopsSonarButton).GetField("_sonarActive", BindingFlags.NonPublic | BindingFlags.Instance);

                if (activeField != null)
                {
                    bool currentActive = (bool)activeField.GetValue(sonarButton);
                    if (currentActive != isOn)
                    {
                        sonarButton.OnClick();
                    }
                }
            }
        }

        private void SetHealth(GameObject gameObject, float health)
        {
            LiveMixin liveMixin = gameObject.GetComponentInChildren<LiveMixin>(true);
            if (liveMixin)
            {
                liveMixinManager.SyncRemoteHealth(liveMixin, health);
            }
        }

        private void SetDestroyed(GameObject gameObject, bool isDestroyed)
        {
            CyclopsDestructionEvent destructionEvent = gameObject.GetComponentInChildren<CyclopsDestructionEvent>(true);
            if (destructionEvent == null || destructionEvent.subRoot.subDestroyed == isDestroyed)
                return;

            if (isDestroyed)
            {
                destructionEvent.DestroyCyclops();
            }
            else
            {
                destructionEvent.RestoreCyclops();
            }
        }
    }
}
