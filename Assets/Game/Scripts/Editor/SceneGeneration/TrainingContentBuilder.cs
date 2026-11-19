using RhythmDojo.Content;

namespace RhythmDojo.EditorTools
{
    public static class TrainingContentBuilder
    {
        public const string LessonPath = "Assets/Game/Settings/Training/Trill.asset";

        public static TrainingLesson Prepare()
        {
            return TrillContentBuilder.Prepare();
        }

        public static TrainingLesson[] PrepareAll()
        {
            var lesson = Prepare();
            var additions = PatternTrainingContentBuilder.Prepare(lesson.Song.AudioClip);
            var lessons = new TrainingLesson[additions.Length + 1];
            lessons[0] = lesson;
            additions.CopyTo(lessons, 1);
            return lessons;
        }
    }
}
