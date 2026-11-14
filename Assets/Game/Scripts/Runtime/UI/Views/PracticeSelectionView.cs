using System;
using System.Collections;
using System.Collections.Generic;
using RhythmDojo.Services;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    [DefaultExecutionOrder(100)]
    public sealed class PracticeSelectionView : MonoBehaviour
    {
        public const float ApproachSeconds = .65f;
        public static readonly Vector2 CameraDestination = new Vector2(-333.3333f, 368f);
        public const float CameraScale = 10f / 3f;
        public const float BackgroundScale = 1.8f;
        public static readonly Vector2 BackgroundDestination = new Vector2(-95, 50);
        public static readonly Vector2 DummyPosition = new Vector2(100, -135);

        public RectTransform room, dummy;
        public WoodenDummyRig rig;
        public RectTransform leftFist, rightFist, information;
        public CanvasGroup interfaceGroup, hands, impact;
        public Text title, artist, chart, record, counter, status;
        public Text heading, comboRecord;
        public Button previous, next, play, back, library, preview, settings;
        public Button[] difficulties;
        public AudioSource effects;
        public AudioClip contact;
        public PracticeStrikeAnimator strikes;
        [Range(.15f, .8f)] public float holdSeconds = .3f;
        [Range(.15f, .5f)] public float selectionRepeatSeconds = .2f;
        [Range(8f, 12f)] public float visualStrikesPerSecond = 10f;
        [Min(.1f)] public float rotationSeconds = .34f;
        [Min(.05f)] public float barrageRotationSeconds = .095f;

        public bool IsBarrage { get; private set; }
        public int SelectionContactCount { get; private set; }

        public event Action<int> Turned;
        public event Action Settled;
        public bool IsStriking { get; private set; }
        public bool IsLeaving { get; private set; }
        public int PendingStrikes => pending.Count;
        public int ContactCount { get; private set; }
        public int StrikeDirection { get; private set; }
        public float StrikeAge => age;

        // Input has a dynamic lifetime; the fixed visuals and effect pool belong to the scene.
        private readonly Queue<int> pending = new Queue<int>(16);
        private float age;
        private float spinSeconds;
        private float spinAge, recoilAge;
        private float spinStartYaw, spinTravel;
        private Vector2 impactPosition;
        private int spinDirection, barrageDirection;
        private bool singleActive;
        private bool awaitingContact;
        private double nextVisualAt, nextSelectionAt;
        private float contactVolume;
        private Vector2 informationHome;
        private bool captured;
        private bool pendingDetails;
        private string pendingTitle, pendingArtist, pendingChart, pendingRecord, pendingCounter;

        public void Render(SongSelectionPresentation presentation)
        {
            heading.text = presentation.Heading;
            rig.SetSong(presentation.Cover, presentation.Title, presentation.Artist);
            SetSongInformation(presentation.Title, presentation.Metadata, presentation.Details,
                presentation.Record, $"수련곡  {presentation.Index + 1:00} / {presentation.Count:00}");
            comboRecord.text = presentation.ComboRecord;
            for (int i = 0; i < difficulties.Length; i++)
            {
                var color = new Color(1f, .91f, .72f);
                if (presentation.SelectedDifficulty == i)
                {
                    color = new Color(1f, .55f, .25f);
                }
                difficulties[i].GetComponentInChildren<Text>().color = color;
            }
        }

        public void SetSongInformation(string songTitle, string songArtist, string songChart,
            string songRecord, string songCounter)
        {
            pendingTitle = songTitle;
            pendingArtist = songArtist;
            pendingChart = songChart;
            pendingRecord = songRecord;
            pendingCounter = songCounter;
            pendingDetails = true;
            ApplySongInformation();
        }

        private void ApplySongInformation()
        {
            if (!pendingDetails || rig.HoldingDisplay)
            {
                return;
            }
            pendingDetails = false;
            title.text = pendingTitle;
            artist.text = pendingArtist;
            chart.text = pendingChart;
            if (record)
            {
                record.text = pendingRecord;
            }
            if (counter)
            {
                counter.text = pendingCounter;
            }
        }

        public void Validate()
        {
            if (!room || !dummy)
            {
                throw new InvalidOperationException("Practice selection stage missing.");
            }
            if (!rig || !leftFist || !rightFist)
            {
                throw new InvalidOperationException("Practice selection rig missing.");
            }
            if (!information || !interfaceGroup)
            {
                throw new InvalidOperationException("Practice selection animation references missing.");
            }
            if (!hands || !impact)
            {
                throw new InvalidOperationException("Practice selection impact references missing.");
            }
            if (!title || !artist)
            {
                throw new InvalidOperationException("Practice selection song labels missing.");
            }
            if (!chart || !heading)
            {
                throw new InvalidOperationException("Practice selection chart labels missing.");
            }
            if (!status || !record || !counter || !comboRecord)
            {
                throw new InvalidOperationException("Practice selection feedback missing.");
            }
            if (!effects || !contact)
            {
                throw new InvalidOperationException("Practice selection audio missing.");
            }
            if (!previous || !next)
            {
                throw new InvalidOperationException("Practice selection navigation missing.");
            }
            if (!play || !back)
            {
                throw new InvalidOperationException("Practice selection confirmation missing.");
            }
            if (!library || !preview || !settings)
            {
                throw new InvalidOperationException("Practice selection options missing.");
            }
            if (difficulties == null || Array.Exists(difficulties, button => !button))
            {
                throw new InvalidOperationException("Practice selection difficulties missing.");
            }
            if (!strikes)
            {
                throw new InvalidOperationException("Practice strike animation missing.");
            }
            strikes.Validate();
            rig.Validate();
        }

        public void ResetPresentation()
        {
            Validate();
            StopAllCoroutines();
            pending.Clear();
            IsStriking = IsLeaving = false;
            interfaceGroup.interactable = interfaceGroup.blocksRaycasts = true;
            ContactCount = SelectionContactCount = 0;
            IsBarrage = singleActive = false;
            awaitingContact = false;
            strikes.Contact -= StrikeContact;
            strikes.Contact += StrikeContact;
            var preferences = PresentationPreferences.Current ?? PresentationPreferences.Load();
            contactVolume = .65f * preferences.gameplayVolume;
            effects.clip = contact;
            if (!captured)
            {
                informationHome = information.anchoredPosition;
                captured = true;
            }
            SetApproach(1);
            RestorePose();
        }

        public void Strike(int direction)
        {
            if (IsLeaving || !isActiveAndEnabled || direction == 0)
            {
                return;
            }
            // A bounded queue avoids seconds of stale actions after an extreme input burst.
            if (pending.Count >= 16)
            {
                return;
            }
            pending.Enqueue(Math.Sign(direction));
            if (!singleActive)
            {
                BeginStrike();
            }
        }

        private void BeginStrike()
        {
            int direction = pending.Peek();
            if (!strikes.Play(direction, true, false))
            {
                return;
            }
            StrikeDirection = pending.Dequeue();
            age = 0;
            singleActive = true;
            awaitingContact = true;
            IsStriking = true;
            hands.alpha = 1;
        }

        public void BeginBarrage(int direction)
        {
            if (IsLeaving || IsBarrage || !isActiveAndEnabled)
            {
                return;
            }
            if (direction == 0 || singleActive || pending.Count > 0)
            {
                return;
            }
            barrageDirection = Math.Sign(direction);
            nextVisualAt = nextSelectionAt = Time.unscaledTimeAsDouble;
            IsBarrage = IsStriking = true;
            hands.alpha = 1;
        }

        public void StopBarrage()
        {
            IsBarrage = false;
            barrageDirection = 0;
            strikes.StopBarrage();
        }

        private void Update()
        {
            if (!IsStriking || IsLeaving)
            {
                return;
            }
            float deltaTime = Time.unscaledDeltaTime;
            age += deltaTime;
            // Preserve the side/rear poses after a synchronous artwork-load hitch. At the
            // normal presentation rate this still completes in rotationSeconds.
            // Selection repeat retains its independent wall clock.
            float animationDeltaTime = deltaTime;
            if (deltaTime > .05f)
            {
                animationDeltaTime = 1f / 60f;
            }
            spinAge += animationDeltaTime;
            recoilAge += animationDeltaTime;
            if (pending.Count > 0 && !singleActive)
            {
                BeginStrike();
            }
            if (IsBarrage)
            {
                double now = Time.unscaledTimeAsDouble;
                bool changesSong = now >= nextSelectionAt;
                if (changesSong || now >= nextVisualAt)
                {
                    if (strikes.Play(barrageDirection, changesSong, true))
                    {
                        StrikeDirection = barrageDirection;
                        nextVisualAt = now + 1f / Mathf.Clamp(visualStrikesPerSecond, 8, 12);
                        if (changesSong)
                        {
                            nextSelectionAt = now + Mathf.Max(.15f, selectionRepeatSeconds);
                        }
                    }
                }
            }

            strikes.Tick(deltaTime);
            float hold = spinSeconds * .0515f;
            float landing = spinSeconds * .1765f;
            float spin = Mathf.Clamp01((spinAge - hold) / (spinSeconds - hold - landing));
            float yaw = spinStartYaw + spinTravel * DojoMotion.Travel(spin);
            float recoil = Mathf.Exp(-recoilAge * 30);
            float settle = Mathf.Clamp01((spinAge - spinSeconds + landing) / landing);
            float rebound = Mathf.Sin(settle * Mathf.PI * 2) * (1 - settle) * .38f;
            rig.Pose(yaw, StrikeDirection * recoil + spinDirection * rebound);
            ApplySongInformation();
            float flash = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.04f, .18f, recoilAge));
            impact.alpha = flash;
            var impactRect = (RectTransform)impact.transform;
            impactRect.anchoredPosition = impactPosition;
            float spread = 1f - Mathf.Pow(1f - Mathf.Clamp01(recoilAge / .065f), 3);
            impactRect.localScale = new Vector3(StrikeDirection * (.45f + spread * .7f), .7f + spread * .3f, 1);
            impactRect.localRotation = Quaternion.Euler(0, 0, -StrikeDirection * (12 + spread * 14));
            information.anchoredPosition = informationHome + new Vector2(StrikeDirection * recoil * 3, 0);
            room.anchoredPosition = BackgroundDestination + new Vector2(-StrikeDirection * recoil * 2, recoil);

            if (singleActive && !awaitingContact && spinAge >= spinSeconds)
            {
                singleActive = false;
            }
            if (IsBarrage || pending.Count > 0 || strikes.IsAnimating)
            {
                return;
            }
            if (spinAge >= spinSeconds && !singleActive)
            {
                IsStriking = false;
                RestorePose();
                Settled?.Invoke();
            }
        }

        private void StrikeContact(int direction, bool changesSong, bool rapid)
        {
            ContactCount++;
            StrikeDirection = direction;
            recoilAge = 0;
            impactPosition = impact.transform.parent.InverseTransformPoint(
                strikes.transform.TransformPoint(strikes.LastContactPosition));
            effects.pitch = 1 + (strikes.LastContactMotion - 1) * .08f;
            var preferences = PresentationPreferences.Current;
            if (preferences != null)
            {
                contactVolume = .65f * preferences.gameplayVolume;
            }
            effects.volume = contactVolume;
            // One authored source bounds concurrent voices, even throughout a long barrage.
            effects.Play();
            if (!changesSong)
            {
                if (spinAge >= spinSeconds)
                {
                    BeginRotation(direction, true);
                }
                return;
            }
            SelectionContactCount++;
            if (!rapid)
            {
                awaitingContact = false;
            }
            BeginRotation(direction, rapid);
            // Commit the selection on contact, then reveal its artwork only behind the body.
            rig.HoldDisplay();
            Turned?.Invoke(direction);
        }

        private void BeginRotation(int direction, bool rapid)
        {
            spinAge = 0;
            spinSeconds = Mathf.Max(.1f, rotationSeconds);
            if (rapid)
            {
                spinSeconds = Mathf.Clamp(barrageRotationSeconds, .05f, selectionRepeatSeconds);
            }
            spinDirection = direction;
            spinStartYaw = rig.Yaw;
            float remaining = Mathf.Repeat(direction * spinStartYaw, 360);
            if (remaining < 45)
            {
                remaining += 360;
            }
            spinTravel = -direction * remaining;
        }

        private void RestorePose()
        {
            rig.ReleaseDisplay();
            ApplySongInformation();
            rig.Pose(0, 0);
            age = spinAge = recoilAge = 1;
            spinSeconds = .17f;
            spinStartYaw = spinTravel = 0;
            awaitingContact = false;
            strikes.ResetPresentation();
            hands.alpha = impact.alpha = 0;
            information.anchoredPosition = informationHome;
            room.anchoredPosition = BackgroundDestination;
        }

        public void SetApproach(float amount)
        {
            float scale = Mathf.Lerp(1, CameraScale, amount);
            // The distant room advances less than the dummy, giving the 2D push-in parallax.
            room.localScale = Vector3.one * Mathf.Lerp(1, BackgroundScale, amount);
            room.anchoredPosition = BackgroundDestination * amount;
            dummy.localScale = Vector3.one * (.3f * scale);
            dummy.anchoredPosition = DummyPosition * scale + CameraDestination * amount;
            float reveal = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.65f, 1, amount));
            interfaceGroup.alpha = reveal;
            hands.alpha = 0;
            if (IsStriking)
            {
                hands.alpha = reveal;
            }
            rig.jacketGroup.alpha = reveal;
        }

        public IEnumerator Leave()
        {
            IsLeaving = true;
            IsStriking = IsBarrage = singleActive = false;
            pending.Clear();
            RestorePose();
            interfaceGroup.interactable = interfaceGroup.blocksRaycasts = false;
            float elapsed = 0;
            while (elapsed < ApproachSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetApproach(1f - Mathf.SmoothStep(0, 1, elapsed / ApproachSeconds));
                yield return null;
            }
            SetApproach(0);
        }

        private void OnDisable()
        {
            pending.Clear();
            IsStriking = IsBarrage = singleActive = false;
            if (strikes)
            {
                strikes.Contact -= StrikeContact;
            }
            if (captured)
            {
                RestorePose();
            }
        }
    }
}
