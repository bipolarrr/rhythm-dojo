using System;
using RhythmDojo.Content;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class SongRowView : MonoBehaviour, ISelectHandler
    {
        public Button select,
            favorite;
        public Image cover,
            background;
        public Text title,
            artist,
            bpm,
            difficulty,
            favoriteLabel;

        public bool pixelTheme;
        public Sprite legacyFallbackCover;
        public Text indexLabel;

        public SongEntry Entry { get; private set; }

        private Action<string> choose;

        public void Bind(
            SongEntry entry,
            Sprite fallback,
            string difficultyName,
            bool liked,
            Action<string> onChoose,
            Action<string> onFavorite
        )
        {
            Entry = entry;
            choose = onChoose;
            title.text = entry.Title;
            artist.text = entry.Artist;

            if (bpm)
            {
                if (entry.MinBpm == entry.MaxBpm)
                {
                    bpm.text = $"{entry.MinBpm:0.##}";
                }
                else
                {
                    bpm.text = $"{entry.MinBpm:0.##}–{entry.MaxBpm:0.##}";
                }
            }

            if (entry.Error == null)
            {
                difficulty.text = difficultyName;
            }
            else
            {
                difficulty.text = "재생 불가";
            }

            if (entry.Cover && entry.Cover != legacyFallbackCover)
            {
                cover.sprite = entry.Cover;
            }
            else
            {
                cover.sprite = fallback;
            }

            if (cover.sprite)
            {
                cover.GetComponent<AspectRatioFitter>().aspectRatio =
                    cover.sprite.rect.width / cover.sprite.rect.height;
            }

            if (liked)
            {
                favoriteLabel.text = "★";
            }
            else
            {
                favoriteLabel.text = "☆";
            }

            select.onClick.AddListener(() => choose(entry.Id));
            favorite.onClick.AddListener(() => onFavorite(entry.Id));
        }

        public void SetSelected(bool selected)
        {
            if (pixelTheme)
            {
                background.color = Color.clear;
                var frame = GetComponentInChildren<PixelDojoFrameGraphic>();
                if (selected)
                {
                    frame.color = new Color(1f, .88f, .62f);
                    frame.borderColor = new Color(.72f, .30f, .12f);
                    frame.trimColor = new Color(1f, .88f, .52f);
                    title.color = new Color(1f, .94f, .77f);
                }
                else
                {
                    frame.color = new Color(.74f, .65f, .53f);
                    frame.borderColor = new Color(.33f, .24f, .18f);
                    frame.trimColor = new Color(.59f, .46f, .34f);
                    title.color = new Color(.88f, .80f, .68f);
                }
                frame.SetVerticesDirty();
                return;
            }

            if (selected)
            {
                background.color = Color.white;
            }
            else
            {
                background.color = new Color(.07f, .085f, .10f, .95f);
            }

            if (background is SelectionGradientImage gradient)
            {
                gradient.useGradient = selected;
                gradient.leftColor = new Color(.40f, .07f, .10f);
                gradient.rightColor = new Color(.13f, .065f, .08f);
                gradient.SetVerticesDirty();
            }
        }

        public void OnSelect(BaseEventData eventData) => choose?.Invoke(Entry.Id);
    }
}
