using RhythmDojo.Audio;
using UnityEditor;
using UnityEngine;

namespace RhythmDojo.EditorTools
{
    public static class DojoSoundBuilder
    {
        public const string BankPath = "Assets/Game/Settings/DojoSoundBank.asset";

        public static DojoSoundPlayer Build(Transform parent, AudioSource first = null, AudioSource motion = null)
        {
            // Keep the object inactive until every required serialized reference has been assigned.
            var root = new GameObject("Recorded Dojo Audio");
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            var player = root.AddComponent<DojoSoundPlayer>();
            player.bank = Prepare();
            player.voices = new AudioSource[6];
            for (int i = 0; i < player.voices.Length; i++)
            {
                AudioSource source = null;
                if (i == 0)
                {
                    source = first;
                }
                if (i == 3)
                {
                    source = motion;
                }
                if (!source)
                {
                    source = new GameObject("Dojo Voice " + i).AddComponent<AudioSource>();
                    source.transform.SetParent(root.transform, false);
                }
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0;
                source.priority = 100;
                player.voices[i] = source;
            }
            player.ambience = new GameObject("Courtyard Ambience").AddComponent<AudioSource>();
            player.ambience.transform.SetParent(root.transform, false);
            player.ambience.clip = player.bank.ambience;
            player.ambience.playOnAwake = false;
            player.ambience.loop = true;
            player.ambience.volume = 0;
            player.ambience.priority = 220;
            player.Validate();
            root.SetActive(true);
            return player;
        }

        public static DojoSoundBank Prepare()
        {
            var bank = AssetDatabase.LoadAssetAtPath<DojoSoundBank>(BankPath);
            if (!bank)
            {
                bank = ScriptableObject.CreateInstance<DojoSoundBank>();
                AssetDatabase.CreateAsset(bank, BankPath);
            }
            bank.sounds = new[]
            {
                Sound(DojoSoundCue.Swing, "Swing", 3, .17f),
                Sound(DojoSoundCue.KickSwing, "KickSwing", 1, .20f),
                Sound(DojoSoundCue.Perfect, "HitPerfect", 3, .30f),
                Sound(DojoSoundCue.Good, "HitGood", 3, .24f),
                Sound(DojoSoundCue.DummyHit, "DummyHit", 3, .40f),
                Sound(DojoSoundCue.Footstep, "Footstep", 3, .13f),
                Sound(DojoSoundCue.Creak, "WoodCreak", 1, .12f),
                Sound(DojoSoundCue.Transition, "RoomTransition", 1, .16f),
                Sound(DojoSoundCue.Complete, "TrainingComplete", 1, .27f, 0),
                Sound(DojoSoundCue.Failed, "TrainingFailed", 1, .24f, 0),
            };
            bank.ambience = Clip("CourtyardWind");
            bank.Validate();
            EditorUtility.SetDirty(bank);
            return bank;
        }

        private static DojoSoundBank.Sound Sound(DojoSoundCue cue, string name, int count,
            float gain, float pitchVariation = .025f)
        {
            var clips = new AudioClip[count];
            for (int i = 0; i < count; i++)
            {
                string suffix = "";
                if (count > 1)
                {
                    suffix = (i + 1).ToString();
                }
                clips[i] = Clip(name + suffix);
            }
            return new DojoSoundBank.Sound
            {
                cue = cue,
                clips = clips,
                gain = gain,
                pitchVariation = pitchVariation
            };
        }

        private static AudioClip Clip(string name)
        {
            return SceneResources.Require<AudioClip>("Assets/Game/Audio/Foley/" + name + ".wav");
        }
    }
}
