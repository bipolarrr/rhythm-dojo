using System;

namespace RhythmDojo.Input
{
    // A host may own focus independently of the OS (for example, synthetic input
    // during verification). Without a provider, platform focus remains authoritative.
    public static class ApplicationFocusState
    {
        public static Func<bool> Provider { get; set; }

        public static bool IsFocused => Provider?.Invoke() ?? UnityEngine.Application.isFocused;
        public static bool UsesPlatformEvents => Provider == null;
    }
}
