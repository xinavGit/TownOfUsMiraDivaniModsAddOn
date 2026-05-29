using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using DivaniMods.Roles.Crewmate.CrewmatePower;

namespace DivaniMods.Options;

public class ThiefOptions : AbstractOptionGroup<ThiefRole>
{
    public override string GroupName => "Thief";

    public ModdedNumberOption MaxStolenModifiers { get; } = new(
        "Max Stolen Modifiers", 2f, 1f, 5f, 1f, MiraNumberSuffixes.None);

    public ModdedNumberOption PickpocketCooldown { get; } = new(
        "Pickpocket Cooldown", 25f, 10f, 60f, 5f, MiraNumberSuffixes.Seconds);

    public ModdedNumberOption PickpocketDuration { get; } = new(
        "Pickpocket Duration", 3f, 1f, 10f, 0.5f, MiraNumberSuffixes.Seconds);

    public ModdedNumberOption PickpocketRange { get; } = new(
        "Pickpocket Range", 1f, 0.5f, 3f, 0.25f, MiraNumberSuffixes.Multiplier);

    [ModdedToggleOption("Stealing Lover Breaks Their Heart")]
    public bool StealingLoverHeartbreaksVictim { get; set; } = true;
}
