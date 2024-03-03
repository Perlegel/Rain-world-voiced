using Menu.Remix.MixedUI;
using UnityEngine;


namespace RainWorldVoiced
{
    public class RWVRemixMenu : OptionInterface
    {
        public static Configurable<bool> MuteIterators;
        public static Configurable<bool> MuteEchoes;
        public static Configurable<bool> MuteTransmissions;
        public static Configurable<bool> MuteTutorialText;
        public static Configurable<float> VoiceVolume;

        public RWVRemixMenu()
        {
            MuteIterators = config.Bind("RWV_Mute_Iterators", defaultValue: false);
            MuteEchoes = config.Bind("RWV_Mute_Echoes", defaultValue: false);
            MuteTransmissions = config.Bind("RWV_Mute_Transmissions", defaultValue: false);
            MuteTutorialText = config.Bind("RWV_Mute_TutorialText", defaultValue: false);
            VoiceVolume = config.Bind("RWV_Voiceline_Volume", 1f);
        }

        public override void Initialize()
        {
            base.Initialize();
            OpTab opTab = new OpTab(this, "Config");
            Tabs = new OpTab[1] { opTab };

            UIelement[] elements = new UIelement[11]
            {
                new OpLabel(20, 560f, "~CONFIG~", bigText: true),

                new OpLabel(25f, 510f, "Voiceline Volume"),
                new OpFloatSlider(VoiceVolume, new Vector2(50f, 300f), 200, 2, vertical: true),

                new OpLabel(175f, 510f, "Mute Iterators"),
                new OpCheckBox(MuteIterators, 175f, 480f),
                new OpLabel(175f, 440f, "Mute Echoes"),
                new OpCheckBox(MuteEchoes, 175f, 410f),
                new OpLabel(175f, 370f, "Mute Transmissions"),
                new OpCheckBox(MuteTransmissions, 175f, 340f),
                new OpLabel(175f, 300f, "Mute Tutorial Voice"),
                new OpCheckBox(MuteTutorialText, 175f, 270f),
            };
            opTab.AddItems(elements);
        }
    }
}