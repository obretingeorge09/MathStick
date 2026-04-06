#if UNITY_EDITOR
// ============================================================
//  SceneBootstrap — runs automatically on first project open.
//  Builds the MainScene if it does not yet exist.
// ============================================================
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
static class SceneBootstrap
{
    static SceneBootstrap()
    {
        // delayCall fires after Unity finishes compiling & importing assets.
        EditorApplication.delayCall += AutoSetup;
    }

    static void AutoSetup()
    {
        // Mobile player settings
        PlayerSettings.companyName = "MyStudio";
        PlayerSettings.productName = "PlusMinus";
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

        try
        {
            PlayerSettings.SetApplicationIdentifier(
                BuildTargetGroup.Android, "com.mystudio.plusminus");
            PlayerSettings.SetApplicationIdentifier(
                BuildTargetGroup.iOS, "com.mystudio.plusminus");
        }
        catch { /* BuildTargetGroup may not be available on all installs */ }

        // Always rebuild so any SceneBuilder changes take effect immediately
        const string scenePath = "Assets/Scenes/MainScene.unity";
        if (!EditorApplication.isPlaying)
        {
            UnityEngine.Debug.Log("[PlusMinus] Building scene...");

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            SceneBuilder.Build();

            // Add to Build Settings
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(scenePath, true)
            };

            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("[PlusMinus] ✓ Scene ready. Press Play to test!");
        }
    }
}
#endif
