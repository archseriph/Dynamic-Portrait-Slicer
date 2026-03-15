using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("BenchmarkSuite1")]

namespace DynamicPortraitSlicer;

internal static class PortraitSlicer
{
    public static bool TryGetFrameRect(Texture2D texture, int portraitIndex, string? npcName, ModConfig config, out Rectangle rect, out string reason)
    {
        rect = Rectangle.Empty;

        if (texture is null)
        {
            reason = "texture is null";
            return false;
        }

        int texW = texture.Width;
        int texH = texture.Height;

        if (texW <= 0 || texH <= 0)
        {
            reason = $"invalid texture size {texW}x{texH}";
            return false;
        }

        // NEW: per-NPC overrides in NpcOverrides can specify:
        // - ForceSingleImage
        // - explicit FrameWidth/FrameHeight (supports non-square)
        // - explicit Columns/Rows
        if (!string.IsNullOrWhiteSpace(npcName)
            && config.NpcOverrides.TryGetValue(npcName, out var pov)
            && pov is not null)
        {
            if (pov.ForceSingleImage == true)
            {
                rect = new Rectangle(0, 0, texW, texH);
                reason = $"SLICER_V2 npc override ForceSingleImage=true -> rect=(0,0,{texW}x{texH})";
                return true;
            }

            if (pov.FrameWidth is int fw && pov.FrameHeight is int fh && fw > 0 && fh > 0)
            {
                if (TryBuildRect(texW, texH, portraitIndex, fw, fh, out rect, out reason))
                {
                    reason = $"SLICER_V2 npc override frame={fw}x{fh}; {reason}";
                    return true;
                }

                reason = $"SLICER_V2 npc override frame={fw}x{fh} failed: {reason}";
                return false;
            }

            if (pov.Columns is int cols && pov.Rows is int rows && cols > 0 && rows > 0)
            {
                if (texW % cols != 0 || texH % rows != 0)
                {
                    reason = $"SLICER_V2 npc override grid={cols}x{rows} not divisible for tex={texW}x{texH}";
                    return false;
                }

                int fw2 = texW / cols;
                int fh2 = texH / rows;

                if (TryBuildRect(texW, texH, portraitIndex, fw2, fh2, out rect, out reason))
                {
                    reason = $"SLICER_V2 npc override grid={cols}x{rows} frame={fw2}x{fh2}; {reason}";
                    return true;
                }

                reason = $"SLICER_V2 npc override grid={cols}x{rows} frame={fw2}x{fh2} failed: {reason}";
                return false;
            }
        }

        // Legacy per-NPC square override wins (keep for back-compat)
        if (!string.IsNullOrWhiteSpace(npcName) && config.NpcFrameSizeOverride.TryGetValue(npcName, out int forcedSize) && forcedSize > 0)
        {
            if (TryBuildRect(texW, texH, portraitIndex, forcedSize, forcedSize, out rect, out reason))
                return true;

            reason = $"SLICER_V2 NPC override frameSize={forcedSize} failed: {reason}";
            return false;
        }

        // Heuristic: try preferred sizes, but choose the largest "reasonable" square frame.
        // "Divides evenly" alone is too weak for huge sheets (64 divides almost everything).
        foreach (int size in (config.PreferredFrameSizes ?? new List<int>())
            .Where(s => s > 0)
            .Distinct()
            .OrderByDescending(s => s))
        {
            if (texW % size != 0 || texH % size != 0)
                continue;

            int cols = texW / size;
            int rows = texH / size;

            // Reject absurd grids (helps avoid picking 64 for huge textures like 2048x5120).
            if (cols <= 0 || rows <= 0)
                continue;

            if (rows > 10)
                continue;

            if (cols > 30)
                continue;

            if (TryBuildRect(texW, texH, portraitIndex, size, size, out rect, out reason))
            {
                reason = $"SLICER_V2 preferred size={size} cols={cols} rows={rows}; {reason}";
                return true;
            }
        }

        // Fallback: choose the largest square that divides both dimensions.
        int fallback = GreatestSquareDivisor(texW, texH);
        if (fallback >= 32 && TryBuildRect(texW, texH, portraitIndex, fallback, fallback, out rect, out reason))
            return true;

        reason = $"couldn't infer frame size for texture {texW}x{texH}";
        return false;
    }

    private static bool TryBuildRect(int texW, int texH, int index, int frameW, int frameH, out Rectangle rect, out string reason)
    {
        rect = Rectangle.Empty;

        if (frameW <= 0 || frameH <= 0)
        {
            reason = "invalid frame size";
            return false;
        }

        int framesPerRow = texW / frameW;
        if (framesPerRow <= 0)
        {
            reason = "framesPerRow <= 0";
            return false;
        }

        int col = index % framesPerRow;
        int row = index / framesPerRow;

        int x = col * frameW;
        int y = row * frameH;

        // Clamp index if out of bounds (some mods use fewer frames)
        if (x + frameW > texW || y + frameH > texH)
        {
            int maxCols = framesPerRow;
            int maxRows = texH / frameH;
            int maxFrames = maxCols * maxRows;

            if (maxFrames <= 0)
            {
                reason = "maxFrames <= 0";
                return false;
            }

            int clamped = ((index % maxFrames) + maxFrames) % maxFrames;
            col = clamped % framesPerRow;
            row = clamped / framesPerRow;
            x = col * frameW;
            y = row * frameH;

            if (x + frameW > texW || y + frameH > texH)
            {
                reason = $"frame rect still out of bounds after clamp (index={index}, clamped={clamped})";
                return false;
            }

            reason = $"SLICER_V2 clamped index {index}->{clamped} for {texW}x{texH} frame {frameW}x{frameH}";
        }
        else
        {
            reason = $"SLICER_V2 ok {texW}x{texH} frame {frameW}x{frameH}";
        }

        rect = new Rectangle(x, y, frameW, frameH);
        return true;
    }

    private static int GreatestSquareDivisor(int a, int b)
    {
        int limit = System.Math.Min(a, b);

        // Include 1024/512 for HD packs
        int[] commonSizes = { 1024, 512, 256, 200, 160, 128, 120, 100, 96, 80, 72, 64, 48, 32 };
        foreach (int size in commonSizes)
        {
            if (size > limit) continue;
            if (a % size == 0 && b % size == 0)
                return size;
        }

        for (int n = limit; n >= 32; n--)
        {
            if (a % n == 0 && b % n == 0)
                return n;
        }
        return 0;
    }

    // Expose for benchmarking
    internal static int TestGreatestSquareDivisor(int a, int b) => GreatestSquareDivisor(a, b);
}