using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace NerfedShields
{
    public class SubModule : MBSubModuleBase
    {
        private bool _settingsHooked;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
        }

        // MCM docs: Settings.Instance is only safe to touch from this point onward,
        // not from OnSubModuleLoad.
        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();

            if (_settingsHooked)
            {
                return;
            }

            try
            {
                var settings = NerfedShieldsSettings.Instance;
                if (settings != null)
                {
                    settings.PropertyChanged += OnSettingsPropertyChanged;
                    _settingsHooked = true;
                }
            }
            catch (Exception ex)
            {
                LogError("Failed to hook MCM settings.", ex);
            }
        }

        private void OnSettingsPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NerfedShieldsSettings.ShieldHpPercent) ||
                e.PropertyName == MCM.Abstractions.Base.BaseSettings.SaveTriggered)
            {
                ApplyCurrentSetting();
            }
        }

        // Fires once per game session (campaign start, custom battle start, etc.)
        // after all game objects (including ItemObjects) are loaded and registered.
        public override void OnGameInitializationFinished(Game game)
        {
            base.OnGameInitializationFinished(game);
            ShieldHpService.Initialize();
            ApplyCurrentSetting();
        }

        private void ApplyCurrentSetting()
        {
            int percent = NerfedShieldsSettings.Instance?.ShieldHpPercent ?? 100;

            try
            {
                ShieldHpService.ApplyMultiplier(percent);
            }
            catch (Exception ex)
            {
                LogError("Failed to apply shield HP multiplier.", ex);
            }
        }

        private static void LogError(string message, Exception ex)
        {
            InformationManager.DisplayMessage(new InformationMessage(
                "[Nerfed Shields] " + message + " " + ex.Message, Colors.Red));
        }
    }
}
