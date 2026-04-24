using BepInEx;
using BepInEx.Unity.Mono;
using System;
using System.Linq;
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

    // cheat toggles and values
    private bool noBugs;
    private bool noPing;
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
        var harmony = new HarmonyLib.Harmony("com.mmo98.trainer");

        // search for all simulation classes to speed up the game time properly
        foreach (var type in typeof(IIncrementalSimulation).Assembly.GetTypes())
        {
            if (!typeof(IIncrementalSimulation).IsAssignableFrom(type))
                continue;

            if (type.IsInterface || type.IsAbstract)
                continue;

            // grab the update method
            var method = type.GetMethod(
                "OnUpdateSimulation",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            // if we couldn't find it easily, try finding it via the interface map
            if (method == null)
            {
                var map = type.GetInterfaceMap(typeof(IIncrementalSimulation));
                method = map.TargetMethods.FirstOrDefault(m => m.Name.Contains("OnUpdateSimulation"));
            }

            // skip if still not found
            if (method == null) continue;

            // apply the patch
            harmony.Patch(
                method,
                prefix: new HarmonyLib.HarmonyMethod(typeof(Plugin), nameof(SimulationPrefix))
            );
        }
    }

    public static void SimulationPrefix(ref float deltaTime)
    {
        if (Instance == null) return;

        // multiply the game's delta time
        deltaTime *= Mathf.Clamp(Instance.timeScale, 0.1f, 50f);
    }

    #endregion

    #region unity life cycle

    private void Update()
    {
        // toggle the menu visibility when the insert key is pressed
        if (Keyboard.current != null && Keyboard.current.insertKey.wasPressedThisFrame)
            showMenu = !showMenu;

        // try to find the game controller if we don't have it yet
        if (controller == null)
            controller = UnityEngine.Object.FindObjectOfType<GameController>();

        // handle no bugs toggle
        if (noBugs && Database.State.Resources.Bugs.Value > 0f)
            Database.State.Resources.Bugs.Value = 0f;

        // handle no ping toggle
        if (noPing)
        {
            Database.State.Resources.Load.Value = 0f;
            Database.State.Resources.LoadPlayers.Value = 0f;
            // prevent players from exceeding datacenter capacity
            Database.State.Resources.Players.Value =
                Math.Min(Database.State.Resources.Players.Value,
                         Database.Derived.DatacenterCapacity.CurrentValue);

            Database.State.Resources.Ping.Value = 5f;
        }
    }

    #endregion

    #region ui components

    private void OnGUI()
    {
        // dont draw anything if the menu is hidden
        if (!showMenu) return;

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
    }

    private void DrawWindow(int id)
    {
        GUILayout.BeginVertical();
        GUILayout.Space(5);

        // draw the top tab navigation
        selectedTab = GUILayout.Toolbar(selectedTab, tabs, GUILayout.Height(30));

        GUILayout.Space(10);

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
            if (double.TryParse(input, out double amount))
                onAdd(amount);
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
        var sequel = Database.State.Sequel;
        if (sequel == null) return;

        // force state into "ready"
        if (sequel.Time != null && sequel.Duration != null)
        {
            sequel.Time.SetValue(sequel.Duration.Value, 0f);
        }
        
        if (sequel.Developing != null)
        {
            sequel.Developing.Value = true;
        }

        // directly finish by invoking the private method
        var method = sequel.GetType().GetMethod(
            "FinishDevelopment",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method?.Invoke(sequel, null);
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
        if (controller == null) return;

        // use reflection to call private unlock methods on the game controller
        MethodInfo method = typeof(GameController).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        method?.Invoke(controller, null);
    }

    #endregion
}