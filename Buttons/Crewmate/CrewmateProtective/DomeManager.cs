using System.Collections;
using System.Collections.Generic;
using MiraAPI.GameOptions;
using Reactor.Utilities;
using DivaniMods.Options;
using DivaniMods.Roles.Crewmate.CrewmateProtective;
using TownOfUs.Assets;
using TownOfUs.Utilities;
using UnityEngine;
using UnityEngine.Rendering;

namespace DivaniMods.Buttons.Crewmate.CrewmateProtective;

public static class DomeManager
{
    private const float ShrinkDuration = 1f;
    private const float GrowDuration = ShrinkDuration;

    public sealed class Dome
    {
        public byte OwnerId { get; init; }
        public Vector3 Position { get; init; }
        public GameObject? GameObject { get; set; }
        public float BaseDiameter { get; init; }
        public float SpawnedAt { get; init; }
        public float ExpiresAt { get; set; } 
    }

    private static readonly List<Dome> _domes = new();
    private static Material? _domeMaterialTemplate;
    public static IReadOnlyList<Dome> Domes => _domes;

    public static void PlaceDome(byte ownerId, Vector3 position)
    {
        if (ShipStatus.Instance == null)
        {
            return;
        }

        var opts = OptionGroupSingleton<DomesmithOptions>.Instance;
        var rangeMultiplier = opts.DomeSize.Value;
        var diameter = rangeMultiplier * ShipStatus.Instance.MaxLightRadius * 2f;

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = $"DomesmithDome_{ownerId}_{_domes.Count}";
        go.transform.localScale = Vector3.zero;
        var sphereCollider = go.GetComponent("SphereCollider");
        if (sphereCollider != null)
        {
            UnityEngine.Object.Destroy(sphereCollider);
        }

        var renderer = go.GetComponent<MeshRenderer>();
        renderer.material = CreateDomeMaterial();

        var pos = position;
        pos.z += 0.001f;
        go.transform.position = pos;

        var now = Time.time;
        var expiresAt = now + opts.ActiveSeconds.Value;

        var dome = new Dome
        {
            OwnerId = ownerId,
            Position = pos,
            GameObject = go,
            BaseDiameter = diameter,
            SpawnedAt = now,
            ExpiresAt = expiresAt,
        };
        _domes.Add(dome);

        ApplyVisibility(dome);
        Coroutines.Start(GrowDome(dome));
        PlaceDomeButton.SyncDomeTimerFromManager();
    }

    private static IEnumerator GrowDome(Dome dome)
    {
        var elapsed = 0f;
        while (elapsed < GrowDuration && dome.GameObject != null)
        {
            elapsed += Time.deltaTime;
            var s = dome.BaseDiameter * Mathf.Clamp01(elapsed / GrowDuration);
            dome.GameObject.transform.localScale = new Vector3(s, s, s);
            yield return null;
        }

        if (dome.GameObject != null)
        {
            dome.GameObject.transform.localScale =
                new Vector3(dome.BaseDiameter, dome.BaseDiameter, dome.BaseDiameter);
        }
    }

    public static void Tick()
    {
        if (_domes.Count == 0)
        {
            PlaceDomeButton.SyncDomeTimerFromManager();
            return;
        }

        var now = Time.time;
        for (var i = _domes.Count - 1; i >= 0; i--)
        {
            var dome = _domes[i];
            if (now >= dome.ExpiresAt)
            {
                Destroy(dome);
                _domes.RemoveAt(i);
                continue;
            }

            UpdateDomeScale(dome, now);
        }

        PlaceDomeButton.SyncDomeTimerFromManager();
    }

    public static float GetLongestRemainingSeconds(byte ownerId)
    {
        var now = Time.time;
        var longest = 0f;
        foreach (var dome in _domes)
        {
            if (dome.OwnerId != ownerId || dome.ExpiresAt >= float.MaxValue)
            {
                continue;
            }

            longest = Mathf.Max(longest, dome.ExpiresAt - now);
        }

        return longest;
    }

    private static void UpdateDomeScale(Dome dome, float now)
    {
        if (dome.GameObject == null)
        {
            return;
        }

        if (now < dome.ExpiresAt - ShrinkDuration)
        {
            return;
        }

        var t = (dome.ExpiresAt - now) / ShrinkDuration;
        var scale = dome.BaseDiameter * Mathf.Clamp01(t);
        dome.GameObject.transform.localScale = new Vector3(scale, scale, scale);
    }

    public static Dome? FindContaining(Vector2 position)
    {
        if (_domes.Count == 0 || ShipStatus.Instance == null)
        {
            return null;
        }

        foreach (var dome in _domes)
        {
            if (dome.GameObject == null)
            {
                continue;
            }

            var radius = GetCurrentRadius(dome);
            if (radius <= 0f)
            {
                continue;
            }

            var center = new Vector2(dome.Position.x, dome.Position.y);
            if (Vector2.Distance(center, position) <= radius)
            {
                return dome;
            }
        }
        return null;
    }

    public static float GetCurrentRadius(Dome dome)
    {
        if (dome.GameObject == null || ShipStatus.Instance == null || dome.BaseDiameter <= 0f)
        {
            return 0f;
        }

        var baseRadius = OptionGroupSingleton<DomesmithOptions>.Instance.DomeSize.Value
                         * ShipStatus.Instance.MaxLightRadius;
        var scaleFactor = dome.GameObject.transform.localScale.x / dome.BaseDiameter;
        return baseRadius * scaleFactor;
    }

    public static void RefreshVisibility()
    {
        foreach (var dome in _domes)
        {
            ApplyVisibility(dome);
        }
    }

    public static void Clear()
    {
        foreach (var dome in _domes)
        {
            Destroy(dome);
        }
        _domes.Clear();
        PlaceDomeButton.SyncDomeTimerFromManager();
    }

    public static void SetVisibleAll(bool visible)
    {
        foreach (var dome in _domes)
        {
            if (dome.GameObject == null)
            {
                continue;
            }
            var renderer = dome.GameObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.enabled = visible && LocalShouldSee(dome);
            }
        }
    }

    private static void Destroy(Dome dome)
    {
        if (dome.GameObject != null)
        {
            UnityEngine.Object.Destroy(dome.GameObject);
            dome.GameObject = null;
        }
    }

    private static void ApplyVisibility(Dome dome)
    {
        if (dome.GameObject == null)
        {
            return;
        }
        var renderer = dome.GameObject.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            return;
        }
        renderer.enabled = LocalShouldSee(dome);
    }

    private static Material CreateDomeMaterial()
    {
        _domeMaterialTemplate ??= BuildDomeMaterialTemplate();
        return new Material(_domeMaterialTemplate);
    }

    private static Material BuildDomeMaterialTemplate()
    {
        var mat = new Material(AuAvengersAnims.TrapMaterial.LoadAsset());
        var tint = DomesmithRole.DomesmithColor;
        tint.a = 0.35f;

        foreach (var texName in new[] { "_MainTex", "_BaseMap", "_MainTexture" })
        {
            if (mat.HasProperty(texName))
            {
                mat.SetTexture(texName, Texture2D.whiteTexture);
            }
        }

        var shader = mat.shader;
        for (var i = 0; i < shader.GetPropertyCount(); i++)
        {
            if (shader.GetPropertyType(i) != ShaderPropertyType.Color)
            {
                continue;
            }
            mat.SetColor(shader.GetPropertyName(i), tint);
        }

        mat.color = tint;
        return mat;
    }

    private static bool LocalShouldSee(Dome dome)
    {
        var localPlayer = PlayerControl.LocalPlayer;
        if (localPlayer == null || localPlayer.Data == null)
        {
            return false;
        }

        var mode = (DomesmithVisibility)OptionGroupSingleton<DomesmithOptions>.Instance.SeenBy.Value;
        return mode switch
        {
            DomesmithVisibility.Everyone => true,
            DomesmithVisibility.NonImpostor => !localPlayer.Data.Role.IsImpostor(),
            DomesmithVisibility.Crewmates => localPlayer.IsCrewmate() || localPlayer.IsNeutral(),
            _ => localPlayer.Data.Role is DomesmithRole || localPlayer.PlayerId == dome.OwnerId,
        };
    }
}
