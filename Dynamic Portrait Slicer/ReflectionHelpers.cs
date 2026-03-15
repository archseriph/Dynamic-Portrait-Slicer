using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace DynamicPortraitSlicer;

internal static class ReflectionHelpers
{
    public static NPC? TryGetSpeakerNpc(DialogueBox box)
    {
        object? dialogue =
            AccessTools.Field(box.GetType(), "characterDialogue")?.GetValue(box) ??
            AccessTools.Field(box.GetType(), "dialogue")?.GetValue(box);

        if (dialogue is null)
            return null;

        NPC? speaker =
            AccessTools.Field(dialogue.GetType(), "speaker")?.GetValue(dialogue) as NPC ??
            AccessTools.Property(dialogue.GetType(), "speaker")?.GetValue(dialogue) as NPC;

        return speaker;
    }

    public static object? TryGetDialogueObject(DialogueBox box)
    {
        return
            AccessTools.Field(box.GetType(), "characterDialogue")?.GetValue(box) ??
            AccessTools.Field(box.GetType(), "dialogue")?.GetValue(box);
    }

    public static int TryGetPortraitIndex(DialogueBox box, NPC _)
    {
        // current guess (probably wrong on 1.6.15)
        object? val =
            AccessTools.Field(box.GetType(), "portraitIndex")?.GetValue(box) ??
            AccessTools.Field(box.GetType(), "currentPortraitIndex")?.GetValue(box) ??
            AccessTools.Property(box.GetType(), "portraitIndex")?.GetValue(box);

        return val is int i ? i : 0;
    }
}