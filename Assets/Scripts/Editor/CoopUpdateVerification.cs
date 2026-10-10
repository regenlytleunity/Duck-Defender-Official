using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CoopUpdateVerification
{
    static CoopUpdateVerification()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            SessionState.EraseBool(CoopVerificationProbe.SessionKey);
            LocalCoopSession.RequestedPlayers = 1; LocalCoopSession.KeyboardTest = false;
            LocalCoopSession.Respawning = true; LocalCoopSession.DisabledCards.Clear();
            LocalCoopSession.HealthStyle = LocalCoopSession.HealthBarStyle.AbovePlayer;
            int[] colors = { 2, 1, 4, 3 };
            for (int i = 0; i < 4; i++) { LocalCoopSession.PlayerControllers[i] = null; LocalCoopSession.PlayerNames[i] = "PLAYER " + (i + 1); LocalCoopSession.PlayerColors[i] = colors[i]; }
        };
    }
    [MenuItem("Duck Defender/Co-op/Run isolated Play Mode verification")]
    public static void StartPlay()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");
        SessionState.SetBool(CoopVerificationProbe.SessionKey, true);
        UIUpdateVerification.StartPlay();
    }

    [MenuItem("Duck Defender/Co-op/Verify current WebGL build in temporary folder")]
    public static void BuildWeb() { QueueWebBuild(false); }

    [MenuItem("Duck Defender/Co-op/Verify development WebGL build in temporary folder")]
    public static void BuildDevelopmentWeb() { QueueWebBuild(true); }

    static void QueueWebBuild(bool development)
    {
        if (EditorApplication.isPlaying || EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            throw new System.InvalidOperationException("Exit Play Mode and select WebGL before verifying a web build.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new System.InvalidOperationException("Save open scene changes first.");
        EditorApplication.delayCall += () =>
        {
            var output = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "duck-coop-web-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var scene in EditorBuildSettings.scenes) if (scene.enabled) scenes.Add(scene.path);
            System.IO.Directory.CreateDirectory(CoopVerificationProbe.OutputPath);
            string reportPath = System.IO.Path.Combine(CoopVerificationProbe.OutputPath, "web-build.txt");
            System.IO.File.WriteAllText(reportPath, "Building: " + output);
            try
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = scenes.ToArray(), locationPathName = output, target = BuildTarget.WebGL, options = development ? BuildOptions.Development : BuildOptions.None
                });
                string result = report.summary.result + ": " + report.summary.totalErrors + " errors, " + report.summary.totalWarnings + " warnings\n" + output;
                System.IO.File.WriteAllText(reportPath, result); Debug.Log(result);
            }
            catch (System.Exception ex) { System.IO.File.WriteAllText(reportPath, "FAILED: " + ex); Debug.LogException(ex); }
        };
    }
}
