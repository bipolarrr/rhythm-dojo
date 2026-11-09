using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.TestTools.TestRunner.Api;

namespace RhythmDojo.EditorTools
{
    // Feature and cost are independent axes. TestRunner category arrays use OR, so
    // resolve the intersection to exact leaf names before handing it to the runner.
    public static class VerificationProfile
    {
        public static readonly IReadOnlyList<string> Costs = Array.AsReadOnly(
            new[] { "Fast", "SceneIntegration", "AssetGeneration", "Hardware", "Endurance" });

        public static bool Includes(string profile, IEnumerable<string> categories)
        {
            var tags = new HashSet<string>(categories);
            if (tags.Count(Costs.Contains) != 1)
            {
                throw new InvalidOperationException("Every test needs exactly one execution cost category.");
            }

            if (profile == "All")
            {
                return true;
            }

            if (Costs.Contains(profile))
            {
                return tags.Contains(profile);
            }

            if (!new[] { "UI", "Gameplay", "Authoring", "Smoke", "HealthPlaytest" }.Contains(profile))
            {
                throw new ArgumentException("Unknown verification profile: " + profile);
            }

            return tags.Contains(profile) && (tags.Contains("Fast") || tags.Contains("SceneIntegration"));
        }

        public static string[] Select(ITestAdaptor root, string profile, string[] assemblies)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var test in Leaves(root))
            {
                if (!assemblies.Contains(AssemblyName(test)))
                {
                    continue;
                }

                try
                {
                    if (!Includes(profile, test.Categories))
                    {
                        continue;
                    }

                    if (!names.Add(test.FullName))
                    {
                        throw new InvalidOperationException("Duplicate test name: " + test.FullName);
                    }
                }
                catch (Exception error)
                {
                    throw new InvalidOperationException(test.FullName + ": " + error.Message, error);
                }
            }

            return names.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        }

        internal static bool Passed(string state, int expected, int passed, int failed, int skipped, int inconclusive)
        {
            if (state != "Passed" || expected <= 0)
            {
                return false;
            }

            if (failed != 0 || skipped != 0 || inconclusive != 0)
            {
                return false;
            }

            return passed == expected;
        }

        private static string AssemblyName(ITestAdaptor test)
        {
            while (test != null && !test.IsTestAssembly)
            {
                test = test.Parent;
            }

            return System.IO.Path.GetFileNameWithoutExtension(test?.Name ?? "");
        }

        private static IEnumerable<ITestAdaptor> Leaves(ITestAdaptor test)
        {
            if (!test.IsSuite)
            {
                yield return test;
                yield break;
            }

            foreach (var child in test.Children)
            {
                foreach (var leaf in Leaves(child))
                {
                    yield return leaf;
                }
            }
        }
    }
}
