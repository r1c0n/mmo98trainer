using System;
using BepInEx.Configuration;
using R3;
using UnityEngine;

public partial class Plugin
{
    private const float WindowWidth = 440f, WindowHeight = 580f;
    private Rect windowRect = new Rect(100, 100, WindowWidth, WindowHeight);
    private bool showMenu;
    private int selectedTab;
    private static readonly string[] Tabs = { "Resources", "Progression", "Simulation", "Settings" };
    private static readonly float[] SpeedPresets = { 0.5f, 1f, 2f, 5f, 10f, 25f, 50f };
    private readonly Vector2[] scrollPositions = new Vector2[Tabs.Length];
    private string moneyInput = "1000", playersInput = "1000", fansInput = "1000", dataInput = "1000";
    private GUIStyle headerStyle, footerStyle, statusStyle;

    private void OnGUI()
    {
        if (!showMenu || uiScale == null) return;
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            footerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
            statusStyle = new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.MiddleLeft };
        }
        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousBackground = GUI.backgroundColor;
        bool previousEnabled = GUI.enabled;
        try
        {
            float scale = Mathf.Min(TrainerNumbers.ClampScale(uiScale.Value),
                Mathf.Min(Screen.width / WindowWidth, Screen.height / WindowHeight));
            if (scale <= 0f) return;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            windowRect.x = ClampPosition(windowRect.x, Screen.width / scale - WindowWidth);
            windowRect.y = ClampPosition(windowRect.y, Screen.height / scale - WindowHeight);
            GUI.backgroundColor = new Color(0.18f, 0.2f, 0.24f, 0.98f);
            windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, $"{PluginName} {PluginVersion}");
        }
        finally
        {
            GUI.matrix = previousMatrix;
            GUI.backgroundColor = previousBackground;
            GUI.enabled = previousEnabled;
        }
    }

    private static float ClampPosition(float value, float maximum) =>
        TrainerNumbers.IsFinite(value) ? Mathf.Clamp(value, 0f, Mathf.Max(0f, maximum)) : 0f;

    private void DrawWindow(int id)
    {
        GUILayout.BeginVertical();
        GUILayout.Space(4);
        selectedTab = GUILayout.Toolbar(selectedTab, Tabs, GUILayout.Height(28));
        GUILayout.Space(8);
        if (IsGameReady)
            GUILayout.Label($"Profile {Database.Profile} | {(Database.State.Studio.Paused.Value ? "Paused" : "Running")} | Speed {simulationSpeed.Value:0.0}x", footerStyle);
        else
            GUILayout.Label("Load a profile to use game actions.", footerStyle);

        scrollPositions[selectedTab] = GUILayout.BeginScrollView(scrollPositions[selectedTab]);
        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && (IsGameReady || selectedTab == 3);
        switch (selectedTab)
        {
            case 0: DrawResources(); break;
            case 1: DrawProgression(); break;
            case 2: DrawSimulation(); break;
            case 3: DrawSettings(); break;
        }
        GUI.enabled = previousEnabled;
        GUILayout.EndScrollView();
        GUILayout.Space(5);
        GUILayout.Label(statusMessage, statusStyle, GUILayout.Height(44));
        if (GUILayout.Button($"Disable all trainer effects ({resetKey.Value})", GUILayout.Height(26))) DisableEffects();
        GUILayout.Label($"{menuKey.Value}: menu | {pauseKey.Value}: pause | by Official B", footerStyle);
        GUILayout.EndVertical();
        GUI.DragWindow(new Rect(0, 0, WindowWidth, 20));
    }

    private void DrawResources()
    {
        GUILayout.Label("Resources", headerStyle);
        GUILayout.Label("Add increases a balance; Set replaces it. Zero and scientific notation are supported.");
        var state = IsGameReady ? Database.State : null;
        DrawResourceRow("Money", ref moneyInput, state?.Resources.Money);
        DrawResourceRow("Players", ref playersInput, state?.Resources.Players);
        DrawResourceRow("Fans", ref fansInput, state?.Prestige.Fans);
        DrawResourceRow("Data", ref dataInput, state?.Prestige.Data);
        GUILayout.Space(8);
        GUILayout.Label("Quick input", headerStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("All inputs: 1,000")) moneyInput = playersInput = fansInput = dataInput = "1000";
        if (GUILayout.Button("All inputs: 1,000,000")) moneyInput = playersInput = fansInput = dataInput = "1000000";
        GUILayout.EndHorizontal();
    }

    private void DrawResourceRow(string label, ref string input, ReactiveProperty<double> resource)
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label($"{label}: {(resource == null ? "-" : resource.Value.ToString("N2"))}");
        GUILayout.BeginHorizontal();
        input = GUILayout.TextField(input, 40, GUILayout.MinWidth(90));
        if (GUILayout.Button("Add", GUILayout.Width(65))) ChangeResource(label, input, resource, true);
        if (GUILayout.Button("Set", GUILayout.Width(65))) ChangeResource(label, input, resource, false);
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void DrawProgression()
    {
        GUILayout.Label("Unlocks", headerStyle);
        GUILayout.Label("These changes are kept in the game's normal saves.");
        DrawUnlockButton("Unlock all upgrades", "UnlockAllUpgrades");
        DrawUnlockButton("Unlock all research", "UnlockAllResearch");
        DrawUnlockButton("Unlock all datacenters", "UnlockAllDatacenters");
        GUILayout.Space(12);
        GUILayout.Label("Development", headerStyle);
        if (IsGameReady)
        {
            var sequel = Database.State.Sequel;
            GUILayout.Label($"Round {sequel.Round.Value} | Quality {sequel.Progress.Normalized:P0}");
            GUILayout.Label(sequel.Developing.Value ? $"Developing: {sequel.Time.Value:0.0} / {sequel.Duration.Value:0.0} s" : "No cycle currently running.");
        }
        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && developmentSupported && developmentSimulation != null;
        if (GUILayout.Button("Complete a free development cycle", GUILayout.Height(30))) InstantDevelopmentCycle();
        GUI.enabled = previousEnabled;
        if (GUILayout.Button("Set all sequel quality to 100%", GUILayout.Height(30))) MaximizeSequelQuality();
        GUILayout.Space(12);
        GUILayout.Label("Datacenters", headerStyle);
        if (GUILayout.Button("Repair degraded / critical datacenters", GUILayout.Height(30))) RepairDatacenters();
    }

    private void DrawUnlockButton(string label, string method)
    {
        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && controller != null && unlockMethods.ContainsKey(method);
        if (GUILayout.Button(label, GUILayout.Height(30))) UnlockAll(method);
        GUI.enabled = previousEnabled;
    }

    private void DrawSimulation()
    {
        GUILayout.Label("Performance", headerStyle);
        DrawEffectToggle(noBugs, "No bugs", bugsSupported);
        DrawEffectToggle(noPing, "Zero ping", pingSupported);
        DrawEffectToggle(fullUptime, "100% uptime", uptimeSupported);
        if (IsGameReady)
        {
            var resources = Database.State.Resources;
            GUILayout.Label($"Bugs {resources.Bugs.Value:N1} | Ping {resources.Ping.Value:N1} ms");
            GUILayout.Label($"Load {resources.Load.Value:P1} | Uptime {resources.Uptime.Value:P1}");
        }
        GUILayout.Space(12);
        GUILayout.Label("Simulation speed", headerStyle);
        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && gameTimer != null;
        GUILayout.Label($"Multiplier: {simulationSpeed.Value:0.0}x");
        simulationSpeed.Value = Mathf.Round(GUILayout.HorizontalSlider(simulationSpeed.Value, 0.1f, 50f) * 10f) / 10f;
        GUILayout.BeginHorizontal();
        foreach (float speed in SpeedPresets)
            if (GUILayout.Button($"{speed:0.#}x")) simulationSpeed.Value = speed;
        GUILayout.EndHorizontal();
        GUI.enabled = previousEnabled;
        GUILayout.Label("Speed affects game timers and all simulations. The game's pause state is respected.");
        if (GUILayout.Button(IsGameReady && Database.State.Studio.Paused.Value ? "Resume game" : "Pause game", GUILayout.Height(30))) TogglePause();
    }

    private static void DrawEffectToggle(ConfigEntry<bool> setting, string label, bool available)
    {
        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && available;
        setting.Value = GUILayout.Toggle(setting.Value, available ? label : label + " (unavailable)");
        GUI.enabled = previousEnabled;
    }

    private void DrawSettings()
    {
        GUILayout.Label("Menu preferences", headerStyle);
        showOnStartup.Value = GUILayout.Toggle(showOnStartup.Value, "Show menu on startup");
        GUILayout.Label($"Menu scale: {uiScale.Value:0.00}x");
        uiScale.Value = Mathf.Round(GUILayout.HorizontalSlider(uiScale.Value, 0.75f, 2f) * 20f) / 20f;
        if (GUILayout.Button("Reset menu position and scale"))
        {
            uiScale.Value = 1f;
            windowRect.position = new Vector2(100, 100);
            windowX.Value = windowY.Value = 100f;
        }
        GUILayout.Space(12);
        GUILayout.Label("Shortcuts", headerStyle);
        GUILayout.Label($"Menu: {menuKey.Value}\nPause: {pauseKey.Value}\nDisable effects: {resetKey.Value}");
        GUILayout.Label("Change shortcuts in BepInEx/config/com.mmo98.trainer.cfg while the game is closed. Settings and effects are saved automatically.");
        GUILayout.Space(12);
        GUILayout.Label("Compatibility", headerStyle);
        GUILayout.Label($"Game: {Application.version}\nTrainer: {PluginVersion}");
        GUILayout.Label(unavailableFeatures.Count == 0 ? "All trainer hooks and unlock actions are available." :
            "Unavailable in this build:\n" + string.Join("\n", unavailableFeatures));
    }
}
