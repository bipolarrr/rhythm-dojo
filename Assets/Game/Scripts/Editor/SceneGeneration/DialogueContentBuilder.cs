using RhythmDojo.Content;
using RhythmDojo.Dialogue;
using UnityEditor;
using static RhythmDojo.EditorTools.ContentAssets;

namespace RhythmDojo.EditorTools
{
    public static class DialogueContentBuilder
    {
        public static void Prepare(TrainingLesson lesson)
        {
            const string folder = "Assets/Game/Settings/Training/Dialogue";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets/Game/Settings/Training", "Dialogue");
            }
            var introduction = Sequence(folder + "/Introduction.asset", "basic-pulse-introduction",
                Line("welcome", "왔느냐? 오늘은 네 손놀림을 조금 살펴보마.", DialogueExpression.Calm, DialogueMotion.Nod),
                Line("teach", "기본부터 시작하지. 잘 보고 따라 해 보거라.", DialogueExpression.Teaching, DialogueMotion.Lean),
                Line("begin", "준비되었다면 시작하자.", DialogueExpression.Confident, DialogueMotion.Nod));
            var succeeded = Sequence(folder + "/Succeeded.asset", "basic-pulse-succeeded",
                Line("praise", "잘했다. 손끝에 박자가 제법 익었구나.", DialogueExpression.Praising, DialogueMotion.Nod),
                Line("finish", "오늘 배운 감각을 기억해 두거라. 수련은 여기까지다.", DialogueExpression.Calm, DialogueMotion.Breathe));
            var failed = Sequence(folder + "/Failed.asset", "basic-pulse-failed",
                Line("encourage", "서두를 것 없다. 박자를 놓쳐도 호흡부터 다시 고르거라.", DialogueExpression.Serious, DialogueMotion.Nod),
                Line("retry", "한 번 더 해보겠느냐? 이번에는 내가 지켜보마.", DialogueExpression.Playful, DialogueMotion.Lean));
            var aborted = Sequence(folder + "/Aborted.asset", "basic-pulse-aborted",
                Line("rest", "잠시 쉬어도 좋다. 마음이 준비되면 다시 시작하자.", DialogueExpression.Calm, DialogueMotion.Nod));

            var serialized = new SerializedObject(lesson);
            AssignMissing(serialized, "introductionDialogue", introduction);
            AssignMissing(serialized, "successDialogue", succeeded);
            AssignMissing(serialized, "failureDialogue", failed);
            AssignMissing(serialized, "abortedDialogue", aborted);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Dirty(lesson);
        }

        private static void AssignMissing(SerializedObject lesson, string name, DialogueSequenceAsset sequence)
        {
            var property = lesson.FindProperty(name);
            if (!property.objectReferenceValue)
            {
                property.objectReferenceValue = sequence;
            }
        }

        private static DialogueSequenceAsset Sequence(string path, string id, params DialogueSequenceAsset.Line[] lines)
        {
            var sequence = Asset<DialogueSequenceAsset>(path, out bool created);
            if (created)
            {
                sequence.sequenceId = id;
                sequence.lines = lines;
                Dirty(sequence);
            }
            sequence.CreateLines();
            return sequence;
        }

        private static DialogueSequenceAsset.Line Line(string id, string text,
            DialogueExpression expression, DialogueMotion motion)
        {
            return new DialogueSequenceAsset.Line { id = id, text = text, expression = expression, motion = motion };
        }
    }
}
