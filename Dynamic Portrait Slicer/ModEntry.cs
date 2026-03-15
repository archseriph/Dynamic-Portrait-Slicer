using System;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace DynamicPortraitSlicer;

public sealed class ModEntry : Mod
{
    private Harmony _harmony = null!;
    internal static IMonitor Log = null!;
    internal static ModConfig Config = null!;
    private IModHelper _helper = null!;

    // Write config after dialogue closes if we changed values during a dialogue
    private bool _pendingWriteConfig;

    // Track dialogue box open/close so we can apply saved offsets once per box instance.
    private DialogueBox? _lastDialogueBox;

    public override void Entry(IModHelper helper)
    {
        _helper = helper;

        Log = this.Monitor;
        Config = helper.ReadConfig<ModConfig>();

        _harmony = new Harmony(this.ModManifest.UniqueID);

        // Prove which DLL SMAPI loaded (helps diagnose stale builds / duplicate mod folders).
        Log.Log(
            $"Dynamic Portrait Slicer loaded. BuildStamp=INJECTOR_NO_ASDICTIONARY_2026-03-14 Assembly={typeof(ModEntry).Assembly.Location}",
            LogLevel.Info
        );

        LogInjectorTypeScan();

        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;

        helper.Events.GameLoop.GameLaunched += (_, _) =>
        {
            try
            {
                RegisterGmcm();
                PatchDdfc();
            }
            catch (Exception ex)
            {
                Log.Log($"Failed during GameLaunched init.\n{ex}", LogLevel.Error);
            }
        };
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        // Detect dialogue box open/close and apply saved dialogue box offset once per box instance.
        if (Game1.activeClickableMenu is DialogueBox currentBox)
        {
            if (!ReferenceEquals(currentBox, _lastDialogueBox))
            {
                _lastDialogueBox = currentBox;
                ApplySavedDialogueBoxOffset(currentBox);

                if (ModEntry.Config.VerboseLogging)
                    Monitor.Log(
                        $"DialogueBox opened: applied saved offset ({ModEntry.Config.DialogueUiOffsetX},{ModEntry.Config.DialogueUiOffsetY}).",
                        LogLevel.Info
                    );
            }
        }
        else
        {
            // Dialogue closed: if we had pending changes, write now.
            if (_lastDialogueBox is not null)
                _lastDialogueBox = null;

            if (_pendingWriteConfig)
            {
                _pendingWriteConfig = false;
                _helper.WriteConfig(ModEntry.Config);

                if (ModEntry.Config.VerboseLogging)
                    Monitor.Log("Wrote config after dialogue closed.", LogLevel.Info);
            }
        }
    }

    private void ApplySavedDialogueBoxOffset(DialogueBox box)
    {
        if (!ModEntry.Config.EnableDialogueUiNudge)
            return;

        int dx = ModEntry.Config.DialogueUiOffsetX;
        int dy = ModEntry.Config.DialogueUiOffsetY;

        if (dx == 0 && dy == 0)
            return;

        try
        {
            box.x += dx;
            box.y += dy;
        }
        catch
        {
            // Do not break UI if something changes across SDV versions.
        }
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        if (!ModEntry.Config.EnableLiveNudgeHotkeys)
            return;

        if (Game1.activeClickableMenu is not DialogueBox box)
            return;

        // Identify speaker for per-NPC override editing
        var npc = ReflectionHelpers.TryGetSpeakerNpc(box);
        string? npcName = npc?.Name;

        NpcPortraitOverride? targetNpcOverride = null;
        if (ModEntry.Config.LiveNudgeEditsNpcOverride && !string.IsNullOrWhiteSpace(npcName))
        {
            if (ModEntry.Config.AutoCreateNpcOverrideOnTalk)
                targetNpcOverride = GetOrCreateNpcOverride(npcName!);
            else if (ModEntry.Config.NpcOverrides.TryGetValue(npcName!, out var existing) && existing is not null)
                targetNpcOverride = existing;
        }

        bool editNpc = targetNpcOverride is not null;

        // --- Dialogue BOX nudge hotkeys (Ctrl+Shift+Arrows) ---
        // This moves the actual menu (background + everything), and saves the offset in config.
        if (ModEntry.Config.EnableDialogueUiNudge)
        {
            int dx = 0;
            int dy = 0;

            if (ModEntry.Config.DialogueUiLeft.JustPressed()) dx = -ModEntry.Config.DialogueUiNudgePixels;
            else if (ModEntry.Config.DialogueUiRight.JustPressed()) dx = +ModEntry.Config.DialogueUiNudgePixels;
            else if (ModEntry.Config.DialogueUiUp.JustPressed()) dy = -ModEntry.Config.DialogueUiNudgePixels;
            else if (ModEntry.Config.DialogueUiDown.JustPressed()) dy = +ModEntry.Config.DialogueUiNudgePixels;

            if (dx != 0 || dy != 0)
            {
                ModEntry.Config.DialogueUiOffsetX += dx;
                ModEntry.Config.DialogueUiOffsetY += dy;

                // Apply immediately to the currently-open box
                try
                {
                    box.x += dx;
                    box.y += dy;
                }
                catch { }

                _helper.Input.Suppress(e.Button);
                _pendingWriteConfig = true;

                if (ModEntry.Config.VerboseLogging)
                    Monitor.Log(
                        $"Dialogue BOX nudge: dx={dx} dy={dy} total=({ModEntry.Config.DialogueUiOffsetX},{ModEntry.Config.DialogueUiOffsetY})",
                        LogLevel.Info
                    );

                return; // don't also treat this input as a portrait nudge
            }
        }

        // Commit hotkey: enable the current NPC override without changing values
        if (editNpc && ModEntry.Config.CommitNpcOverride.JustPressed())
        {
            targetNpcOverride!.Enabled = true;
            _pendingWriteConfig = true;

            _helper.Input.Suppress(e.Button);

            if (ModEntry.Config.VerboseLogging)
                Monitor.Log($"Committed NPC override: {npcName} Enabled=true", LogLevel.Info);

            return;
        }

        // --- Portrait live nudge hotkeys (Shift+Arrows / Shift+PlusMinus) ---
        int n = ModEntry.Config.NudgePixels;
        float ds = ModEntry.Config.NudgeScale;

        int getX() => editNpc ? targetNpcOverride!.OffsetX : ModEntry.Config.PortraitOffsetX;
        int getY() => editNpc ? targetNpcOverride!.OffsetY : ModEntry.Config.PortraitOffsetY;
        float getS() => editNpc ? targetNpcOverride!.ScaleMultiplier : ModEntry.Config.PortraitScaleMultiplier;

        void setX(int v) { if (editNpc) targetNpcOverride!.OffsetX = v; else ModEntry.Config.PortraitOffsetX = v; }
        void setY(int v) { if (editNpc) targetNpcOverride!.OffsetY = v; else ModEntry.Config.PortraitOffsetY = v; }
        void setS(float v) { if (editNpc) targetNpcOverride!.ScaleMultiplier = v; else ModEntry.Config.PortraitScaleMultiplier = v; }

        bool changed = false;

        if (ModEntry.Config.OffsetLeft.JustPressed())
        {
            setX(getX() - n);
            changed = true;
        }
        else if (ModEntry.Config.OffsetRight.JustPressed())
        {
            setX(getX() + n);
            changed = true;
        }
        else if (ModEntry.Config.OffsetUp.JustPressed())
        {
            setY(getY() - n);
            changed = true;
        }
        else if (ModEntry.Config.OffsetDown.JustPressed())
        {
            setY(getY() + n);
            changed = true;
        }
        else if (ModEntry.Config.ScaleUp.JustPressed())
        {
            setS(getS() + ds);
            changed = true;
        }
        else if (ModEntry.Config.ScaleDown.JustPressed())
        {
            setS(Math.Max(0.05f, getS() - ds));
            changed = true;
        }

        if (changed)
        {
            _helper.Input.Suppress(e.Button);
            _pendingWriteConfig = true;

            // Safer than auto-enable-on-talk: only enable when user explicitly nudges or commits.
            if (editNpc)
                targetNpcOverride!.Enabled = true;

            if (ModEntry.Config.VerboseLogging)
            {
                string target = editNpc ? $"NpcOverrides['{npcName}']" : "global sliders";
                Monitor.Log($"Live nudge ({target}): OffsetX={getX()} OffsetY={getY()} Scale={getS()}", LogLevel.Info);
            }
        }
    }

    private void LogInjectorTypeScan()
    {
        try
        {
            var asm = typeof(ModEntry).Assembly;
            int hits = 0;

            foreach (var t in asm.GetTypes())
            {
                foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static))
                {
                    if (m.Name.Contains("AsDictionary", StringComparison.OrdinalIgnoreCase))
                    {
                        hits++;
                        Log.Log($"Self-check hit: method name contains AsDictionary: {t.FullName}.{m.Name}", LogLevel.Warn);
                    }
                }
            }

            if (hits == 0)
                Log.Log("Self-check: no methods in this assembly have 'AsDictionary' in their name.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Log.Log($"Self-check failed: {ex}", LogLevel.Warn);
        }
    }

    private void RegisterGmcm()
    {
        object? api = _helper.ModRegistry.GetApi("spacechase0.GenericModConfigMenu");
        if (api == null)
            throw new InvalidOperationException("GMCM is required but API could not be retrieved.");

        var apiType = api.GetType();

        // Register(IManifest mod, Action reset, Action save, bool titleScreenOnly)
        GmcmReflection.GetMethodByParamCount(this.Monitor, apiType, "Register", 4)
            .Invoke(api, new object[]
            {
                this.ModManifest,
                (Action)(() => ModEntry.Config = new ModConfig()),
                (Action)(() => _helper.WriteConfig(ModEntry.Config)),
                false
            });

        // --- Dialogue box nudge (moves the actual box) ---
        AddBool(api, apiType,
            get: () => ModEntry.Config.EnableDialogueUiNudge,
            set: v => ModEntry.Config.EnableDialogueUiNudge = v,
            name: () => "Enable dialogue box nudge",
            tooltip: () => "If enabled, Ctrl+Shift+Arrow nudges move the entire dialogue box (background + all DDFC elements).",
            fieldId: "EnableDialogueUiNudge"
        );

        AddNumberInt(api, apiType,
            get: () => ModEntry.Config.DialogueUiOffsetMaxAbs,
            set: v => ModEntry.Config.DialogueUiOffsetMaxAbs = Math.Max(0, v),
            name: () => "Advanced: dialogue box offset max (abs)",
            tooltip: () => "Sets the +/- range for the dialogue box offset sliders. Reopen GMCM to refresh slider bounds.",
            fieldId: "DialogueUiOffsetMaxAbs",
            min: 0,
            max: 5000,
            interval: 50
        );

        int uiMax = Math.Max(0, ModEntry.Config.DialogueUiOffsetMaxAbs);

        AddNumberInt(api, apiType,
            get: () => ModEntry.Config.DialogueUiOffsetX,
            set: v => ModEntry.Config.DialogueUiOffsetX = v,
            name: () => "Dialogue box offset X",
            tooltip: () => "Shifts the whole dialogue box left/right (pixels).",
            fieldId: "DialogueUiOffsetX",
            min: -uiMax,
            max: uiMax,
            interval: 1
        );

        AddNumberInt(api, apiType,
            get: () => ModEntry.Config.DialogueUiOffsetY,
            set: v => ModEntry.Config.DialogueUiOffsetY = v,
            name: () => "Dialogue box offset Y",
            tooltip: () => "Shifts the whole dialogue box up/down (pixels).",
            fieldId: "DialogueUiOffsetY",
            min: -uiMax,
            max: uiMax,
            interval: 1
        );

        // --- Live tuning / hotkeys ---
        AddBool(api, apiType,
            get: () => ModEntry.Config.EnableLiveNudgeHotkeys,
            set: v => ModEntry.Config.EnableLiveNudgeHotkeys = v,
            name: () => "Enable live nudge hotkeys",
            tooltip: () => "Enable hotkeys to adjust portrait offsets/scale while a dialogue box is open.",
            fieldId: "EnableLiveNudgeHotkeys"
        );

        AddBool(api, apiType,
            get: () => ModEntry.Config.EnableRuntimeOverrides,
            set: v => ModEntry.Config.EnableRuntimeOverrides = v,
            name: () => "Enable runtime overrides",
            tooltip: () => "Applies portrait offsets/scale at runtime while portraits are drawn.",
            fieldId: "EnableRuntimeOverrides"
        );

        AddBool(api, apiType,
            get: () => ModEntry.Config.PreferLiveTuningOverNpcOverrides,
            set: v => ModEntry.Config.PreferLiveTuningOverNpcOverrides = v,
            name: () => "Prefer live tuning over NPC overrides",
            tooltip: () => "If enabled, live tuning (hotkeys/GMCM sliders) overrides any per-NPC override while speaking (useful for tuning).",
            fieldId: "PreferLiveTuningOverNpcOverrides"
        );

        AddBool(api, apiType,
            get: () => ModEntry.Config.LiveNudgeEditsNpcOverride,
            set: v => ModEntry.Config.LiveNudgeEditsNpcOverride = v,
            name: () => "Live nudges edit per-NPC override",
            tooltip: () => "If enabled, hotkey nudges write into NpcOverrides for the current speaker.",
            fieldId: "LiveNudgeEditsNpcOverride"
        );

        AddBool(api, apiType,
            get: () => ModEntry.Config.AutoCreateNpcOverrideOnTalk,
            set: v => ModEntry.Config.AutoCreateNpcOverrideOnTalk = v,
            name: () => "Auto-create NPC override on talk",
            tooltip: () => "When talking to an NPC, create a NpcOverrides entry if missing (starts disabled unless you nudge or commit).",
            fieldId: "AutoCreateNpcOverrideOnTalk"
        );

        AddBool(api, apiType,
            get: () => ModEntry.Config.AutoEnableNpcOverrideOnTalk,
            set: v => ModEntry.Config.AutoEnableNpcOverrideOnTalk = v,
            name: () => "Auto-enable new NPC overrides (advanced)",
            tooltip: () => "If enabled, newly-created NPC overrides start Enabled=true. Not recommended unless you know what you're doing.",
            fieldId: "AutoEnableNpcOverrideOnTalk"
        );

        // --- Legacy single-target editing (kept for back-compat) ---
        AddText(api, apiType,
            get: () => ModEntry.Config.TargetNpcKey,
            set: v => ModEntry.Config.TargetNpcKey = string.IsNullOrWhiteSpace(v) ? "default" : v.Trim(),
            name: () => "Legacy: Target NPC key",
            tooltip: () => "Legacy field kept for older workflows. Prefer per-NPC overrides + live nudging.",
            fieldId: "TargetNpcKey",
            allowedValues: new[] { "default", "Abigail", "Gus" }
        );

        // --- Slicer ---
        AddBool(api, apiType,
            get: () => ModEntry.Config.OnlyClampWhenXySet,
            set: v => ModEntry.Config.OnlyClampWhenXySet = v,
            name: () => "Only clamp when DDFC sets X/Y",
            tooltip: () => "Recommended ON. Prevents interfering with DDFC 'auto' portrait mode (X/Y = -1).",
            fieldId: "OnlyClampWhenXySet"
        );

        AddNumberInt(api, apiType,
            get: () => ModEntry.Config.ForceFrameSize,
            set: v => ModEntry.Config.ForceFrameSize = v,
            name: () => "Force frame size",
            tooltip: () => "0 = auto. Use 64 or 128 if a portrait pack is cropped incorrectly.",
            fieldId: "ForceFrameSize",
            min: 0,
            max: 256,
            interval: 64
        );

        // --- Live tuning sliders (global) ---
        AddText(api, apiType,
            get: () => ModEntry.Config.PortraitSide.ToString(),
            set: value =>
            {
                if (Enum.TryParse(value, out PortraitSide parsed))
                    ModEntry.Config.PortraitSide = parsed;
            },
            name: () => "Live: Portrait side",
            tooltip: () => "Live tuning value used when runtime overrides are enabled (and not editing per-NPC overrides).",
            fieldId: "PortraitSide",
            allowedValues: new[]
            {
                PortraitSide.Default.ToString(),
                PortraitSide.Left.ToString(),
                PortraitSide.Right.ToString()
            }
        );

        AddNumberFloat(api, apiType,
            get: () => ModEntry.Config.PortraitScaleMultiplier,
            set: v => ModEntry.Config.PortraitScaleMultiplier = v,
            name: () => "Live: Portrait scale",
            tooltip: () => "Live tuning value for portrait scale.",
            fieldId: "PortraitScaleMultiplier",
            min: 0.1f,
            max: 3.0f,
            interval: 0.05f
        );

        AddNumberInt(api, apiType,
            get: () => ModEntry.Config.PortraitOffsetX,
            set: v => ModEntry.Config.PortraitOffsetX = v,
            name: () => "Live: Portrait offset X",
            tooltip: () => "Live tuning value for portrait X offset.",
            fieldId: "PortraitOffsetX",
            min: -2000,
            max: 2000,
            interval: 1
        );

        AddNumberInt(api, apiType,
            get: () => ModEntry.Config.PortraitOffsetY,
            set: v => ModEntry.Config.PortraitOffsetY = v,
            name: () => "Live: Portrait offset Y",
            tooltip: () => "Live tuning value for portrait Y offset.",
            fieldId: "PortraitOffsetY",
            min: -2000,
            max: 2000,
            interval: 1
        );

        // --- Debug ---
        AddBool(api, apiType,
            get: () => ModEntry.Config.VerboseLogging,
            set: v => ModEntry.Config.VerboseLogging = v,
            name: () => "Verbose logging",
            tooltip: () => "Logs extra details for troubleshooting.",
            fieldId: "VerboseLogging"
        );
    }

    private void AddBool(
        object api,
        Type apiType,
        Func<bool> get,
        Action<bool> set,
        Func<string> name,
        Func<string> tooltip,
        string fieldId
    )
    {
        GmcmReflection.GetMethodByParamCount(this.Monitor, apiType, "AddBoolOption", 6)
            .Invoke(api, new object[] { this.ModManifest, get, set, name, tooltip, fieldId });
    }

    private void AddNumberInt(
        object api,
        Type apiType,
        Func<int> get,
        Action<int> set,
        Func<string> name,
        Func<string> tooltip,
        string fieldId,
        int min,
        int max,
        int interval
    )
    {
        var m = GmcmReflection.GetMethodByParamTypes(
            this.Monitor,
            apiType,
            "AddNumberOption",
            typeof(IManifest),
            typeof(Func<int>),
            typeof(Action<int>),
            typeof(Func<string>),
            typeof(Func<string>),
            typeof(int?),
            typeof(int?),
            typeof(int?),
            typeof(string)
        );

        m.Invoke(api, new object[]
        {
            this.ModManifest,
            get,
            set,
            name,
            tooltip,
            (int?)min,
            (int?)max,
            (int?)interval,
            fieldId
        });
    }

    private void AddNumberFloat(
        object api,
        Type apiType,
        Func<float> get,
        Action<float> set,
        Func<string> name,
        Func<string> tooltip,
        string fieldId,
        float min,
        float max,
        float interval
    )
    {
        var m = GmcmReflection.GetMethodByParamTypes(
            this.Monitor,
            apiType,
            "AddNumberOption",
            typeof(IManifest),
            typeof(Func<float>),
            typeof(Action<float>),
            typeof(Func<string>),
            typeof(Func<string>),
            typeof(float?),
            typeof(float?),
            typeof(float?),
            typeof(string)
        );

        m.Invoke(api, new object[]
        {
            this.ModManifest,
            get,
            set,
            name,
            tooltip,
            (float?)min,
            (float?)max,
            (float?)interval,
            fieldId
        });
    }

    private void AddText(
        object api,
        Type apiType,
        Func<string> get,
        Action<string> set,
        Func<string> name,
        Func<string> tooltip,
        string fieldId,
        string[] allowedValues
    )
    {
        try
        {
            var m = GmcmReflection.GetMethodByParamTypes(
                this.Monitor,
                apiType,
                "AddTextOption",
                typeof(IManifest),
                typeof(Func<string>),
                typeof(Action<string>),
                typeof(Func<string>),
                typeof(Func<string>),
                typeof(string[]),
                typeof(string)
            );

            m.Invoke(api, new object[] { this.ModManifest, get, set, name, tooltip, allowedValues, fieldId });
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"GMCM: couldn't register a dropdown (AddTextOption overload mismatch).\n{ex}", LogLevel.Warn);
        }
    }

    private NpcPortraitOverride GetOrCreateNpcOverride(string npcName)
    {
        npcName = npcName.Trim();
        if (npcName.Length == 0)
            throw new ArgumentException("npcName is blank", nameof(npcName));

        if (!ModEntry.Config.NpcOverrides.TryGetValue(npcName, out var ov) || ov is null)
        {
            ov = new NpcPortraitOverride
            {
                Enabled = ModEntry.Config.AutoEnableNpcOverrideOnTalk,
                Side = PortraitSide.Default,
                OffsetX = 0,
                OffsetY = 0,
                ScaleMultiplier = 1f
            };

            ModEntry.Config.NpcOverrides[npcName] = ov;
            _pendingWriteConfig = true;

            if (ModEntry.Config.VerboseLogging)
                Monitor.Log($"Auto-created NpcOverrides['{npcName}'] (Enabled={ov.Enabled}).", LogLevel.Info);
        }

        return ov;
    }

    private void PatchDdfc()
    {
        var rendererType = AccessTools.TypeByName("DialogueDisplayFramework.Framework.DialogueBoxRenderer");
        if (rendererType is null)
        {
            Log.Log("DDFC patch: couldn't find DialogueDisplayFramework.Framework.DialogueBoxRenderer. Is DDFC installed/loaded?", LogLevel.Warn);
            return;
        }

        var drawPortrait = AccessTools.FirstMethod(rendererType, m =>
        {
            if (m.Name != "DrawPortrait")
                return false;

            var p = m.GetParameters();
            return p.Length == 3
                   && p[0].ParameterType == typeof(SpriteBatch)
                   && p[1].ParameterType.FullName == "StardewValley.Menus.DialogueBox";
        });

        if (drawPortrait is null)
        {
            Log.Log("DDFC patch: couldn't find DialogueBoxRenderer.DrawPortrait(SpriteBatch, DialogueBox, ...).", LogLevel.Warn);
            return;
        }

        _harmony.Patch(
            original: drawPortrait,
            prefix: new HarmonyMethod(typeof(DdfcPortraitClampPatch), nameof(DdfcPortraitClampPatch.Prefix))
        );

        // NOTE:
        // We intentionally do NOT patch DDFC GetDataVector anymore.
        // Dialogue box nudge should move the actual DialogueBox (box.x/box.y), not just its contents.

        Log.Log(
            $"Patched DDFC DialogueBoxRenderer.DrawPortrait (static={drawPortrait.IsStatic}) for portrait clamp.",
            LogLevel.Info
        );
    }
}
