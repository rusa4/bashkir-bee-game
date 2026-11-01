using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class BeeGladeFirstOpen
{
    const string Attempt = "BeeGlade.FirstOpenAttempt";
    static BeeGladeFirstOpen() { EditorApplication.delayCall += PrepareIfNeeded; }

    static void PrepareIfNeeded()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode ||
            File.Exists("Assets/Scenes/BeeGlade.unity") || SessionState.GetBool(Attempt, false)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += PrepareIfNeeded; return; }
        SessionState.SetBool(Attempt, true);
        BeeGladeSetup.Run();
    }

    [MenuItem("Bee Glade/Prepare or open game scene")]
    public static void PrepareOrOpen() { BeeGladeSetup.Run(); }
}
