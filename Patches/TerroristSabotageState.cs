using System.Collections;
using System.Collections.Generic;

using BepInEx.Unity.IL2CPP.Utils.Collections;

using MiraAPI.GameOptions;
using MiraAPI.Networking;

using Reactor.Networking.Attributes;

using Reactor.Utilities;

using Reactor.Utilities.Extensions;

using DivaniMods.Assets;
using DivaniMods.Buttons.Neutral.NeutralEvil;
using DivaniMods.Utilities;

using DivaniMods.Options;

using DivaniMods.Roles.Neutral.NeutralEvil;

using TownOfUs.Utilities;

using UnityEngine;



namespace DivaniMods.Patches;



/// <summary>

/// Shared state + RPC entry points for the Terrorist sabotage.

/// </summary>

public static class TerroristSabotageState

{

    private const byte NoPlayer = byte.MaxValue;



    public static bool IsActive { get; private set; }

    public static byte TerroristId { get; private set; } = NoPlayer;

    public static Vector2 PlantedPosition { get; private set; }

    public static int PlantedConsoleKey { get; private set; }

    public static TerroristUtilityKind PlantedUtilityKind { get; private set; }

    public static string PlantedLocationName { get; private set; } = string.Empty;

    public static float TimeRemaining { get; private set; }

    public static int SuccessfulSabotages { get; private set; }

    public static int FlashPulseIndex { get; private set; }



    public static bool LocalPlantInProgress { get; set; }

    public static bool LocalDefuseInProgress { get; set; }



    public static readonly Color SecondaryColor = new Color32(0xF9, 0xA1, 0x23, 255);

    private static readonly Color DefuseFlashColor = Palette.AcceptedGreen;

    private static readonly Color ExplosionFlashColor = Palette.ImpostorRed;

    private static readonly List<(int ConsoleKey, TerroristUtilityKind Kind)> DisabledUtilities = new();

    private static TerroristSabotageTask? _localTask;

    private static ArrowBehaviour? _arrow;

    private static Coroutine? _flashRoutine;

    private static AudioSource? _sabotageSoundSource;

    private static bool _tickRunning;



    public static void RegisterTerrorist(PlayerControl terrorist)

    {

        TerroristId = terrorist.PlayerId;

    }



    public static void ResetAll()

    {

        ClearActiveSabotage();

        SuccessfulSabotages = 0;

        TerroristId = NoPlayer;

        LocalPlantInProgress = false;

        LocalDefuseInProgress = false;

        FlashPulseIndex = 0;

        DisabledUtilities.Clear();

        TerroristUtilityConsoles.InvalidateCache();
        TerroristNumpad.Controller.ResetAll();

    }

    public static bool IsUtilityDisabled(int consoleKey, TerroristUtilityKind kind)
    {
        foreach (var entry in DisabledUtilities)
        {
            if (entry.ConsoleKey == consoleKey && entry.Kind == kind)
            {
                return true;
            }
        }

        return false;
    }

    private static void RegisterDisabledUtility(int consoleKey, TerroristUtilityKind kind)
    {
        if (IsUtilityDisabled(consoleKey, kind))
        {
            return;
        }

        DisabledUtilities.Add((consoleKey, kind));
    }



    /// <summary>
    /// Vanilla critical sabotage is live. Matches Grenadier flash (non–SabotageFlashing): <c>system is {{ AnyActive: true }}</c>.
    /// </summary>
    public static bool IsCriticalVanillaSabotageActive()
    {
        var system = ShipStatus.Instance.Systems[SystemTypes.Sabotage].Cast<SabotageSystemType>();
        return system is { AnyActive: true };
    }

    public static bool TryGetPlantedWorldPosition(out Vector3 worldPos)
    {
        return TerroristUtilityConsoles.TryGetWorldPosition(PlantedUtilityKind, PlantedPosition, out worldPos);
    }



    [MethodRpc((uint)DivaniRpcCalls.TerroristPlantSabotage)]
    public static void RpcPlantSabotage(
        PlayerControl sender, byte terroristId, float x, float y, float duration, int consoleKey, byte utilityKind)
    {
        var resolvedTerroristId = sender != null ? sender.PlayerId : terroristId;
        PlantSabotage(resolvedTerroristId, new Vector2(x, y), duration, consoleKey, (TerroristUtilityKind)utilityKind);
    }

    [MethodRpc((uint)DivaniRpcCalls.TerroristDefuseSabotage)]
    public static void RpcDefuseSabotage(PlayerControl sender, byte defuserId)
    {
        var resolvedDefuserId = sender != null ? sender.PlayerId : defuserId;
        DefuseSabotage(resolvedDefuserId);
    }



    [MethodRpc((uint)DivaniRpcCalls.TerroristSabotageExpired)]

    public static void RpcSabotageExpired(PlayerControl sender)

    {

        ExpireSabotage();

    }



    private static void PlantSabotage(
        byte terroristId, Vector2 position, float duration, int consoleKey, TerroristUtilityKind utilityKind)

    {

        if (IsActive) ClearActiveSabotage();

        TerroristUtilityConsoles.InvalidateCache();

        IsActive = true;

        TerroristId = terroristId;

        PlantedPosition = position;

        PlantedConsoleKey = consoleKey;

        PlantedUtilityKind = utilityKind;

        PlantedLocationName = TerroristUtilityConsoles.GetDisplayName(utilityKind);

        TimeRemaining = duration;

        FlashPulseIndex = 0;

        TerroristUtilityConsoles.InvalidateCache();

        CreateArrowToTarget();

        StartSabotageAlarm();

        StartFlash();

        EnsureLocalSabotageTask();



        var colorHex = ColorUtility.ToHtmlStringRGB(TerroristRole.TerroristColor);

        MiraAPI.Utilities.Helpers.CreateAndShowNotification(

            $"<b><color=#{colorHex}>Terrorist Sabotage active\nLocation: {PlantedLocationName}</color></b>",

            Color.white,

            new Vector3(0f, 1f, -20f),

            spr: DivaniAssets.TerroristSabotageButton.LoadAsset());

        EnsureTickRunning();

    }

    private static void DefuseSabotage(byte defuserId)

    {

        if (!IsActive) return;

        var location = PlantedLocationName;

        StopFlash();

        PlayResultFlash(DefuseFlashColor);

        var colorHex = ColorUtility.ToHtmlStringRGB(TerroristRole.TerroristColor);

        MiraAPI.Utilities.Helpers.CreateAndShowNotification(

            $"<b><color=#{colorHex}>Terrorist Sabotage Defused\nLocation: {location}</color></b>",

            Color.white,

            new Vector3(0f, 1f, -20f),

            spr: DivaniAssets.TerroristSabotageButton.LoadAsset());

        ClearActiveSabotage();
        TerroristPlantButton.SyncAfterSabotageEnded(startCooldown: true);
    }

    private static void ExpireSabotage()

    {

        if (!IsActive) return;

        var location = PlantedLocationName;

        var consoleKey = PlantedConsoleKey;

        var kind = PlantedUtilityKind;

        SuccessfulSabotages++;

        var needed = (int)OptionGroupSingleton<TerroristOptions>.Instance.SabotagesToWin;

        var remaining = Mathf.Max(0, needed - SuccessfulSabotages);

        StopFlash();

        PlayResultFlash(ExplosionFlashColor);

        PlayExplosionSound();

        var colorHex = ColorUtility.ToHtmlStringRGB(TerroristRole.TerroristColor);

        var progress = remaining > 0

            ? $" ({SuccessfulSabotages}/{needed})"

            : " — Terrorist goal reached!";

        MiraAPI.Utilities.Helpers.CreateAndShowNotification(

            $"<b><color=#{colorHex}>Terrorist Exploded {location}{progress}</color></b>",

            Color.white,

            new Vector3(0f, 1f, -20f),

            spr: DivaniAssets.TerroristSabotageButton.LoadAsset());

        if (OptionGroupSingleton<TerroristOptions>.Instance.DisableExplodedConsoles)

        {

            RegisterDisabledUtility(consoleKey, kind);

            EjectFromExplodedUtility(kind);

        }

        KillLocalIfDefusing();

        ClearActiveSabotage();
        TerroristPlantButton.SyncAfterSabotageEnded(startCooldown: true);
    }

    /// <summary>
    /// If the local player is mid-defuse when the sabotage expires and the option is on,
    /// kill them as a suicide. Each client checks locally so only the actual defuser dies.
    /// </summary>
    private static void KillLocalIfDefusing()
    {
        if (!OptionGroupSingleton<TerroristOptions>.Instance.ExplosionKillsDefusers)
        {
            return;
        }

        if (!TerroristDefuseButton.IsLocalDefusing)
        {
            return;
        }

        var local = PlayerControl.LocalPlayer;
        if (local == null || local.Data == null || local.Data.IsDead)
        {
            return;
        }

        if (TerroristNumpad.Controller.InProgress)
        {
            TerroristNumpad.Controller.CancelActive();
        }

        local.RpcCustomMurder(local, MeetingCheck.OutsideMeeting);
    }

    /// <summary>
    /// Force-close any open minigame matching the exploded utility kind. Runs on every client
    /// (each closes their own open minigame), so anyone inside the console is ejected.
    /// </summary>
    private static void EjectFromExplodedUtility(TerroristUtilityKind kind)
    {
        switch (kind)
        {
            case TerroristUtilityKind.Admin:
                var map = MapBehaviour.Instance;
                if (map != null && map.IsOpen)
                {
                    map.Close();
                }
                break;
            case TerroristUtilityKind.Cameras:
            case TerroristUtilityKind.Vitals:
            case TerroristUtilityKind.DoorLog:
                var mg = Minigame.Instance;
                if (mg == null)
                {
                    return;
                }

                var matches = kind switch
                {
                    TerroristUtilityKind.Cameras => mg.TryCast<SurveillanceMinigame>() != null
                        || mg.TryCast<PlanetSurveillanceMinigame>() != null,
                    TerroristUtilityKind.Vitals => mg.TryCast<VitalsMinigame>() != null,
                    TerroristUtilityKind.DoorLog => mg.TryCast<SecurityLogGame>() != null,
                    _ => false,
                };

                if (matches)
                {
                    mg.Close();
                }
                break;
        }
    }

    private static void ClearActiveSabotage()

    {
        IsActive = false;

        TimeRemaining = 0f;

        PlantedLocationName = string.Empty;

        PlantedConsoleKey = 0;

        PlantedUtilityKind = TerroristUtilityKind.None;



        RemoveLocalSabotageTask();

        StopSabotageAlarm();

        StopFlash();



        if (_arrow != null && !_arrow.IsDestroyedOrNull())

        {

            _arrow.gameObject.Destroy();

            _arrow = null;

        }

    }



    public static void Tick()

    {

        if (!IsActive)

        {

            return;

        }



        TimeRemaining -= Time.deltaTime;



        if (TimeRemaining <= 0f)

        {

            TimeRemaining = 0f;

            var local = PlayerControl.LocalPlayer;

            if (local != null && local.PlayerId == TerroristId)

            {

                RpcSabotageExpired(local);

            }

            return;

        }



        UpdateArrow();

    }



    /// <summary>

    /// Same proximity rules as Plant: must be at the planted utility console.

    /// </summary>

    public static bool IsLocalPlayerAtPlantedConsole()

    {

        if (!IsActive) return false;



        var local = PlayerControl.LocalPlayer;

        if (local == null) return false;



        return TerroristUtilityConsoles.IsAtPlantedUtility(
            local, PlantedUtilityKind, PlantedPosition, PlantedConsoleKey);

    }



    private static void EnsureTickRunning()

    {

        if (_tickRunning) return;

        _tickRunning = true;

        Coroutines.Start(CoTick());

    }



    private static IEnumerator CoTick()

    {

        while (IsActive)

        {

            Tick();

            yield return null;

        }

        _tickRunning = false;

    }



    private static void EnsureLocalSabotageTask()

    {

        var local = PlayerControl.LocalPlayer;

        if (local == null) return;



        RemoveLocalSabotageTask();



        var go = new GameObject("TerroristSabotageTask");

        go.transform.SetParent(local.transform);

        _localTask = go.AddComponent<TerroristSabotageTask>();

        _localTask.Owner = local;

        _localTask.Initialize();

        local.myTasks.Add(_localTask);

    }



    private static void RemoveLocalSabotageTask()

    {

        if (_localTask == null) return;

        _localTask.Complete();

        _localTask = null;

    }



    private static void CreateArrowToTarget()

    {

        var local = PlayerControl.LocalPlayer;

        if (local == null) return;



        if (_arrow != null && !_arrow.IsDestroyedOrNull())

        {

            _arrow.gameObject.Destroy();

            _arrow = null;

        }



        _arrow = MiscUtils.CreateArrow(local.transform, GetCurrentPulseColor());

        _arrow.target = GetArrowTarget();

        _arrow.Update();

        ApplyArrowColor();

    }



    private static void UpdateArrow()

    {

        if (_arrow == null || _arrow.IsDestroyedOrNull() || !IsActive) return;



        _arrow.target = GetArrowTarget();

        _arrow.Update();

        ApplyArrowColor();

    }



    /// <summary>Alternates with screen flash / task list (primary blue, secondary gold).</summary>
    private static Color GetCurrentPulseColor() =>
        (FlashPulseIndex & 1) == 0 ? TerroristRole.TerroristColor : SecondaryColor;

    private static void ApplyArrowColor()

    {

        if (_arrow == null || _arrow.IsDestroyedOrNull() || _arrow.image == null)

        {

            return;

        }

        _arrow.image.color = GetCurrentPulseColor();

    }



    private static Vector3 GetArrowTarget()

    {

        if (TryGetPlantedWorldPosition(out var worldPos))

        {

            return worldPos;

        }



        var local = PlayerControl.LocalPlayer;

        var z = local != null ? local.transform.position.z : 0f;

        return new Vector3(PlantedPosition.x, PlantedPosition.y, z);

    }



    private static void StartSabotageAlarm()

    {

        StopSabotageAlarm();

        if (!SoundManager.Instance || !ShipStatus.Instance) return;



        try

        {

            var clip = ShipStatus.Instance.SabotageSound;

            if (clip == null) return;



            _sabotageSoundSource = SoundManager.Instance.PlaySound(clip, false, 1f);

            if (_sabotageSoundSource != null)

            {

                _sabotageSoundSource.loop = true;

                if (!_sabotageSoundSource.isPlaying)

                {

                    _sabotageSoundSource.Play();

                }

            }

        }

        catch

        {

            // SabotageSound field name can differ between AU versions.

        }

    }



    private static void StopSabotageAlarm()

    {

        if (_sabotageSoundSource != null)

        {

            _sabotageSoundSource.Stop();

            _sabotageSoundSource = null;

        }

    }



    private static void StartFlash()

    {

        StopFlash();

        if (!HudManager.Instance) return;

        FlashPulseIndex = 0;

        _flashRoutine = HudManager.Instance.StartCoroutine(CoAlternateFlash().WrapToIl2Cpp());

    }



    private static void StopFlash()

    {

        if (_flashRoutine != null && HudManager.Instance)

        {

            HudManager.Instance.StopCoroutine(_flashRoutine);

            _flashRoutine = null;

        }

    }



    private static IEnumerator CoAlternateFlash()

    {

        while (IsActive)

        {

            var color = GetCurrentPulseColor();

            FlashPulseIndex++;

            yield return MiscUtils.CoFlash(color, waitfor: 0.5f, alpha: 0.35f);

        }

    }



    private static string? GetPlayerName(byte playerId)

    {

        foreach (var pc in PlayerControl.AllPlayerControls)

        {

            if (pc != null && pc.PlayerId == playerId && pc.Data != null)

            {

                return pc.Data.PlayerName;

            }

        }

        return null;

    }



    private static void PlayResultFlash(Color color)

    {

        if (!HudManager.Instance)

        {

            return;

        }

        Coroutines.Start(CoResultFlash(color));

    }

    private static IEnumerator CoResultFlash(Color color)

    {

        yield return MiscUtils.CoFlash(color, waitfor: 0.75f, alpha: 0.4f);

    }

    private static void PlayExplosionSound()

    {

        if (!SoundManager.Instance)

        {

            return;

        }

        try

        {

            var clip = DivaniAssets.TerroristExplosionSound.LoadAsset();

            if (clip != null)

            {

                SoundManager.Instance.PlaySound(clip, false, 1f);

            }

        }

        catch

        {

            // Optional SFX — ignore load failures.

        }

    }

}


