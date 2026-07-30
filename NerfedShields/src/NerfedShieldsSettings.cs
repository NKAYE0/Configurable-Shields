using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace NerfedShields
{
    // Global = shared across every save/campaign, which matches "applies to all
    // shields everywhere" rather than being tied to one save file.
    internal sealed class NerfedShieldsSettings : AttributeGlobalSettings<NerfedShieldsSettings>
    {
        private int _shieldHpPercent = 100;

        public override string Id => "NerfedShields_v1";
        public override string DisplayName => "Nerfed Shields";
        public override string FolderName => "NerfedShields";
        public override string FormatType => "json";

        [SettingPropertyInteger("Shield HP %", 1, 100, Order = 0, RequireRestart = false,
            HintText = "Scales the hit points of every shield in the game (player, companions, troops, and AI lords) to this percentage of its original value.")]
        [SettingPropertyGroup("Nerfed Shields")]
        public int ShieldHpPercent
        {
            get => _shieldHpPercent;
            set
            {
                if (_shieldHpPercent != value)
                {
                    _shieldHpPercent = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
