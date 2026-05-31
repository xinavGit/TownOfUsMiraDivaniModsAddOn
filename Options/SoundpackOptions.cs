using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;

namespace DivaniMods.Options;

public sealed class SoundpackOptions : AbstractOptionGroup
{
    public override string GroupName => "Soundpack";

    [ModdedToggleOption("Use Dutch Meme Soundpack")]
    public bool UseDutchMemeSoundpack { get; set; } = false;
}
