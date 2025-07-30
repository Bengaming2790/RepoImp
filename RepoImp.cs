using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;


[BepInPlugin("net.regularben.repoimp", "REPO Impostor", "1.0.0")]
[BepInDependency(REPOLib.MyPluginInfo.PLUGIN_GUID, BepInDependency.DependencyFlags.HardDependency)]
public class RepoImp : BaseUnityPlugin
{
    
    
    private void Awake()
    {
        Logger.LogInfo("[RepoImp] Plugin loaded and initialized.");


        var harmony = new Harmony("net.regularben.repoimp");
        harmony.PatchAll();


        SceneManager.sceneLoaded += OnSceneLoaded;
    }


    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Logger.LogInfo($"[RepoImp] Scene loaded: {scene.name}");


        if (GameObject.Find("RepoImpHandler") == null)
        {
            var handler = new GameObject("RepoImpHandler");
            handler.AddComponent<RepoImpHandler>();
            DontDestroyOnLoad(handler);
        }


    }
}