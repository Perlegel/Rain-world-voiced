using IL.Menu;
using JetBrains.Annotations;
using System.Collections.Generic;
using System.Drawing.Text;

namespace RainWorldVoiced;

/// <summary>
/// Holds a cache of the translated lines so we can untranslate them later in order to find the voiceline from the english version
/// </summary>
public static class Translator
{
    private static readonly Dictionary<string, string> ReverseTranslations = new();
    
    public static void Init()
    {
        On.InGameTranslator.Translate += InGameTranslator_Translate;
        On.InGameTranslator.TryTranslate += InGameTranslator_TryTranslate;
    }

    //-- TODO: Handle <PLAYERNAME>, <CAPPLAYERNAME>, <PlayerName>, <CapPlayerName>
    public static string Untranslate(string text) => ReverseTranslations.TryGetValue(text.Replace("\r\n", "<LINE>"), out var result) ? result : text;
    private static void StoreTranslation(string from, string to) => ReverseTranslations[to] = from;

    private static string InGameTranslator_Translate(On.InGameTranslator.orig_Translate orig, InGameTranslator self, string s)
    {
        var result = orig(self, s);

        if (!string.IsNullOrEmpty(result) && !"DOUBLE TRANSLATION".Equals(result) && !"!NO TRANSLATION!".Equals(result))
        {
            StoreTranslation(s, result);
        }

        return result;
    }
    
    private static bool InGameTranslator_TryTranslate(On.InGameTranslator.orig_TryTranslate orig, InGameTranslator self, string text, out string res)
    {
        var result = orig(self, text, out res);

        if (result)
        {
            StoreTranslation(text, res);
        }

        return result;
    }
}