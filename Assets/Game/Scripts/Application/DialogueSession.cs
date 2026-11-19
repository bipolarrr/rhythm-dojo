using System;
using System.Collections.Generic;
using System.Globalization;
using RhythmDojo.Dialogue;

namespace RhythmDojo.Application
{
    public interface IDialogueReadHistory
    {
        bool IsRead(string key);
        void MarkRead(string key);
    }

    public sealed class DialogueSession
    {
        private readonly DialogueLine[] lines;
        private readonly IDialogueReadHistory history;
        private int[] characters;
        private double elapsed;
        private int visible;

        public int Index { get; private set; }
        public int Count => lines.Length;
        public bool IsComplete => Index == Count;
        public bool IsLineComplete => !IsComplete && visible == characters.Length;
        public bool CanSkipRead => !IsComplete && history.IsRead(Current.ReadKey);
        public DialogueLine Current
        {
            get
            {
                if (IsComplete)
                {
                    return null;
                }
                return lines[Index];
            }
        }

        public string VisibleText
        {
            get
            {
                if (IsComplete)
                {
                    return "";
                }
                if (IsLineComplete)
                {
                    return Current.Text;
                }
                return Current.Text.Substring(0, characters[visible]);
            }
        }

        public DialogueSession(IReadOnlyList<DialogueLine> lines, IDialogueReadHistory history)
        {
            if (lines == null || lines.Count == 0)
            {
                throw new ArgumentException("A dialogue needs at least one line.", nameof(lines));
            }

            this.history = history ?? throw new ArgumentNullException(nameof(history));
            this.lines = new DialogueLine[lines.Count];
            for (int i = 0; i < lines.Count; i++)
            {
                this.lines[i] = lines[i] ?? throw new ArgumentException("Dialogue line missing.");
            }
            PrepareLine();
        }

        public void Tick(double seconds)
        {
            if (IsComplete || IsLineComplete)
            {
                return;
            }
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }

            elapsed += seconds;
            visible = (int)Math.Min(characters.Length, Math.Floor(elapsed * 32));
            if (IsLineComplete)
            {
                history.MarkRead(Current.ReadKey);
            }
        }

        public void Confirm()
        {
            if (IsComplete)
            {
                return;
            }
            if (!IsLineComplete)
            {
                visible = characters.Length;
                history.MarkRead(Current.ReadKey);
                return;
            }

            MoveNext();
        }

        public void SkipRead()
        {
            while (CanSkipRead)
            {
                MoveNext();
            }
        }

        private void MoveNext()
        {
            Index++;
            if (!IsComplete)
            {
                PrepareLine();
            }
        }

        private void PrepareLine()
        {
            characters = StringInfo.ParseCombiningCharacters(Current.Text);
            elapsed = 0;
            visible = 0;
        }
    }
}
