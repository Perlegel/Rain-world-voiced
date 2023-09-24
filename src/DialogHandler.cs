using RWCustom;

namespace RainWorldVoiced;

/// <summary>
/// Handles the actual playing of the voicelines along with the in-game dialogue
/// </summary>
public static class DialogHandler
{
    public static void Init()
    {
        On.HUD.DialogBox.InitNextMessage += DialogBox_InitNextMessage;
        
        //-- TODO: Handle transmissions, DevTools and Collection
    }

    private static void DialogBox_InitNextMessage(On.HUD.DialogBox.orig_InitNextMessage orig, HUD.DialogBox self)
    {
        orig(self);

        var originalText = Translator.Untranslate(self.CurrentMessage.text);

        //-- TODO: Log to a file so we can catch mistakes and missing voicelines
        if (string.IsNullOrEmpty(originalText) || !VoicelineHandler.TryGet(originalText, out var sound)) return;

        //-- Can't grab the room directly from the HUD's owner because SplitScreenCoop exists
        if (Custom.rainWorld.processManager.currentMainLoop is RainWorldGame game)
        {
            var played = false;

            //-- A bit ugly, but should make it compatible with SplitScreenCoop, might require some testing 
            foreach (var camera in game.cameras)
            {
                if (played) break;

                var room = camera.room;

                foreach (var obj in room.updateList)
                {
                    if (obj is Oracle oracle)
                    {
                        room.PlaySound(sound, oracle.bodyChunks[0]);
                        played = true;
                        break;
                    }

                    if (obj is Ghost ghost)
                    {
                        room.PlaySound(sound, ghost.pos);
                        played = true;
                        break;
                    }
                }

                //-- Probably won't happen, but should work as a fallback in case something weird happens and the source of the voice can't be found
                if (!played)
                {
                    self.hud.PlaySound(sound);
                }
            }
        }
    }
}