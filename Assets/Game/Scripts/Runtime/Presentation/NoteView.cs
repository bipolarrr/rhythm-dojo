using System;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;

namespace RhythmDojo.Presentation
{
    public sealed class NoteView : MonoBehaviour
    {
        [SerializeField] private RectTransform head;
        [SerializeField] private RectTransform body;
        [SerializeField] private RectTransform tailMarker;

        public bool HasBody => body;

        public void Validate(NoteKind kind)
        {
            if (!HasArtwork(head))
            {
                throw new InvalidOperationException($"Missing note head on {name}.");
            }
            if (kind == NoteKind.Hold && (!HasArtwork(body) || !HasArtwork(tailMarker)))
            {
                throw new InvalidOperationException($"Missing hold artwork on {name}.");
            }
        }

        private static bool HasArtwork(RectTransform part)
        {
            return part && part.TryGetComponent<UnityEngine.UI.Image>(out var image) && image.sprite;
        }

        public void Render(NoteData note, NoteState state, double songTime, IScrollTimeline scroll,
            GameModeDefinition mode, GameplayPresentationSettings settings)
        {
            if (state != NoteState.Pending && state != NoteState.Holding)
            {
                if (gameObject.activeSelf)
                {
                    gameObject.SetActive(false);
                }
                return;
            }

            double start = 0;
            if (state != NoteState.Holding)
            {
                start = scroll.DistanceBetween(songTime, note.StartTime);
            }

            var point = GameplayViewport.Position(note.Lane, mode.LaneCount, start);
            float tail = point.y;
            if (body)
            {
                double distance = scroll.DistanceBetween(songTime, note.EndTime);
                tail = Mathf.Max(point.y, GameplayViewport.Position(note.Lane, mode.LaneCount, distance).y);
            }

            bool visible = point.y <= GameplayViewport.Height + 4 && tail >= -4;
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
            if (!visible)
            {
                return;
            }

            head.anchoredPosition = point;
            if (!body)
            {
                return;
            }

            // Clip before tiling so an arbitrarily long hold uses only a screenful of quads.
            float bottom = Mathf.Max(-4, point.y);
            float top = Mathf.Clamp(tail, bottom, GameplayViewport.Height + 4);
            body.anchoredPosition = new Vector2(point.x, bottom);
            body.sizeDelta = new Vector2(body.sizeDelta.x, top - bottom);
            bool showEnd = tail > point.y + 2;
            if (tailMarker.gameObject.activeSelf != showEnd)
            {
                tailMarker.gameObject.SetActive(showEnd);
            }
            tailMarker.anchoredPosition = new Vector2(point.x, tail);
        }
    }
}
