using BepInEx;
using BepInEx.Unity.Mono;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

[BepInPlugin("com.mmo98.trainer", "MMO98 Trainer", "1.0.0")]
public class Plugin : BaseUnityPlugin
{
    #region variables
    
    // ui layout and state
    private Rect windowRect = new Rect(100, 100, 340, 480);
    private bool showMenu = true;
    private int selectedTab = 0;
    private string[] tabs = { "Recovery", "Unlocks", "Game" };
    private Vector2 scrollPos;

    // text field inputs
    private string moneyInput = "1000";
    private string playersInput = "1000";
    private string fansInput = "1000";
    private string dataInput = "1000";

    // game references
    private GameController controller;
    private GameTimer gameTimer;
    private float originalGameSpeed;
    private float nextReferenceSearch;
    private HarmonyLib.Harmony harmony;
    internal SequelDevelopSimulation developmentSimulation;
    private readonly Dictionary<string, MethodInfo> unlockMethods = new Dictionary<string, MethodInfo>();
    private string statusMessage = "Load a profile to use the trainer.";

    internal static bool IsGameReady => Instance != null && Instance.isActiveAndEnabled &&
        !Database.Disposed && Database.State != null && Database.Commands != null;

    // cheat toggles and values
    public bool noBugs;
    public bool noPing;
    private float timeScale = 1f;

    // singleton instance for harmony patches to access
    public static Plugin Instance;

    // style helpers
    private GUIStyle centerHeaderStyle;

    #endregion

    #region initialization and patching

    private void Awake()
    {
        // setup singleton
        Instance = this;

        // initialize harmony patcher
        harmony = new HarmonyLib.Harmony("com.mmo98.trainer");
        TryPatch(typeof(PingSimulation), "OnUpdateSimulation", nameof(PingPrefix));
        TryPatch(typeof(BugSimulation), "OnUpdateSimulation", postfix: nameof(BugsPostfix));
        TryPatch(typeof(SequelDevelopSimulation), "Registered", postfix: nameof(DevelopmentRegistered));
        TryPatch(typeof(SequelDevelopSimulation), "Unregistered", postfix: nameof(DevelopmentUnregistered));

        foreach (string name in new[] { "UnlockAllUpgrades", "UnlockAllResearch", "UnlockAllDatacenters" })
        {
            var method = HarmonyLib.AccessTools.DeclaredMethod(typeof(GameController), name, Type.EmptyTypes);
            if (method == null)
                Logger.LogWarning($"Unavailable game action: {name}");
            else
                unlockMethods.Add(name, method);
        }
        Logger.LogInfo($"MMO98 Trainer loaded for game {Application.version}.");
    }

    private void TryPatch(Type type, string methodName, string prefix = null, string postfix = null)
    {
        try
        {
            var method = HarmonyLib.AccessTools.DeclaredMethod(type, methodName);
            if (method == null) throw new MissingMethodException(type.FullName, methodName);
            harmony.Patch(method,
                prefix: prefix == null ? null : new HarmonyLib.HarmonyMethod(typeof(Plugin), prefix),
                postfix: postfix == null ? null : new HarmonyLib.HarmonyMethod(typeof(Plugin), postfix));
        }
        catch (Exception ex)
        {
            Logger.LogError($"Could not patch {type.Name}.{methodName}: {ex}");
        }
    }

    internal static bool PingPrefix()
    {
        if (!IsGameReady || !Instance.noPing) return true;
        Database.State.Resources.Ping.SetValue(0f, 0f);
        return false;
    }

    internal static void BugsPostfix()
    {
        if (IsGameReady && Instance.noBugs)
            Database.State.Resources.Bugs.SetValue(0f, 0f);
    }

    internal static void DevelopmentRegistered(SequelDevelopSimulation __instance)
    {
        if (Instance != null) Instance.developmentSimulation = __instance;
    }

    internal static void DevelopmentUnregistered(SequelDevelopSimulation __instance)
    {
        if (Instance != null && Instance.developmentSimulation == __instance)
            Instance.developmentSimulation = null;
    }

    private void RestoreGameSpeed()
    {
        if (gameTimer != null) gameTimer.GameSpeedMultiplier = originalGameSpeed;
        gameTimer = null;
    }

    private void OnDisable() => RestoreGameSpeed();

    private void OnDestroy()
    {
        RestoreGameSpeed();
        harmony?.UnpatchSelf();
        if (Instance == this) Instance = null;
    }

    #endregion

    #region unity life cycle

    private void Update()
    {
        // toggle the menu visibility when the insert key is pressed
        if (Keyboard.current != null && Keyboard.current.insertKey.wasPressedThisFrame)
            showMenu = !showMenu;

        if (!IsGameReady)
        {
            RestoreGameSpeed();
            controller = null;
            developmentSimulation = null;
            return;
        }

        if (Time.unscaledTime >= nextReferenceSearch)
        {
            nextReferenceSearch = Time.unscaledTime + 1f;
            if (controller == null) controller = UnityEngine.Object.FindFirstObjectByType<GameController>();
            if (gameTimer == null)
            {
                gameTimer = UnityEngine.Object.FindFirstObjectByType<GameTimer>();
                if (gameTimer != null) originalGameSpeed = gameTimer.GameSpeedMultiplier;
            }
        }
        // Let the game timer advance commands and interval simulations in its own bounded steps.
        if (gameTimer != null) gameTimer.GameSpeedMultiplier = originalGameSpeed * Mathf.Clamp(timeScale, 0.1f, 50f);
        BugsPostfix();
        if (noPing) Database.State.Resources.Ping.SetValue(0f, 0f);
    }

    #endregion

    #region ui components

    private void OnGUI()
    {
        // dont draw anything if the menu is hidden
        if (!showMenu) return;

        var previousBackground = GUI.backgroundColor;
        // setup a dark background tint for the UI elements
        GUI.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);
        
        // initialize center header style if null
        if (centerHeaderStyle == null)
        {
            centerHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                fontSize = 13
            };
        }

        // draw the main window
        windowRect = GUI.Window(0, windowRect, DrawWindow, $"<b><color=#00FFB2>MMO98 Trainer</color></b>");
        GUI.backgroundColor = previousBackground;
    }

    private void DrawWindow(int id)
    {
        GUILayout.BeginVertical();
        GUILayout.Space(5);

        // draw the top tab navigation
        selectedTab = GUILayout.Toolbar(selectedTab, tabs, GUILayout.Height(30));

        GUILayout.Space(10);

        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && IsGameReady && controller != null;

        // start the scroll view area so the content doesn't bleed out
        scrollPos = GUILayout.BeginScrollView(scrollPos);

        // switch between our different menu tabs
        switch (selectedTab)
        {
            case 0:
                DrawRecoveryTab();
                break;
            case 1:
                DrawUnlocksTab();
                break;
            case 2:
                DrawGameTab();
                break;
        }

        GUILayout.EndScrollView();
        GUI.enabled = previousEnabled;
        GUILayout.Label(IsGameReady ? statusMessage : "Load a profile to use the trainer.", GUI.skin.box);

        // draw footer outside the scrollview so it stays at the bottom
        GUILayout.Space(5);
        GUIStyle footerStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            richText = true
        };
        GUILayout.Label($"<color=#AAAAAA>{Info.Metadata.Name} by <b>Official B</b> v{Info.Metadata.Version}</color>", footerStyle);

        GUILayout.EndVertical();

        // allow the window to be dragged around by the top part
        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    // =========================
    // RECOVERY TAB
    // =========================
    private void DrawRecoveryTab()
    {
        GUILayout.Label("<b><color=#50c878>RESOURCES</color></b>", centerHeaderStyle);
        GUILayout.Space(5);

        DrawResourceRow("Money", ref moneyInput, AddMoney);
        DrawResourceRow("Players", ref playersInput, AddPlayers);
        DrawResourceRow("Fans", ref fansInput, AddFans);
        DrawResourceRow("Data", ref dataInput, AddData);
    }

    private void DrawResourceRow(string label, ref string input, Action<double> onAdd)
    {
        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label(label, GUILayout.Width(60));
        input = GUILayout.TextField(input, GUILayout.ExpandWidth(true));
        
        if (GUILayout.Button("Add", GUILayout.Width(60)))
        {
            if (double.TryParse(input, out double amount) && !double.IsNaN(amount) && !double.IsInfinity(amount) && amount > 0)
                RunAction($"Added {amount:G} {label.ToLowerInvariant()}.", () => onAdd(amount));
            else
                statusMessage = "Enter a finite amount greater than zero.";
        }
        GUILayout.EndHorizontal();
    }

    // =========================
    // UNLOCK TAB
    // =========================
    private void DrawUnlocksTab()
    {
        GUILayout.Label("<b><color=#FF9D00>GAME PROGRESSION</color></b>", centerHeaderStyle);
        GUILayout.Space(5);

        GUILayout.BeginVertical(GUI.skin.box);
        if (GUILayout.Button("Unlock All Upgrades", GUILayout.Height(30))) UnlockAll("UnlockAllUpgrades");
        GUILayout.Space(5);
        if (GUILayout.Button("Unlock All Research", GUILayout.Height(30))) UnlockAll("UnlockAllResearch");
        GUILayout.Space(5);
        if (GUILayout.Button("Unlock All Datacenters", GUILayout.Height(30))) UnlockAll("UnlockAllDatacenters");
        GUILayout.EndVertical();
    }
    
    // =========================
    // GAME TAB
    // =========================
    private void DrawGameTab()
    {
        // --- PERFORMANCE SECTION ---
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("<b><color=#00D8FF>PERFORMANCE</color></b>", centerHeaderStyle);
        GUILayout.Space(5);

        // toggles for performance cheats
        noBugs = GUILayout.Toggle(noBugs, " No Bugs");
        GUILayout.Space(2);
        noPing = GUILayout.Toggle(noPing, " Zero Ping");
        GUILayout.EndVertical();

        GUILayout.Space(10);

        // --- DEV TOOLS SECTION ---
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("<b><color=#FF5E00>DEV TOOLS</color></b>", centerHeaderStyle);
        GUILayout.Space(5);

        // button to instantly run & finish a sequel development round
        if (GUILayout.Button("Instant Development Cycle", GUILayout.Height(30)))
        {
            InstantDevelopmentCycle();
        }
        GUILayout.EndVertical();

        GUILayout.Space(10);

        // --- TIME SECTION ---
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("<b><color=#B500FF>TIME MANIPULATION</color></b>", centerHeaderStyle);
        GUILayout.Space(5);

        // time speed controls and reset button
        GUILayout.BeginHorizontal();
        
        // increased width and disabled word wrap to prevent clipping with larger numbers
        GUIStyle speedLabelStyle = new GUIStyle(GUI.skin.label) 
        { 
            richText = true, 
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false 
        };
        GUILayout.Label($"Speed: <color=#00FFFF>{timeScale:F1}x</color>", speedLabelStyle, GUILayout.Width(90));
        
        timeScale = GUILayout.HorizontalSlider(timeScale, 0.1f, 50f);
        
        if (GUILayout.Button("Reset", GUILayout.Width(60)))
        {
            timeScale = 1f;
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    #endregion

    #region cheat functions

    // =========================
    // DEV TOOLS
    // =========================
    private void InstantDevelopmentCycle()
    {
        if (!IsGameReady) return;
        if (developmentSimulation == null)
        {
            statusMessage = "Development simulation is not ready.";
            return;
        }
        RunAction("Development cycle completed.", () =>
        {
        var sequel = Database.State.Sequel;
        // Run the active simulation's normal completion path, including progress and next cost.
        sequel.Time.SetValue(sequel.Duration.Value, 0f);
        sequel.Developing.Value = true;
        developmentSimulation.OnUpdateSimulation(0f);
        });
    }

    private void RunAction(string successMessage, Action action)
    {
        if (!IsGameReady) { statusMessage = "Load a profile first."; return; }
        try
        {
            action();
            statusMessage = successMessage;
        }
        catch (Exception ex)
        {
            statusMessage = "Action failed. See the BepInEx log for details.";
            Logger.LogError(ex);
        }
    }

    // =========================
    // RESOURCES
    // =========================
    private void AddMoney(double amount)
    {
        Database.State.Resources.Money.AddValue(amount, 0.0);
    }

    private void AddPlayers(double amount)
    {
        Database.State.Resources.Players.AddValue(amount, 0.0);
    }

    private void AddData(double amount)
    {
        Database.State.Prestige.Data.AddValue(amount, 0.0);
    }

    private void AddFans(double amount)
    {
        Database.State.Prestige.Fans.AddValue(amount, 0.0);
    }

    // =========================
    // UNLOCK SYSTEM
    // =========================
    private void UnlockAll(string methodName)
    {
        if (controller == null || !unlockMethods.TryGetValue(methodName, out var method))
        {
            statusMessage = "This unlock action is unavailable in this game build.";
            return;
        }
        RunAction("Unlocks applied.", () => method.Invoke(controller, null));
    }

    #endregion
}
