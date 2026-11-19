using System;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using UnityEngine;

namespace RhythmDojo.Presentation
{
    public sealed class NoteView : MonoBehaviour
    {
        [SerializeField]
        private Transform head;

        [SerializeField]
        private Transform body;

        [SerializeField]
        private SpriteRenderer headSkin;

        [SerializeField]
        private SpriteRenderer tailMarker;

        public bool HasBody => body;

        public void Validate(NoteKind kind)
        {
            if (!head || kind == NoteKind.Hold && !body)
            {
                throw new InvalidOperationException($"Missing head/body on {name}.");
            }

            if (!headSkin || !headSkin.sprite)
            {
                throw new InvalidOperationException($"Missing authored head skin on {name}.");
            }

            if (kind == NoteKind.Hold && !tailMarker)
            {
                throw new InvalidOperationException($"Missing authored hold end marker on {name}.");
            }
        }

        public void SetMaterial(Material material)
        {
            if (body)
            {
                body.GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        public void Render(
            NoteData note,
            NoteState state,
            double songTime,
            IScrollTimeline scroll,
            GameModeDefinition mode,
            GameplayPresentationSettings settings
        )
        {
            bool visible = state == NoteState.Pending || state == NoteState.Holding;

            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            float start;

            if (state == NoteState.Holding)
            {
                start = 0;
            }
            else
            {
                start = (float)scroll.DistanceBetween(songTime, note.StartTime);
            }

            float tail = (float)scroll.DistanceBetween(songTime, note.EndTime);
            float x = mode.LaneX(note.Lane);
            head.position = new Vector3(x, settings.headHeight, start);
            head.localScale = Vector3.one * settings.headSize.x;

            if (body)
            {
                body.gameObject.SetActive(note.Kind == NoteKind.Hold);
            }

            if (tailMarker)
            {
                tailMarker.enabled = note.Kind == NoteKind.Hold && tail > start + settings.minimumBodyLength;
            }

            if (note.Kind == NoteKind.Hold)
            {
                float length = Mathf.Max(0, tail - start);
                body.position = new Vector3(x, settings.bodyElevation, start + length * .5f);
                body.localScale = new Vector3(
                    settings.bodyWidth,
                    settings.bodyHeight,
                    Mathf.Max(settings.minimumBodyLength, length)
                );

                RenderTailMarker(x, Mathf.Max(start, tail), settings);
            }
        }

        private void RenderTailMarker(float x, float tail, GameplayPresentationSettings settings)
        {
            var markerTransform = tailMarker.transform;
            markerTransform.position = new Vector3(x, settings.bodyElevation + settings.bodyHeight * .5f, tail);
            markerTransform.localScale = new Vector3(settings.headSize.x * .78f, settings.headSize.x * .61f, 1);
        }

    }
}
