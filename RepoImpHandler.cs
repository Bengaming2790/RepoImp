using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Random = UnityEngine.Random;
using REPOLib.Modules;

public class RepoImpHandler : MonoBehaviour
{

// Sabotage cooldowns (seconds)
    private float _lightsCooldown = 60f;
    private float _voiceCooldown = 30f;
    private float _spawnCooldown = 60f;

// Cooldown timers
    private float _lastLightsTime = -999f;
    private float _lastVoiceTime = -999f;
    private float _lastSpawnTime = -999f;

// Enemy list for spawn sabotage
    private List<string> _spawnableEnemies = new List<string>
    {
        "Rugrat",
        "Spewer",
        "Banger",
        "Upscream",
        "ApexPredator"
    };

// UI for keybind settings menu
    private GameObject _keybindsMenu;
    private Text _lightsKeyText;
    private Text _voiceKeyText;
    private Text _spawnKeyText;

// Tracks which keybind is currently being rebinded, or null if none
    private string _waitingForKeybind = null;

    private bool _levelInitialized = false;
    private object _levelGeneratorInstance = null;
    private Type _levelGeneratorType = null;
    private bool _impostorSelected = false;
    private string _selectedImpostorClientID = "";
    private string _localClientID = "";
    private bool _resurrectionPrevention = true; // Toggle for resurrection prevention


    List<string> _clientIDs = new List<string>();
    List<string> _steamIDs = new List<string>();
    List<object> _trackedBodies = new List<object>(); // Track dead bodies

    // UI Components
    private GameObject _impostorUI;
    private Text _impostorText;
    private GameObject _crewUI;
    private Text _crewText;
    private Canvas _uiCanvas;


    private bool _isHost = false;

    private void Awake()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;

        Debug.Log($"[RepoImp] Initial Scene: {SceneManager.GetActiveScene().name}");

    

        CreateKeybindsMenu();

        // Get local client ID early if possible
        GetLocalClientID();
    }



    private void CreateKeybindsMenu()
    {
        // Root Canvas for menu
        GameObject canvasGO = new GameObject("RepoImpKeybindsCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        _keybindsMenu = new GameObject("KeybindsMenu");
        _keybindsMenu.transform.SetParent(canvasGO.transform, false);
        RectTransform rect = _keybindsMenu.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(300, 200);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;

        // Background panel
        Image panelImage = _keybindsMenu.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.8f);

        // Title
        CreateUIText("Keybind Settings", new Vector2(0, 80), 24, _keybindsMenu.transform);

        // Create labels and buttons for each keybind
        _keybindsMenu.SetActive(false); // Hide by default
    }

    private Text CreateKeybindEntry(string label, KeyCode key, Vector2 pos)
    {
        // Label
        CreateUIText(label + ":", new Vector2(-80, pos.y), 18, _keybindsMenu.transform, TextAnchor.MiddleRight);

        // Key Button (shows current key, clickable to rebind)
        GameObject keyBtnGO = new GameObject(label + "KeyBtn");
        keyBtnGO.transform.SetParent(_keybindsMenu.transform, false);
        RectTransform keyRect = keyBtnGO.AddComponent<RectTransform>();
        keyRect.sizeDelta = new Vector2(100, 25);
        keyRect.anchoredPosition = pos;

        Button btn = keyBtnGO.AddComponent<Button>();
        Image img = keyBtnGO.AddComponent<Image>();
        img.color = Color.gray;

        Text btnText = CreateUIText(key.ToString(), Vector2.zero, 16, keyBtnGO.transform, TextAnchor.MiddleCenter);

        btn.onClick.AddListener(() =>
        {
            _waitingForKeybind = label; // mark this keybind waiting for keypress
            btnText.text = "Press any key...";
        });

        return btnText;
    }

    private Text CreateUIText(string text, Vector2 pos, int fontSize, Transform parent,
        TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        GameObject go = new GameObject("Text_" + text);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(280, 25);
        rect.anchoredPosition = pos;

        Text txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.color = Color.white;
        txt.alignment = anchor;

        return txt;
    }



    private IEnumerator TriggerLightsOut()
    {
        var flashlights = GameObject.FindObjectsOfType<Light>().Where(l => l.name.ToLower().Contains("flash"));
        List<float> originalIntensities = new List<float>();

        foreach (var light in flashlights)
        {
            originalIntensities.Add(light.intensity);
            light.intensity = 0f;
        }

        yield return new WaitForSeconds(60f);

        int i = 0;
        foreach (var light in flashlights)
        {
            if (light != null && i < originalIntensities.Count)
            {
                light.intensity = originalIntensities[i];
            }

            i++;
        }

        Debug.Log("[RepoImp] Lights out triggered.");
    }

    private IEnumerator TriggerVoiceSabotage()
    {
        var voiceManagers = GameObject.FindObjectsOfType<MonoBehaviour>()
            .Where(obj => obj.GetType().Name.ToLower().Contains("voice"))
            .ToList();

        foreach (var voice in voiceManagers)
        {
            var enabledProp = voice.GetType().GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance);
            if (enabledProp != null && enabledProp.CanWrite)
                enabledProp.SetValue(voice, false);
        }

        yield return new WaitForSeconds(30f);

        foreach (var voice in voiceManagers)
        {
            var enabledProp = voice.GetType().GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance);
            if (enabledProp != null && enabledProp.CanWrite)
                enabledProp.SetValue(voice, true);
        }

        Debug.Log("[RepoImp] Voice sabotage triggered.");
    }

    private void TriggerEnemySpawn()
    {
        // Pick random enemy ID string
        string enemyID = _spawnableEnemies[Random.Range(0, _spawnableEnemies.Count)];

        // Try to get EnemySetup from the REPOLib API
        if (!REPOLib.Modules.Enemies.TryGetEnemyByName(enemyID, out var enemySetup) || enemySetup == null)
        {
            Debug.LogWarning($"[RepoImp] Could not find EnemySetup for enemy ID: {enemyID}");
            return;
        }

        Vector3 spawnPos = transform.position + transform.forward * 5f; // example spawn position
        Quaternion spawnRot = Quaternion.identity; // or use your desired rotation

        // Call SpawnEnemy with the 4 required parameters
        var spawnedEnemies = REPOLib.Modules.Enemies.SpawnEnemy(enemySetup, spawnPos, spawnRot, spawnDespawned: true);
        Debug.Log($"[RepoImp] # of Spawned Enemies: {spawnedEnemies.Count}");
        if (spawnedEnemies == null || spawnedEnemies.Count == 0)
        {
            Debug.LogWarning($"[RepoImp] Failed to spawn enemy: {enemyID}");
        }
        else
        {
            Debug.Log($"[RepoImp] Spawned enemy: {enemyID} at {spawnPos}");
        }
    }






    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (_impostorUI != null)
            Destroy(_impostorUI);
        if (_crewUI != null)
            Destroy(_crewUI);
    }

    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        Debug.Log($"[RepoImp] Active Scene Changed: {oldScene.name} → {newScene.name}");

        // Reset impostor selection for new scene
        _impostorSelected = false;
        _selectedImpostorClientID = "";

        if (_impostorUI != null)
        {
            _impostorUI.SetActive(false);
        }

        if (_crewUI != null)
        {
            _crewUI.SetActive(false);
        }
    }
    
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[RepoImp] Scene Loaded: {scene.name}, Mode: {mode}");
        StartCoroutine(ScanIDsAfterDelay());

        SetupImpostorUI();
        SetupCrewUI();
    }
    private void GetLocalClientID()
    {
        // Try to find the local player's client ID
        // This might vary depending on your game's structure
        var allObjects = FindObjectsOfType<MonoBehaviour>();

        foreach (var obj in allObjects)
        {
            var type = obj.GetType();

            // Look for common local player indicators
            if (type.Name.Contains("LocalPlayer") || type.Name.Contains("Player"))
            {
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var f in fields)
                {
                    string fieldNameLower = f.Name.ToLower();
                    if (fieldNameLower.Contains("clientid") || fieldNameLower.Contains("client_id"))
                    {
                        try
                        {
                            var value = f.GetValue(obj);
                            if (value != null)
                            {
                                _localClientID = value.ToString();
                                Debug.Log($"[RepoImp] Found local client ID: {_localClientID}");
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[RepoImp] Failed to get local client ID: {ex.Message}");
                        }
                    }
                }
            }
        }

        // Fallback: assume first client ID is local (this might need adjustment)
        if (string.IsNullOrEmpty(_localClientID) && _clientIDs.Count > 0)
        {
            _localClientID = _clientIDs[0];
            Debug.Log($"[RepoImp] Using fallback local client ID: {_localClientID}");
        }
    }

    private void SetupImpostorUI()
    {
        // Find or create canvas
        _uiCanvas = FindObjectOfType<Canvas>();
        if (_uiCanvas == null)
        {
            GameObject canvasGo = new GameObject("RepoImp_Canvas");
            _uiCanvas = canvasGo.AddComponent<Canvas>();
            _uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _uiCanvas.sortingOrder = 1000; // High sorting order to appear on top
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        // Create impostor UI GameObject
        _impostorUI = new GameObject("ImpostorUI");
        _impostorUI.transform.SetParent(_uiCanvas.transform, false);

        // Add RectTransform and set anchoring to top-left
        RectTransform rectTransform = _impostorUI.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(1, 1); // Top-left anchor
        rectTransform.anchorMax = new Vector2(1, 1);
        rectTransform.pivot = new Vector2(1, 1);
        rectTransform.anchoredPosition = new Vector2(-20, -25); // 20 pixels from edges
        rectTransform.sizeDelta = new Vector2(200, 50);

        // Add Text component
        _impostorText = _impostorUI.AddComponent<Text>();
        _impostorText.text = "IMPOSTOR";
        _impostorText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _impostorText.fontSize = 24;
        _impostorText.fontStyle = FontStyle.Bold;
        _impostorText.color = Color.red;
        _impostorText.alignment = TextAnchor.MiddleCenter;

        // Add outline for better visibility
        Outline outline = _impostorUI.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, 2);

        // Initially hide the UI
        _impostorUI.SetActive(false);

        Debug.Log("[RepoImp] Impostor UI setup complete");
    }

    private void SetupCrewUI()
    {
        // Use existing canvas or create one if it doesn't exist
        if (_uiCanvas == null)
        {
            GameObject canvasGo = new GameObject("RepoImp_Canvas");
            _uiCanvas = canvasGo.AddComponent<Canvas>();
            _uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _uiCanvas.sortingOrder = 1000; // High sorting order to appear on top
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        // Create crew UI GameObject
        _crewUI = new GameObject("CrewUI");
        _crewUI.transform.SetParent(_uiCanvas.transform, false);

        // Add RectTransform and set anchoring to top-left
        RectTransform rectTransform = _crewUI.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0, 1); // Top-left anchor
        rectTransform.anchorMax = new Vector2(0, 1);
        rectTransform.pivot = new Vector2(0, 1);
        rectTransform.anchoredPosition = new Vector2(20, -25); // 20 pixels from edges
        rectTransform.sizeDelta = new Vector2(200, 50);

        // Add Text component
        _crewText = _crewUI.AddComponent<Text>();
        _crewText.text = "CREWMATE";
        _crewText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _crewText.fontSize = 24;
        _crewText.fontStyle = FontStyle.Bold;
        _crewText.color = Color.cyan;
        _crewText.alignment = TextAnchor.MiddleCenter;

        // Add outline for better visibility
        Outline outline = _crewUI.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, 2);

        // Initially hide the UI
        _crewUI.SetActive(false);

        Debug.Log("[RepoImp] Crew UI setup complete");
    }

    private void ScanIDs()
    {
        _clientIDs.Clear();
        _steamIDs.Clear();

        var allObjects = FindObjectsOfType<MonoBehaviour>();

        foreach (var obj in allObjects)
        {
            var type = obj.GetType();

            // Scan fields
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var f in fields)
            {
                string fieldNameLower = f.Name.ToLower();

                if (fieldNameLower.Contains("clientid") || fieldNameLower.Contains("client_id") ||
                    fieldNameLower == "clientid")
                {
                    try
                    {
                        var value = f.GetValue(obj);
                        if (value != null)
                        {
                            string valStr = value.ToString();
                            if (!_clientIDs.Contains(valStr) && !string.IsNullOrEmpty(valStr))
                                _clientIDs.Add(valStr);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            $"[RepoImp] Failed to get clientID field {f.Name} in {type.Name}: {ex.Message}");
                    }
                }
                else if (fieldNameLower.Contains("steamid") || fieldNameLower.Contains("steam_id") ||
                         fieldNameLower == "steamid")
                {
                    try
                    {
                        var value = f.GetValue(obj);
                        if (value != null)
                        {
                            string valStr = value.ToString();
                            if (!_steamIDs.Contains(valStr) && !string.IsNullOrEmpty(valStr))
                                _steamIDs.Add(valStr);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            $"[RepoImp] Failed to get steamID field {f.Name} in {type.Name}: {ex.Message}");
                    }
                }
            }

            // Scan properties
            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var p in properties)
            {
                if (!p.CanRead)
                    continue;

                string propNameLower = p.Name.ToLower();

                if (propNameLower.Contains("clientid") || propNameLower.Contains("client_id") ||
                    propNameLower == "clientid")
                {
                    try
                    {
                        var value = p.GetValue(obj);
                        if (value != null)
                        {
                            string valStr = value.ToString();
                            if (!_clientIDs.Contains(valStr) && !string.IsNullOrEmpty(valStr))
                                _clientIDs.Add(valStr);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            $"[RepoImp] Failed to get clientID property {p.Name} in {type.Name}: {ex.Message}");
                    }
                }
                else if (propNameLower.Contains("steamid") || propNameLower.Contains("steam_id") ||
                         propNameLower == "steamid")
                {
                    try
                    {
                        var value = p.GetValue(obj);
                        if (value != null)
                        {
                            string valStr = value.ToString();
                            if (!_steamIDs.Contains(valStr) && !string.IsNullOrEmpty(valStr))
                                _steamIDs.Add(valStr);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            $"[RepoImp] Failed to get steamID property {p.Name} in {type.Name}: {ex.Message}");
                    }
                }
            }
        }

        Debug.Log($"[RepoImp] Found {_clientIDs.Count} client IDs and {_steamIDs.Count} steam IDs.");
    }

    private void SelectImpostor()
    {
        if (_clientIDs.Count == 0)
        {
            Debug.LogWarning("[RepoImp] No client IDs available to select impostor!");
            return;
        }

        // Select random client ID as impostor
        _selectedImpostorClientID = _clientIDs[Random.Range(0, _clientIDs.Count)];
        _impostorSelected = true;
        Debug.Log($"[RepoImp] Selected impostor: {_selectedImpostorClientID}");

        // Check if local player is the impostor
        if (_selectedImpostorClientID == _localClientID)
        {
            ShowImpostorUI();
            Debug.Log("[RepoImp] You are the impostor!");
        }
        else
        {
            ShowCrewUI();
            Debug.Log("[RepoImp] You are a crewmate.");

        }
    }

    private void ShowImpostorUI()
    {
        if (_impostorUI != null)
        {
            _impostorUI.SetActive(true);
            Debug.Log("[RepoImp] Impostor UI displayed");
        }

        // Make sure crew UI is hidden
        if (_crewUI != null)
        {
            _crewUI.SetActive(false);
        }
    }

    private void ShowCrewUI()
    {
        if (_crewUI != null)
        {
            _crewUI.SetActive(true);
            Debug.Log("[RepoImp] Crew UI displayed");
        }

        // Make sure impostor UI is hidden
        if (_impostorUI != null)
        {
            _impostorUI.SetActive(false);
        }
    }

    private void HideImpostorUI()
    {
        if (_impostorUI != null)
        {
            _impostorUI.SetActive(false);
            Debug.Log("[RepoImp] Impostor UI hidden");
        }
    }

    private void HideCrewUI()
    {
        if (_crewUI != null)
        {
            _crewUI.SetActive(false);
            Debug.Log("[RepoImp] Crew UI hidden");
        }
    }

    private void HideAllUI()
    {
        HideImpostorUI();
        HideCrewUI();
    }



    private void DetermineIfHost()
    {
        _isHost = SemiFunc.IsMasterClientOrSingleplayer();

        if (_isHost)
            Debug.Log("[RepoImp] Host status confirmed. You may use debug features");
        else
            Debug.Log("[RepoImp] You are not the host.");
    }




    private IEnumerator ScanIDsAfterDelay()
    {
        yield return null;
        yield return null;

        ScanIDs();
        GetLocalClientID();
        DetermineIfHost();
    }

    
    void Update()
    {
        if (!_levelInitialized && TryGetLevelGeneratorInstance(out _levelGeneratorInstance, out _levelGeneratorType))
        {
            var generatedField = _levelGeneratorType.GetField("Generated",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (generatedField != null && (bool)generatedField.GetValue(_levelGeneratorInstance) == true)
            {
                _levelInitialized = true;
                Debug.Log("[RepoImp] Level is initialized (LevelGenerator.Generated == true)");

                // Select impostor once level is initialized
                if (!_impostorSelected && _clientIDs.Count > 0)
                {
                    SelectImpostor();
                }
            }
        }




            //DEBUG functions
            // Toggle the keybind menu with F7
   /*         if (Input.GetKeyDown(KeyCode.F7))
            {
                _keybindsMenu.SetActive(!_keybindsMenu.activeSelf);
                Debug.Log($"[RepoImp] Keybind menu " + (_keybindsMenu.activeSelf ? "opened" : "closed"));
            }
    */
            // Debug key to display client/steam IDs

            if (Input.GetKeyDown(KeyCode.F8) && SemiFunc.IsMasterClientOrSingleplayer())
            {
                Debug.Log(
                    $"[RepoImp] Client IDs: {string.Join(", ", _clientIDs)} \nSteam IDs: {string.Join(", ", _steamIDs)}");
                Debug.Log($"[RepoImp] Local Client ID: {_localClientID}");
                Debug.Log($"[RepoImp] Selected Impostor: {_selectedImpostorClientID}");
            }

            // Debug key to manually select impostor (for testing)
            if (Input.GetKeyDown(KeyCode.F9) && SemiFunc.IsMasterClientOrSingleplayer())
            {
                if (_clientIDs.Count > 0)
                {
                    SelectImpostor();
                }
                else
                {
                    Debug.LogWarning("[RepoImp] No client IDs found for impostor selection!");
                }
            }

            // Debug key to toggle impostor UI (for testing)
            if (Input.GetKeyDown(KeyCode.F10) && SemiFunc.IsMasterClientOrSingleplayer())
            {
                if (_impostorUI != null)
                {
                    _impostorUI.SetActive(!_impostorUI.activeSelf);
                    Debug.Log($"[RepoImp] Impostor UI toggled: {_impostorUI.activeSelf}");
                }
            }

            // Debug key to toggle crew UI (for testing)
            if (Input.GetKeyDown(KeyCode.F11) && SemiFunc.IsMasterClientOrSingleplayer())
            {
                if (_crewUI != null)
                {
                    _crewUI.SetActive(!_crewUI.activeSelf);
                    Debug.Log($"[RepoImp] Crew UI toggled: {_crewUI.activeSelf}");
                }
            }
        }

        bool TryGetLevelGeneratorInstance(out object instance, out Type type)
        {
            type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == "LevelGenerator");

            if (type != null)
            {
                var instanceField = type.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceField != null)
                {
                    instance = instanceField.GetValue(null);
                    return instance != null;
                }
            }

            instance = null;
            return false;
        }
    }

    // Public methods for external access if needed
//need to disable in Main, Lobby Menu, Arena, Shop, Lobby
