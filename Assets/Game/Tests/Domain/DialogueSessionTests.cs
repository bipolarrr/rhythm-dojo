using System.Collections.Generic;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Dialogue;

namespace RhythmDojo.Tests
{
    [Category("Dialogue")]
    [Category("Fast")]
    public sealed class DialogueSessionTests
    {
        private sealed class History : IDialogueReadHistory
        {
            private readonly HashSet<string> read = new HashSet<string>();
            public bool IsRead(string key) => read.Contains(key);
            public void MarkRead(string key) => read.Add(key);
        }

        private static DialogueLine Line(string id, string text = "기본부터 시작하지.")
        {
            return new DialogueLine(id, "사형", text, DialogueExpression.Teaching, DialogueMotion.Nod);
        }

        [Test]
        public void ConfirmCompletesCurrentLineBeforeAdvancingAndCompletionIsStable()
        {
            var history = new History();
            var session = new DialogueSession(new[] { Line("first"), Line("second") }, history);
            session.Tick(.04);
            Assert.That(session.VisibleText, Is.EqualTo("기"));
            Assert.That(history.IsRead("first"), Is.False);
            session.Confirm();
            Assert.That(session.IsLineComplete, Is.True);
            Assert.That(session.Index, Is.Zero);
            Assert.That(history.IsRead("first"), Is.True);
            session.Confirm();
            Assert.That(session.Index, Is.EqualTo(1));
            Assert.That(session.VisibleText, Is.Empty);
            session.Tick(10);
            Assert.That(history.IsRead("second"), Is.True);
            session.Confirm();
            session.Confirm();
            session.SkipRead();
            Assert.That(session.IsComplete, Is.True);
            Assert.That(session.Index, Is.EqualTo(2));
        }

        [Test]
        public void ReadSkipStopsAtFirstUnreadLineAndPartialDisplayIsNotRead()
        {
            var history = new History();
            history.MarkRead("one");
            history.MarkRead("three");
            var lines = new[] { Line("one"), Line("two"), Line("three") };
            var session = new DialogueSession(lines, history);
            session.SkipRead();
            Assert.That(session.Index, Is.EqualTo(1));
            session.Tick(.05);
            session.SkipRead();
            Assert.That(session.Index, Is.EqualTo(1));
            Assert.That(history.IsRead("two"), Is.False);
            var reopened = new DialogueSession(lines, history);
            reopened.SkipRead();
            Assert.That(reopened.Index, Is.EqualTo(1));
            reopened.Confirm();
            reopened.SkipRead();
            Assert.That(reopened.IsComplete, Is.True);
        }

        [Test]
        public void TypewriterKeepsCombiningCharactersAndSurrogatePairsTogether()
        {
            var session = new DialogueSession(new[] { Line("unicode", "a\u0301😀가") }, new History());
            session.Tick(.032);
            Assert.That(session.VisibleText, Is.EqualTo("a\u0301"));
            session.Tick(.032);
            Assert.That(session.VisibleText, Is.EqualTo("a\u0301😀"));
            session.Tick(.032);
            Assert.That(session.IsLineComplete, Is.True);
        }
    }
}
