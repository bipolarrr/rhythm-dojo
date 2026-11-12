using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Registered upper-body silhouettes turn around the authored vertical bearing.
    // The feet, lower column and bearing never receive an animated transform.
    public sealed class WoodenDummyRig : MonoBehaviour
    {
        [Serializable]
        public sealed class RotationFrame
        {
            public Sprite sprite;
            public Vector2 panelPosition, panelSize, leftContact, rightContact;
            public Vector2 leftKickContact, rightKickContact;
            public float panelTilt;
        }

        public RectTransform body, head;
        public RectTransform fixedBase, rotationJoint;
        public Image side, front, rear, jacket;
        public CanvasGroup jacketGroup;
        public Text title, artist;
        public RotationFrame[] frames;
        public Vector2 bodyHome;

        public float Yaw { get; private set; }
        public int FrameIndex { get; private set; }
        public bool FrontVisible => front.gameObject.activeSelf;
        public bool HoldingDisplay { get; private set; }

        private Sprite pendingJacket;
        private string pendingTitle, pendingArtist;
        private bool pendingDisplay;

        public void Validate()
        {
            if (!fixedBase || !rotationJoint)
            {
                throw new InvalidOperationException("Wooden dummy fixed base and rotation joint missing.");
            }
            if (!body || !head || !side)
            {
                throw new InvalidOperationException("Wooden dummy body and head composition missing.");
            }
            if (!front || !rear)
            {
                throw new InvalidOperationException("Wooden dummy rotating faces missing.");
            }
            if (!jacket || !jacketGroup)
            {
                throw new InvalidOperationException("Wooden dummy jacket composition missing.");
            }
            if (!title || !artist)
            {
                throw new InvalidOperationException("Wooden dummy display labels missing.");
            }
            if (frames == null || frames.Length != 8)
            {
                throw new InvalidOperationException("Wooden dummy requires eight registered upper-body rotation frames.");
            }
            foreach (var frame in frames)
            {
                if (frame == null || !frame.sprite)
                {
                    throw new InvalidOperationException("Wooden dummy rotation frame missing.");
                }
            }
        }

        public void Pose(float yaw, float recoil)
        {
            Yaw = Mathf.Repeat(yaw + 180, 360) - 180;
            FrameIndex = Mathf.RoundToInt(Mathf.Repeat(yaw + recoil * 3, 360) / 45) % frames.Length;
            var frame = frames[FrameIndex];
            bool facing = FrameIndex <= 1 || FrameIndex == 7;
            bool behind = FrameIndex >= 3 && FrameIndex <= 5;
            front.gameObject.SetActive(facing);
            rear.gameObject.SetActive(behind);
            side.gameObject.SetActive(!facing && !behind);
            Image image = side;
            if (facing)
            {
                image = front;
            }
            else if (behind)
            {
                image = rear;
            }
            image.sprite = frame.sprite;
            image.rectTransform.pivot = frame.sprite.pivot / frame.sprite.rect.size;

            head.gameObject.SetActive(facing);
            head.anchoredPosition = frame.panelPosition;
            head.localScale = new Vector3(frame.panelSize.x / head.sizeDelta.x,
                frame.panelSize.y / head.sizeDelta.y, 1);
            head.localRotation = Quaternion.Euler(0, 0, frame.panelTilt);
            if (!facing)
            {
                ReleaseDisplay();
            }

            // Recoil twists about the bearing, without displacing or tilting its connection.
            body.anchoredPosition = bodyHome;
            body.localRotation = Quaternion.identity;
            body.localScale = Vector3.one;
        }

        public Vector2 ContactPoint(int direction, RectTransform relativeTo)
        {
            return ContactPoint(direction, relativeTo, false);
        }

        public Vector2 ContactPoint(int direction, RectTransform relativeTo, bool kick)
        {
            var point = frames[FrameIndex].rightContact;
            if (direction < 0)
            {
                point = frames[FrameIndex].leftContact;
            }
            if (kick)
            {
                point = frames[FrameIndex].rightKickContact;
                if (direction < 0)
                {
                    point = frames[FrameIndex].leftKickContact;
                }
            }
            return relativeTo.InverseTransformPoint(body.TransformPoint(point));
        }

        public void HoldDisplay()
        {
            HoldingDisplay = true;
        }

        public void SetSong(Sprite sprite, string songTitle, string songArtist)
        {
            pendingJacket = sprite;
            pendingTitle = songTitle;
            pendingArtist = songArtist;
            pendingDisplay = true;
            if (!HoldingDisplay || !FrontVisible)
            {
                ReleaseDisplay();
            }
        }

        public void ReleaseDisplay()
        {
            HoldingDisplay = false;
            if (!pendingDisplay)
            {
                return;
            }
            pendingDisplay = false;
            jacket.sprite = pendingJacket;
            title.text = pendingTitle;
            artist.text = pendingArtist;
            if (pendingJacket)
            {
                jacket.GetComponent<AspectRatioFitter>().aspectRatio =
                    pendingJacket.rect.width / pendingJacket.rect.height;
            }
        }
    }
}
