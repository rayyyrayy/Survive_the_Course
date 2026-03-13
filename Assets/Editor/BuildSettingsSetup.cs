using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEngine;

public static class BuildSettingsSetup
{
    private const string StartScenePath = "Assets/Scenes/StartScene.unity/StartScene.unity";
    private const string GameScenePath = "Assets/Scenes/TopDownSurvival.unity";

    [MenuItem("Tools/Setup Build Settings (StartScene + TopDownSurvival)")]
    public static void SetupBuildSettings()
    {
        var scenes = new[]
        {
            new EditorBuildSettingsScene(StartScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true)
        };
        EditorBuildSettings.scenes = scenes;
        Debug.Log("Build Settings: StartScene (index 0), TopDownSurvival (index 1).");
    }
}
