using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class TrainingPlaybackLayoutBuilder
    {
        public static TrainingPlaybackView Build(Transform parent)
        {
            var root = Rect("Training Mentor", parent, new Vector2(.72f, .19f), new Vector2(.97f, .80f));
            var view = root.gameObject.AddComponent<TrainingPlaybackView>();
            var ink = root.gameObject.AddComponent<Image>();
            ink.color = new Color(.035f, .025f, .02f, .72f);
            ink.raycastTarget = false;
            view.heading = Label("Step", root, .84f, 1, 24);
            view.instruction = Label("Instruction", root, .68f, .84f, 20);
            view.pulse = Label("Pulse", root, .02f, .12f, 19);
            var figure = Rect("Senior", root, new Vector2(.12f, .22f), new Vector2(.88f, .69f));
            var artwork = figure.gameObject.AddComponent<Image>();
            artwork.sprite = SceneResources.Require<Sprite>("Assets/Game/Art/AlphaDojo/Training/SeniorFrame0.asset");
            artwork.preserveAspect = true;
            artwork.raycastTarget = false;
            var actor = figure.gameObject.AddComponent<DojoActorAnimation>();
            actor.artwork = artwork;
            actor.animator = figure.gameObject.AddComponent<Animator>();
            actor.animator.runtimeAnimatorController = SceneResources.Require<RuntimeAnimatorController>(
                "Assets/Game/Art/AlphaDojo/Training/Senior.controller");
            actor.poses = new[]
            {
                new DojoActorAnimation.Pose
                {
                    motion = DojoIdleVariation.Motion.None,
                    clip = SceneResources.Require<AnimationClip>("Assets/Game/Art/AlphaDojo/Training/SeniorIdle.anim"),
                },
                new DojoActorAnimation.Pose
                {
                    motion = DojoIdleVariation.Motion.Punch,
                    clip = SceneResources.Require<AnimationClip>("Assets/Game/Art/AlphaDojo/Training/SeniorPunch.anim"),
                },
            };
            view.actor = actor;
            view.keys = new Text[4];
            for (int i = 0; i < view.keys.Length; i++)
            {
                var key = Label("Lane " + (i + 1), root, .12f, .28f, 22);
                key.rectTransform.anchorMin = new Vector2(.04f + i * .23f, .12f);
                key.rectTransform.anchorMax = new Vector2(.25f + i * .23f, .28f);
                view.keys[i] = key;
            }
            view.Validate();
            root.gameObject.SetActive(false);
            return view;
        }

        private static Text Label(string name, Transform parent, float bottom, float top, int size)
        {
            var label = UiElements.Label(name, parent, "", Vector2.zero, Vector2.zero, size);
            label.rectTransform.anchorMin = new Vector2(.035f, bottom);
            label.rectTransform.anchorMax = new Vector2(.965f, top);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1, .95f, .82f);
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
