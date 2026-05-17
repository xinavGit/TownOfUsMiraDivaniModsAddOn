using AmongUs.GameOptions;
using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using DivaniMods.Roles.Neutral.NeutralEvil;

namespace DivaniMods.Options;

public class TerroristOptions : AbstractOptionGroup<TerroristRole>
{
    public override string GroupName => "Terrorist";

    /// <summary>Sabotages the Terrorist must successfully detonate to win alone.</summary>
    [ModdedNumberOption("Successful Sabotages To Win", 1f, 4f, 1f)]
    public float SabotagesToWin { get; set; } = 2f;

    /// <summary>Cooldown between Plant attempts. Mirrors the impostor sabotage cooldown so plant/sabo
    /// pace at the same rate.</summary>
    [ModdedNumberOption("Plant Cooldown", 10f, 60f, 5f, MiraNumberSuffixes.Seconds)]
    public float PlantCooldown { get; set; } = 30f;

    /// <summary>Sabotage duration on The Skeld / Dleks.</summary>
    [ModdedNumberOption("Sabotage Duration (Skeld)", 10f, 120f, 5f, MiraNumberSuffixes.Seconds)]
    public float SabotageDurationSkeld { get; set; } = 30f;

    /// <summary>Sabotage duration on MIRA HQ.</summary>
    [ModdedNumberOption("Sabotage Duration (MIRA HQ)", 10f, 120f, 5f, MiraNumberSuffixes.Seconds)]
    public float SabotageDurationMiraHQ { get; set; } = 45f;

    /// <summary>Sabotage duration on Polus.</summary>
    [ModdedNumberOption("Sabotage Duration (Polus)", 10f, 120f, 5f, MiraNumberSuffixes.Seconds)]
    public float SabotageDurationPolus { get; set; } = 60f;

    /// <summary>Sabotage duration on The Fungle.</summary>
    [ModdedNumberOption("Sabotage Duration (Fungle)", 10f, 120f, 5f, MiraNumberSuffixes.Seconds)]
    public float SabotageDurationFungle { get; set; } = 60f;

    /// <summary>Sabotage duration on The Airship.</summary>
    [ModdedNumberOption("Sabotage Duration (Airship)", 10f, 180f, 5f, MiraNumberSuffixes.Seconds)]
    public float SabotageDurationAirship { get; set; } = 90f;

    /// <summary>Picks the duration for the current map. Dleks shares Skeld value.</summary>
    public float SabotageDuration
    {
        get
        {
            var mapId = TutorialManager.InstanceExists
                ? (MapNames)AmongUsClient.Instance.TutorialMapId
                : (MapNames)GameOptionsManager.Instance.currentNormalGameOptions.MapId;

            return mapId switch
            {
                MapNames.MiraHQ => SabotageDurationMiraHQ,
                MapNames.Polus => SabotageDurationPolus,
                MapNames.Fungle => SabotageDurationFungle,
                MapNames.Airship => SabotageDurationAirship,
                _ => SabotageDurationSkeld,
            };
        }
    }

    public ModdedEnumOption SabotageStyle { get; } = new(
        "Sabotage Style",
        (int)TerroristSabotageStyle.Timed,
        typeof(TerroristSabotageStyle));

    /// <summary>Time the Terrorist must hold to finish the Plant action.</summary>
    public ModdedNumberOption PlantTime { get; } = new(
        "Plant Time", 5f, 2f, 10f, 1f, MiraNumberSuffixes.Seconds)
    {
        Visible = () => OptionGroupSingleton<TerroristOptions>.Instance.IsTimedSabotageStyle,
    };

    /// <summary>Time required to defuse a planted sabotage.</summary>
    public ModdedNumberOption DefuseTime { get; } = new(
        "Defuse Time", 5f, 2f, 10f, 1f, MiraNumberSuffixes.Seconds)
    {
        Visible = () => OptionGroupSingleton<TerroristOptions>.Instance.IsTimedSabotageStyle,
    };

    [ModdedToggleOption("Terrorist Can Vent")]
    public bool CanVent { get; set; } = false;

    /// <summary>After a sabotage explodes, that utility cannot be used by anyone for the rest of the game.</summary>
    [ModdedToggleOption("Disable Exploded Utility For Game")]
    public bool DisableExplodedConsoles { get; set; } = true;

    /// <summary>If a player is mid-defuse (inside the defuse keypad) when the sabotage explodes,
    /// kill them (recorded as suicide). Off by default.</summary>
    [ModdedToggleOption("Explosion Kills Active Defusers")]
    public bool ExplosionKillsDefusers { get; set; } = false;

    public bool IsTimedSabotageStyle => (TerroristSabotageStyle)SabotageStyle.Value is TerroristSabotageStyle.Timed;
}

public enum TerroristSabotageStyle
{
    Timed,
    Numpad,
}
