using Il2CppInterop.Runtime.Attributes;
using Il2CppSystem.Text;
using Reactor.Utilities.Attributes;
using DivaniMods.Roles.Neutral.NeutralEvil;
using DivaniMods.Utilities;
using UnityEngine;

namespace DivaniMods.Patches;

/// <summary>
/// Vanilla <see cref="SabotageTask"/> on each client while Terrorist sabotage is
/// active — implements <see cref="IHudOverrideTask"/> (blocks meetings). Emergency
/// table UX is handled in <see cref="TerroristPatches"/> like vanilla sabos.
/// </summary>
[RegisterInIl2Cpp]
public sealed class TerroristSabotageTask(nint cppPtr) : SabotageTask(cppPtr)
{
    public override int TaskStep => _isComplete ? 1 : 0;
    public override bool IsComplete => _isComplete;
    private bool _isComplete;

    public override bool ValidConsole(Console console) => false;

    public override void Initialize()
    {
    }

    public override void AppendTaskText(Il2CppSystem.Text.StringBuilder sb)
    {
        if (!TerroristSabotageState.IsActive)
        {
            return;
        }

        var color = (TerroristSabotageState.FlashPulseIndex & 1) == 0
            ? TerroristRole.TerroristColor
            : TerroristSabotageState.SecondaryColor;
        var location = TerroristSabotageState.PlantedLocationName;
        var seconds = Mathf.CeilToInt(TerroristSabotageState.TimeRemaining);
        var hex = ColorUtility.ToHtmlStringRGB(color);

        sb.AppendLine(
            $"<color=#{hex}>Terrorist Sabotage active\nLocation: {location}\n{seconds}s</color>");
    }

    public override void Complete()
    {
        if (_isComplete)
        {
            return;
        }

        _isComplete = true;

        if (Owner != null)
        {
            Owner.RemoveTask(this);
        }

        if (gameObject != null)
        {
            UnityEngine.Object.Destroy(gameObject);
        }
    }
}
