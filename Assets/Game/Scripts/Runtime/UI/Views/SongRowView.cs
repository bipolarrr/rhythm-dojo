using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using RhythmDojo.Content;

namespace RhythmDojo.UI
{
    public sealed class SongRowView : MonoBehaviour, ISelectHandler
    {
        public Button select, favorite;
        public Image cover, background;
        public Text title, artist, bpm, difficulty, favoriteLabel;
        public SongEntry Entry { get; private set; }
        private Action<string> choose;
        public void Bind(SongEntry entry, Sprite fallback, string difficultyName, bool liked,
            Action<string> onChoose, Action<string> onFavorite)
        {
            Entry = entry; choose = onChoose;
            title.text = entry.Title; artist.text = entry.Artist;
            if (bpm) bpm.text = entry.MinBpm == entry.MaxBpm ? $"{entry.MinBpm:0.##}" : $"{entry.MinBpm:0.##}–{entry.MaxBpm:0.##}";
            difficulty.text = entry.Error == null ? difficultyName : "재생 불가";
            cover.sprite = entry.Cover ? entry.Cover : fallback;
            if (cover.sprite) cover.GetComponent<AspectRatioFitter>().aspectRatio = cover.sprite.rect.width / cover.sprite.rect.height;
            favoriteLabel.text = liked ? "★" : "☆";
            select.onClick.AddListener(() => choose(entry.Id));
            favorite.onClick.AddListener(() => onFavorite(entry.Id));
        }
        public void SetSelected(bool selected)
        {
            background.color = selected ? Color.white : new Color(.07f, .085f, .10f, .95f);
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