using Microsoft.Xna.Framework;

namespace DynamicPortraitSlicer;

internal static class DdfcDialogueUiOffsetPatch
{
    // Postfix for DialogueDisplayFramework.Framework.DialogueBoxRenderer.GetDataVector(DialogueBox, BaseData)
    public static void Postfix(ref Vector2 __result)
    {
        if (!ModEntry.Config.EnableDialogueUiNudge)
            return;

        __result.X += ModEntry.Config.DialogueUiOffsetX;
        __result.Y += ModEntry.Config.DialogueUiOffsetY;
    }
}
