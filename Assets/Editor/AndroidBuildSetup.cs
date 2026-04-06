using UnityEditor;
using UnityEngine;

public class AndroidBuildSetup
{
    [MenuItem("PlusMinus/Setup Android Build")]
    public static void SetupAndroidBuild()
    {
        Debug.Log("Setting up Android build...");

        // 1. Switch to Android platform
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        Debug.Log("✓ Switched to Android platform");

        // 2. Configure Player Settings
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        Debug.Log("✓ Set Portrait orientation");

        // 3. Android specific settings
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        Debug.Log("✓ Set Android API level 24+");

        // Additional settings
        PlayerSettings.productName = "PlusMinus";
        PlayerSettings.companyName = "JOC";
        Debug.Log("✓ Set product name: PlusMinus");

        Debug.Log("✅ Android build setup complete!");
        Debug.Log("Next: File > Build Settings > Build (or Build and Run)");
    }
}
