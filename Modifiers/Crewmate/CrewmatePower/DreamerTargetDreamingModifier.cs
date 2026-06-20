using MiraAPI.Modifiers;

namespace DivaniMods.Modifiers.Crewmate.CrewmatePower;

public sealed class DreamerTargetDreamingModifier(ushort originalRole, ushort dreamRole) : BaseModifier
{
    public override string ModifierName => "Dreaming"; // "information tag" carries info for revert, need to add patch for thief no steal
    public override bool HideOnUi => true;

    public ushort OriginalRole { get; set; } = originalRole;
    public ushort DreamRole { get; set; } = dreamRole;

    public override void OnDeath(DeathReason reason)
    {
        ModifierComponent?.RemoveModifier(this);
    }
}
