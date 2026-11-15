using System.Collections;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.Audio;
using RhythmDojo.Services;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace RhythmDojo.Tests
{
    [Category("UI")]
    public sealed class RecordedDojoAudioTests : GameplayTestContext
    {
        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator PracticeInputSoundsBeforeContactAndReopeningDoesNotDuplicateAudio()
        {
            var practice = Object.FindFirstObjectByType<PracticeSelectionView>();
            var sounds = practice.sounds;
            var sources = sounds.voices.ToArray();
            int motions = sounds.MotionCount;
            int contacts = sounds.ContactCount;
            practice.next.onClick.Invoke();
            Assert.That(sounds.MotionCount, Is.EqualTo(motions + 1), "The visible swing starts immediately.");
            Assert.That(sounds.ContactCount, Is.EqualTo(contacts), "No impact before the contact pose.");
            yield return Await(() => practice.ContactCount == 1);
            Assert.That(sounds.ContactCount, Is.EqualTo(contacts + 1));
            yield return Await(() => !practice.IsStriking);

            yield return Click(practice.library);
            yield return Press(UnityEngine.InputSystem.Key.Escape);
            contacts = sounds.ContactCount;
            motions = sounds.MotionCount;
            practice.previous.onClick.Invoke();
            Assert.That(sounds.MotionCount, Is.EqualTo(motions + 1), "Reopening must not duplicate subscriptions.");
            yield return Await(() => sounds.ContactCount > contacts);
            Assert.That(sounds.ContactCount, Is.EqualTo(contacts + 1));
            CollectionAssert.AreEqual(sources, sounds.voices);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator DenseContactsVaryTakesBoundVoicesAndHonorMuteWithoutChangingInput()
        {
            var practice = Object.FindFirstObjectByType<PracticeSelectionView>();
            var sounds = practice.sounds;
            var preferences = PresentationPreferences.Current;
            float volume = preferences.gameplayVolume;
            var sources = sounds.voices.ToArray();
            AudioClip previous = null;
            try
            {
                preferences.gameplayVolume = 1;
                for (int i = 0; i < 18; i++)
                {
                    int count = sounds.ContactCount;
                    Assert.That(sounds.Play(DojoSoundCue.Perfect), Is.True);
                    Assert.That(sounds.Play(DojoSoundCue.Perfect), Is.False, "A chord cannot multiply the attack.");
                    Assert.That(sounds.ContactCount, Is.EqualTo(count + 1));
                    Assert.That(sounds.LastClip, Is.Not.SameAs(previous), "Consecutive hits use distinct recordings.");
                    previous = sounds.LastClip;
                    Assert.That(sources.Take(3).Count(source => source.isPlaying), Is.LessThanOrEqualTo(3));
                    Assert.That(sources.Take(3).Sum(source => source.volume), Is.LessThan(.91f));
                    yield return new WaitForSecondsRealtime(.04f);
                }
                sounds.StopAll();
                preferences.gameplayVolume = 0;
                int contacts = sounds.ContactCount;
                int visualContacts = practice.ContactCount;
                practice.next.onClick.Invoke();
                yield return Await(() => practice.ContactCount > visualContacts);
                Assert.That(sounds.ContactCount, Is.EqualTo(contacts), "Effects mute must reach the actual playback path.");
                Assert.That(sources.All(source => !source.isPlaying), Is.True);
                CollectionAssert.AreEqual(sources, sounds.voices);
            }
            finally
            {
                sounds.StopAll();
                preferences.gameplayVolume = volume;
            }
        }
    }
}
