using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;

namespace RhythmDojo.Tests
{
    [Category("UI")]
    public sealed class GameplayHudFlowTests : GameplayTestContext
    {
        private IEnumerator LoadGameplay()
        {
            yield return Click(UnityEngine.Object.FindFirstObjectByType<SongSelectionView>().play);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReturnButtonInReadyStateLoadsSongSelectionAndReleasesSong()
        {
            yield return LoadGameplay();
            var view = UnityEngine.Object.FindFirstObjectByType<GameplayHudView>();
            Assert.That(view.returnButton.gameObject.activeSelf, Is.True);
            yield return Click(view.returnButton);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>());
            Assert.That(UnityEngine.Object.FindFirstObjectByType<AppFlowController>().CurrentRequest, Is.Null);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<RhythmGameController>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator ReturnButtonIsHiddenWhilePlayingAndEscapeReturnsToSelection()
        {
            yield return LoadGameplay();
            yield return Press(Key.Space);
            var view = UnityEngine.Object.FindFirstObjectByType<GameplayHudView>();
            Assert.That(view.returnButton.gameObject.activeSelf, Is.False);
            yield return Press(Key.Escape);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>());
            Assert.That(UnityEngine.Object.FindFirstObjectByType<AppFlowController>().CurrentRequest, Is.Null);
        }

        [UnityTest]
        public IEnumerator ReturnButtonReappearsAfterCompletionAndLoadsSongSelection()
        {
            yield return LoadGameplay();
            var game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
            yield return Press(Key.Space);
            double deadline = Time.realtimeSinceStartupAsDouble + game.Chart.CompletionTime + 10;
            while (game.Session.State != SessionState.Completed)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }
            yield return null;
            var view = UnityEngine.Object.FindFirstObjectByType<GameplayHudView>();
            Assert.That(view.returnButton.gameObject.activeSelf, Is.True);
            yield return Click(view.returnButton);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>());
        }
    }
}
