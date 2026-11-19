using System;

namespace RhythmDojo.Dialogue
{
    public enum DialogueExpression
    {
        Calm,
        Playful,
        Serious,
        Confident,
        Exasperated,
        Teaching,
        Praising,
    }

    public enum DialogueMotion
    {
        Breathe,
        Nod,
        Lean,
    }

    public sealed class DialogueLine
    {
        public string ReadKey { get; }
        public string Speaker { get; }
        public string Text { get; }
        public DialogueExpression Expression { get; }
        public DialogueMotion Motion { get; }

        public DialogueLine(string readKey, string speaker, string text,
            DialogueExpression expression, DialogueMotion motion)
        {
            if (string.IsNullOrWhiteSpace(readKey) || string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Dialogue identity and text are required.");
            }
            if (string.IsNullOrWhiteSpace(speaker))
            {
                throw new ArgumentException("Dialogue speaker is required.");
            }
            if (!Enum.IsDefined(typeof(DialogueExpression), expression))
            {
                throw new ArgumentOutOfRangeException(nameof(expression));
            }
            if (!Enum.IsDefined(typeof(DialogueMotion), motion))
            {
                throw new ArgumentOutOfRangeException(nameof(motion));
            }

            ReadKey = readKey;
            Speaker = speaker;
            Text = text;
            Expression = expression;
            Motion = motion;
        }
    }
}
