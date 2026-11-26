using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class TrainingPlaybackLayoutBuilder
    {
        public static TrainingPlaybackView Build(Transform parent)
        {
            var root = Rect("Training Briefing and Feedback", parent, Vector2.zero, Vector2.one);
            var view = root.gameObject.AddComponent<TrainingPlaybackView>();
            var blocker = root.gameObject.AddComponent<Image>();
            blocker.color = new Color(.035f, .045f, .04f, .15f);
            BuildTraining(view);
            var dialogueView = DialogueLayoutBuilder.Build(root);
            var dialogue = dialogueView.gameObject.AddComponent<DialogueScreen>();
            SceneDependencyAssembler.Wire(dialogue, "view", dialogueView);
            view.dialogue = dialogue;
            var dialogueAudio = new GameObject("Dialogue Audio", typeof(AudioSource));
            dialogueAudio.transform.SetParent(dialogueView.transform, false);
            dialogueView.source = dialogueAudio.GetComponent<AudioSource>();
            dialogueView.source.playOnAwake = false;
            dialogueView.source.spatialBlend = 0;
            dialogueView.turnSound = SceneResources.Require<AudioClip>("Assets/Game/Audio/Foley/ResultsStatistic.wav");
            dialogueView.arrivalSound = SceneResources.Require<AudioClip>("Assets/Game/Audio/Foley/ResultsBrush.wav");
            view.Validate();
            root.gameObject.SetActive(false);
            return view;
        }

        private static void BuildTraining(TrainingPlaybackView view)
        {
            var panel = Rect("Training", view.transform, new Vector2(.035f, .055f), new Vector2(.655f, .64f));
            view.trainingPanel = panel.gameObject;
            var sheet = panel.gameObject.AddComponent<DialoguePaperGraphic>();
            sheet.paperTexture = SceneResources.Require<Texture2D>("Assets/Game/Art/DojoGameplay/PaperPanel.png");
            sheet.color = new Color(1, .98f, .92f);
            sheet.raycastTarget = false;
            var rule = Rect("Ink Rule", panel, new Vector2(.06f, .78f), new Vector2(.94f, .792f))
                .gameObject.AddComponent<DojoInkGraphic>();
            rule.color = new Color(.26f, .19f, .10f, .65f);
            rule.raycastTarget = false;
            view.lessonTitle = TrainingLabel("Lesson Title", panel, .79f, .98f, 32);
            view.progress = TrainingLabel("Progress", panel, .80f, .97f, 18);
            view.progress.alignment = TextAnchor.MiddleRight;
            view.progress.rectTransform.anchorMin = new Vector2(.77f, .80f);
            view.progress.color = new Color(.52f, .18f, .12f);
            view.instructions = TrainingLabel("Instructions", panel, .40f, .76f, 21);
            view.feedback = TrainingLabel("Feedback", panel, .18f, .40f, 20);
            view.startButton = UiElements.Button("Start Training", panel, "수련 시작", Vector2.zero, Vector2.zero);
            var rect = (RectTransform)view.startButton.transform;
            rect.anchorMin = new Vector2(.51f, .035f);
            rect.anchorMax = new Vector2(.88f, .17f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            view.startLabel = view.startButton.GetComponentInChildren<Text>();
            view.startLabel.fontSize = 22;
            view.startLabel.color = new Color(1, .96f, .84f);
            view.startButton.image.color = new Color(.46f, .15f, .10f);
            view.backButton = UiElements.Button("Choose Training", panel, "〈  수련 선택", Vector2.zero, Vector2.zero);
            var back = (RectTransform)view.backButton.transform;
            back.anchorMin = new Vector2(.12f, .035f);
            back.anchorMax = new Vector2(.47f, .17f);
            back.offsetMin = back.offsetMax = Vector2.zero;
            view.backLabel = view.backButton.GetComponentInChildren<Text>();
            view.backLabel.fontSize = 21;
            view.backLabel.color = new Color(1, .96f, .84f);
            view.backButton.image.color = new Color(.18f, .23f, .22f);
            view.startButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = view.backButton };
            view.backButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = view.startButton };
        }

        private static Text TrainingLabel(string name, Transform parent, float bottom, float top, int size)
        {
            var label = UiElements.Label(name, parent, "", Vector2.zero, Vector2.zero, size);
            label.rectTransform.anchorMin = new Vector2(.06f, bottom);
            label.rectTransform.anchorMax = new Vector2(.94f, top);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = new Color(.15f, .105f, .065f);
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 minimum, Vector2 maximum)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

    }
}
