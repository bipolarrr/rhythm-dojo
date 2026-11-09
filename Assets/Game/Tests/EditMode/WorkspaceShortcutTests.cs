using System.Linq;
using NUnit.Framework;
using RhythmDojo.Authoring;
using UnityEngine.InputSystem;

namespace RhythmDojo.Tests
{
    [Category("Authoring")]
    public sealed class WorkspaceShortcutTests
    {
        [Test]
        [Category("Fast")]
        public void ShortcutChordsAndActionsAreUnique()
        {
            var chords = WorkspaceShortcuts.Bindings.Select(binding =>
                new { binding.Key, binding.Control, binding.Shift }).ToArray();
            Assert.That(chords.Distinct().Count(), Is.EqualTo(chords.Length));
            Assert.That(WorkspaceShortcuts.Bindings.Select(binding => binding.Action).Distinct().Count(),
                Is.EqualTo(WorkspaceShortcuts.Bindings.Count));
            foreach (var action in new[] { WorkspaceAction.Open, WorkspaceAction.ImportJacket,
                WorkspaceAction.ImportAudio, WorkspaceAction.EstimateBpm })
            {
                Assert.That(WorkspaceShortcuts.Bindings.Any(binding => binding.Action == action), Is.False, action.ToString());
                Assert.That(WorkspaceShortcuts.Label(action), Is.Empty, action.ToString());
            }
        }

        [TestCase(WorkspaceAction.Exit, Key.Escape, "Esc")]
        [TestCase(WorkspaceAction.Pencil, Key.P, "P")]
        [TestCase(WorkspaceAction.Stop, Key.Digit0, "0")]
        [Category("Fast")]
        public void RequestedChangesUseEscPAndZero(WorkspaceAction action, Key key, string label)
        {
            var binding = WorkspaceShortcuts.Bindings.Single(item => item.Action == action);
            Assert.That(binding.Key, Is.EqualTo(key));
            Assert.That(binding.Control, Is.False);
            Assert.That(binding.Shift, Is.False);
            Assert.That(WorkspaceShortcuts.Label(action), Is.EqualTo(label));
        }

    }
}
