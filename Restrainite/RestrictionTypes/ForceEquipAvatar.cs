using Restrainite.RestrictionTypes.Base;

namespace Restrainite.RestrictionTypes;

internal sealed class ForceEquipAvatar : BaseRestriction
{
    public override string Name => "Force Equip Avatar";
    public override string Description => "Should others be able to force you into a specific avatar?";

    public StringSetParameter AvatarUrl { get; } = new();

    protected override IRestrictionParameter[] InitRestrictionParameters()
    {
        return [AvatarUrl];
    }
}
