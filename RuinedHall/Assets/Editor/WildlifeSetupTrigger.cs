using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class WildlifeSetupTrigger
{
    const string SessionKey = "RuinedHall.WildlifeSetup.v2";

    static WildlifeSetupTrigger()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += Run;
            return;
        }

        try
        {
            CharacterFrameworkSetup.ConfigureWildlife();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            Debug.LogError("[WildlifeSetup] FAILED");
        }
    }
}
