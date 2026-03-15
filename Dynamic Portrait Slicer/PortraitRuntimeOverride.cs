using HarmonyLib;
using StardewModdingAPI;
using StardewValley.Menus;
using System;
using System.Runtime.CompilerServices;

namespace DynamicPortraitSlicer;

internal static class PortraitRuntimeOverride
{
    private sealed class ScaleSnapshot
    {
        public float OriginalScale;
    }

    private static readonly ConditionalWeakTable<object, ScaleSnapshot> _scaleSnapshots = new();

    public static void ApplyIfAny(DialogueBox dialogueBox, object portraitDataObj)
    {
        var npc = ReflectionHelpers.TryGetSpeakerNpc(dialogueBox);
        string? npcName = npc?.Name;

        if (string.IsNullOrWhiteSpace(npcName))
            return;

        if (ModEntry.Config.EnableRuntimeOverrides && ModEntry.Config.PreferLiveTuningOverNpcOverrides)
        {
            NpcPortraitOverride live;

            // If hotkeys are editing the per-NPC override, treat that as the live source of truth.
            if (ModEntry.Config.LiveNudgeEditsNpcOverride
                && ModEntry.Config.NpcOverrides.TryGetValue(npcName, out var pov)
                && pov is not null)
            {
                live = new NpcPortraitOverride
                {
                    Enabled = true,
                    Side = pov.Side,
                    OffsetX = pov.OffsetX,
                    OffsetY = pov.OffsetY,
                    ScaleMultiplier = pov.ScaleMultiplier
                };
            }
            else
            {
                // Otherwise, use the global live sliders.
                live = new NpcPortraitOverride
                {
                    Enabled = true,
                    Side = ModEntry.Config.PortraitSide,
                    OffsetX = ModEntry.Config.PortraitOffsetX,
                    OffsetY = ModEntry.Config.PortraitOffsetY,
                    ScaleMultiplier = ModEntry.Config.PortraitScaleMultiplier
                };
            }

            if (ModEntry.Config.VerboseLogging)
                ModEntry.Log.Log($"ApplyIfAny path=LIVE npc={npcName} off=({live.OffsetX},{live.OffsetY})", LogLevel.Info);

            ApplyOverride(portraitDataObj, live);
            return;
        }

        // Prefer explicit per-NPC override (normal play mode)
        if (ModEntry.Config.NpcOverrides.TryGetValue(npcName, out var ov) && ov is not null && ov.Enabled)
        {
            if (ModEntry.Config.VerboseLogging)
                ModEntry.Log.Log($"ApplyIfAny path=NPCOVERRIDE npc={npcName} off=({ov.OffsetX},{ov.OffsetY})", LogLevel.Info);

            ApplyOverride(portraitDataObj, ov);
            return;
        }

        // Live-tuning: apply current sliders to whoever is speaking (optional, for testing/tuning)
        if (ModEntry.Config.EnableRuntimeOverrides)
        {
            if (ModEntry.Config.VerboseLogging)
                ModEntry.Log.Log($"ApplyIfAny path=LIVE_FALLBACK npc={npcName} off=({ModEntry.Config.PortraitOffsetX},{ModEntry.Config.PortraitOffsetY})", LogLevel.Info);

            var live = new NpcPortraitOverride
            {
                Enabled = true,
                Side = ModEntry.Config.PortraitSide,
                OffsetX = ModEntry.Config.PortraitOffsetX,
                OffsetY = ModEntry.Config.PortraitOffsetY,
                ScaleMultiplier = ModEntry.Config.PortraitScaleMultiplier
            };

            ApplyOverride(portraitDataObj, live);
        }
    }

    private static void ApplyOverride(object portraitDataObj, NpcPortraitOverride ov)
    {
        DumpPortraitMembersOnce(portraitDataObj);

        var pt = portraitDataObj.GetType();

        // Anchor
        SetBool(pt, portraitDataObj, "Bottom", "bottom", false);

        // Offsets: FIRST set the canonical names that the dump showed exist (XOffset/YOffset).
        SetInt(pt, portraitDataObj, "XOffset", "xOffset", ov.OffsetX);
        SetInt(pt, portraitDataObj, "YOffset", "yOffset", ov.OffsetY);

        // Also try additional common offset names (harmless if absent)
        SetInt(pt, portraitDataObj, "OffsetX", "offsetX", ov.OffsetX);
        SetInt(pt, portraitDataObj, "OffsetY", "offsetY", ov.OffsetY);

        SetInt(pt, portraitDataObj, "PortraitOffsetX", "portraitOffsetX", ov.OffsetX);
        SetInt(pt, portraitDataObj, "PortraitOffsetY", "portraitOffsetY", ov.OffsetY);

        SetInt(pt, portraitDataObj, "DrawOffsetX", "drawOffsetX", ov.OffsetX);
        SetInt(pt, portraitDataObj, "DrawOffsetY", "drawOffsetY", ov.OffsetY);

        // NEW: immediately read back XOffset/YOffset so we can detect if they changed or got overwritten.
        if (ModEntry.Config.VerboseLogging)
        {
            int xoAfter = GetInt(pt, portraitDataObj, "XOffset", "xOffset", int.MinValue);
            int yoAfter = GetInt(pt, portraitDataObj, "YOffset", "yOffset", int.MinValue);
            ModEntry.Log.Log($"ApplyOverride wrote offsets -> XOffset={xoAfter} YOffset={yoAfter}", LogLevel.Info);
        }

        // Side
        if (ov.Side != PortraitSide.Default)
        {
            bool wantRight = ov.Side == PortraitSide.Right;
            SetBool(pt, portraitDataObj, "Right", "right", wantRight);
        }

        // Scale (idempotent with snapshot)
        float currentScale = GetFloat(pt, portraitDataObj, "Scale", "scale", 1f);
        var snap = _scaleSnapshots.GetValue(portraitDataObj, _ => new ScaleSnapshot { OriginalScale = currentScale });

        float sliderValue = ov.ScaleMultiplier;
        if (sliderValue <= 0f) sliderValue = 1f;

        float targetScale = ModEntry.Config.RuntimeScaleMode switch
        {
            ScaleMode.MultiplyOriginal => snap.OriginalScale * sliderValue,
            _ => sliderValue, // Absolute
        };

        if (targetScale < 0.05f) targetScale = 0.05f;
        if (targetScale > 10f) targetScale = 10f;

        SetFloat(pt, portraitDataObj, "Scale", "scale", targetScale);

        if (ModEntry.Config.VerboseLogging)
        {
            int xo = GetInt(pt, portraitDataObj, "XOffset", "xOffset", 0);
            int yo = GetInt(pt, portraitDataObj, "YOffset", "yOffset", 0);
            bool right = GetBool(pt, portraitDataObj, "Right", "right", false);
            bool bottom = GetBool(pt, portraitDataObj, "Bottom", "bottom", false);
            float sc = GetFloat(pt, portraitDataObj, "Scale", "scale", 0f);

            ModEntry.Log.Log($"Runtime override applied: XOffset={xo} YOffset={yo} Right={right} Bottom={bottom} Scale={sc}", LogLevel.Info);
        }
    }

    private static float GetFloat(Type t, object obj, string pascal, string camel, float fallback)
    {
        var p = AccessTools.Property(t, pascal) ?? AccessTools.Property(t, camel);
        if (p?.CanRead == true)
        {
            if (p.PropertyType == typeof(float)) return (float)(p.GetValue(obj) ?? fallback);
            if (p.PropertyType == typeof(double)) return (float)((double)(p.GetValue(obj) ?? (double)fallback));
            if (p.PropertyType == typeof(int)) return (int)(p.GetValue(obj) ?? (int)fallback);
        }

        var f = AccessTools.Field(t, pascal) ?? AccessTools.Field(t, camel);
        if (f is not null)
        {
            if (f.FieldType == typeof(float)) return (float)(f.GetValue(obj) ?? fallback);
            if (f.FieldType == typeof(double)) return (float)((double)(f.GetValue(obj) ?? (double)fallback));
            if (f.FieldType == typeof(int)) return (int)(f.GetValue(obj) ?? (int)fallback);
        }

        return fallback;
    }

    private static int GetInt(Type t, object obj, string pascal, string camel, int fallback)
    {
        var p = AccessTools.Property(t, pascal) ?? AccessTools.Property(t, camel);
        if (p?.CanRead == true && p.PropertyType == typeof(int))
            return (int)(p.GetValue(obj) ?? fallback);

        var f = AccessTools.Field(t, pascal) ?? AccessTools.Field(t, camel);
        if (f is not null && f.FieldType == typeof(int))
            return (int)(f.GetValue(obj) ?? fallback);

        return fallback;
    }

    private static bool GetBool(Type t, object obj, string pascal, string camel, bool fallback)
    {
        var p = AccessTools.Property(t, pascal) ?? AccessTools.Property(t, camel);
        if (p?.CanRead == true && p.PropertyType == typeof(bool))
            return (bool)(p.GetValue(obj) ?? fallback);

        var f = AccessTools.Field(t, pascal) ?? AccessTools.Field(t, camel);
        if (f is not null && f.FieldType == typeof(bool))
            return (bool)(f.GetValue(obj) ?? fallback);

        return fallback;
    }

    private static void SetInt(Type t, object obj, string pascal, string camel, int value)
    {
        var p = AccessTools.Property(t, pascal) ?? AccessTools.Property(t, camel);
        if (p?.CanWrite == true && p.PropertyType == typeof(int))
        {
            p.SetValue(obj, value);
            return;
        }

        var f = AccessTools.Field(t, pascal) ?? AccessTools.Field(t, camel);
        if (f is not null && f.FieldType == typeof(int))
            f.SetValue(obj, value);
    }

    private static void SetFloat(Type t, object obj, string pascal, string camel, float value)
    {
        var p = AccessTools.Property(t, pascal) ?? AccessTools.Property(t, camel);
        if (p?.CanWrite == true)
        {
            if (p.PropertyType == typeof(float)) { p.SetValue(obj, value); return; }
            if (p.PropertyType == typeof(double)) { p.SetValue(obj, (double)value); return; }
        }

        var f = AccessTools.Field(t, pascal) ?? AccessTools.Field(t, camel);
        if (f is not null)
        {
            if (f.FieldType == typeof(float)) f.SetValue(obj, value);
            else if (f.FieldType == typeof(double)) f.SetValue(obj, (double)value);
        }
    }

    private static void SetBool(Type t, object obj, string pascal, string camel, bool value)
    {
        var p = AccessTools.Property(t, pascal) ?? AccessTools.Property(t, camel);
        if (p?.CanWrite == true && p.PropertyType == typeof(bool))
        {
            p.SetValue(obj, value);
            return;
        }

        var f = AccessTools.Field(t, pascal) ?? AccessTools.Field(t, camel);
        if (f is not null && f.FieldType == typeof(bool))
            f.SetValue(obj, value);
    }

    private static bool _dumpedMembers = false;

    private static void DumpPortraitMembersOnce(object portraitDataObj)
    {
        if (_dumpedMembers || !ModEntry.Config.VerboseLogging)
            return;

        _dumpedMembers = true;

        var t = portraitDataObj.GetType();
        ModEntry.Log.Log($"PortraitData runtime type: {t.FullName}", StardewModdingAPI.LogLevel.Info);

        foreach (var p in t.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
        {
            if (p.GetIndexParameters().Length != 0) continue;
            string name = p.Name;
            if (name.Contains("offset", StringComparison.OrdinalIgnoreCase)
                || name.Equals("X", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Y", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Scale", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Right", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Bottom", StringComparison.OrdinalIgnoreCase))
            {
                ModEntry.Log.Log($"PortraitData prop: {p.PropertyType.Name} {p.Name} (set={p.CanWrite})", StardewModdingAPI.LogLevel.Info);
            }
        }

        foreach (var f in t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
        {
            string name = f.Name;
            if (name.Contains("offset", StringComparison.OrdinalIgnoreCase)
                || name.Equals("X", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Y", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Scale", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Right", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Bottom", StringComparison.OrdinalIgnoreCase))
            {
                ModEntry.Log.Log($"PortraitData field: {f.FieldType.Name} {f.Name}", StardewModdingAPI.LogLevel.Info);
            }
        }
    }
}