using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Content;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RhythmDojo.Tests
{
    [Category("UI")]
    public sealed class PracticeSelectionTests : GameplayTestContext
    {

        private sealed class DifficultySongProvider : ISongProvider
        {
            public IReadOnlyList<SongEntry> GetEntries()
            {
                return new[]
                {
                    new SongEntry("difficulty-input", "Difficulty Input", "Test", 30, 1, 4, 120, 120,
                        charts: new[]
                        {
                            new ChartEntry("difficulty-input-easy", ChartSlot.Easy),
                            new ChartEntry("difficulty-input-standard", ChartSlot.Standard),
                            new ChartEntry("difficulty-input-hard", ChartSlot.Hard)
                        })
                };
            }

            public System.Threading.Tasks.Task<PlayableSong> LoadAsync(string songId,
                System.Threading.CancellationToken cancellationToken)
            {
                throw new NotSupportedException("This provider tests selection without playback.");
            }
        }

        private PracticeSelectionView OpenPractice()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var screen = UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>();
            screen.RestoreEntryFocus();
            return UnityEngine.Object.FindFirstObjectByType<PracticeSelectionView>();
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator AlternatingStrikesCommitHiddenFacesInOrderAndKeepAuthoredPieces()
        {
            var practice = OpenPractice();
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            var pieces = practice.GetComponentsInChildren<RectTransform>(true);
            Assert.That(practice.hands.alpha, Is.Zero);
            Assert.That(practice.strikes.ActiveCount, Is.Zero);
            var order = new List<int>();
            string firstSong = flow.Selection.SelectedSongId;
            int firstIndex = view.Rows.FindIndex(row => row.Entry.Id == firstSong);
            Action<int> changed = direction =>
            {
                Assert.That(practice.ContactCount, Is.EqualTo(order.Count + 1),
                    "A discrete selection must commit on the contact frame.");
                Assert.That(practice.rig.HoldingDisplay, Is.True,
                    "The outgoing display must remain on the front until the column turns away.");
                order.Add(direction);
            };
            practice.Turned += changed;
            try
            {
                yield return Press(Key.RightArrow);
                Assert.That(practice.StrikeDirection, Is.EqualTo(1));
                Assert.That(practice.IsStriking, Is.True);
                yield return Press(Key.LeftArrow);
                yield return Press(Key.RightArrow);
                yield return Press(Key.RightArrow);
                yield return Await(() => !practice.IsStriking, "Buffered strikes never settled.");
                CollectionAssert.AreEqual(new[] { 1, -1, 1, 1 }, order);
                Assert.That(flow.Selection.SelectedSongId, Is.EqualTo(view.Rows[(firstIndex + 2) % view.Rows.Count].Entry.Id));
                Assert.That(practice.ContactCount, Is.EqualTo(4));
                Assert.That(practice.rig.FrontVisible, Is.True);
                Assert.That(practice.rig.jacket.sprite, Is.SameAs(view.album.sprite));
                Assert.That(practice.rig.title.text, Is.EqualTo(view.songTitle.text));
                Assert.That(practice.title.text, Is.EqualTo(view.songTitle.text));
                Assert.That(practice.rig.head.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(practice.PendingStrikes, Is.Zero);
                Assert.That(practice.hands.alpha, Is.Zero);
                Assert.That(practice.strikes.IsAnimating, Is.False);

                yield return Click(practice.library);
                yield return Press(Key.Escape);
                // The shared keyboard-focus border can move into this screen. Every authored
                // piece must retain its identity even when that transient decoration is present.
                CollectionAssert.IsSubsetOf(pieces, practice.GetComponentsInChildren<RectTransform>(true));
                Assert.That(practice.gameObject.activeInHierarchy, Is.True);
                Assert.That(practice.rig.jacket.sprite, Is.SameAs(view.album.sprite));
            }
            finally
            {
                practice.Turned -= changed;
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator HeldArrowsAlternatePosesWrapSongsAndStopImmediatelyOnRelease()
        {
            var practice = OpenPractice();
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            var randomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(31415);
            int expected = view.Rows.FindIndex(row => row.Entry.Id == flow.Selection.SelectedSongId);
            int changes = 0;
            int hitMotions = 0;
            var fixedBase = practice.rig.fixedBase.localToWorldMatrix;
            var joint = practice.rig.rotationJoint.localToWorldMatrix;
            var pieces = practice.strikes.GetComponentsInChildren<Transform>(true);
            Action<int, bool, bool> contacted = (direction, changesSong, rapid) =>
            {
                int motion = practice.strikes.LastContactMotion;
                hitMotions |= 1 << motion;
                var relativeTo = (RectTransform)practice.strikes.transform;
                var punch = practice.rig.ContactPoint(direction, relativeTo);
                var kick = practice.rig.ContactPoint(direction, relativeTo, true);
                var bearing = relativeTo.InverseTransformPoint(practice.rig.rotationJoint.position);
                Assert.That(punch.y - kick.y, Is.GreaterThan(50), "Kick and hand heights must stay distinct in every turn frame.");
                Assert.That(kick.y - bearing.y, Is.GreaterThan(30), "Kicks must hit the movable column above the bearing.");
                var target = punch;
                if (motion == 2)
                {
                    target = kick;
                }
                Assert.That(Vector2.Distance(practice.strikes.LastContactPosition, target), Is.LessThan(1));
                Assert.That(practice.rig.fixedBase.localToWorldMatrix == fixedBase, Is.True);
                Assert.That(practice.rig.rotationJoint.localToWorldMatrix == joint, Is.True);
            };
            practice.strikes.Contact += contacted;
            Action<int> changed = direction =>
            {
                expected = (expected + direction + view.Rows.Count) % view.Rows.Count;
                Assert.That(flow.Selection.SelectedSongId, Is.EqualTo(view.Rows[expected].Entry.Id));
                changes++;
            };
            practice.Turned += changed;
            try
            {
                for (int direction = -1; direction <= 1; direction += 2)
                {
                    Key key = Key.LeftArrow;
                    if (direction > 0)
                    {
                        key = Key.RightArrow;
                    }
                    int start = practice.ContactCount;
                    int selected = changes;
                    var motions = new HashSet<int>();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                    yield return Await(() => practice.IsBarrage, "Held input never began its barrage.", 2);
                    double until = Time.unscaledTimeAsDouble + 2;
                    int last = practice.strikes.LastMotion;
                    motions.Add(last);
                    while (Time.unscaledTimeAsDouble < until)
                    {
                        yield return null;
                        motions.Add(practice.strikes.LastMotion);
                        Assert.That(practice.StrikeDirection, Is.EqualTo(direction));
                        Assert.That(practice.strikes.ActiveCount, Is.LessThanOrEqualTo(3));
                        Assert.That(practice.strikes.AfterimageCount, Is.LessThanOrEqualTo(4));
                        Assert.That(Mathf.Abs(practice.rig.Yaw), Is.LessThanOrEqualTo(360));
                    }
                    Assert.That(motions.Count, Is.EqualTo(3), "Barrage must contain punch, chop and kick.");
                    Assert.That(practice.ContactCount - start, Is.GreaterThan(changes - selected + 4));
                    Assert.That(changes - selected, Is.InRange(7, 14));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                    yield return null;
                    Assert.That(practice.IsBarrage, Is.False);
                    int released = practice.ContactCount;
                    yield return Await(() => !practice.IsStriking, "Release did not retract the limbs.", .4);
                    Assert.That(practice.ContactCount, Is.EqualTo(released), "Released barrage must not land queued hits.");
                    Assert.That(practice.hands.alpha, Is.Zero);
                    Assert.That(practice.strikes.IsAnimating, Is.False);
                    Assert.That(practice.title.text, Is.EqualTo(view.songTitle.text));
                    Assert.That(practice.rig.jacket.sprite, Is.SameAs(view.album.sprite));
                }

                int previous = -1;
                for (int i = 0; i < 8; i++)
                {
                    yield return Press(Key.RightArrow);
                    int motion = practice.strikes.LastMotion;
                    Assert.That(motion, Is.Not.EqualTo(previous), "Same-side attacks must not immediately repeat.");
                    previous = motion;
                    yield return Await(() => !practice.IsStriking);
                }
                Assert.That(hitMotions, Is.EqualTo(7));
                CollectionAssert.AreEqual(pieces, practice.strikes.GetComponentsInChildren<Transform>(true));
            }
            finally
            {
                practice.Turned -= changed;
                practice.strikes.Contact -= contacted;
                UnityEngine.Random.state = randomState;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator SidePegsDriveOppositeFullBodyTurnsAndRevealTheDisplayOnReturn()
        {
            var practice = OpenPractice();
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            var randomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(702);
            var fixedBase = practice.rig.fixedBase.localToWorldMatrix;
            var joint = practice.rig.rotationJoint.localToWorldMatrix;
            try
            {
                for (int direction = -1; direction <= 1; direction += 2)
                {
                    var oldJacket = practice.rig.jacket.sprite;
                    string oldTitle = practice.rig.title.text;
                    int contacts = practice.ContactCount;
                    practice.Strike(direction);
                    var target = practice.rig.ContactPoint(direction, (RectTransform)practice.strikes.transform,
                        practice.strikes.LastMotion == 2);
                    yield return Await(() => practice.ContactCount > contacts);
                    var visual = practice.strikes.visuals.First(item => item.active && item.contacted);
                    Assert.That(Vector2.Distance(visual.contactPosition, target), Is.LessThan(1));
                    Assert.That(Mathf.Sign(visual.contactPosition.x), Is.EqualTo(direction));
                    var headPosition = practice.strikes.transform.InverseTransformPoint(practice.rig.head.position);
                    Assert.That(headPosition.y - visual.contactPosition.y, Is.GreaterThan(practice.rig.head.rect.height),
                        "The attack must land on the column peg below the display.");
                    Assert.That(practice.rig.jacket.sprite, Is.SameAs(oldJacket));
                    Assert.That(practice.rig.title.text, Is.EqualTo(oldTitle));
                    Assert.That(practice.title.text, Is.EqualTo(oldTitle));

                    var frames = new HashSet<int>();
                    var order = new List<int>();
                    bool hidden = false;
                    float rotation = 0;
                    float previous = practice.rig.Yaw;
                    float firstStep = -1;
                    float fastestStep = 0;
                    float finalStep = 0;
                    var timings = new System.Text.StringBuilder();
                    double deadline = Time.unscaledTimeAsDouble + 2;
                    // Cold NUnit comparers above can stall the Editor before its next frame.
                    // Time the rendered animation after those contact assertions have finished.
                    double contactedAt = Time.realtimeSinceStartupAsDouble;
                    while (practice.IsStriking && Time.unscaledTimeAsDouble < deadline)
                    {
                        yield return null;
                        timings.AppendLine((Time.realtimeSinceStartupAsDouble - contactedAt)
                            + " delta=" + Time.unscaledDeltaTime + " yaw=" + practice.rig.Yaw);
                        Assert.That(practice.rig.fixedBase.localToWorldMatrix == fixedBase, Is.True,
                            "Impact and turning must never displace or rotate the feet and lower column.");
                        Assert.That(practice.rig.rotationJoint.localToWorldMatrix == joint, Is.True);
                        if (Time.realtimeSinceStartupAsDouble - contactedAt > .14)
                        {
                            Assert.That(practice.strikes.ActiveCount, Is.Zero, "The limb must snap out before the turn ends.");
                            Assert.That(practice.strikes.AfterimageCount, Is.Zero);
                        }
                        int index = practice.rig.FrameIndex;
                        if (frames.Add(index))
                        {
                            order.Add(index);
                        }
                        float step = Mathf.DeltaAngle(previous, practice.rig.Yaw);
                        rotation += step;
                        previous = practice.rig.Yaw;
                        float speed = Mathf.Abs(step) / Time.unscaledDeltaTime;
                        if (speed > 1)
                        {
                            if (firstStep < 0)
                            {
                                firstStep = speed;
                            }
                            fastestStep = Mathf.Max(fastestStep, speed);
                            finalStep = speed;
                        }
                        if (!practice.rig.FrontVisible)
                        {
                            hidden = true;
                            Assert.That(practice.rig.head.gameObject.activeSelf, Is.False);
                            Assert.That(practice.rig.jacket.sprite, Is.SameAs(view.album.sprite));
                            Assert.That(practice.title.text, Is.EqualTo(view.songTitle.text));
                        }
                    }
                    Assert.That(practice.IsStriking, Is.False);
                    double elapsed = Time.realtimeSinceStartupAsDouble - contactedAt;
                    System.IO.File.WriteAllText("Logs/articulated-test-timing-" + direction + ".txt", timings.ToString());
                    Assert.That(elapsed, Is.InRange(.28, .46),
                        "The former 0.68 second turn should finish in about 0.34 seconds.");
                    Assert.That(hidden, Is.True);
                    CollectionAssert.IsSubsetOf(new[] { 1, 2, 3, 4, 5, 6, 7 }, frames,
                        "Both side silhouettes and the full rear must be visible during a single turn.");
                    Assert.That(rotation * direction, Is.EqualTo(-360).Within(2));
                    Assert.That(fastestStep, Is.GreaterThan(firstStep * 1.5f));
                    Assert.That(fastestStep, Is.GreaterThan(finalStep * 1.5f));
                    if (direction < 0)
                    {
                        Assert.That(order.IndexOf(2), Is.LessThan(order.IndexOf(6)));
                    }
                    else
                    {
                        Assert.That(order.IndexOf(6), Is.LessThan(order.IndexOf(2)));
                    }
                    Assert.That(practice.rig.title.text, Is.EqualTo(view.songTitle.text));
                    Assert.That(practice.rig.jacket.sprite, Is.SameAs(view.album.sprite));
                    Assert.That(practice.rig.body.anchoredPosition, Is.EqualTo(practice.rig.bodyHome));
                    Assert.That(practice.rig.body.localRotation, Is.EqualTo(Quaternion.identity));
                }

                // Check the visible wood at the seam, not just its transform: source cells
                // have different centres, so a shared sprite pivot makes the top drift.
                var lower = practice.rig.fixedBase.GetComponent<UnityEngine.UI.Image>();
                float lowerCentre = SeamCentre(lower, 356);
                for (int i = 0; i < 8; i++)
                {
                    practice.rig.Pose(i * 45, 0);
                    var upper = practice.rig.side;
                    if (practice.rig.FrontVisible)
                    {
                        upper = practice.rig.front;
                    }
                    else if (practice.rig.rear.gameObject.activeSelf)
                    {
                        upper = practice.rig.rear;
                    }
                    Assert.That(SeamCentre(upper, 356), Is.EqualTo(lowerCentre).Within(.1f),
                        "The upper and lower timber centres must meet at the bearing in frame " + i);
                }
            }
            finally
            {
                practice.rig.Pose(0, 0);
                UnityEngine.Random.state = randomState;
            }
        }

        private static float SeamCentre(UnityEngine.UI.Image image, int row)
        {
            var sprite = image.sprite;
            int cellHeight = sprite.texture.height / 2;
            int top = sprite.texture.height;
            if (sprite.rect.y < cellHeight)
            {
                top = cellHeight;
                row++;
            }
            int left = int.MaxValue;
            int right = int.MinValue;
            for (int x = 110; x < 250; x++)
            {
                if (sprite.texture.GetPixel((int)sprite.rect.x + x, top - row - 1).a < .71f)
                {
                    continue;
                }
                left = Math.Min(left, x);
                right = Math.Max(right, x);
            }
            Assert.That(left, Is.LessThan(right), "The seam must contain opaque timber.");
            float centre = (left + right) * .5f - sprite.pivot.x;
            return image.rectTransform.TransformPoint(new Vector3(
                centre * image.rectTransform.rect.width / sprite.rect.width, 0, 0)).x;
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator ReversingAndOpposedArrowsCancelTheOldBarrage()
        {
            var practice = OpenPractice();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftArrow));
            yield return Await(() => practice.IsBarrage);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
            yield return null;
            yield return null;
            int atReversal = practice.ContactCount;
            yield return Await(() => practice.ContactCount > atReversal);
            Assert.That(practice.StrikeDirection, Is.EqualTo(1));
            yield return Await(() => practice.IsBarrage);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftArrow, Key.RightArrow));
            yield return null;
            yield return null;
            Assert.That(practice.IsBarrage, Is.False);
            yield return Await(() => !practice.IsStriking, "Opposed inputs must settle.", .4);
            Assert.That(practice.hands.alpha, Is.Zero);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
            yield return Await(() => practice.IsBarrage);
            var screen = UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>();
            try
            {
                screen.enabled = false;
                yield return Await(() => !practice.IsStriking, "Disabled input must stop the barrage.", .4);
                Assert.That(practice.hands.alpha, Is.Zero);
            }
            finally
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                screen.enabled = true;
            }
        }

        [UnityTest]
        [Category("Endurance")]
        public IEnumerator SustainedBarrageKeepsObjectsAndAnimationAllocationsBounded()
        {
            var practice = OpenPractice();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
            yield return Await(() => practice.ContactCount >= 30, "Warm-up failed.", 6);
            var pieces = practice.GetComponentsInChildren<Transform>(true);
            int sprites = Resources.FindObjectsOfTypeAll<Sprite>().Length;
            int textures = Resources.FindObjectsOfTypeAll<Texture>().Length;
            int meshes = Resources.FindObjectsOfTypeAll<Mesh>().Length;
            // Collect at matching settled frame boundaries, outside the frame-time sample.
            GC.Collect();
            yield return null;
            long before = GC.GetTotalMemory(true);
            yield return null;
            yield return null;
            int contacts = practice.ContactCount;
            double until = Time.unscaledTimeAsDouble + 60;
            float worstFrame = 0;
            int slowFrames = 0;
            int frames = 0;
            while (Time.unscaledTimeAsDouble < until)
            {
                yield return null;
                worstFrame = Mathf.Max(worstFrame, Time.unscaledDeltaTime);
                if (Time.unscaledDeltaTime > .05f)
                {
                    slowFrames++;
                }
                frames++;
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            double releaseDeadline = Time.unscaledTimeAsDouble + 1;
            while (practice.IsStriking && Time.unscaledTimeAsDouble < releaseDeadline)
            {
                yield return null;
            }
            Assert.That(practice.IsStriking, Is.False,
                "Release: barrage=" + practice.IsBarrage + " pending=" + practice.PendingStrikes
                + " active=" + practice.strikes.ActiveCount + " trails=" + practice.strikes.AfterimageCount
                + " held=" + keyboard.rightArrowKey.isPressed
                + " current=" + Keyboard.current.name
                + " screenEnabled=" + UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>().enabled);
            Assert.That(practice.ContactCount - contacts, Is.GreaterThan(400));
            CollectionAssert.AreEqual(pieces, practice.GetComponentsInChildren<Transform>(true));
            Assert.That(Resources.FindObjectsOfTypeAll<Sprite>().Length, Is.EqualTo(sprites));
            Assert.That(Resources.FindObjectsOfTypeAll<Texture>().Length, Is.EqualTo(textures));
            Assert.That(Resources.FindObjectsOfTypeAll<Mesh>().Length, Is.EqualTo(meshes));
            GC.Collect();
            yield return null;
            GC.Collect();
            yield return null;
            long retained = GC.GetTotalMemory(true) - before;

            // Measure the warmed animation path separately from song metadata formatting and
            // Test Runner's per-frame allocations. Tick is the same path used by the live view.
            int completed = practice.ContactCount;
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1200; i++)
            {
                if (i % 6 == 0)
                {
                    practice.strikes.Play(1, false, true);
                }
                practice.strikes.Tick(1f / 60f);
            }
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            practice.ResetPresentation();
            System.IO.File.WriteAllText("Logs/practice-barrage-performance.txt",
                "60s real held input after warm-up; contacts=" + (completed - contacts)
                + " retainedBytes=" + retained + " animationAllocatedBytes=" + allocated
                + " frames=" + frames + " framesOver50ms=" + slowFrames + " worstFrameSeconds=" + worstFrame
                + " authoredTransforms=" + pieces.Length);
            // The process heap includes the Editor and Test Runner. Record it as diagnostic
            // evidence; enforce ownership and allocation bounds on the actual animation path.
            // Compare process retention across alternating idle/barrage windows in the live
            // Editor probe (Logs/PracticeMemoryProbe.cs), outside the Test Runner.
            Assert.That(allocated, Is.Zero, "Reused strike animation must not allocate managed memory.");
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator ConfirmationWaitsForBufferedSelectionThenLoadsThatSong()
        {
            var practice = OpenPractice();
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            string selected = flow.Selection.SelectedSongId;
            yield return Press(Key.RightArrow);
            yield return Press(Key.LeftArrow);
            yield return Press(Key.Enter);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<RhythmGameController>(), Is.Null);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<RhythmGameController>());
            Assert.That(flow.CurrentRequest.Song.SongId, Is.EqualTo(selected));
            flow.ReturnFromGameplay();
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<PracticeSelectionView>());
            var returned = UnityEngine.Object.FindFirstObjectByType<PracticeSelectionView>();
            Assert.That(returned.IsStriking, Is.False);
            Assert.That(flow.Selection.SelectedSongId, Is.EqualTo(selected));
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator EscapeCancelsPunchQueuePullsBackAndCanReenter()
        {
            var practice = OpenPractice();
            float initialScale = practice.room.localScale.x;
            yield return Press(Key.RightArrow);
            yield return Press(Key.LeftArrow);
            yield return Press(Key.Escape);
            Assert.That(practice.IsLeaving, Is.True);
            Assert.That(practice.PendingStrikes, Is.Zero);
            Assert.That(practice.room.localScale.x, Is.LessThan(initialScale));
            yield return Await(() => SceneManager.GetActiveScene().path == MainMenuActions.ModeSelectionScenePath);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<MainMenuView>());
            var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuView>();
            yield return Await(() => menu.GetComponent<MainMenuScreen>().CanSubmit);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(menu.gameObject);
            yield return Press(Key.Enter);
            yield return Await(() => SceneManager.GetActiveScene().path == AppFlowController.SelectionPath);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<PracticeSelectionView>());
            var returned = UnityEngine.Object.FindFirstObjectByType<PracticeSelectionView>();
            Assert.That(returned.IsLeaving, Is.False);
            Assert.That(returned.interfaceGroup.blocksRaycasts, Is.True);
            yield return Press(Key.RightArrow);
            yield return Await(() => !returned.IsStriking);
            Assert.That(returned.ContactCount, Is.EqualTo(1));
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator DifficultyAndEmptyFavoritesUseTheExistingSelectionService()
        {
            var practice = OpenPractice();
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            var provider = new DifficultySongProvider();
            flow.Library.Register(provider);
            var gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                flow.Selection.UpdateSelection("difficulty-input", flow.Settings.difficulties[1],
                    flow.Selection.SelectedScrollMode, flow.Selection.SelectedMultiplier);
                view.GetComponent<SongSelectionScreen>().RestoreEntryFocus();
                yield return null;
                Assert.That(practice.difficulties.All(button => button.interactable), Is.True);
                int difficulty = Array.IndexOf(flow.Settings.difficulties, flow.SelectedDifficulty);
                yield return Press(Key.UpArrow);
                int expected = difficulty + 1;
                Assert.That(flow.Selection.SelectedDifficulty, Is.SameAs(flow.Settings.difficulties[expected]));
                yield return Press(Key.UpArrow);
                Assert.That(flow.Selection.SelectedDifficulty, Is.SameAs(flow.Settings.difficulties[expected]),
                    "Up cannot move above the hardest difficulty.");
                yield return Press(Key.DownArrow);
                Assert.That(flow.Selection.SelectedDifficulty, Is.SameAs(flow.Settings.difficulties[1]));
                yield return Press(Key.DownArrow);
                Assert.That(flow.Selection.SelectedDifficulty, Is.SameAs(flow.Settings.difficulties[0]));
                yield return Press(Key.DownArrow);
                Assert.That(flow.Selection.SelectedDifficulty, Is.SameAs(flow.Settings.difficulties[0]),
                    "Down cannot move below the easiest difficulty.");

                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.DpadUp));
                yield return null;
                yield return null;
                Assert.That(flow.Selection.SelectedDifficulty, Is.SameAs(flow.Settings.difficulties[1]));
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return null;
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.DpadDown));
                yield return null;
                yield return null;
                Assert.That(flow.Selection.SelectedDifficulty, Is.SameAs(flow.Settings.difficulties[0]));
            }
            finally
            {
                InputSystem.RemoveDevice(gamepad);
                flow.Library.Unregister(provider);
            }
            yield return null;
            yield return Click(practice.library);
            yield return Click(view.favoritesTab);
            Assert.That(view.Rows, Is.Empty);
            yield return Press(Key.Escape);
            Assert.That(practice.gameObject.activeInHierarchy, Is.True);
            yield return null;
            Assert.That(practice.play.interactable, Is.False);
            Assert.That(practice.next.interactable, Is.False);
            yield return Press(Key.RightArrow);
            Assert.That(practice.IsStriking, Is.False);
            Assert.That(practice.status.text, Does.Contain("즐겨찾기"));
            Assert.That(flow.Selection.SelectedSongId, Is.Null);
        }
    }
}
