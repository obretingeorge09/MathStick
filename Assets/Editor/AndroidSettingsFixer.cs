#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Sets the Android player settings this project needs, through Unity's own API.
///
/// These live in ProjectSettings/ProjectSettings.asset, and editing that file by
/// hand does not work: Unity holds the values in memory while it is open and
/// writes them back over the file on the next save, so the change is reverted
/// and — worse — quietly, which is how the package name has flipped back to the
/// template default more than once here.
///
/// Going through PlayerSettings writes into that in-memory copy instead, so the
/// value sticks and Unity serialises it out for real.
/// </summary>
public static class AndroidSettingsFixer
{
    /// <summary>
    /// The published application id. This is permanent once the app is on the
    /// Play Store — an app cannot change its package name, only be replaced by
    /// a different listing — so shipping the template's com.mystudio.plusminus
    /// is not a cosmetic mistake.
    /// </summary>
    const string PACKAGE = "com.JOC.PlusMinus";

    [MenuItem("PlusMinus/Apply Android Settings")]
    public static void Apply()
    {
        int changed = 0;

        string current = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
        if (current != PACKAGE)
        {
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, PACKAGE);
            Debug.Log("Package name: " + current + "  ->  " + PACKAGE);
            changed++;
        }

        // The canvas already adapts: CanvasOrientationAdapter drops
        // matchWidthOrHeight to 0 in landscape so the layout matches width and
        // scrolls instead of squashing. Locking to portrait threw that away.
        if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.AutoRotation)
        {
            Debug.Log("Orientation: " + PlayerSettings.defaultInterfaceOrientation + "  ->  AutoRotation");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            changed++;
        }

        // Upside-down is the one a phone should not rotate into: the speaker and
        // the camera end up at the bottom, and nothing else on the device does it.
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        AssetDatabase.SaveAssets();

        Debug.Log(changed > 0
            ? "PlusMinus: applied " + changed + " Android setting(s). Save the project to write them out."
            : "PlusMinus: Android settings were already correct.");
    }

    /// <summary>Reports drift without changing anything, so a build can be checked first.</summary>
    [MenuItem("PlusMinus/Check Android Settings")]
    public static void Check()
    {
        string pkg = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);

        Debug.Log("PlusMinus Android settings\n" +
                  "  package     : " + pkg + (pkg == PACKAGE ? "  OK" : "  EXPECTED " + PACKAGE) + "\n" +
                  "  orientation : " + PlayerSettings.defaultInterfaceOrientation +
                  (PlayerSettings.defaultInterfaceOrientation == UIOrientation.AutoRotation ? "  OK" : "  EXPECTED AutoRotation") + "\n" +
                  "  min SDK     : " + PlayerSettings.Android.minSdkVersion + "\n" +
                  "  target SDK  : " + PlayerSettings.Android.targetSdkVersion + "\n" +
                  "  scripting   : " + PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) + "\n" +
                  "  architecture: " + PlayerSettings.Android.targetArchitectures);
    }
}
#endif
