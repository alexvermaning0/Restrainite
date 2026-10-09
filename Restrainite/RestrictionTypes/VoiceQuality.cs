using Restrainite.RestrictionTypes.Base;

namespace Restrainite.RestrictionTypes;

internal sealed class VoiceQuality : BaseRestriction
{
    public override string Name => "Voice Quality";

    public override string Description =>
        "Should others be able to lower your voice quality, by reducing the encoding bitrate for everyone else?";

    // 1.0 = full quality (unchanged bitrate), 0.0 = worst quality (minimum bitrate).
    public LowestFloatParameter LowestFloat { get; } = new(0.0f, 1.0f);

    protected override IRestrictionParameter[] InitRestrictionParameters()
    {
        return [LowestFloat];
    }
}
