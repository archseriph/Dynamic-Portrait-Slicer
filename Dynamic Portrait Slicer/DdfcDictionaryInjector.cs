using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DynamicPortraitSlicer;

internal sealed class DdfcDictionaryInjector
{
    private const string DdfcDictionaryAsset = "aedenthorn.DialogueDisplayFramework/dictionary";
    private readonly IMonitor _monitor;

    public DdfcDictionaryInjector(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (!e.NameWithoutLocale.IsEquivalentTo(DdfcDictionaryAsset))
            return;

        e.Edit(asset =>
        {
            if (ModEntry.Config.VerboseLogging)
                _monitor.Log("DDFC inject: running injector stamp=ASDICTIONARY_REFLECTION_V2", LogLevel.Info);

            IDictionary dict = GetDictionaryDataViaAsDictionary(asset);

            // Apply overrides for all configured NPCs.
            foreach (var pair in ModEntry.Config.NpcOverrides)
            {
                string npcKey = pair.Key?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(npcKey))
                    continue;

                var ov = pair.Value;
                if (ov is null || !ov.Enabled)
                    continue;

                object? entryObj = dict.Contains(npcKey) ? dict[npcKey] : null;
                if (entryObj is null)
                {
                    object? defaultObj = dict.Contains("default") ? dict["default"] : null;
                    if (defaultObj is null)
                        continue;

                    entryObj = ShallowCloneByMemberwiseClone(defaultObj);
                    dict[npcKey] = entryObj;

                    if (ModEntry.Config.VerboseLogging)
                        _monitor.Log($"DDFC inject: created new entry '{npcKey}' by cloning 'default'.", LogLevel.Info);
                }

                ApplyOverrideToDialogueDisplayData(entryObj, ov);
            }

            // Back-compat: also apply "current sliders" to TargetNpcKey (so your current GMCM workflow still works).
            ApplyLegacyTargetKeyEdit(dict);
        });
    }

    private void ApplyLegacyTargetKeyEdit(IDictionary dict)
    {
        string npc = (ModEntry.Config.TargetNpcKey ?? "default").Trim();
        if (string.IsNullOrWhiteSpace(npc))
            npc = "default";

        var ov = new NpcPortraitOverride
        {
            Enabled = true,
            Side = ModEntry.Config.PortraitSide,
            OffsetX = ModEntry.Config.PortraitOffsetX,
            OffsetY = ModEntry.Config.PortraitOffsetY,
            ScaleMultiplier = ModEntry.Config.PortraitScaleMultiplier
        };

        foreach (string key in GetMatchingKeys(dict, npc).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            object? entryObj = dict.Contains(key) ? dict[key] : null;
            if (entryObj is null)
            {
                object? defaultObj = dict.Contains("default") ? dict["default"] : null;
                if (defaultObj is null)
                    continue;

                entryObj = ShallowCloneByMemberwiseClone(defaultObj);
                dict[key] = entryObj;

                if (ModEntry.Config.VerboseLogging)
                    _monitor.Log($"DDFC inject: created new legacy target entry '{key}' by cloning 'default'.", LogLevel.Info);
            }

            ApplyOverrideToDialogueDisplayData(entryObj, ov);

            if (ModEntry.Config.VerboseLogging)
                _monitor.Log($"DDFC inject: applied legacy override to key '{key}'", LogLevel.Info);
        }
    }
    private void ApplyOverrideToDialogueDisplayData(object entryObj, NpcPortraitOverride ov)
    {
        var entryType = entryObj.GetType();

        object? portraitObj =
            AccessTools.Property(entryType, "Portrait")?.GetValue(entryObj) ??
            AccessTools.Property(entryType, "portrait")?.GetValue(entryObj) ??
            AccessTools.Field(entryType, "Portrait")?.GetValue(entryObj) ??
            AccessTools.Field(entryType, "portrait")?.GetValue(entryObj);

        if (portraitObj is null)
        {
            _monitor.Log($"DDFC inject: entry type {entryType.FullName} has no Portrait/portrait.", LogLevel.Warn);
            return;
        }
        
        var pt = portraitObj.GetType();

        
        if (ModEntry.Config.VerboseLogging)
        {
            int xo = GetInt(pt, portraitObj, "XOffset", "xOffset", 0);
            int yo = GetInt(pt, portraitObj, "YOffset", "yOffset", 0);
            bool right = GetBool(pt, portraitObj, "Right", "right", false);
            bool bottom = GetBool(pt, portraitObj, "Bottom", "bottom", false);
            float sc = GetFloat(pt, portraitObj, "Scale", "scale", 0f);

            _monitor.Log($"DDFC inject wrote: key applied -> XOffset={xo} YOffset={yo} Right={right} Bottom={bottom} Scale={sc}", LogLevel.Info);
        }

        SetInt(pt, portraitObj, "XOffset", "xOffset", ov.OffsetX);
        SetInt(pt, portraitObj, "YOffset", "yOffset", ov.OffsetY);

        if (ov.Side != PortraitSide.Default)
        {
            bool wantRight = ov.Side == PortraitSide.Right;
            SetBool(pt, portraitObj, "Right", "right", wantRight);
        }

        float existingScale = GetFloat(pt, portraitObj, "Scale", "scale", fallback: 4f);
        if (ov.ScaleMultiplier > 0f && Math.Abs(ov.ScaleMultiplier - 1f) > 0.0001f)
            SetFloat(pt, portraitObj, "Scale", "scale", existingScale * ov.ScaleMultiplier);
    }

    /// <summary>
    /// Get the underlying dictionary using SMAPI's AsDictionary&lt;TKey, TValue&gt;,
    /// but discover TValue dynamically to avoid compile-time dependency on DDFC types.
    /// </summary>
    private static IDictionary GetDictionaryDataViaAsDictionary(IAssetData asset)
    {
        object raw = FindRawDataObject(asset);

        Type rawType = raw.GetType();
        if (!rawType.IsGenericType || rawType.GetGenericTypeDefinition() != typeof(Dictionary<,>))
            throw new InvalidOperationException($"Unexpected raw type for DDFC dictionary: {rawType.FullName}");

        Type[] genericArgs = rawType.GetGenericArguments();
        Type keyType = genericArgs[0];
        Type valueType = genericArgs[1];

        MethodInfo? asDictMethod = asset.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(m =>
                m.Name == "AsDictionary"
                && m.IsGenericMethodDefinition
                && m.GetGenericArguments().Length == 2
                && m.GetParameters().Length == 0);

        if (asDictMethod is null)
            throw new MissingMethodException(asset.GetType().FullName, "AsDictionary<TKey, TValue>()");

        object wrapper = asDictMethod.MakeGenericMethod(keyType, valueType)
            .Invoke(asset, Array.Empty<object>())!;

        PropertyInfo? dataProp = wrapper.GetType().GetProperty("Data", BindingFlags.Instance | BindingFlags.Public);
        if (dataProp is null)
            throw new MissingMemberException(wrapper.GetType().FullName, "Data");

        object data = dataProp.GetValue(wrapper)!;

        if (data is not IDictionary dict)
            throw new InvalidCastException($"AsDictionary wrapper.Data is not IDictionary: {data.GetType().FullName}");

        return dict;
    }

    private static IEnumerable<string> GetMatchingKeys(IDictionary dict, string npcName)
    {
        // Apply to exact name and any variant keys like Abigail_Beach, Abigail_<AppearanceId>, etc.
        foreach (object k in dict.Keys)
        {
            if (k is not string s) continue;

            if (s.Equals(npcName, StringComparison.OrdinalIgnoreCase))
                yield return s;
            else if (s.StartsWith(npcName + "_", StringComparison.OrdinalIgnoreCase))
                yield return s;
        }

        // Ensure base key exists too, even if it wasn't in dict yet.
        yield return npcName;
    }

    /// <summary>Locate SMAPI's private backing field/property that holds the raw Dictionary&lt;,&gt;.</summary>
    private static object FindRawDataObject(IAssetData asset)
    {
        var t = asset.GetType();

        foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            object? val = f.GetValue(asset);
            if (val is null) continue;

            Type vt = val.GetType();
            if (vt.IsGenericType && vt.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                return val;
        }

        foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (!p.CanRead || p.GetIndexParameters().Length != 0) continue;

            object? val = null;
            try { val = p.GetValue(asset); } catch { }

            if (val is null) continue;

            Type vt = val.GetType();
            if (vt.IsGenericType && vt.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                return val;
        }

        throw new MissingMemberException(t.FullName, "raw Dictionary<,> backing field/property");
    }

    private static object ShallowCloneByMemberwiseClone(object obj)
    {
        var m = AccessTools.Method(obj.GetType(), "MemberwiseClone");
        return m.Invoke(obj, Array.Empty<object>())!;
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
}