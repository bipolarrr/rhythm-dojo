using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.EditorTools;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.TestTools;
using TestMode = UnityEditor.TestTools.TestRunner.Api.TestMode;

namespace RhythmDojo.Tests
{
    [Category("Fast")]
    public sealed class VerificationProfileTests
    {
        [Test]
        public void FeatureProfilesIntersectCostsAndRejectInvalidClassification()
        {
            foreach (string cost in VerificationProfile.Costs)
            {
                var tags = new[] { "UI", "Gameplay", "Smoke", cost, "Compatibility" };
                Assert.That(VerificationProfile.Includes("All", tags), Is.True);
                Assert.That(VerificationProfile.Includes(cost, tags), Is.True);
                Assert.That(VerificationProfile.Includes("Authoring", tags), Is.False);
                Assert.That(VerificationProfile.Includes("UI", tags),
                    Is.EqualTo(cost == "Fast" || cost == "SceneIntegration"));
            }

            Assert.That(VerificationProfile.Passed("Passed", 1, 1, 0, 0, 0), Is.True);
            Assert.That(VerificationProfile.Passed("Passed", 0, 0, 0, 0, 0), Is.False);
            Assert.That(VerificationProfile.Passed("Passed", 2, 1, 0, 1, 0), Is.False);
            Assert.That(VerificationProfile.Passed("Passed", 2, 1, 0, 0, 1), Is.False);
            Assert.That(VerificationProfile.Passed("Failed", 2, 1, 1, 0, 0), Is.False);
            Assert.That(VerificationProfile.Passed("Passed", 2, 1, 0, 0, 0), Is.False);

            Assert.Throws<InvalidOperationException>(() => VerificationProfile.Includes("All", new[] { "UI" }));
            Assert.Throws<InvalidOperationException>(() => VerificationProfile.Includes("All", new[] { "Fast", "Hardware" }));
            Assert.Throws<ArgumentException>(() => VerificationProfile.Includes("Unknown", new[] { "Fast" }));
        }

        [UnityTest]
        public IEnumerator ActualInventoryHasOneCostPerCaseAndProfilesPartitionAllTests()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            try
            {
                foreach (var mode in new[] { TestMode.EditMode, TestMode.PlayMode })
                {
                    ITestAdaptor root = null;
                    api.RetrieveTestList(mode, tests => root = tests);
                    double deadline = UnityEditor.EditorApplication.timeSinceStartup + 30;
                    while (root == null)
                    {
                        Assert.That(UnityEditor.EditorApplication.timeSinceStartup, Is.LessThan(deadline),
                            "Test inventory discovery timed out: " + mode);
                        yield return null;
                    }

                    var assemblies = new[] { "Game.Domain.Tests", "Game.EditMode.Tests", "Game.PlayMode.Tests" };
                    var all = VerificationProfile.Select(root, "All", assemblies);
                    Assert.That(all, Is.Not.Empty, mode.ToString());
                    var partition = VerificationProfile.Costs.SelectMany(cost =>
                        VerificationProfile.Select(root, cost, assemblies)).ToArray();
                    Assert.That(partition, Is.EquivalentTo(all));
                    Assert.That(partition.Distinct().Count(), Is.EqualTo(partition.Length));
                    var slow = new[] { "Hardware", "Endurance", "AssetGeneration" }
                        .SelectMany(cost => VerificationProfile.Select(root, cost, assemblies)).ToArray();
                    foreach (string profile in new[] { "UI", "Gameplay", "Authoring", "Smoke" })
                    {
                        Assert.That(VerificationProfile.Select(root, profile, assemblies).Intersect(slow), Is.Empty);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(api);
            }
        }
    }
}
