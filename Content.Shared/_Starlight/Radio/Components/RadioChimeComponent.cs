using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Radio.Components
{
    [RegisterComponent]
    public sealed partial class RadioChimeComponent : Component
    {
        [DataField("chimeSound")]
        public SoundSpecifier? ChimeSound { get; set; }
    }
}
