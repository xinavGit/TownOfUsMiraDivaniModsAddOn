using System.Collections;
using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Utilities.Assets;
using Reactor.Utilities;
using DivaniMods.Assets;
using DivaniMods.Options;
using DivaniMods.Patches;
using DivaniMods.Roles.Neutral.NeutralEvil;
using TownOfUs.Buttons;
using UnityEngine;

namespace DivaniMods.Buttons.Neutral.NeutralEvil;

/// <summary>
/// Defuse at the planted utility while Terrorist sabotage is active (terrorist or crew).
/// </summary>
public class TerroristDefuseButton : CustomActionButton
{
    public override string Name => "Defuse";
    public override float Cooldown => 1f;
    public override float EffectDuration => OptionGroupSingleton<TerroristOptions>.Instance.IsTimedSabotageStyle
        ? OptionGroupSingleton<TerroristOptions>.Instance.DefuseTime.Value
        : 0f;
    public override int MaxUses => 0;
    public override LoadableAsset<Sprite> Sprite => DivaniAssets.TerroristSabotageButton;
    public override ButtonLocation Location { get; set; } = ButtonLocation.BottomLeft;
    public override Color TextOutlineColor => TerroristRole.TerroristColor;
    public override BaseKeybind Keybind => Keybinds.TertiaryAction;

    public static TerroristDefuseButton? Instance { get; set; }

    private bool _isDefusing;

    public static bool IsLocalDefusing =>
        (Instance != null && Instance._isDefusing)
        || TerroristNumpad.Controller.DefuseInProgress;

    public override bool Enabled(RoleBehaviour? role)
    {
        Instance = this;
        return role != null;
    }

    public override bool CanUse()
    {
        var player = PlayerControl.LocalPlayer;
        if (player == null || player.Data == null || player.Data.IsDead) return false;
        if (MeetingHud.Instance || ExileController.Instance) return false;
        if (!TerroristSabotageState.IsActive) return false;
        if (TerroristNumpad.Controller.InProgress) return false;
        if (_isDefusing) return false;
        if (!TerroristSabotageState.IsLocalPlayerAtPlantedConsole()) return false;

        return base.CanUse();
    }

    public override void ClickHandler()
    {
        if (!CanClick())
        {
            return;
        }

        OnClick();
        Button?.SetDisabled();
    }

    protected override void OnClick()
    {
        var player = PlayerControl.LocalPlayer;
        if (player == null) return;
        if (!TerroristSabotageState.IsActive) return;
        if (!TerroristSabotageState.IsLocalPlayerAtPlantedConsole()) return;
        if (_isDefusing) return;

        if (!OptionGroupSingleton<TerroristOptions>.Instance.IsTimedSabotageStyle)
        {
            Coroutines.Start(DefuseNumpadCoroutine(player));
            return;
        }

        Coroutines.Start(DefuseTimedCoroutine(player));
    }

    private IEnumerator DefuseTimedCoroutine(PlayerControl player)
    {
        _isDefusing = true;
        var defuseTime = EffectDuration;
        var colorHex = ColorUtility.ToHtmlStringRGB(TerroristRole.TerroristColor);

        EffectActive = true;
        Timer = defuseTime;

        MiraAPI.Utilities.Helpers.CreateAndShowNotification(
            $"<b><color=#{colorHex}>Defusing...</color></b>",
            Color.white,
            new Vector3(0f, 1f, -20f),
            spr: DivaniAssets.TerroristSabotageButton.LoadAsset());

        var elapsed = 0f;
        while (elapsed < defuseTime)
        {
            if (player == null || player.Data == null || player.Data.IsDead)
            {
                AbortDefuse();
                yield break;
            }

            if (!TerroristSabotageState.IsActive)
            {
                AbortDefuse();
                yield break;
            }

            if (!TerroristSabotageState.IsLocalPlayerAtPlantedConsole())
            {
                MiraAPI.Utilities.Helpers.CreateAndShowNotification(
                    $"<b><color=#{colorHex}>Defuse aborted — too far from sabotage!</color></b>",
                    Color.white,
                    new Vector3(0f, 1f, -20f),
                    spr: DivaniAssets.TerroristSabotageButton.LoadAsset());
                AbortDefuse();
                yield break;
            }

            elapsed += Time.deltaTime;
            Timer = defuseTime - elapsed;
            if (Button != null)
            {
                Button.SetFillUp(Timer, defuseTime);
            }

            yield return null;
        }

        if (player == null || player.Data == null || player.Data.IsDead)
        {
            AbortDefuse();
            yield break;
        }

        if (!TerroristSabotageState.IsActive)
        {
            AbortDefuse();
            yield break;
        }

        if (!TerroristSabotageState.IsLocalPlayerAtPlantedConsole())
        {
            MiraAPI.Utilities.Helpers.CreateAndShowNotification(
                $"<b><color=#{colorHex}>Defuse aborted — too far from sabotage!</color></b>",
                Color.white,
                new Vector3(0f, 1f, -20f),
                spr: DivaniAssets.TerroristSabotageButton.LoadAsset());
            AbortDefuse();
            yield break;
        }

        TerroristSabotageState.RpcDefuseSabotage(player, player.PlayerId);
        EffectActive = false;
        Timer = Cooldown;
        _isDefusing = false;
    }

    private IEnumerator DefuseNumpadCoroutine(PlayerControl player)
    {
        _isDefusing = true;
        EffectActive = true;

        if (!TerroristNumpad.Controller.OpenDefuse(player))
        {
            AbortDefuse();
            yield break;
        }

        // Same as plant: IsLocalPlayerAtPlantedConsole uses CouldUse paths that fail while KeypadGame is open.
        while (TerroristNumpad.Controller.InProgress)
        {
            if (player == null || player.Data == null || player.Data.IsDead
                || !TerroristSabotageState.IsActive)
            {
                AbortDefuse();
                yield break;
            }

            yield return null;
        }

        EffectActive = false;
        Timer = Cooldown;
        _isDefusing = false;
    }

    private void AbortDefuse()
    {
        if (TerroristNumpad.Controller.InProgress)
        {
            TerroristNumpad.Controller.CancelActive();
        }

        EffectActive = false;
        Timer = Cooldown;
        _isDefusing = false;
    }
}
