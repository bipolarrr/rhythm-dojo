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

        private static Sprite songpanSkin;
        private SpriteRenderer headSkin;
        private MeshRenderer tailMarker;

        public bool HasBody => body;

        public void Validate(NoteKind kind)
        {
            if (!head || kind == NoteKind.Hold && !body)
            {
                throw new InvalidOperationException($"Missing head/body on {name}.");
            }
        }

        public void SetMaterial(Material material)
        {
            head.GetComponent<Renderer>().sharedMaterial = material;

            if (body)
            {
                body.GetComponent<Renderer>().sharedMaterial = material;
            }

            if (tailMarker)
            {
                tailMarker.sharedMaterial = material;
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
            if (UnityEngine.Application.isPlaying)
            {
                if (!headSkin)
                {
                    CreateHeadSkin();
                }

                head.localScale = Vector3.one * settings.headSize.x;
            }
            else
            {
                head.localScale = settings.headSize;
            }

            if (body)
            {
                body.gameObject.SetActive(note.Kind == NoteKind.Hold);
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

                if (UnityEngine.Application.isPlaying)
                {
                    RenderTailMarker(x, Mathf.Max(start, tail), settings);
                }
            }
        }

        private void RenderTailMarker(float x, float tail, GameplayPresentationSettings settings)
        {
            if (!tailMarker)
            {
                var marker = new GameObject("Hold End Marker", typeof(MeshFilter), typeof(MeshRenderer));
                marker.layer = body.gameObject.layer;
                marker.transform.SetParent(transform, false);
                marker.GetComponent<MeshFilter>().sharedMesh = body.GetComponent<MeshFilter>().sharedMesh;
                tailMarker = marker.GetComponent<MeshRenderer>();
                tailMarker.sharedMaterial = body.GetComponent<Renderer>().sharedMaterial;
            }

            var markerTransform = tailMarker.transform;
            markerTransform.position = new Vector3(x, settings.bodyElevation + settings.bodyHeight * .5f, tail);
            markerTransform.localScale = new Vector3(settings.headSize.x, settings.bodyHeight, settings.headSize.z * .45f);
        }

        private void CreateHeadSkin()
        {
            if (!songpanSkin)
            {
                songpanSkin = Resources.Load<Sprite>("UI/SongpanNote");
                if (!songpanSkin)
                {
                    throw new InvalidOperationException("Songpan note sprite missing.");
                }
            }

            var skin = new GameObject("Songpan Skin", typeof(SpriteRenderer));
            skin.layer = head.gameObject.layer;
            skin.transform.SetParent(head, false);
            // Match the playfield camera's tilt so the flat sprite reads at the hit line.
            skin.transform.localRotation = Quaternion.Euler(28f, 0, 0);
            headSkin = skin.GetComponent<SpriteRenderer>();
            headSkin.sprite = songpanSkin;
            head.GetComponent<Renderer>().enabled = false;
        }
    }
}
