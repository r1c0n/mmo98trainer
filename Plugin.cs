using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.Mono;
using UnityEngine;
using UnityEngine.InputSystem;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public partial class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.mmo98.trainer";
    public const string PluginName = "MMO98 Trainer";
    public const string PluginVersion = "1.1.0";
    public static Plugin Instance { get; private set; }

    private HarmonyLib.Harmony harmony;
    private GameController controller;
    private GameTimer gameTimer;
    private float originalGameSpeed;
    private float nextReferenceSearch;
    private float nextConfigSave;
    private DatabaseState currentState;
    private SequelDevelopSimulation developmentSimulation;
    private readonly Dictionary<string, MethodInfo> unlockMethods = new Dictionary<string, MethodInfo>();
    private readonly List<string> unavailableFeatures = new List<string>();
    private bool pingSupported, bugsSupported, uptimeSupported, developmentSupported;
    private string statusMessage = "Ready.";

    private ConfigEntry<Key> menuKey, pauseKey, resetKey;
    private ConfigEntry<bool> showOnStartup, noBugs, noPing, fullUptime;
    private ConfigEntry<float> simulationSpeed, uiScale, windowX, windowY;

    internal static bool IsGameReady => Instance != null && Instance.isActiveAndEnabled &&
        !Database.Disposed && Database.State != null && Database.Commands != null;

    private void Awake()
    {
        Instance = this;
        Config.SaveOnConfigSet = false;
        BindSettings();
        showMenu = showOnStartup.Value;
        windowRect.position = new Vector2(windowX.Value, windowY.Value);
        Config.SettingChanged += (_, __) => nextConfigSave = Time.unscaledTime + 1f;

        harmony = new HarmonyLib.Harmony(PluginGuid);
        pingSupported = TryPatch(typeof(PingSimulation), "OnUpdateSimulation", nameof(PingPrefix));
        bugsSupported = TryPatch(typeof(BugSimulation), "OnUpdateSimulation", postfix: nameof(BugsPostfix));
        uptimeSupported = TryPatch(typeof(UptimeSimulation), "OnUpdateSimulation", nameof(UptimePrefix));
        bool registered = TryPatch(typeof(SequelDevelopSimulation), "Registered", postfix: nameof(DevelopmentRegistered));
        bool unregistered = TryPatch(typeof(SequelDevelopSimulation), "Unregistered", postfix: nameof(DevelopmentUnregistered));
        developmentSupported = registered && unregistered;

        foreach (string name in new[] { "UnlockAllUpgrades", "UnlockAllResearch", "UnlockAllDatacenters" })
        {
            var method = HarmonyLib.AccessTools.DeclaredMethod(typeof(GameController), name, Type.EmptyTypes);
            if (method == null)
            {
                unavailableFeatures.Add(name);
                Logger.LogWarning($"Unavailable game action: {name}");
            }
            else unlockMethods.Add(name, method);
        }
        Config.Save();
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded for game {Application.version}; {unavailableFeatures.Count} unavailable hooks/actions.");
    }

    private void BindSettings()
    {
        menuKey = Config.Bind("Shortcuts", "Menu", Key.Insert, new ConfigDescription("Show or hide the trainer.", new TrainerShortcutKeys(Key.Insert)));
        pauseKey = Config.Bind("Shortcuts", "Pause", Key.F6, new ConfigDescription("Toggle the game's pause state while a profile is loaded.", new TrainerShortcutKeys(Key.F6)));
        resetKey = Config.Bind("Shortcuts", "DisableEffects", Key.F8, new ConfigDescription("Disable trainer effects and restore normal simulation speed.", new TrainerShortcutKeys(Key.F8)));
        showOnStartup = Config.Bind("Menu", "ShowOnStartup", true, "Show the trainer when the game starts.");
        uiScale = Config.Bind("Menu", "Scale", 1f, new ConfigDescription("Menu scale; automatically fits smaller screens.", new AcceptableValueRange<float>(0.75f, 2f)));
        windowX = Config.Bind("Menu", "WindowX", 100f, "Last menu position, before UI scaling.");
        windowY = Config.Bind("Menu", "WindowY", 100f, "Last menu position, before UI scaling.");
        noBugs = Config.Bind("Effects", "NoBugs", false, "Keep bugs at zero.");
        noPing = Config.Bind("Effects", "ZeroPing", false, "Keep ping at zero without changing player count or capacity.");
        fullUptime = Config.Bind("Effects", "FullUptime", false, "Keep uptime at 100 percent.");
        simulationSpeed = Config.Bind("Effects", "Speed", 1f,
            new ConfigDescription("Simulation speed relative to the game's normal speed.", new AcceptableValueRange<float>(0.1f, 50f)));
        uiScale.Value = TrainerNumbers.ClampScale(uiScale.Value);
        simulationSpeed.Value = TrainerNumbers.ClampSpeed(simulationSpeed.Value);
    }

    private bool TryPatch(Type type, string methodName, string prefix = null, string postfix = null)
    {
        try
        {
            var method = HarmonyLib.AccessTools.DeclaredMethod(type, methodName);
            if (method == null) throw new MissingMethodException(type.FullName, methodName);
            harmony.Patch(method,
                prefix: prefix == null ? null : new HarmonyLib.HarmonyMethod(typeof(Plugin), prefix),
                postfix: postfix == null ? null : new HarmonyLib.HarmonyMethod(typeof(Plugin), postfix));
            return true;
        }
        catch (Exception ex)
        {
            unavailableFeatures.Add(type.Name + "." + methodName);
            Logger.LogError($"Could not patch {type.Name}.{methodName}: {ex}");
            return false;
        }
    }

    internal static bool PingPrefix()
    {
        if (!IsGameReady || !Instance.pingSupported || !Instance.noPing.Value) return true;
        Database.State.Resources.Ping.SetValue(0f, 0f);
        return false;
    }

    internal static void BugsPostfix()
    {
        if (IsGameReady && Instance.bugsSupported && Instance.noBugs.Value)
            Database.State.Resources.Bugs.SetValue(0f, 0f);
    }

    internal static bool UptimePrefix()
    {
        if (!IsGameReady || !Instance.uptimeSupported || !Instance.fullUptime.Value) return true;
        Database.State.Resources.Uptime.SetValue(1f, 0f);
        return false;
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

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            // Menu and reset take precedence if the user assigns overlapping shortcuts.
            if (keyboard[menuKey.Value].wasPressedThisFrame) showMenu = !showMenu;
            else if (keyboard[resetKey.Value].wasPressedThisFrame) DisableEffects();
            else if (IsGameReady && keyboard[pauseKey.Value].wasPressedThisFrame) TogglePause();
        }
        if (nextConfigSave > 0f && Time.unscaledTime >= nextConfigSave)
        {
            Config.Save();
            nextConfigSave = 0f;
        }
        if (showMenu && Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            windowX.Value = windowRect.x;
            windowY.Value = windowRect.y;
        }
        if (!IsGameReady)
        {
            RestoreGameSpeed();
            controller = null;
            currentState = null;
            developmentSimulation = null;
            return;
        }
        if (!ReferenceEquals(currentState, Database.State))
        {
            RestoreGameSpeed();
            currentState = Database.State;
            statusMessage = "Ready.";
            nextReferenceSearch = 0f;
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
        // GameTimer applies its maxStepTime to every command and simulation, including intervals.
        if (gameTimer != null) gameTimer.GameSpeedMultiplier = originalGameSpeed * TrainerNumbers.ClampSpeed(simulationSpeed.Value);
        BugsPostfix();
        PingPrefix();
        UptimePrefix();
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
        Config.Save();
        if (Instance == this) Instance = null;
    }

    private void DisableEffects()
    {
        noBugs.Value = noPing.Value = fullUptime.Value = false;
        simulationSpeed.Value = 1f;
        if (gameTimer != null) gameTimer.GameSpeedMultiplier = originalGameSpeed;
        statusMessage = "Trainer effects disabled; normal speed restored.";
    }

    private void TogglePause() => RunAction(() =>
    {
        Database.State.Studio.Paused.Value = !Database.State.Studio.Paused.Value;
        return Database.State.Studio.Paused.Value ? "Game paused." : "Game resumed.";
    });

    private void RunAction(Func<string> action)
    {
        if (!IsGameReady) { statusMessage = "Load a profile first."; return; }
        try { statusMessage = action(); }
        catch (Exception ex)
        {
            statusMessage = "Action failed. See the BepInEx log for details.";
            Logger.LogError(ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex);
        }
    }

    private void ChangeResource(string label, string input, R3.ReactiveProperty<double> resource, bool add)
    {
        if (!TrainerNumbers.TryAmount(input, out double amount))
        {
            statusMessage = "Enter a finite, non-negative number (for example 1000 or 1e6).";
            return;
        }
        RunAction(() =>
        {
            if (!TrainerNumbers.TryBalance(resource.Value, amount, add, out double result))
                return "That amount would exceed the supported numeric range.";
            resource.SetValue(result, 0.0);
            return $"{label} {(add ? "increased by" : "set to")} {amount:G6}.";
        });
    }

    private void InstantDevelopmentCycle() => RunAction(() =>
    {
        if (!developmentSupported || developmentSimulation == null) return "Development simulation is not ready.";
        var sequel = Database.State.Sequel;
        // Completing a free cycle uses the registered simulation so round, cost and notifications stay consistent.
        sequel.Time.SetValue(sequel.Duration.Value, 0f);
        sequel.Developing.Value = true;
        developmentSimulation.OnUpdateSimulation(0f);
        return "Development cycle completed.";
    });

    private void MaximizeSequelQuality() => RunAction(() =>
    {
        var progress = Database.State.Sequel.Progress;
        progress.GameDesign.Value = progress.Art.Value = progress.Netcode.Value = progress.Marketing.Value = progress.Qa.Value = 1f;
        progress.FactorRange.Value = Vector2.one;
        return "All sequel quality categories set to 100 percent.";
    });

    private void RepairDatacenters() => RunAction(() =>
    {
        int count = 0;
        // Snapshot keys because game commands can publish events that change the collection.
        var keys = new List<Datacenter>();
        foreach (var entry in Database.State.Datacenters.Details) keys.Add(entry.Key);
        foreach (var key in keys)
        {
            var state = Database.State.Datacenters.GetState(key);
            if (state != DatacenterState.Degraded && state != DatacenterState.Critical) continue;
            Database.Commands.Datacenters.Restore(key);
            count++;
        }
        return $"Repaired {count} datacenter(s).";
    });

    private void UnlockAll(string methodName) => RunAction(() =>
    {
        if (controller == null || !unlockMethods.TryGetValue(methodName, out var method))
            return "This unlock action is unavailable in this game build.";
        if (methodName == "UnlockAllDatacenters")
        {
            // The game's helper adds entries for unfinished construction, even when that key exists.
            foreach (var entry in Database.State.Datacenters.Details)
            {
                if (entry.Value.State.Value != DatacenterState.Construction &&
                    entry.Value.State.Value != DatacenterState.Unprovisioned) continue;
                entry.Value.ReprovisionProgress.Value = 0f;
                entry.Value.State.Value = DatacenterState.Nominal;
            }
        }
        method.Invoke(controller, null);
        return "Unlocks applied.";
    });
}
