using System;
using System.Security;
using System.Security.Permissions;
using BepInEx;
using UnityEngine;

[module: UnverifiableCode]
#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete

namespace RainWorldVoiced;

[BepInPlugin(MOD_ID, "Rain World Voiced", "1.0.0")]
public class Plugin : BaseUnityPlugin
{
    public const string MOD_ID = "daszombes.rainworldvoiced";

    public bool IsInit;

    private void OnEnable()
    {
        On.RainWorld.OnModsInit += RainWorld_OnModsInit;
    }

    private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);

        try
        {
            if (IsInit) return;
            IsInit = true;

            DialogueHandler.Init();
            Translator.Init();
            VoicelineHandler.Init();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }
}