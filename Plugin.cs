using BepInEx;
using BepInEx.Unity.Mono;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

[BepInPlugin("com.mmo98.trainer", "MMO98 Trainer", "1.1.0")]
public class Plugin : BaseUnityPlugin
{
    private Rect windowRect = new Rect(100, 100, 340, 500);
    private bool showMenu = true;

    private int selectedTab = 0;
    private string[] tabs = { "Recovery", "Unlocks", "Game" };

    private string moneyInput = "1000";
    private string playersInput = "1000";
    private string fansInput = "1000";
    private string dataInput = "1000";

    private GameController controller;

    private Vector2 scrollPos;
    private float windowHeight = 400f;

    private bool noBugs;
    private bool noPing;
    private float timeScale = 1f;

    public static Plugin Instance;

    private void Awake()
    {
        Instance = this;

        var harmony = new HarmonyLib.Harmony("com.mmo98.trainer");

        foreach (var type in typeof(IIncrementalSimulation).Assembly.GetTypes())
        {
            if (!typeof(IIncrementalSimulation).IsAssignableFrom(type))
                continue;

            if (type.IsInterface || type.IsAbstract)
                continue;

            var method = type.GetMethod(
                "OnUpdateSimulation",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (method == null)
            {
                var map = type.GetInterfaceMap(typeof(IIncrementalSimulation));
                method = map.TargetMethods.FirstOrDefault(m => m.Name.Contains("OnUpdateSimulation"));
            }

            if (method == null) continue;

            harmony.Patch(
                method,
                prefix: new HarmonyLib.HarmonyMethod(typeof(Plugin), nameof(SimulationPrefix))
            );
        }
    }

    public static void SimulationPrefix(ref float deltaTime)
    {
        if (Instance == null) return;

        deltaTime *= Mathf.Clamp(Instance.timeScale, 0.1f, 50f);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.insertKey.wasPressedThisFrame)
            showMenu = !showMenu;

        if (controller == null)
            controller = UnityEngine.Object.FindObjectOfType<GameController>();

        if (noBugs && Database.State.Resources.Bugs.Value > 0f)
            Database.State.Resources.Bugs.Value = 0f;

        if (noPing)
        {
            Database.State.Resources.Load.Value = 0f;
            Database.State.Resources.LoadPlayers.Value = 0f;
            Database.State.Resources.Players.Value =
                Math.Min(Database.State.Resources.Players.Value,
                         Database.Derived.DatacenterCapacity.CurrentValue);

            Database.State.Resources.Ping.Value = 5f;
        }
    }

    private void OnGUI()
    {
        if (!showMenu) return;

        windowRect = GUI.Window(0, windowRect, DrawWindow, "MMO98 Trainer");
    }

    private void DrawWindow(int id)
    {
        GUILayout.BeginVertical();

        selectedTab = GUILayout.Toolbar(selectedTab, tabs);

        GUILayout.Space(5);

        scrollPos = GUILayout.BeginScrollView(scrollPos);

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

        GUILayout.EndVertical();

        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    // =========================
    // RECOVERY TAB
    // =========================
    private void DrawRecoveryTab()
    {
        // MONEY
        GUILayout.Label("Money Amount:");
        moneyInput = GUILayout.TextField(moneyInput);

        GUILayout.Space(5);

        if (GUILayout.Button("Add Money"))
        {
            if (double.TryParse(moneyInput, out double amount))
                AddMoney(amount);
        }

        GUILayout.Space(10);

        // PLAYERS
        GUILayout.Label("Players:");
        playersInput = GUILayout.TextField(playersInput);

        if (GUILayout.Button("Add Players"))
        {
            if (double.TryParse(playersInput, out double amount))
                AddPlayers(amount);
        }

        GUILayout.Space(10);

        // TRUE FANS
        GUILayout.Label("Fans:");
        fansInput = GUILayout.TextField(fansInput);

        if (GUILayout.Button("Add True Fans"))
        {
            if (double.TryParse(fansInput, out double amount))
                AddFans(amount);
        }

        GUILayout.Space(10);

        // DATA
        GUILayout.Label("Data:");
        dataInput = GUILayout.TextField(dataInput);

        if (GUILayout.Button("Add Data"))
        {
            if (double.TryParse(dataInput, out double amount))
                AddData(amount);
        }
    }

    // =========================
    // UNLOCK TAB
    // =========================
    private void DrawUnlocksTab()
    {
        if (GUILayout.Button("Unlock All Upgrades"))
            UnlockAll("UnlockAllUpgrades");

        GUILayout.Space(5);

        if (GUILayout.Button("Unlock All Research"))
            UnlockAll("UnlockAllResearch");

        GUILayout.Space(5);

        if (GUILayout.Button("Unlock All Datacenters"))
            UnlockAll("UnlockAllDatacenters");
    }
    
    // =========================
    // GAME TAB
    // =========================
    private void DrawGameTab()
    {
        GUILayout.Space(5);
        GUILayout.Label("=== PERFORMANCE ===");

        noBugs = GUILayout.Toggle(noBugs, "No Bugs");
        noPing = GUILayout.Toggle(noPing, "No Ping");

        GUILayout.Space(10);
        GUILayout.Label("=== DEV TOOLS ===");

        if (GUILayout.Button("Instant Development Cycle"))
        {
            InstantDevelopmentCycle();
        }

        GUILayout.Space(15);
        GUILayout.Label("=== TIME ===");

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Speed: {timeScale:F1}x", GUILayout.Width(80));
        timeScale = GUILayout.HorizontalSlider(timeScale, 0.1f, 50f);
        
        if (GUILayout.Button("Reset", GUILayout.Width(60)))
        {
            timeScale = 1f;
        }
        GUILayout.EndHorizontal();
    }

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

        // directly finish
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

        MethodInfo method = typeof(GameController).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        method?.Invoke(controller, null);
    }
}