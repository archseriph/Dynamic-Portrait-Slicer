using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using System;

namespace DynamicPortraitSlicer;

internal static partial class DdfcPortraitClampPatch
{
    public static void Prefix(SpriteBatch __0, DialogueBox __1, object __2)
    {
        if (ModEntry.Config.VerboseLogging)
            ModEntry.Log.Log("DdfcPortraitClampPatch.Prefix running", LogLevel.Trace);

        var dialogueBox = __1;
        var portrait = __2;

        try
        {
            var t = portrait.GetType();

            // Apply per-NPC runtime override (Right/XOffset/YOffset/Scale) to the actual instance being drawn.
            PortraitRuntimeOverride.ApplyIfAny(dialogueBox, portrait);

            // Resolve NPC + portrait texture ONCE (used for slicing + logs).
            NPC? npc = ReflectionHelpers.TryGetSpeakerNpc(dialogueBox);
            string npcName = npc?.Name ?? "(unknown)";
            Texture2D? npcPortrait = npc?.Portrait;

            int portraitIndex = 0;
            try
            {
                if (npc is not null)
                    portraitIndex = ReflectionHelpers.TryGetPortraitIndex(dialogueBox, npc);
            }
            catch
            {
                portraitIndex = 0;
            }

            // Read portrait draw data (after runtime override)
            int x = GetInt(t, portrait, "X");
            int y = GetInt(t, portrait, "Y");
            int w = GetInt(t, portrait, "W");
            int h = GetInt(t, portrait, "H");
            bool tileSheet = GetBool(t, portrait, "TileSheet");

            int targetWidth = GetInt(t, portrait, "Width");
            float scale = GetFloat(t, portrait, "Scale");

            if (ModEntry.Config.VerboseLogging)
                ModEntry.Log.Log($"Portrait raw: npc={npcName} index={portraitIndex} TileSheet={tileSheet} X={x} Y={y} W={w} H={h} Width={targetWidth} Scale={scale}", LogLevel.Info);

            bool normalizedThisCall = false;

            // Normalize if DDFC is in "auto" (X/Y=-1) OR if it left an obviously wrong huge W/H after switching packs.
            bool needsNormalize = tileSheet && ((x < 0 || y < 0) || w > 256 || h > 256);
            if (needsNormalize)
            {
                if (npcPortrait != null
                    && PortraitSlicer.TryGetFrameRect(npcPortrait, portraitIndex, npc?.Name, ModEntry.Config, out var rect, out var reason))
                {
                    normalizedThisCall = true;

                    SetInt(t, portrait, "X", rect.X);
                    SetInt(t, portrait, "Y", rect.Y);
                    SetInt(t, portrait, "W", rect.Width);
                    SetInt(t, portrait, "H", rect.Height);

                    x = rect.X;
                    y = rect.Y;
                    w = rect.Width;
                    h = rect.Height;

                    // If the inferred "tilesheet" is actually a single frame, force single-image mode.
                    bool dividesEvenly =
                        rect.Width > 0 && rect.Height > 0
                        && npcPortrait.Width % rect.Width == 0
                        && npcPortrait.Height % rect.Height == 0;

                    int cols = (dividesEvenly && rect.Width > 0) ? npcPortrait.Width / rect.Width : 0;
                    int rows = (dividesEvenly && rect.Height > 0) ? npcPortrait.Height / rect.Height : 0;

                    if (cols == 1 && rows == 1)
                    {
                        SetBool(t, portrait, "TileSheet", false);
                        SetInt(t, portrait, "X", 0);
                        SetInt(t, portrait, "Y", 0);
                        SetInt(t, portrait, "W", npcPortrait.Width);
                        SetInt(t, portrait, "H", npcPortrait.Height);

                        x = 0;
                        y = 0;
                        w = npcPortrait.Width;
                        h = npcPortrait.Height;
                        tileSheet = false;

                        if (ModEntry.Config.VerboseLogging)
                            ModEntry.Log.Log($"Portrait normalize: npc={npcName} detected single-image texture; forcing TileSheet=false rect=(0,0,{w}x{h})", LogLevel.Info);
                    }

                    if (ModEntry.Config.VerboseLogging)
                        ModEntry.Log.Log($"Portrait normalize: npc={npcName} index={portraitIndex} -> rect=({rect.X},{rect.Y},{rect.Width}x{rect.Height}) reason={reason}", LogLevel.Info);
                }
                else
                {
                    // Fallback if we can't infer anything: use 64 to avoid a crash/stretch.
                    const int fallbackFrame = 64;
                    SetInt(t, portrait, "X", 0);
                    SetInt(t, portrait, "Y", 0);
                    SetInt(t, portrait, "W", fallbackFrame);
                    SetInt(t, portrait, "H", fallbackFrame);

                    x = 0;
                    y = 0;
                    w = fallbackFrame;
                    h = fallbackFrame;

                    if (ModEntry.Config.VerboseLogging)
                        ModEntry.Log.Log($"Portrait normalize fallback: npc={npcName} -> rect=(0,0,{fallbackFrame}x{fallbackFrame})", LogLevel.Info);
                }
            }

            // If DDFC didn't set a source frame (X/Y = -1), don't interfere further.
            if (x < 0 || y < 0)
                return;

            // If DDFC isn't drawing from a tilesheet, don't force tilesheet clamping.
            // (If we forced single-image mode above, we should stop here.)
            if (!tileSheet)
                return;

            // FINAL FORM:
            // Never early-return just because we normalized.
            // Instead, skip ONLY the legacy clamp heuristics in that case.
            if (!normalizedThisCall)
            {
                int frame = ResolveFrameSize(npcPortrait, w, h, targetWidth, scale);

                bool rectMissing = w <= 0 || h <= 0;
                bool rectTooBig = (npcPortrait != null) && (w >= npcPortrait.Width || h >= npcPortrait.Height);

                bool looksLikeFrame = (w == 64 && h == 64) || (w == 128 && h == 128);
                bool quarterSymptom = looksLikeFrame && frame == 128 && w == 64 && h == 64;

                if (rectMissing || rectTooBig || quarterSymptom)
                {
                    if (npcPortrait != null)
                    {
                        if (x < 0 || y < 0 || x >= npcPortrait.Width || y >= npcPortrait.Height)
                        {
                            x = 0;
                            y = 0;
                        }

                        if (x + frame > npcPortrait.Width) x = 0;
                        if (y + frame > npcPortrait.Height) y = 0;
                    }
                    else
                    {
                        x = 0;
                        y = 0;
                    }

                    SetInt(t, portrait, "X", x);
                    SetInt(t, portrait, "Y", y);
                    SetInt(t, portrait, "W", frame);
                    SetInt(t, portrait, "H", frame);
                    SetBool(t, portrait, "TileSheet", true);

                    if (ModEntry.Config.VerboseLogging)
                        ModEntry.Log.Log($"DDFC clamp applied npc={npcName} -> rect=({x},{y},{frame}x{frame})", LogLevel.Trace);
                }
            }
        }
        catch (Exception ex)
        {
            ModEntry.Log.Log($"DDFC clamp patch failed: {ex}", LogLevel.Error);
        }
    }

    private static int ResolveFrameSize(Texture2D? npcPortrait, int currentW, int currentH, int targetW, float scale)
    {
        int forced = ModEntry.Config.ForceFrameSize;
        if (forced == 64 || forced == 128)
            return forced;

        if (currentW > 0 && currentW == currentH && (currentW == 64 || currentW == 128))
            return currentW;

        if (scale > 0.01f)
        {
            int fromTarget = (int)Math.Round(targetW / scale);
            if (fromTarget == 64 || fromTarget == 128)
                return fromTarget;
        }

        if (npcPortrait != null)
        {
            bool div128 = npcPortrait.Width % 128 == 0 && npcPortrait.Height % 128 == 0;
            bool div64 = npcPortrait.Width % 64 == 0 && npcPortrait.Height % 64 == 0;

            if (npcPortrait.Width >= 512 || npcPortrait.Height >= 512)
            {
                if (div128) return 128;
                if (div64) return 64;
            }
            else
            {
                if (div64) return 64;
                if (div128) return 128;
            }

            int size = Math.Min(npcPortrait.Width, npcPortrait.Height);
            return Math.Clamp(size, 16, 1024);
        }

        return 64;
    }

    // --- helpers used above ---
    private static int GetInt(Type t, object obj, string name)
        => (int)(AccessTools.Property(t, name)?.GetValue(obj) ?? AccessTools.Field(t, name)?.GetValue(obj) ?? 0);

    private static float GetFloat(Type t, object obj, string name)
        => (float)(AccessTools.Property(t, name)?.GetValue(obj) ?? AccessTools.Field(t, name)?.GetValue(obj) ?? 0f);

    private static bool GetBool(Type t, object obj, string name)
        => (bool)(AccessTools.Property(t, name)?.GetValue(obj) ?? AccessTools.Field(t, name)?.GetValue(obj) ?? false);

    private static void SetInt(Type t, object obj, string name, int value)
    {
        AccessTools.Property(t, name)?.SetValue(obj, value);
        AccessTools.Field(t, name)?.SetValue(obj, value);
    }

    private static void SetBool(Type t, object obj, string name, bool value)
    {
        AccessTools.Property(t, name)?.SetValue(obj, value);
        AccessTools.Field(t, name)?.SetValue(obj, value);
    }

    private static void SetBool(Type t, object obj, string pascal, string camel, bool value)
    {
        AccessTools.Property(t, pascal)?.SetValue(obj, value);
        AccessTools.Property(t, camel)?.SetValue(obj, value);
        AccessTools.Field(t, pascal)?.SetValue(obj, value);
        AccessTools.Field(t, camel)?.SetValue(obj, value);
    }
}