using System;
using System.Collections.Generic;
using RhythmDojo.Dialogue;
using UnityEngine;

namespace RhythmDojo.Content
{
    [CreateAssetMenu(menuName = "Rhythm Dojo/Dialogue Sequence")]
    public sealed class DialogueSequenceAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Line
        {
            public string id;
            public string speaker = "사형";
            [TextArea(2, 5)] public string text;
            public DialogueExpression expression;
            public DialogueMotion motion;
        }

        public string sequenceId;
        [Min(1)] public int version = 1;
        public Line[] lines;

        public DialogueLine[] CreateLines()
        {
            if (string.IsNullOrWhiteSpace(sequenceId) || version < 1)
            {
                throw new InvalidOperationException("Dialogue sequence identity/version missing.");
            }
            if (lines == null || lines.Length == 0)
            {
                throw new InvalidOperationException("Dialogue sequence is empty.");
            }

            var result = new DialogueLine[lines.Length];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line == null || string.IsNullOrWhiteSpace(line.id))
                {
                    throw new InvalidOperationException("Dialogue line identity missing.");
                }
                if (!ids.Add(line.id))
                {
                    throw new InvalidOperationException("Duplicate dialogue line: " + line.id);
                }

                // Changed wording must be read again, even if the author keeps the same version.
                string key = sequenceId + "/" + version + "/" + line.id + "/" + line.speaker + "/" + line.text;
                result[i] = new DialogueLine(key, line.speaker, line.text, line.expression, line.motion);
            }
            return result;
        }
    }
}
