using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Fixed stage composition is authored by AlphaDojoLayoutBuilder. Only poses change here.
    public sealed class DojoModeView : MonoBehaviour
    {
        public CanvasGroup[] stages;
        public RectTransform[] props;
        public RectTransform[] companions;
        public RectTransform[] banners;
        public RectTransform[] characterMarks;
        public Image[] lessonMarks;
        public Text lessonCue;
        public Image character;
        public DojoFigureGraphic traineeMotion, teacherMotion, sageMotion;
        public DojoActorAnimation traineeAnimation, teacherAnimation;
        public DojoLessonSequence lessonSequence = new DojoLessonSequence();
        public Sprite[] teacherPoses;
        public DojoIdleVariation traineeIdle = new DojoIdleVariation();
        public DojoIdleVariation teacherIdle = new DojoIdleVariation();
        public DojoIdleVariation sageIdle = new DojoIdleVariation();
        [Min(0)] public float idleSeparation = .45f;
        public DojoFigureGraphic[] scrollMotion;
        public Sprite[] poses;
        public Text heading, description, detail, counter, actionLabel;
        public Button previous, next;
        public Button practiceTarget;
        public AudioSource effects;
        public AudioClip contact;
        public RectTransform roomRoot, travelCover, teacherGesture, sageGesture;
        public RectTransform[] farLayers, middleLayers, nearLayers;
        public DojoWindFlag summitFlag;
        public CanvasGroup[] interfaceGroups;
        public CanvasGroup scribingLight;
        public Image canvasBackdrop;
        public Sprite trainingBackdrop;
        public DojoModeTypography[] typography;
        public Image[] selectionMarks;
        public RectTransform readyMark;
        [Range(.45f, .72f)] public float titleArrival = .62f;
        [Range(.08f, .3f)] public float titleExit = .16f;
        public AudioClip footstep, wind;
        public bool IsArriving { get; private set; }
        public float TravelProgress { get; private set; }
        public const float TravelSeconds = .82f;
        public int SelectedIndex { get; private set; } = 1;
        public bool IsMoving { get; private set; }
        public bool InputEnabled
        {
            get => inputEnabled;
            set
            {
                if (inputEnabled == value)
                {
                    return;
                }
                inputEnabled = value;
                if (!value)
                {
                    CancelIdleVariations();
                }
            }
        }
        public bool CanEnter => SelectedIndex >= 0 && SelectedIndex < stages.Length;
        public int LessonHits { get; private set; }
        public int ContactCount { get; private set; }
        private float age;
        private float reaction;
        private float strikeAge = -1f;
        private bool contactMade;
        private float instructionAge = -1f;
        private const float StrikeContactSeconds = .22f;
        private const float StrikeSeconds = .54f;
        private const float ReactionSeconds = .85f;
        private bool struck;
        private bool lesson;
        private int lessonBeat = -1;
        private int cueBeat = -1;
        private Vector2 characterHome;
        private Color backdropColor;
        private Vector2[] propHomes;
        private Vector2[] companionHomes;
        private int previousIndex;
        private int footfall;
        private float arrivalAge;
        private bool entranceArrived;
        private bool arrivalRun;
        private bool inputEnabled;
        private bool idleLocked;
        private float idleGap;
        private bool awaitingResponse;
        private bool practiceContactMade;
        private float feedbackAge = -1;
        private bool lessonCorrect;
        private readonly string[] titles = { "비급 전수", "자유 연습", "실력 검정", "율보 편찬" };
        private readonly string[] descriptions = {
            "처음이지? 내가 옆에서 박자를 짚어 줄게.", "자유롭게 연습하자!",
            "하산할 준비가 되었다고 생각하나?", "속세에서는 그것을 에디터라 부른다더라" };
        private readonly string[] details = {
            "선배 사형의 손짓에 맞춰 네 번 두드려 보세요.",
            "목인 클릭 · Space로 가볍게 몸을 풀고, 곡을 골라 수련하세요.",
            "흔들림 없는 박자로, 수련의 깊이를 증명하세요.",
            "곡을 불러오고, 네 줄 위에 나만의 채보를 새기세요." };

        public void Validate()
        {
            if (!traineeAnimation || !teacherAnimation)
            {
                throw new InvalidOperationException("Dojo frame animations missing.");
            }
            traineeAnimation.Validate();
            teacherAnimation.Validate();
            traineeIdle.Validate();
            teacherIdle.Validate();
            sageIdle.Validate();
            if (teacherPoses == null || teacherPoses.Length != 6)
            {
                throw new InvalidOperationException("Dojo senior teaching poses missing.");
            }
            foreach (var pose in teacherPoses)
            {
                if (!pose)
                {
                    throw new InvalidOperationException("Dojo senior teaching pose missing.");
                }
            }
            if (!traineeMotion || !teacherMotion || !sageMotion)
            {
                throw new InvalidOperationException("Dojo articulated figures missing.");
            }
            if (scrollMotion == null || scrollMotion.Length != 4)
            {
                throw new InvalidOperationException("Dojo paper motion composition missing.");
            }
            if (!scrollMotion[1])
            {
                throw new InvalidOperationException("Dojo paper motion reference missing.");
            }
            if (typography == null || selectionMarks == null || !readyMark)
            {
                throw new InvalidOperationException("Dojo arcade interface composition missing.");
            }
            if (typography.Length != 4 || selectionMarks.Length != 4)
            {
                throw new InvalidOperationException("Dojo arcade mode count invalid.");
            }
            if (!scribingLight)
            {
                throw new InvalidOperationException("Dojo manuscript lamplight missing.");
            }
            if (!canvasBackdrop || !trainingBackdrop)
            {
                throw new InvalidOperationException("Dojo viewport backdrop missing.");
            }
            for (int i = 0; i < typography.Length; i++)
            {
                if (!typography[i] || !selectionMarks[i])
                {
                    throw new InvalidOperationException("Dojo arcade mode reference missing.");
                }
                typography[i].Validate();
            }
            if (!roomRoot || !travelCover || !teacherGesture)
            {
                throw new InvalidOperationException("Dojo spatial animation composition missing.");
            }
            if (!sageGesture || !summitFlag || interfaceGroups == null)
            {
                throw new InvalidOperationException("Dojo arrival composition missing.");
            }
            if (!summitFlag.cloth || interfaceGroups.Length != 8)
            {
                throw new InvalidOperationException("Dojo arrival artwork or controls missing.");
            }
            foreach (var group in interfaceGroups)
            {
                if (!group)
                {
                    throw new InvalidOperationException("Dojo interface animation reference missing.");
                }
            }
            if (farLayers == null || middleLayers == null || nearLayers == null)
            {
                throw new InvalidOperationException("Dojo depth layers missing.");
            }
            if (farLayers.Length != 4 || middleLayers.Length != 4 || nearLayers.Length != 4)
            {
                throw new InvalidOperationException("Dojo depth layer count invalid.");
            }
            if (!character || poses == null)
            {
                throw new InvalidOperationException("Dojo trainee poses missing.");
            }
            if (poses.Length != 4)
            {
                throw new InvalidOperationException("Dojo trainee poses missing.");
            }
            if (stages == null || props == null || companions == null)
            {
                throw new InvalidOperationException("Dojo stage composition missing.");
            }
            if (stages.Length != 4 || props.Length != 4 || companions.Length != 4)
            {
                throw new InvalidOperationException("Dojo stage composition missing.");
            }
            if (characterMarks == null || banners == null || lessonMarks == null)
            {
                throw new InvalidOperationException("Dojo authored motion references missing.");
            }
            if (characterMarks.Length != 4 || banners.Length != 4 || lessonMarks.Length != 4)
            {
                throw new InvalidOperationException("Dojo authored motion references missing.");
            }
            for (int i = 0; i < 4; i++)
            {
                if (!farLayers[i] || !middleLayers[i] || !nearLayers[i])
                {
                    throw new InvalidOperationException("Dojo depth layer reference missing.");
                }
                if (!stages[i])
                {
                    throw new InvalidOperationException("Dojo stage reference missing.");
                }
                if (i != 3 && (!props[i] || !companions[i]))
                {
                    throw new InvalidOperationException("Dojo training figure reference missing.");
                }
                if (!characterMarks[i] || !lessonMarks[i])
                {
                    throw new InvalidOperationException("Dojo authored motion reference missing.");
                }
                if (i == 1)
                {
                    if (!banners[i])
                    {
                        throw new InvalidOperationException("Dojo authored motion reference missing.");
                    }
                }
                if (!poses[i])
                {
                    throw new InvalidOperationException("Dojo trainee pose missing.");
                }
            }
            if (!lessonCue || !counter)
            {
                throw new InvalidOperationException("Dojo lesson labels missing.");
            }
            if (!heading || !description || !detail)
            {
                throw new InvalidOperationException("Dojo mode labels missing.");
            }
            if (!previous || !next || !actionLabel)
            {
                throw new InvalidOperationException("Dojo mode controls missing.");
            }
            if (!practiceTarget)
            {
                throw new InvalidOperationException("Dojo practice target interaction missing.");
            }
        }

        public void ResetPresentation()
        {
            Validate();
            StopAllCoroutines();
            if (propHomes == null)
            {
                backdropColor = canvasBackdrop.color;
                propHomes = new Vector2[4];
                companionHomes = new Vector2[4];
                for (int i = 0; i < 4; i++)
                {
                    if (props[i])
                    {
                        propHomes[i] = props[i].anchoredPosition;
                    }
                    if (companions[i])
                    {
                        companionHomes[i] = companions[i].anchoredPosition;
                    }
                }
            }
            IsMoving = false;
            IsArriving = false;
            entranceArrived = false;
            TravelProgress = 0f;
            ContactCount = 0;
            lesson = false;
            LessonHits = 0;
            SelectedIndex = 1;
            characterHome = characterMarks[SelectedIndex].anchoredPosition;
            character.rectTransform.anchoredPosition = characterHome;
            character.rectTransform.localScale = Vector3.one;
            character.rectTransform.localRotation = Quaternion.identity;
            UpdateLessonMarks();
            ShowStage();
        }

        public void Move(int direction)
        {
            if (!InputEnabled || IsMoving || direction == 0)
            {
                return;
            }
            StartCoroutine(Travel(Math.Sign(direction)));
        }

        private IEnumerator Travel(int direction)
        {
            IsMoving = true;
            IsArriving = false;
            lesson = false;
            ResetFigureMotion();
            float elapsed = 0f;
            previousIndex = SelectedIndex;
            SelectedIndex = (SelectedIndex + direction + 4) % 4;
            character.enabled = previousIndex != 3 && SelectedIndex != 3;
            var destination = characterMarks[SelectedIndex].anchoredPosition;
            character.rectTransform.localScale = new Vector3(direction, 1, 1);
            character.rectTransform.localRotation = Quaternion.identity;
            character.sprite = poses[1];
            stages[SelectedIndex].transform.SetAsLastSibling();
            character.transform.SetAsLastSibling();
            travelCover.SetAsLastSibling();
            stages[SelectedIndex].alpha = 1f;
            footfall = -1;
            travelCover.gameObject.SetActive(true);
            bool labelsChanged = false;
            typography[SelectedIndex].SetRest(false);
            while (elapsed < TravelSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / TravelSeconds);
                TravelProgress = t;
                float run = Mathf.InverseLerp(.12f, .88f, t);
                float blend = run * run * run * (run * (run * 6f - 15f) + 10f);
                SetTravelLayers(previousIndex, -direction * blend);
                SetTravelLayers(SelectedIndex, direction * (1f - blend));
                travelCover.anchoredPosition = new Vector2(direction * Mathf.Lerp(1000, -1000, blend), 0);

                // Distance, pose cadence, footfalls and scenery share the same eased travel curve.
                float stride = blend * 6f;
                int step = Mathf.FloorToInt(stride);
                character.sprite = poses[1 + step % 2];
                var position = Vector2.Lerp(characterHome, destination, blend);
                position.x += direction * Mathf.Sin(blend * Mathf.PI) * 52f;
                position.y += Mathf.Abs(Mathf.Sin(stride * Mathf.PI)) * 7f;
                if (t < .12f)
                {
                    character.sprite = poses[0];
                    position.x -= direction * Mathf.Sin(t / .12f * Mathf.PI) * 9f;
                    position.y -= Mathf.Sin(t / .12f * Mathf.PI) * 4f;
                }
                if (t > .88f)
                {
                    character.sprite = poses[0];
                    position.x += direction * Mathf.Sin((t - .88f) / .12f * Mathf.PI) * 8f;
                }
                character.rectTransform.anchoredPosition = position;
                character.rectTransform.localRotation = Quaternion.Euler(0, 0, -direction * Mathf.Sin(run * Mathf.PI) * 5f);
                if (step != footfall && run > 0f && run < 1f)
                {
                    footfall = step;
                    if (character.enabled)
                    {
                        PlayEffect(footstep, .10f);
                    }
                }
                typography[previousIndex].Exit(t / titleExit, direction);
                if (t >= titleArrival)
                {
                    if (!labelsChanged)
                    {
                        UpdateLabels();
                        labelsChanged = true;
                    }
                    typography[SelectedIndex].Enter((t - titleArrival) * TravelSeconds, direction);
                }
                readyMark.localScale = new Vector3(Mathf.InverseLerp(titleArrival, 1f, t), 1, 1);
                yield return null;
            }
            character.rectTransform.localScale = Vector3.one;
            characterHome = destination;
            character.rectTransform.anchoredPosition = characterHome;
            ShowStage();
            IsMoving = false;
            BeginArrival();
        }

        private void SetTravelLayers(int index, float distance)
        {
            ((RectTransform)stages[index].transform).anchoredPosition = Vector2.zero;
            farLayers[index].anchoredPosition = new Vector2(distance * 1280f, 0);
            middleLayers[index].anchoredPosition = new Vector2(distance * 1480f, 0);
            nearLayers[index].anchoredPosition = new Vector2(distance * 1880f, 0);
        }

        public void CancelMotion()
        {
            StopAllCoroutines();
            if (IsMoving)
            {
                SelectedIndex = previousIndex;
            }
            IsMoving = false;
            IsArriving = false;
            lesson = false;
            if (propHomes == null)
            {
                return;
            }
            characterHome = characterMarks[SelectedIndex].anchoredPosition;
            character.rectTransform.anchoredPosition = characterHome;
            character.rectTransform.localScale = Vector3.one;
            character.rectTransform.localRotation = Quaternion.identity;
            ShowStage();
        }

        public void SetEntranceProgress(float amount)
        {
            float approach = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.43f, 1f, amount));
            roomRoot.localScale = Vector3.one * Mathf.Lerp(.87f, 1f, approach);
            roomRoot.anchoredPosition = Vector2.down * (28f * (1f - approach));
            foreach (var layer in farLayers)
            {
                layer.localScale = Vector3.one * Mathf.Lerp(1.16f, 1.04f, approach);
            }
            float reveal = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.78f, 1f, amount));
            foreach (var group in interfaceGroups)
            {
                group.alpha = reveal;
                group.blocksRaycasts = amount >= 1f;
            }
            if (amount < 1f)
            {
                entranceArrived = false;
                character.color = new Color(1, 1, 1, reveal);
                if (propHomes != null)
                {
                    character.rectTransform.anchoredPosition = characterHome + Vector2.left * 36;
                }
                return;
            }
            character.color = Color.white;
            if (!entranceArrived && propHomes != null)
            {
                entranceArrived = true;
                BeginArrival(true);
            }
        }

        private void BeginArrival(bool runIn = false)
        {
            arrivalAge = 0f;
            arrivalRun = runIn;
            IsArriving = true;
            struck = false;
            character.rectTransform.localScale = Vector3.one;
            character.rectTransform.localRotation = Quaternion.identity;
            if (runIn)
            {
                typography[SelectedIndex].Enter(0, 1);
            }
            if (SelectedIndex == 2)
            {
                PlayEffect(wind, .24f);
            }
        }

        private void PlayEffect(AudioClip clip, float volume)
        {
            if (effects && clip)
            {
                effects.PlayOneShot(clip, volume);
            }
        }

        private void ShowStage()
        {
            age = 0;
            ResetFigureMotion();
            struck = false;
            travelCover.gameObject.SetActive(false);
            teacherGesture.localRotation = Quaternion.identity;
            sageGesture.localRotation = Quaternion.identity;
            for (int i = 0; i < stages.Length; i++)
            {
                stages[i].alpha = 0;
                if (i == SelectedIndex)
                {
                    stages[i].alpha = 1;
                }
                ((RectTransform)stages[i].transform).anchoredPosition = Vector2.zero;
                SetTravelLayers(i, 0f);
                stages[i].blocksRaycasts = i == SelectedIndex;
                if (props[i])
                {
                    props[i].localRotation = Quaternion.identity;
                    props[i].anchoredPosition = propHomes[i];
                }
                if (companions[i])
                {
                    companions[i].anchoredPosition = companionHomes[i];
                    companions[i].localRotation = Quaternion.identity;
                }
                if (banners[i])
                {
                    banners[i].localRotation = Quaternion.identity;
                }
                typography[i].SetRest(i == SelectedIndex);
            }
            character.sprite = poses[0];
            character.enabled = SelectedIndex != 3;
            readyMark.localScale = Vector3.one;
            UpdateLabels();
            lessonCue.text = "내 손을 보고, 천천히 따라 해 봐.";
        }

        private void UpdateLabels()
        {
            canvasBackdrop.sprite = trainingBackdrop;
            canvasBackdrop.color = backdropColor;
            if (SelectedIndex == 3)
            {
                // Frame the close-up with ink-dark margins on non-widescreen displays.
                // Enlarging a second copy behind it would duplicate the sage and desk.
                canvasBackdrop.sprite = null;
                canvasBackdrop.color = new Color(.035f, .055f, .065f);
            }
            heading = typography[SelectedIndex].heading;
            description = typography[SelectedIndex].description;
            detail = typography[SelectedIndex].detail;
            heading.text = titles[SelectedIndex];
            if (SelectedIndex == 1)
            {
                heading.text = heading.text.Replace(" ", "\n");
            }
            description.text = descriptions[SelectedIndex];
            detail.text = details[SelectedIndex];
            counter.text = $"0{SelectedIndex + 1} / 04";
            actionLabel.text = "수련 시작";
            if (SelectedIndex == 0)
            {
                actionLabel.text = "박자 배우기";
            }
            else if (SelectedIndex == 3)
            {
                actionLabel.text = "편찬 시작";
            }
            for (int i = 0; i < selectionMarks.Length; i++)
            {
                selectionMarks[i].color = new Color(1, .94f, .8f, .38f);
                selectionMarks[i].rectTransform.localScale = Vector3.one;
                if (i == SelectedIndex)
                {
                    selectionMarks[i].color = new Color(1, .65f, .34f);
                    selectionMarks[i].rectTransform.localScale = Vector3.one * 1.5f;
                }
            }
            readyMark.gameObject.SetActive(CanEnter);
        }

        public void Practice()
        {
            if (!InputEnabled || IsMoving || SelectedIndex != 0)
            {
                return;
            }
            IsArriving = false;
            CancelIdleVariations();
            if (!lesson)
            {
                strikeAge = -1f;
                instructionAge = -1f;
            }
            typography[SelectedIndex].SetRest(true);
            teacherGesture.localRotation = Quaternion.identity;
            if (!lesson)
            {
                lesson = true;
                age = 0;
                LessonHits = 0;
                UpdateLessonMarks();
                lessonBeat = -1;
                cueBeat = -1;
                actionLabel.text = "두드리기";
                detail.text = "사형의 손짓에 Space 또는 버튼!  0 / 4";
                return;
            }
            int beat = Mathf.FloorToInt(age / .75f);
            float phase = age % .75f;
            if (beat == lessonBeat)
            {
                return;
            }
            if (strikeAge >= 0f)
            {
                return;
            }
            lessonBeat = beat;
            Strike();
            feedbackAge = 0;
            lessonCorrect = phase < .19f || phase > .56f;
            if (lessonCorrect)
            {
                LessonHits++;
                UpdateLessonMarks();
                detail.text = $"좋아, 그 박자야. 자세도 잊지 마!  {LessonHits} / 4";
            }
            else
            {
                detail.text = "어깨에 힘 빼고, 내 손짓에 맞춰 다시 해 봐.";
            }
            if (LessonHits >= 4)
            {
                lesson = false;
                detail.text = "기초 수련 완료! → 자유 연습에서 곡을 골라 보세요.";
                actionLabel.text = "다시 배우기";
                lessonCue.text = "잘했다! 이제 혼자서도 해볼 수 있겠네.";
            }
        }

        private void UpdateLessonMarks()
        {
            for (int i = 0; i < lessonMarks.Length; i++)
            {
                lessonMarks[i].color = new Color(.52f, .38f, .24f);
                if (i < LessonHits)
                {
                    lessonMarks[i].color = new Color(1, .85f, .4f);
                }
                lessonMarks[i].rectTransform.localScale = Vector3.one;
            }
        }

        private void ResetFigureMotion()
        {
            CancelIdleVariations();
            feedbackAge = -1;
            reaction = 0f;
            strikeAge = -1f;
            contactMade = false;
            instructionAge = -1f;
            traineeMotion.Rest();
            teacherMotion.Rest();
            sageMotion.Rest();
            foreach (var scroll in scrollMotion)
            {
                if (scroll)
                {
                    scroll.Rest();
                }
            }
        }

        private void Strike()
        {
            if (strikeAge >= 0f)
            {
                return;
            }
            strikeAge = 0f;
            CancelIdleVariations();
            contactMade = false;
        }

        public void PracticeTarget()
        {
            if (!InputEnabled || IsMoving || IsArriving)
            {
                return;
            }
            if (SelectedIndex != 1)
            {
                return;
            }
            Strike();
        }

        private void Update()
        {
            if (!InputEnabled || IsMoving || propHomes == null)
            {
                return;
            }
            age += Time.unscaledDeltaTime;
            reaction = Mathf.Max(0, reaction - Time.unscaledDeltaTime);
            float readyPulse = Mathf.Sin(age * 24f) * Mathf.Exp(-age * 7f);
            readyMark.localScale = new Vector3(1f + readyPulse * .08f, 1f + readyPulse * .3f, 1);
            character.sprite = poses[0];
            character.rectTransform.localRotation = Quaternion.identity;
            character.rectTransform.anchoredPosition = characterHome;
            character.rectTransform.localScale = Vector3.one;
            UpdateStandingPose();
            UpdateIdleVariations();
            if (lesson)
            {
                UpdateLesson();
            }
            if (IsArriving)
            {
                UpdateArrival();
            }
            UpdateStrike();
            UpdatePropReaction();
            UpdateInstruction();
            UpdateLessonFeedback();
        }

        private void UpdateStandingPose()
        {
            float breath = Mathf.Sin(age * 1.65f) * .75f;
            float weight = Mathf.Sin(age * .72f) * .65f;
            float focus = 0;
            float breeze = .12f * Mathf.Sin(age * 1.1f - .4f);
            float windStrength = DojoAmbientMotion.Wind(Time.unscaledTime);
            switch (SelectedIndex)
            {
                case 0:
                    breath = Mathf.Sin(age * 1.8f) * .42f;
                    weight = Mathf.Sin(age * .63f) * .18f;
                    focus = .35f;
                    break;
                case 2:
                    breath = Mathf.Sin(age * 1.35f) * .3f;
                    weight = Mathf.Sin(age * .52f) * .10f;
                    focus = .55f;
                    breeze = windStrength;
                    sageMotion.SetPose(breath, 0, 0, breeze, 0);
                    summitFlag.Animate(Time.unscaledTime, windStrength);
                    break;
                case 3:
                    breath *= .5f;
                    weight = 0;
                    scribingLight.alpha = .78f + Mathf.Sin(age * 2.7f) * .12f
                        + Mathf.Sin(age * 6.1f) * .07f;
                    break;
            }
            traineeMotion.SetPose(breath, weight, focus, breeze, 0);
            if (scrollMotion[SelectedIndex])
            {
                scrollMotion[SelectedIndex].SetPose(0, 0, 0, breeze, 0);
            }
            if (SelectedIndex == 0)
            {
                teacherMotion.SetPose(breath * .4f, 0, 0, breeze, 0);
                teacherAnimation.Sample(DojoIdleVariation.Motion.None, age + .37f);
            }
            traineeAnimation.Sample(DojoIdleVariation.Motion.None, age);
        }

        private void CancelIdleVariations()
        {
            lessonSequence.Cancel();
            if (traineeAnimation)
            {
                traineeAnimation.Stop();
            }
            if (teacherAnimation)
            {
                teacherAnimation.Stop();
            }
            traineeIdle.Cancel();
            teacherIdle.Cancel();
            sageIdle.Cancel();
            idleGap = idleSeparation;
            awaitingResponse = false;
            practiceContactMade = false;
            if (traineeMotion)
            {
                traineeMotion.SetVariation(DojoIdleVariation.Motion.None, 0, 0, SelectedIndex);
            }
            if (teacherMotion)
            {
                teacherMotion.SetVariation(DojoIdleVariation.Motion.None, 0, 0, SelectedIndex);
                if (teacherPoses != null && teacherPoses.Length > 0)
                {
                    teacherMotion.sprite = teacherPoses[0];
                }
            }
            if (sageMotion)
            {
                sageMotion.SetVariation(DojoIdleVariation.Motion.None, 0, 0, SelectedIndex);
            }
        }

        private void UpdateIdleVariations()
        {
            bool locked = IsArriving || lesson;
            locked |= strikeAge >= 0;
            locked |= instructionAge >= 0 || feedbackAge >= 0;
            if (locked)
            {
                if (!idleLocked)
                {
                    CancelIdleVariations();
                }
                idleLocked = true;
                return;
            }
            idleLocked = false;
            float seconds = Time.unscaledDeltaTime;
            if (SelectedIndex == 0)
            {
                lessonSequence.Advance(seconds, age, teacherIdle, teacherAnimation, traineeAnimation);
                awaitingResponse = lessonSequence.Phase == DojoLessonSequence.Step.Delay;
                return;
            }
            teacherAnimation.Stop();
            if (SelectedIndex == 3)
            {
                traineeAnimation.Sample(DojoIdleVariation.Motion.None, age);
                return;
            }
            idleGap = Mathf.Max(0, idleGap - seconds);
            DojoIdleVariation companion = null;
            DojoFigureGraphic graphic = null;
            if (SelectedIndex == 2)
            {
                companion = sageIdle;
                graphic = sageMotion;
            }
            bool wasPlaying = traineeIdle.IsPlaying;
            if (companion != null)
            {
                wasPlaying |= companion.IsPlaying;
                bool wasTeaching = companion.IsPlaying;
                bool canBegin = !traineeIdle.IsPlaying && idleGap <= 0;
                companion.Advance(seconds, SelectedIndex, canBegin && !awaitingResponse);
                ApplyIdle(companion, graphic);
                if (wasTeaching && !companion.IsPlaying)
                {
                    idleGap = idleSeparation;
                }
                if (companion.IsPlaying)
                {
                    traineeIdle.Advance(seconds, SelectedIndex, false);
                    ApplyIdle(traineeIdle, traineeMotion);
                    return;
                }
            }
            traineeIdle.Advance(seconds, SelectedIndex, idleGap <= 0 && !wasPlaying);
            ApplyIdle(traineeIdle, traineeMotion);
            UpdatePracticeContact();
            if (wasPlaying && !traineeIdle.IsPlaying)
            {
                idleGap = idleSeparation;
            }
        }

        private void ApplyIdle(DojoIdleVariation idle, DojoFigureGraphic graphic)
        {
            if (graphic == traineeMotion)
            {
                if (idle.IsPlaying)
                {
                    traineeAnimation.Sample(idle.Current.motion, idle.Progress * idle.Current.Duration);
                }
                else
                {
                    traineeAnimation.Sample(DojoIdleVariation.Motion.None, age);
                }
                return;
            }
            graphic.SetVariation(DojoIdleVariation.Motion.None, 0, 0, SelectedIndex);
            if (graphic == teacherMotion)
            {
                graphic.sprite = teacherPoses[0];
            }
            if (!idle.IsPlaying)
            {
                return;
            }
            graphic.SetVariation(idle.Current.motion, idle.Strength, idle.Progress, SelectedIndex);
            if (idle.Current.sprite && idle.Strength > .15f)
            {
                graphic.sprite = idle.Current.sprite;
            }
        }

        private void UpdatePracticeContact()
        {
            if (!traineeIdle.IsPlaying)
            {
                practiceContactMade = false;
                return;
            }
            if (SelectedIndex != 1 || practiceContactMade)
            {
                return;
            }
            var motion = traineeIdle.Current.motion;
            if (motion != DojoIdleVariation.Motion.Punch && motion != DojoIdleVariation.Motion.Kick)
            {
                return;
            }
            if (traineeIdle.Progress < .36f)
            {
                return;
            }
            // The authored practice mark is beside the wooden target. Recoil starts on the
            // extension frame; it must not cancel the remainder of the training clip.
            practiceContactMade = true;
            ContactCount++;
            reaction = ReactionSeconds;
            PlayEffect(contact, .14f);
        }

        private void UpdateLessonFeedback()
        {
            if (feedbackAge < 0)
            {
                return;
            }
            feedbackAge += Time.unscaledDeltaTime;
            if (feedbackAge < .24f)
            {
                return;
            }
            teacherMotion.sprite = teacherPoses[4];
            if (lessonCorrect)
            {
                teacherMotion.sprite = teacherPoses[3];
            }
            teacherMotion.SetPose(0, 0, 0, .1f, Gesture(feedbackAge - .24f, .55f));
            if (lessonCorrect)
            {
                teacherAnimation.Sample(DojoIdleVariation.Motion.Approve, feedbackAge - .24f);
            }
            if (feedbackAge >= .8f)
            {
                feedbackAge = -1;
                teacherMotion.sprite = teacherPoses[0];
            }
        }

        private void UpdateLesson()
        {
            float phase = age % .75f;
            lessonCue.text = "듣고…";
            if (phase < .19f || phase > .56f)
            {
                lessonCue.text = "지금!  톡";
            }
            if (LessonHits < lessonMarks.Length)
            {
                float pulse = 1 + Mathf.Max(0, 1 - phase / .19f) * .16f;
                lessonMarks[LessonHits].rectTransform.localScale = Vector3.one * pulse;
            }
            int beat = Mathf.FloorToInt(age / .75f);
            if (beat != cueBeat)
            {
                cueBeat = beat;
                instructionAge = 0f;
                PlayEffect(contact, .16f);
            }
        }

        private void UpdateInstruction()
        {
            if (SelectedIndex != 0 || instructionAge < 0f)
            {
                return;
            }
            instructionAge += Time.unscaledDeltaTime;
            teacherMotion.sprite = teacherPoses[2];
            teacherMotion.SetPose(0, 0, 0, .1f, Gesture(instructionAge, .46f));
            teacherAnimation.Sample(DojoIdleVariation.Motion.Point,
                instructionAge / .46f * teacherAnimation.Duration(DojoIdleVariation.Motion.Point));
            if (instructionAge >= .46f)
            {
                instructionAge = -1f;
                teacherMotion.sprite = teacherPoses[0];
            }
        }

        private void UpdateStrike()
        {
            if (strikeAge < 0f)
            {
                return;
            }
            strikeAge += Time.unscaledDeltaTime;
            float t = strikeAge;
            float reach = 36f;
            float weight;
            if (t < .12f)
            {
                weight = -3f * Gesture(t, .24f);
            }
            else if (t < StrikeContactSeconds)
            {
                character.sprite = poses[3];
                weight = Mathf.Lerp(-3f, reach, Mathf.SmoothStep(0, 1, (t - .12f) / .10f));
            }
            else
            {
                if (!contactMade)
                {
                    contactMade = true;
                    ContactCount++;
                    reaction = ReactionSeconds;
                    PlayEffect(contact, .22f);
                }
                float recovery = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.27f, StrikeSeconds, t));
                weight = reach * (1 - recovery);
                if (t < .40f)
                {
                    character.sprite = poses[3];
                }
            }
            traineeMotion.SetPose(0, weight, 0, -weight * .08f, 0);
            traineeAnimation.Sample(DojoIdleVariation.Motion.Punch,
                t / StrikeSeconds * traineeAnimation.Duration(DojoIdleVariation.Motion.Punch));
            if (t >= StrikeSeconds)
            {
                strikeAge = -1f;
                character.sprite = poses[0];
            }
        }

        private void UpdatePropReaction()
        {
            if (!props[SelectedIndex])
            {
                return;
            }

            float recoil = 0f;
            if (reaction > 0f)
            {
                // The heavy target follows contact by 25 ms, overshoots once and comes to rest.
                float sinceContact = Mathf.Max(0, ReactionSeconds - reaction - .025f);
                float settle = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.55f, .80f, sinceContact));
                recoil = Mathf.Sin(sinceContact * 20f) * Mathf.Exp(-sinceContact * 6f) * 7f * settle;
                if (ContactCount % 2 == 0)
                {
                    // A firmer contact has a slower, deeper recoil; both settle at the same deadline.
                    recoil = Mathf.Sin(sinceContact * 17f) * Mathf.Exp(-sinceContact * 5f) * 9f * settle;
                }
            }
            props[SelectedIndex].localRotation = Quaternion.Euler(0, 0, -recoil);
        }

        private static float Gesture(float time, float duration)
        {
            if (time <= 0 || time >= duration)
            {
                return 0;
            }
            float t = time / duration;
            if (t < .32f)
            {
                return Mathf.SmoothStep(0, 1, t / .32f);
            }
            return 1 - Mathf.SmoothStep(0, 1, (t - .32f) / .68f);
        }

        private void UpdateArrival()
        {
            arrivalAge += Time.unscaledDeltaTime;
            float t = arrivalAge;
            if (arrivalRun)
            {
                typography[SelectedIndex].Enter(t, 1);
            }
            switch (SelectedIndex)
            {
                case 0:
                    // The senior points first, then the trainee answers; the target reacts at contact.
                    teacherMotion.sprite = teacherPoses[2];
                    float gesture = Gesture(t, .48f);
                    teacherMotion.SetPose(0, 0, 0, .1f, gesture);
                    lessonCue.text = "자, 한 박자에 한 동작!";
                    if (t > .25f)
                    {
                        ArrivalStrike(t - .25f);
                    }
                    if (t > .61f)
                    {
                        float pulse = Mathf.Max(0, 1 - (t - .61f) / .35f);
                        foreach (var mark in lessonMarks)
                        {
                            mark.rectTransform.localScale = Vector3.one * (1 + pulse * .12f);
                        }
                    }
                    break;
                case 1:
                    if (arrivalRun && t < .16f)
                    {
                        character.sprite = poses[1 + Mathf.FloorToInt(t * 16) % 2];
                        character.rectTransform.anchoredPosition = characterHome
                            + Vector2.left * (36 * (1 - Mathf.SmoothStep(0, 1, t / .16f)));
                    }
                    else
                    {
                        float strikeTime = t;
                        if (arrivalRun)
                        {
                            strikeTime -= .16f;
                        }
                        ArrivalStrike(strikeTime);
                    }
                    break;
                case 2:
                    float resolve = Gesture(t, .86f);
                    traineeMotion.SetPose(0, -resolve * 1.2f, .6f, DojoAmbientMotion.Wind(Time.unscaledTime), 0);
                    sageMotion.SetPose(0, 0, 0, DojoAmbientMotion.Wind(Time.unscaledTime),
                        Gesture(t - .28f, .67f));
                    break;
            }
            if (t >= 1.05f)
            {
                IsArriving = false;
                teacherGesture.localRotation = Quaternion.identity;
                sageGesture.localRotation = Quaternion.identity;
                character.rectTransform.localRotation = Quaternion.identity;
                if (SelectedIndex == 0)
                {
                    teacherMotion.sprite = teacherPoses[0];
                    lessonCue.text = "내 손을 보고, 천천히 따라 해 봐.";
                }
            }
        }

        private void ArrivalStrike(float t)
        {
            if (t < 0f || struck)
            {
                return;
            }
            struck = true;
            Strike();
        }

        private void OnDisable()
        {
            CancelMotion();
            InputEnabled = false;
        }
    }
}
