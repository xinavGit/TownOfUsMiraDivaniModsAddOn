using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using DivaniMods.Roles.Crewmate.CrewmatePower;

namespace DivaniMods.Options;

// The alignments the Dreamer is NOT allowed to reimagine a crewmate into.
public enum DreamerAssignRestriction
{
    CrewmateKilling,
    CrewmatePower,
    Nothing,
}

public class DreamerOptions : AbstractOptionGroup<DreamerRole>
{
    public override string GroupName => "Dreamer";

    // Enum option: the label array lines up 1:1 with the enum order above.
    public ModdedEnumOption CannotReimagineInto { get; } = new(
        "Dreamer Cannot Reimagine Into", (int)DreamerAssignRestriction.Nothing,
        typeof(DreamerAssignRestriction),
        ["Crewmate Killing", "Crewmate Power", "Nothing"]);

    // If the dream targets a non-crewmate, THEY get warned someone tried.
    public ModdedToggleOption NotifyNonCrewOnAttempt { get; } =
        new("Non-Crew Are Notified On Attempt", false);

    // If the dream fails (non-crew target), the DREAMER learns it wasn't a crewmate.
    public ModdedToggleOption NotifyDreamerOnFail { get; } =
        new("Dreamer Notified On Failed Dream", true);

    // Number: default 1, min 1, max 3, step 1, no suffix.
    public ModdedNumberOption InsomniaRounds { get; } = new(
        "Insomnia Lasts For Rounds", 1f, 1f, 3f, 1f, MiraNumberSuffixes.None);
}
