using UnityEditor;
public static class RemediationBuildDispatch
{
    public static bool Main()
    {
        EditorApplication.delayCall -= RhythmDojo.EditorTools.RemediationPlayerVerification.BuildPlayer;
        EditorApplication.update += Build;
        return true;
    }

    private static void Build()
    {
        EditorApplication.update -= Build;
        RhythmDojo.EditorTools.RemediationPlayerVerification.BuildPlayer();
    }
}
