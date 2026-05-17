using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using Il2CppSystem.Text;
using MiraAPI.GameOptions;
using Reactor.Utilities.Attributes;
using Reactor.Utilities.Extensions;
using DivaniMods.Options;
using DivaniMods.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DivaniMods.Patches;

internal enum TerroristNumpadAction
{
    None,
    Plant,
    Defuse,
}

/// <summary>
/// Numpad plant/defuse flow: session controller, keypad Harmony patches, fake O₂ task.
/// </summary>
internal static class TerroristNumpad
{
    public static void Register(Harmony harmony, ManualLogSource log) => Keypad.Register(harmony, log);

    #region Session controller

    internal static class Controller
    {
        private static TerroristKeypadNoOxyTask? _task;
        private static Minigame? _minigame;
        private static TerroristNumpadAction _action;
        private static Vector2 _plantPosition;
        private static int _plantConsoleKey;
        private static TerroristUtilityKind _plantKind;
        private static bool _numpadSessionCancelled;
        private static int _openNumpadSessionId;
        public static bool InProgress => _task != null;
        public static bool DefuseInProgress => _task != null && _action == TerroristNumpadAction.Defuse;

        internal static int ActiveOpenNumpadSessionId => _openNumpadSessionId;

        public static void ResetAll()
        {
            CleanupTaskAndMinigame();
            _numpadSessionCancelled = false;
            _action = TerroristNumpadAction.None;
            _plantPosition = Vector2.zero;
            _plantConsoleKey = 0;
            _plantKind = TerroristUtilityKind.None;
        }

        internal static void ResetKeypadUiState(KeypadGame game)
        {
            var tr = Traverse.Create(game);
            tr.Field<bool>("animating").Value = false;
            tr.Field<bool>("done").Value = false;
            game.numString = string.Empty;
            game.number = 0;
            if (game.NumberText != null)
            {
                game.NumberText.text = string.Empty;
            }
        }

        public static bool TryGetTerroristNumpadSession(KeypadGame? keypad, out TerroristKeypadNoOxyTask? terroristNumpadSabotage)
        {
            terroristNumpadSabotage = null;
            if (keypad == null || _task == null)
            {
                return false;
            }

            if (_minigame != null && SameMinigameInstance(keypad, _minigame))
            {
                terroristNumpadSabotage = _task;
                return true;
            }

            if (Minigame.Instance != null
                && SameMinigameInstance(keypad, Minigame.Instance)
                && keypad.TryCast<KeypadGame>() != null)
            {
                terroristNumpadSabotage = _task;
                return true;
            }

            return false;
        }

        public static bool OpenPlant(
            PlayerControl player,
            Vector2 position,
            int consoleKey,
            TerroristUtilityKind kind)
        {
            if (player == null || kind == TerroristUtilityKind.None)
            {
                return false;
            }

            CleanupTaskAndMinigame();
            _plantPosition = position;
            _plantConsoleKey = consoleKey;
            _plantKind = kind;
            return Open(player, TerroristNumpadAction.Plant);
        }

        public static bool OpenDefuse(PlayerControl player)
        {
            CleanupTaskAndMinigame();
            return player != null && Open(player, TerroristNumpadAction.Defuse);
        }

        public static bool TryFinalizeSuccessfulKeypad(int numpadSessionId)
        {
            if (_numpadSessionCancelled
                || _action == TerroristNumpadAction.None
                || numpadSessionId != _openNumpadSessionId)
            {
                return false;
            }

            var local = PlayerControl.LocalPlayer;
            if (local == null)
            {
                CleanupTaskAndMinigame();
                return false;
            }

            var action = _action;
            var plantPos = _plantPosition;
            var plantKey = _plantConsoleKey;
            var plantKind = _plantKind;

            if (action == TerroristNumpadAction.Plant)
            {
                var duration = OptionGroupSingleton<TerroristOptions>.Instance.SabotageDuration;
                TerroristSabotageState.RpcPlantSabotage(
                    local,
                    local.PlayerId,
                    plantPos.x,
                    plantPos.y,
                    duration,
                    plantKey,
                    (byte)plantKind);
            }
            else if (action == TerroristNumpadAction.Defuse)
            {
                TerroristSabotageState.RpcDefuseSabotage(local, local.PlayerId);
            }
            else
            {
                return false;
            }

            _minigame = null;
            CleanupTaskOnly();
            _numpadSessionCancelled = false;
            return true;
        }

        public static void Cancel(Minigame minigame)
        {
            if (_minigame == null || minigame == null || !SameMinigameInstance(_minigame, minigame))
            {
                return;
            }

            _numpadSessionCancelled = true;
            _minigame = null;
            CleanupTaskOnly();
        }

        public static void CancelActive()
        {
            var minigame = _minigame;
            if (minigame != null)
            {
                minigame.Close();
                return;
            }

            _numpadSessionCancelled = true;
            CleanupTaskAndMinigame();
        }

        private static bool SameMinigameInstance(Minigame? a, Minigame? b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            if (a.gameObject == b.gameObject)
            {
                return true;
            }

            return a.Pointer == b.Pointer;
        }

        private static bool Open(PlayerControl player, TerroristNumpadAction action)
        {
            _numpadSessionCancelled = false;

            TerroristUtilityConsoles.InvalidateKeypadPrefabCache();
            if (!TerroristUtilityConsoles.TryGetO2KeypadPrefab(out var prefab) || !prefab)
            {
                return false;
            }

            var parent = Camera.main
                ? Camera.main.transform
                : HudManager.Instance
                    ? HudManager.Instance.transform
                    : null;
            if (!parent)
            {
                return false;
            }

            var taskObject = new GameObject("TerroristKeypadTask");
            taskObject.transform.SetParent(player.transform);
            _task = taskObject.AddComponent<TerroristKeypadNoOxyTask>();
            _task.Owner = player;
            _task.targetNumber = UnityEngine.Random.Range(0, 100000);
            _openNumpadSessionId++;
            player.myTasks.Add(_task);

            _action = action;
            _minigame = Object.Instantiate(prefab, parent, false);
            _minigame.transform.SetParent(parent, false);
            _minigame.transform.localPosition = new Vector3(0f, 0f, -50f);
            _minigame.Begin(_task);
            if (_minigame.TryCast<KeypadGame>() is KeypadGame keypad)
            {
                ResetKeypadUiState(keypad);
            }

            return true;
        }

        private static void CleanupTaskOnly()
        {
            _action = TerroristNumpadAction.None;
            _plantPosition = Vector2.zero;
            _plantConsoleKey = 0;
            _plantKind = TerroristUtilityKind.None;

            var task = _task;
            _task = null;

            if (task == null)
            {
                return;
            }

            if (task.Owner != null)
            {
                task.Owner.RemoveTask(task);
            }

            if (task.gameObject != null)
            {
                Object.Destroy(task.gameObject);
            }
        }

        private static void CleanupTaskAndMinigame()
        {
            var minigame = _minigame;
            _minigame = null;
            CleanupTaskOnly();
            if (minigame != null)
            {
                minigame.Close();
            }
        }
    }

    [HarmonyPatch(typeof(Minigame), nameof(Minigame.Close), new Type[] { })]
    internal static class MinigameClose
    {
        private static void Postfix(Minigame __instance) => Controller.Cancel(__instance);
    }

    #endregion

    #region Keypad Harmony + fake O₂ task

    internal static class Keypad
    {
        public static void Register(Harmony harmony, ManualLogSource log)
        {
            var enterPrefix = AccessTools.Method(typeof(Keypad), nameof(KeypadGameEnterPrefix));
            var enter = AccessTools.Method(typeof(KeypadGame), nameof(KeypadGame.Enter));
            if (enterPrefix != null && enter != null)
            {
                harmony.Patch(enter, prefix: new HarmonyMethod(enterPrefix));
            }
            else
            {
                log.LogError("Terrorist keypad: failed to patch KeypadGame.Enter.");
            }

            var beginPostfix = AccessTools.Method(typeof(Keypad), nameof(KeypadGameBeginPostfix));
            var begin = AccessTools.Method(typeof(KeypadGame), nameof(KeypadGame.Begin));
            if (beginPostfix != null && begin != null)
            {
                harmony.Patch(begin, postfix: new HarmonyMethod(beginPostfix));
            }

            var clickNumberPrefix = AccessTools.Method(typeof(Keypad), nameof(KeypadClickNumberPrefix));
            var clickNumber = AccessTools.Method(typeof(KeypadGame), nameof(KeypadGame.ClickNumber), new[] { typeof(int) });
            if (clickNumberPrefix != null && clickNumber != null)
            {
                harmony.Patch(clickNumber, prefix: new HarmonyMethod(clickNumberPrefix));
            }
            else
            {
                log.LogError("Terrorist keypad: failed to patch KeypadGame.ClickNumber.");
            }

            var clearEntryPrefix = AccessTools.Method(typeof(Keypad), nameof(KeypadClearEntryPrefix));
            var clearEntry = AccessTools.Method(typeof(KeypadGame), nameof(KeypadGame.ClearEntry));
            if (clearEntryPrefix != null && clearEntry != null)
            {
                harmony.Patch(clearEntry, prefix: new HarmonyMethod(clearEntryPrefix));
            }
        }

        [HarmonyPatch(typeof(NoOxyTask), nameof(NoOxyTask.Initialize))]
        [HarmonyPrefix]
        private static bool NoOxyTaskInitializePrefix(NoOxyTask __instance)
        {
            if (__instance is not TerroristKeypadNoOxyTask tk)
            {
                return true;
            }

            tk.targetNumber = UnityEngine.Random.Range(0, 100000);

            if (ShipStatus.Instance != null
                && ShipStatus.Instance.Systems != null
                && ShipStatus.Instance.Systems.TryGetValue(SystemTypes.LifeSupp, out var sys))
            {
                var lifeSupp = sys?.TryCast<LifeSuppSystemType>();
                if (lifeSupp != null)
                {
                    Traverse.Create(tk).Field("reactor").SetValue(lifeSupp);
                }
            }

            return false;
        }

        [HarmonyPatch(typeof(NoOxyTask), "FixedUpdate")]
        [HarmonyPrefix]
        private static bool NoOxyTaskFixedUpdatePrefix(NoOxyTask __instance) =>
            __instance is not TerroristKeypadNoOxyTask;

        private static void KeypadGameBeginPostfix(KeypadGame __instance)
        {
            if (!Controller.TryGetTerroristNumpadSession(__instance, out _))
            {
                return;
            }

            Controller.ResetKeypadUiState(__instance);
        }

        private static bool KeypadClickNumberPrefix(KeypadGame __instance, int i)
        {
            if (!Controller.TryGetTerroristNumpadSession(__instance, out _))
            {
                return true;
            }

            var tr = Traverse.Create(__instance);
            tr.Field<bool>("animating").Value = false;
            tr.Field<bool>("done").Value = false;

            var current = __instance.numString?.ToString() ?? string.Empty;
            if (current.Length >= 5)
            {
                return false;
            }

            var next = current + i.ToString(CultureInfo.InvariantCulture);
            __instance.numString = next;
            if (int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                __instance.number = parsed;
            }

            if (__instance.NumberText != null)
            {
                __instance.NumberText.text = next;
            }

            return false;
        }

        private static bool KeypadClearEntryPrefix(KeypadGame __instance)
        {
            if (!Controller.TryGetTerroristNumpadSession(__instance, out _))
            {
                return true;
            }

            var tr = Traverse.Create(__instance);
            tr.Field<bool>("animating").Value = false;
            tr.Field<bool>("done").Value = false;
            __instance.numString = string.Empty;
            __instance.number = 0;
            if (__instance.NumberText != null)
            {
                __instance.NumberText.text = string.Empty;
            }

            return false;
        }

        private static bool KeypadGameEnterPrefix(KeypadGame __instance)
        {
            if (!Controller.TryGetTerroristNumpadSession(__instance, out var terroristNumpadSabotage)
                || terroristNumpadSabotage == null)
            {
                return true;
            }

            var entered = ReadEnteredCode(__instance);
            var correct = terroristNumpadSabotage.targetNumber == entered;
            var sessionId = Controller.ActiveOpenNumpadSessionId;
            __instance.StartCoroutine(
                AnimateTerroristKeypad(__instance, correct, sessionId).WrapToIl2Cpp());
            return false;
        }

        private static int ReadEnteredCode(KeypadGame game)
        {
            var ns = game.numString?.ToString();
            if (!string.IsNullOrEmpty(ns)
                && int.TryParse(ns, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            try
            {
                return Traverse.Create(game).Field<int>("number").Value;
            }
            catch
            {
                return game.number;
            }
        }

        private static IEnumerator AnimateTerroristKeypad(KeypadGame game, bool correct, int numpadSessionId)
        {
            var tr = Traverse.Create(game);
            tr.Field<bool>("animating").Value = true;
            var wait = new WaitForSeconds(0.1f);
            yield return wait;
            game.NumberText.text = string.Empty;
            yield return wait;

            if (correct)
            {
                var applied = Controller.TryFinalizeSuccessfulKeypad(numpadSessionId);
                if (applied)
                {
                    tr.Field<bool>("done").Value = true;
                    game.NumberText.text = "OK";
                    yield return wait;
                    game.NumberText.text = string.Empty;
                    yield return wait;
                    game.NumberText.text = "OK";
                    yield return wait;
                    game.NumberText.text = string.Empty;
                    yield return wait;
                    game.NumberText.text = "OK";
                }
            }
            else
            {
                game.NumberText.text = "Bad";
                yield return wait;
                game.NumberText.text = string.Empty;
                yield return wait;
                game.NumberText.text = "Bad";
                yield return wait;
                game.numString = string.Empty;
                game.number = 0;
                game.NumberText.text = string.Empty;
            }

            tr.Field<bool>("animating").Value = false;
            if (correct)
            {
                game.Close();
            }
        }
    }

    /// <summary>
    /// Minimal <see cref="NoOxyTask"/> for <see cref="KeypadGame.Begin"/>.
    /// </summary>
    [RegisterInIl2Cpp]
    public sealed class TerroristKeypadNoOxyTask(nint cppPtr) : NoOxyTask(cppPtr)
    {
        public override int TaskStep => 0;

        public override bool IsComplete => false;

        public override void AppendTaskText(StringBuilder sb)
        {
        }

        public override bool ValidConsole(global::Console console) => true;

        public override void Complete()
        {
            if (Owner != null)
            {
                Owner.RemoveTask(this);
            }

            if (gameObject != null)
            {
                Object.Destroy(gameObject);
            }
        }
    }

    #endregion
}
