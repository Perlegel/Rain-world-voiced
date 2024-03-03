using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using UnityEngine;

namespace RainWorldVoiced;

/// <summary>
/// Registers and holds references to the voicelines
/// </summary>
public static class VoicelineHandler
{
    private const string SOUND_PREFIX = "RWVoiced";
    private static readonly Dictionary<string, SoundID> Sounds = new();
        
    public static bool TryGet(string text, out SoundID sound) => Sounds.TryGetValue(text, out sound);

    public static bool IsOurs(SoundID sound) => Sounds.Values.Contains(sound);

    public static void Init()
    {
        //-- Allows for the registering of SoundIDs without adding to sounds.txt
        IL.SoundLoader.LoadSounds += SoundLoader_LoadSounds;
        
        LoadVoicelines();
    }

    public static void LoadVoicelines()
    {
        foreach (var kvp in Sounds)
        {
            kvp.Value.Unregister();
        }
        Sounds.Clear();
        
        var lines = File.ReadAllLines(AssetManager.ResolveFilePath("rwvoiced_voicelines.txt"));

        foreach (var line in lines)
        {
            //-- Comments and empty lines
            if (string.IsNullOrEmpty(line.Trim()) || line.StartsWith("//")) continue;
            
            var splitLine = line.Split('|');

            if (splitLine.Length != 2)
            {
                Debug.LogError($"Invalid voiceline entry! {line}");
                continue;
            }

            Sounds[splitLine[1]] = new SoundID(SOUND_PREFIX + splitLine[0], true);
        }
    }
    
    private static void SoundLoader_LoadSounds(ILContext il)
    {
        var cursor = new ILCursor(il);

        var loc = -1;
        cursor.GotoNext(MoveType.After,
            i => i.MatchLdstr("Sounds.txt"),
            i => i.MatchCallOrCallvirt<string>(nameof(string.Concat)),
            i => i.MatchCallOrCallvirt<AssetManager>(nameof(AssetManager.ResolveFilePath)),
            i => i.MatchCallOrCallvirt("System.IO.File", nameof(File.ReadAllLines)),
            i => i.MatchStloc(out loc));

        cursor.MoveAfterLabels();
        cursor.Emit(OpCodes.Ldloc, loc);

        cursor.EmitDelegate((string[] strings) =>
        {
            var index = strings.Length;
            Array.Resize(ref strings, strings.Length + Sounds.Count);
            
            foreach (var kvp in Sounds)
            {
                strings[index] = $"{kvp.Value.value}/dopplerFac=0 : {kvp.Value.value.Substring(SOUND_PREFIX.Length)}";
                index++;
            }

            return strings;
        });
        
        cursor.Emit(OpCodes.Stloc, loc);
    }
}