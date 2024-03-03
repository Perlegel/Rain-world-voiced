using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MoreSlugcats;
using RWCustom;
using UnityEngine;
using HUD;
using On;
using IL;
using System.Xml;

namespace RainWorldVoiced;

/// <summary>
/// Handles the actual playing of the voicelines along with the in-game dialogue
/// </summary>
public static class DialogueHandler
{
    //-- TODO: Could make this, along with other things, a Remix option
    private const int CollectionDelay = 40;

    /// <summary>
    /// Used for chat logs, linear broadcasts and dev commentary, is also used for everything in the collection
    /// </summary>
    private static string[] CurrentMessages;

    private static readonly Queue<SoundID> CollectionQueue = new();

    private static MenuMicrophone.MenuSoundObject CollectionCurrentlyPlayingSound;

    private static SoundEmitter CurrentVoicline; //-- The most recent voiceline that has been played in-game

    private static BodyChunk GhostBodyChunk; //-- An invisible player chunk that we map to the position of an echo so we can use ChunkSoundEmmiter.Room.PlaySound()

    private static int CollectionTimeSinceLastSound;

    public static void Init()
    {
        //-- Handles dialouge advancing faster than the sound effects finish
        On.HUD.DialogBox.Update += DialogBox_Update;

        //-- Handles regular dialogue, iterators and echoes
        On.HUD.DialogBox.InitNextMessage += DialogBox_InitNextMessage;

        //-- Handles tutorial text
        On.HUD.TextPrompt.InitNextMessage += TextPrompt_InitNextMessage;

        //-- Handles chat logs, linear broadcasts and dev commentary
        On.MoreSlugcats.ChatLogDisplay.InitNextMessage += ChatLogDisplay_InitNextMessage;

        //-- Handles collection
        On.MoreSlugcats.CollectionsMenu.InitLabelsFromChatlog += CollectionsMenu_InitLabelsFromChatlog;

        //-- Stores the english version of the current chat log so we can find the correct voicelines
        On.MoreSlugcats.ChatlogData.DecryptResult += ChatlogData_DecryptResult;

        //-- Stores the english version of the pearl when reading from the collection so we can find the correct voicelines
        On.MoreSlugcats.CollectionsMenu.InitLabelsFromPearlFile += CollectionsMenu_InitLabelsFromPearlFile;
        
        //-- The collection menu dumps all text at once instead of line by line, so we have to handle the playback ourselves
        On.MoreSlugcats.CollectionsMenu.Update += CollectionsMenu_Update;

        //-- Stop playing if we leave the collection menu
        On.MoreSlugcats.CollectionsMenu.OnExit += CollectionsMenu_OnExit;
        
        //-- Stores the currently playing voiceline in the collection so we know when to play the next one
        On.MenuMicrophone.MenuSoundObject.ctor += MenuSoundObject_ctor;

        On.VirtualMicrophone.PositionedSound.Update += PositionedSound_Update;

    }

    private static void TextPrompt_InitNextMessage(On.HUD.TextPrompt.orig_InitNextMessage orig, TextPrompt self)
    {
        orig(self);

        if (RWVRemixMenu.MuteTutorialText.Value) return;

        Debug.Log("[RWV]Attempting to play tutorial voiceline:");
        var sound = GetSoundID(self.messageString);

        self.hud.PlaySound(sound);
        Debug.Log("[RWV] Playing tutorial voiceline");
    }


    private static void PositionedSound_Update(On.VirtualMicrophone.PositionedSound.orig_Update orig, VirtualMicrophone.PositionedSound self, float timeStacker, float timeSpeed)
    {
        if (VoicelineHandler.IsOurs(self.soundData.soundID)){
            self.volume = RWVRemixMenu.VoiceVolume.Value;
            timeSpeed = 1; //-- Stops voicelines from being slowed down by echoes like other sounds
        }
        orig(self, timeStacker, timeSpeed);
    }

    private static void MenuSoundObject_ctor(On.MenuMicrophone.MenuSoundObject.orig_ctor orig, MenuMicrophone.MenuSoundObject self, MenuMicrophone mic, SoundLoader.SoundData soundData, bool loop, float initPan, float initVol, float initPitch, bool startAtRandomTime)
    {
        orig(self, mic, soundData,  loop, initPan, initVol, initPitch, startAtRandomTime);

        if (VoicelineHandler.IsOurs(soundData.soundID))
        {
            CollectionCurrentlyPlayingSound = self;
        }
    }

    private static void CollectionsMenu_OnExit(On.MoreSlugcats.CollectionsMenu.orig_OnExit orig, CollectionsMenu self)
    {
        orig(self);
        
        StopCollectionPlayback();
    }

    private static void CollectionsMenu_Update(On.MoreSlugcats.CollectionsMenu.orig_Update orig, CollectionsMenu self)
    {
        orig(self);

        if (CollectionCurrentlyPlayingSound == null || ((CollectionCurrentlyPlayingSound.slatedForDeletion || CollectionCurrentlyPlayingSound.Done) && CollectionTimeSinceLastSound > CollectionDelay))
        {
            if (CollectionQueue.Count > 0)
            {
                self.PlaySound(CollectionQueue.Dequeue());
                CollectionTimeSinceLastSound = 0;
            }
        }

        if (CollectionCurrentlyPlayingSound == null || CollectionCurrentlyPlayingSound.slatedForDeletion || !CollectionCurrentlyPlayingSound.audioSource.isPlaying)
        {
            CollectionTimeSinceLastSound++;
        }
        else
        {
            CollectionTimeSinceLastSound = 0;
        }
    }

    private static void CollectionsMenu_InitLabelsFromChatlog(On.MoreSlugcats.CollectionsMenu.orig_InitLabelsFromChatlog orig, CollectionsMenu self, string[] messages)
    {
        orig(self, messages);

        StopCollectionPlayback();

        foreach (var message in messages)
        {
            var sound = GetSoundID(message);

            //-- TODO: Logging
            if (sound == null) continue;
            
            CollectionQueue.Enqueue(sound);
        }
    }

    private static void CollectionsMenu_InitLabelsFromPearlFile(On.MoreSlugcats.CollectionsMenu.orig_InitLabelsFromPearlFile orig, MoreSlugcats.CollectionsMenu self, int id, SlugcatStats.Name saveFile)
    {
        var conversationLoader = new CollectionsMenu.ConversationLoader(self);

        var currentLanguage = self.rainWorld.options.language;
        try
        {
            self.rainWorld.options.language = InGameTranslator.LanguageID.English;
            conversationLoader.LoadEvents(id, saveFile);
        }
        finally
        {
            self.rainWorld.options.language = currentLanguage;
        }

        var messages = new List<string>();
        foreach (var e in conversationLoader.events)
        {
            if (e is Conversation.TextEvent textEvent)
            {
                messages.Add(textEvent.text);
                Debug.Log("[RWV] New collections text event:");
                Debug.Log("[RWV] " + textEvent.text);

            }
        }

        CurrentMessages = messages.Where(line => line != "").ToArray();
        
        orig(self, id, saveFile);
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

        if (CurrentMessages == null || CurrentMessages.Length <= self.showLine) return;

        Debug.Log("[RWV] Attempting to play transmission voiceline");
        var sound = GetSoundID(CurrentMessages[self.showLine]);
        if (sound == null) return;

        Debug.Log("[RWV] Playing sound " + sound);

        self.hud.PlaySound(sound);
    }

    private static void DialogBox_InitNextMessage(On.HUD.DialogBox.orig_InitNextMessage orig, HUD.DialogBox self)
    {
        orig(self);
        var sound = GetSoundID(self.CurrentMessage.text);
        if (sound == null) return;
        var played = false;
        //-- Can't grab the room directly from the HUD's owner because SplitScreenCoop exists
        if (Custom.rainWorld.processManager.currentMainLoop is RainWorldGame game)
        {

            //-- A bit ugly, but should make it compatible with SplitScreenCoop, might require some testing 
            foreach (var camera in game.cameras)
            {
                if (played) break;

                var room = camera.room;

                foreach (var obj in room.updateList)
                {
                    if (obj is Oracle oracle)
                    {
                        if (RWVRemixMenu.MuteIterators.Value) return;

                        Debug.Log("[RWV] Attempting to play Iterator dialouge");

                        room.PlaySound(sound, oracle.bodyChunks[0]);
                        CurrentVoicline = room.PlaySound(sound, oracle.bodyChunks[0]);
                        played = true;

                        Debug.Log("[RWV] Playing sound: " + sound);
                        break;
                    }

                    if (obj is Ghost ghost)
                    {
                        if (RWVRemixMenu.MuteEchoes.Value) return;

                        Debug.Log("[RWV] Attempting to play Echo dialouge");

                        //-- Creating an invisible body chunk to play a sound from because Echoes don't normaly use bodychunks
                        GhostBodyChunk = new BodyChunk(room.updateList.OfType<Player>().FirstOrDefault(), 0, ghost.pos, 0f, 0);
                        room.PlaySound(sound, GhostBodyChunk);
                        CurrentVoicline = room.PlaySound(sound, GhostBodyChunk);
                        played = true;

                        Debug.Log("[RWV] Playing sound: " + sound);
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
    
    private static SoundID GetSoundID(string text)
    {
        var originalText = Translator.Untranslate(text);

        //-- TODO: Log to a file so we can catch mistakes and missing voicelines
        if (string.IsNullOrEmpty(originalText) || !VoicelineHandler.TryGet(originalText, out var sound)) return null;

        return sound;
    }

    private static void StopCollectionPlayback()
    {
        if (Custom.rainWorld.processManager.currentMainLoop is not CollectionsMenu menu) return;

        CollectionQueue.Clear();
        
        foreach (var obj in menu.manager.menuMic.soundObjects)
        {
            if (VoicelineHandler.IsOurs(obj.soundData.soundID))
            {
                obj.Destroy();
            }
        }
    }
    private static void DialogBox_Update(On.HUD.DialogBox.orig_Update orig, DialogBox self)
    {
        //-- If the sound is still playing do not initiate the next line to prevent overlapping shenanigans
        if (self.CurrentMessage != null && self.showCharacter >= self.CurrentMessage.text.Length && CurrentVoicline != null && CurrentVoicline.soundStillPlaying)
        {
            self.lingerCounter = (self.CurrentMessage.linger - 1);
        }
        orig(self);
    }
}