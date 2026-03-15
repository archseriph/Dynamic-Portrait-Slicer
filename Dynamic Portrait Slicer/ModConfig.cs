using System.Collections.Generic;
using StardewModdingAPI.Utilities;

namespace DynamicPortraitSlicer;

public enum PortraitSide
{
    Default = 0,
    Left = 1,
    Right = 2
}

public enum ScaleMode
{
    Absolute = 0,
    MultiplyOriginal = 1
}

public sealed class ModConfig
{
    public List<int> PreferredFrameSizes { get; set; } = new() { 1024, 512, 256, 200, 160, 128, 120, 100, 96, 80, 72, 64, 48, 32 };
    public bool VerboseLogging { get; set; } = false;

    public Dictionary<string, int> NpcFrameSizeOverride { get; set; } = new();
    public int ForceFrameSize { get; set; } = 0;
    public bool ConservativeClamp { get; set; } = true;
    public bool OnlyClampWhenXySet { get; set; } = true;

    public string TargetNpcKey { get; set; } = "default";
    public Dictionary<string, NpcPortraitOverride> NpcOverrides { get; set; } = new();

    public ScaleMode RuntimeScaleMode { get; set; } = ScaleMode.Absolute;

    // Live tuning sliders (still supported)
    public PortraitSide PortraitSide { get; set; } = PortraitSide.Default;
    public float PortraitScaleMultiplier { get; set; } = 1f;
    public int PortraitOffsetX { get; set; } = 0;
    public int PortraitOffsetY { get; set; } = 0;

    public bool AutoOffsetWhenSwitchingSides { get; set; } = false;
    public int AutoOffsetPadding { get; set; } = 0;

    public bool EnableRuntimeOverrides { get; set; } = true;

    // Option B: import + edit per-NPC overrides
    public bool AutoCreateNpcOverrideOnTalk { get; set; } = true;
    public bool AutoEnableNpcOverrideOnTalk { get; set; } = false;
    public bool LiveNudgeEditsNpcOverride { get; set; } = true;

    // Live nudge hotkeys for portraits in dialogue, only affect current speaker if AutoCreateNpcOverrideOnTalk is enabled and LiveNudgeEditsNpcOverride is enabled
    // Nudge by 10 pixels or 5% scale by default, can be changed in config
    public bool EnableLiveNudgeHotkeys { get; set; } = true;
    public bool PreferLiveTuningOverNpcOverrides { get; set; } = true;

    //Hotkeys for live nudging (hold + arrow keys to nudge, hold + +/- to scale)
    public KeybindList OffsetLeft { get; set; } = KeybindList.Parse("LeftShift + Left");
    public KeybindList OffsetRight { get; set; } = KeybindList.Parse("LeftShift + Right");
    public KeybindList OffsetUp { get; set; } = KeybindList.Parse("LeftShift + Up");
    public KeybindList OffsetDown { get; set; } = KeybindList.Parse("LeftShift + Down");
    public KeybindList ScaleUp { get; set; } = KeybindList.Parse("LeftShift + OemPlus");
    public KeybindList ScaleDown { get; set; } = KeybindList.Parse("LeftShift + OemMinus");

    // NEW: commit current speaker override
    public KeybindList CommitNpcOverride { get; set; } = KeybindList.Parse("LeftShift + Enter");

    public int NudgePixels { get; set; } = 10;
    public float NudgeScale { get; set; } = 0.05f;

    // Dialogue UI nudge (moves whole DDFC UI via GetDataVector patch)
    public bool EnableDialogueUiNudge { get; set; } = true;
    public int DialogueUiOffsetX { get; set; } = 0;
    public int DialogueUiOffsetY { get; set; } = 0;

    // Hotkeys: Ctrl+Shift+Arrows
    public KeybindList DialogueUiLeft { get; set; } = KeybindList.Parse("LeftControl + LeftShift + Left");
    public KeybindList DialogueUiRight { get; set; } = KeybindList.Parse("LeftControl + LeftShift + Right");
    public KeybindList DialogueUiUp { get; set; } = KeybindList.Parse("LeftControl + LeftShift + Up");
    public KeybindList DialogueUiDown { get; set; } = KeybindList.Parse("LeftControl + LeftShift + Down");

    // Step size for Ctrl+Shift nudge
    public int DialogueUiNudgePixels { get; set; } = 10;

    // Advanced: GMCM slider range (lets people go beyond +/-1000 on odd setups)
    public int DialogueUiOffsetMaxAbs { get; set; } = 1000;
}
// Per-NPC override options, can be set in config or imported from current talk dialogue    
public sealed class NpcPortraitOverride
{
    public bool Enabled { get; set; } = true;
    public PortraitSide Side { get; set; } = PortraitSide.Default;

    public int OffsetX { get; set; } = 0;
    public int OffsetY { get; set; } = 0;

    public float ScaleMultiplier { get; set; } = 1f;

    public int? FrameWidth { get; set; } = null;
    public int? FrameHeight { get; set; } = null;

    public int? Columns { get; set; } = null;
    public int? Rows { get; set; } = null;

    public bool? ForceSingleImage { get; set; } = null;
}