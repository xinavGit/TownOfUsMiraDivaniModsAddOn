using MiraAPI.Modifiers;

namespace DivaniMods.Modifiers.Crewmate.CrewmatePower;

// Silent cooldown tag: a recently-reimagined player can't be dreamed again
// until RoundsLeft ticks down to 0.
public sealed class DreamerInsomniaModifier(int rounds) : BaseModifier
{
    public override string ModifierName => "Insomnia";
    public override bool HideOnUi => true;

    public int RoundsLeft { get; set; } = rounds;

    public override void OnDeath(DeathReason reason)
    {
        ModifierComponent?.RemoveModifier(this);
    }
}
