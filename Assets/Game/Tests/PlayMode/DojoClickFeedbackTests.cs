using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RhythmDojo.UI;

namespace RhythmDojo.Tests
{
    [Category("UI")]
    public sealed class DojoClickFeedbackTests : GameplayTestContext
    {
        [UnityTest]
        public IEnumerator ClickEffectDoesNotBlockSettingsAndSurvivesSceneTransition()
        {
            var feedback = Object.FindFirstObjectByType<DojoClickFeedback>();
            Assert.That(feedback, Is.Not.Null);
            var selection = Object.FindFirstObjectByType<SongSelectionView>();
            var point = RectTransformUtility.WorldToScreenPoint(null, selection.settingsButton.transform.position);
            feedback.PlayAt(point);
            Assert.That(feedback.GetComponent<AudioSource>().isPlaying, Is.True, "Click sound should start.");
            yield return Click(selection.settingsButton);
            yield return Await(() => Object.FindFirstObjectByType<SettingsScreen>());
            Assert.That(Object.FindFirstObjectByType<DojoClickFeedback>(), Is.SameAs(feedback));
            yield return Click(Object.FindFirstObjectByType<SettingsView>().back);
            yield return Await(() => Object.FindFirstObjectByType<SongSelectionScreen>());
        }

        [UnityTest]
        public IEnumerator ClickSoundReachesOutputAfterTitlePlaySettingsAndGameplayTransitions()
        {
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Assets/Game/Scenes/Title.unity");
            yield return null;
            var menu = Object.FindFirstObjectByType<MainMenuView>();
            yield return Click(menu.logo);
            yield return new WaitForSecondsRealtime(.3f);
            yield return Click(menu.start);
            yield return Await(() => Object.FindFirstObjectByType<SongSelectionScreen>());
            yield return CheckClickOutput();
            yield return Click(Object.FindFirstObjectByType<SongSelectionView>().settingsButton);
            yield return Await(() => Object.FindFirstObjectByType<SettingsScreen>());
            yield return CheckClickOutput();
            yield return Click(Object.FindFirstObjectByType<SettingsView>().back);
            yield return Await(() => Object.FindFirstObjectByType<SongSelectionScreen>());
            yield return Click(Object.FindFirstObjectByType<SongSelectionView>().play);
            yield return Await(() => Object.FindFirstObjectByType<RhythmDojo.Gameplay.RhythmGameController>());
            yield return CheckClickOutput();
        }

        private static IEnumerator CheckClickOutput()
        {
            var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            int active = 0;
            foreach (var listener in listeners) if (listener.isActiveAndEnabled) active++;
            Assert.That(active, Is.EqualTo(1), "The destination screen must have one audio output listener.");
            // Let any previous button press finish, then measure a fresh click in the mix.
            yield return new WaitForSecondsRealtime(.12f);
            var feedback = Object.FindFirstObjectByType<DojoClickFeedback>();
            feedback.PlayAt(new Vector2(Screen.width*.5f,Screen.height*.5f));
            var samples = new float[1024]; float peak = 0;
            double deadline = Time.realtimeSinceStartupAsDouble + 1;
            while (Time.realtimeSinceStartupAsDouble < deadline && peak < .00001f)
            {
                yield return null;
                AudioListener.GetOutputData(samples,0);
                foreach (float sample in samples) peak = Mathf.Max(peak,Mathf.Abs(sample));
            }
            Assert.That(peak, Is.GreaterThan(.00001f), "A playing AudioSource alone is insufficient: click samples must reach the audio output.");
        }

        [UnityTest]
        public IEnumerator BurstUsesBoundedObjectsAndRipplesExpireWhilePaused()
        {
            var feedback = Object.FindFirstObjectByType<DojoClickFeedback>();
            var source = feedback.GetComponent<AudioSource>();
            float previousScale = Time.timeScale; bool previousMute = source.mute;
            try
            {
                source.mute = true;
                for (int i=0; i<24; i++) feedback.PlayAt(new Vector2(Screen.width*.5f+i,Screen.height*.5f));
                var all = feedback.GetComponentsInChildren<DojoClickRippleGraphic>(true);
                Assert.That(all.Length, Is.InRange(1,8), "Burst effects have a fixed allocation budget.");
                Assert.That(feedback.GetComponentsInChildren<DojoClickRippleGraphic>().Length, Is.GreaterThan(0));
                Time.timeScale = 0;
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That(feedback.GetComponentsInChildren<DojoClickRippleGraphic>().Length, Is.Zero,
                    "UI feedback must expire even while gameplay time is paused.");
                feedback.PlayAt(new Vector2(Screen.width*.5f,Screen.height*.5f));
                Assert.That(feedback.GetComponentsInChildren<DojoClickRippleGraphic>(true), Is.EquivalentTo(all),
                    "New clicks reuse existing objects.");
            }
            finally { Time.timeScale = previousScale; source.mute = previousMute; }
        }
    }
}
