using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RWCustom;

namespace RainWorldVoiced;

/// <summary>
/// Handles the actual playing of the voicelines along with the in-game dialogue
/// </summary>
public static class DialogHandler
{
    /// <summary>
    /// Used for chat logs, linear broadcasts and dev commentary
    /// </summary>
    private static string[] CurrentMessages;
    
    public static void Init()
    {
        //-- Handles regular dialogue, iterators and echoes
        On.HUD.DialogBox.InitNextMessage += DialogBox_InitNextMessage;
        
        //-- Handles chat logs, linear broadcasts and dev commentary
        On.MoreSlugcats.ChatLogDisplay.InitNextMessage += ChatLogDisplay_InitNextMessage;
        
        //-- Stores the english version of the current chat log so we can find the correct voicelines
        On.MoreSlugcats.ChatlogData.DecryptResult += ChatlogData_DecryptResult;
        
        //-- TODO: Handle collection
    }

    private static string ChatlogData_DecryptResult(On.MoreSlugcats.ChatlogData.orig_DecryptResult orig, string result, string path)
    {
        //-- TODO: Might be worth putting this whole thing in a try/catch so the game doesn't blow up if something breaks while finding the original text
        //-- Grab the original path instead of the translation
        var originalPath = Regex.Replace(path, @"text_[a-z]{3}(?=(\\|/)[a-zA-Z0-9_-]*\.txt)", "text_eng");

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalPath);
        var num = fileNameWithoutExtension.Sum(character => character - 48);

        var strings =  Custom.xorEncrypt(File.ReadAllText(originalPath, Encoding.Default), 54 + num + InGameTranslator.LanguageID.English.index * 7).Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        CurrentMessages = new string[strings.Length - 1];
        for (var i = 1; i < strings.Length; i++)
        {
            CurrentMessages[i - 1] = strings[i];
        }

        CurrentMessages = CurrentMessages.Where(line => line != "").ToArray();
        
        return orig(result, path);
    }

    private static void ChatLogDisplay_InitNextMessage(On.MoreSlugcats.ChatLogDisplay.orig_InitNextMessage orig, MoreSlugcats.ChatLogDisplay self)
    {
        orig(self);

        //-- TODO: Logging
        if (CurrentMessages == null || CurrentMessages.Length <= self.showLine) return;
        
        var sound = GetSoundID(CurrentMessages[self.showLine], true);
        if (sound == null) return;
        
        self.hud.PlaySound(sound);
    }

    private static void DialogBox_InitNextMessage(On.HUD.DialogBox.orig_InitNextMessage orig, HUD.DialogBox self)
    {
        orig(self);

        var sound = GetSoundID(self.CurrentMessage.text);
        if (sound == null) return;

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
    
    private static SoundID GetSoundID(string text, bool defaultToSame = false)
    {
        var originalText = Translator.Untranslate(text, defaultToSame);

        //-- TODO: Log to a file so we can catch mistakes and missing voicelines
        if (string.IsNullOrEmpty(originalText) || !VoicelineHandler.TryGet(originalText, out var sound)) return null;

        return sound;
    }
}