using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.Mono;
using HarmonyLib;
using UnityEngine;

[BepInPlugin("com.mmo98.trainer.smoke", "MMO98 Trainer Runtime Smoke", "1.0.0")]
[BepInDependency(Plugin.PluginGuid)]
public class RuntimeSmoke : BaseUnityPlugin
{
    private int checks;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private IEnumerator Start()
    {
        string mode = Environment.GetEnvironmentVariable("MMO98_TRAINER_SMOKE_TEST");
        if (mode != "1" && mode != "baseline") yield break;
        yield return new WaitForSecondsRealtime(5f);
        if (mode == "baseline")
        {
            Logger.LogInfo("TRAINER_SMOKE_BASELINE: main-menu startup and shutdown only.");
            Application.Quit();
            yield break;
        }
        try { Run(); Logger.LogInfo($"TRAINER_SMOKE_PASS: {checks} assertions; no profile loaded or saved."); }
        catch (Exception ex) { Logger.LogError("TRAINER_SMOKE_FAIL: " + ex); }
        finally { Application.Quit(); }
    }

    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }
    private static FieldInfo Field(string name) => typeof(Plugin).GetField(name, PrivateInstance);
    private static ConfigEntry<T> Setting<T>(Plugin plugin, string name) => (ConfigEntry<T>)Field(name).GetValue(plugin);
    private static object Call(Plugin plugin, string name, params object[] arguments) =>
        typeof(Plugin).GetMethod(name, PrivateInstance).Invoke(plugin, arguments);
    private static object Patch(string name) => typeof(Plugin).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
    private static bool SkipPresentation() => false;

    private void Run()
    {
        Check(Database.State == null && Database.Disposed && Database.Commands == null,
            "Smoke tests require the main menu with no profile loaded.");
        var plugin = Plugin.Instance;
        Check(plugin != null, "Trainer did not load.");
        foreach (var target in new[] {
            AccessTools.Method(typeof(PingSimulation), "OnUpdateSimulation"),
            AccessTools.Method(typeof(BugSimulation), "OnUpdateSimulation"),
            AccessTools.Method(typeof(UptimeSimulation), "OnUpdateSimulation"),
            AccessTools.Method(typeof(SequelDevelopSimulation), "Registered"),
            AccessTools.Method(typeof(SequelDevelopSimulation), "Unregistered") })
            Check(Harmony.GetPatchInfo(target)?.Owners.Contains(Plugin.PluginGuid) == true, "Hook missing: " + target);
        Check((bool)Patch("PingPrefix"), "Ping hook suppressed original without a profile.");
        Check((bool)Patch("UptimePrefix"), "Uptime hook suppressed original without a profile.");
        Patch("BugsPostfix");

        var savedSettings = new Dictionary<ConfigEntry<bool>, bool>();
        foreach (string name in new[] { "noBugs", "noPing", "fullUptime" })
        {
            var setting = Setting<bool>(plugin, name);
            savedSettings.Add(setting, setting.Value);
        }
        var speed = Setting<float>(plugin, "simulationSpeed");
        float savedSpeed = speed.Value;
        GameObject timerObject = null;
        GameObject controllerObject = null;
        DatabaseState state = null;
        SequelDevelopSimulation development = null;
        ScriptableObject ping = null, bugs = null, uptime = null;
        var presentation = new Harmony("com.mmo98.trainer.smoke.presentation");
        try
        {
            state = new DatabaseState();
            Database.State = state;
            Database.Commands = new DatabaseCommands(state);
            Database.Modifiers = new DatabaseModifiers();
            Database.Disposed = false;
            // Game logic runs against real catalogs. Suppress presentation that needs a loaded game scene.
            var skip = new HarmonyMethod(typeof(RuntimeSmoke), nameof(SkipPresentation));
            presentation.Patch(AccessTools.PropertyGetter(typeof(UI), "CurrentView"), prefix: skip);
            foreach (var method in typeof(Audio).GetMethods(BindingFlags.Static | BindingFlags.Public).Where(x => x.Name == "PlaySfx"))
                presentation.Patch(method, prefix: skip);
            foreach (var method in typeof(DatabaseCommands.IRCCommands).GetMethods().Where(x => x.Name == "Print"))
                presentation.Patch(method, prefix: skip);
            Setting<bool>(plugin, "noPing").Value = true;
            Setting<bool>(plugin, "noBugs").Value = true;
            Setting<bool>(plugin, "fullUptime").Value = true;
            state.Resources.Players.Value = 12345;
            state.Resources.Ping.Value = 99;
            ping = ScriptableObject.CreateInstance<PingSimulation>();
            ((PingSimulation)ping).OnUpdateSimulation(0.1f);
            Check(state.Resources.Ping.Value == 0, "Native ping prefix failed.");
            Check(state.Resources.Players.Value == 12345, "Zero ping changed players.");
            state.Resources.Bugs.Value = 50;
            bugs = ScriptableObject.CreateInstance<BugSimulation>();
            ((BugSimulation)bugs).OnUpdateSimulation(0.1f);
            Check(state.Resources.Bugs.Value == 0, "Native bugs postfix failed.");
            state.Resources.Uptime.Value = 0.2f;
            uptime = ScriptableObject.CreateInstance<UptimeSimulation>();
            ((UptimeSimulation)uptime).OnUpdateSimulation(0.1f);
            Check(state.Resources.Uptime.Value == 1, "Native uptime prefix failed.");
            Setting<bool>(plugin, "noPing").Value = false;
            Check((bool)Patch("PingPrefix"), "Disabled ping did not restore original.");
            Setting<bool>(plugin, "fullUptime").Value = false;
            Check((bool)Patch("UptimePrefix"), "Disabled uptime did not restore original.");
            state.Resources.Money.Value = 10;
            Call(plugin, "ChangeResource", "Money", "5", state.Resources.Money, true);
            Check(state.Resources.Money.Value == 15, "Resource add failed.");
            Call(plugin, "ChangeResource", "Money", "0", state.Resources.Money, false);
            Check(state.Resources.Money.Value == 0, "Resource set zero failed.");
            Call(plugin, "ChangeResource", "Money", "NaN", state.Resources.Money, true);
            Check(state.Resources.Money.Value == 0, "Invalid input changed money.");
            state.Resources.Money.Value = double.MaxValue;
            Call(plugin, "ChangeResource", "Money", double.MaxValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture), state.Resources.Money, true);
            Check(state.Resources.Money.Value == double.MaxValue, "Overflow changed money.");
            state.Resources.Money.Value = 100;
            development = ScriptableObject.CreateInstance<SequelDevelopSimulation>();
            development.Registered(null);
            Check(ReferenceEquals(Field("developmentSimulation").GetValue(plugin), development), "Registered development simulation was not captured.");
            Call(plugin, "InstantDevelopmentCycle");
            Check(state.Sequel.Round.Value == 1 && !state.Sequel.Developing.Value && state.Sequel.Progress.Normalized > 0,
                "Free development cycle did not complete through game logic.");
            Check(state.Sequel.Cost.Value > 0 && state.Resources.Money.Value == 100, "Development did not update next cost or charged for a free cycle.");
            state.Sequel.Developing.Value = true;
            state.Sequel.Duration.Value = 20;
            state.Sequel.Time.Value = 2;
            Call(plugin, "InstantDevelopmentCycle");
            Check(state.Sequel.Round.Value == 2 && state.Sequel.Time.Value == 0 && state.Sequel.Duration.Value == 0,
                "Completing an existing cycle failed.");
            development.Unregistered();
            Check(Field("developmentSimulation").GetValue(plugin) == null, "Unregistered simulation was retained.");
            Call(plugin, "MaximizeSequelQuality");
            Check(state.Sequel.Progress.Normalized == 1 && state.Sequel.Progress.FactorRange.Value == Vector2.one, "Sequel quality or factor range failed.");
            Call(plugin, "TogglePause");
            Check(state.Studio.Paused.Value, "Pause failed.");
            Call(plugin, "TogglePause");
            Check(!state.Studio.Paused.Value, "Resume failed.");
            var firstDatacenter = CatalogProvider.Datacenters.Collection.First().ID;
            state.Datacenters.Details.Add(firstDatacenter, new DatacenterDetails(DatacenterState.Degraded, 0, 0, 0f));
            Call(plugin, "RepairDatacenters");
            Check(state.Datacenters.GetState(firstDatacenter) == DatacenterState.Nominal, "Datacenter repair failed.");
            state.Datacenters.GetDetails(firstDatacenter).State.Value = DatacenterState.Construction;
            controllerObject = new GameObject("TrainerSmokeController");
            Field("controller").SetValue(plugin, controllerObject.AddComponent<GameController>());
            Call(plugin, "UnlockAll", "UnlockAllDatacenters");
            Check(state.Datacenters.Details.Count == CatalogProvider.Datacenters.Collection.Count() &&
                state.Datacenters.GetState(firstDatacenter) == DatacenterState.Nominal,
                "Datacenter unlock failed with an existing construction entry.");
            Call(plugin, "UnlockAll", "UnlockAllDatacenters");
            Check(state.Datacenters.Details.Count == CatalogProvider.Datacenters.Collection.Count(), "Repeated datacenter unlock failed.");
            Call(plugin, "UnlockAll", "UnlockAllUpgrades");
            Check(state.Upgrades.Unlocked.Count == CatalogProvider.Upgrades.Collection.Count(), "Upgrade unlock failed.");
            Call(plugin, "UnlockAll", "UnlockAllResearch");
            Check(state.Research.Unlocked.Count == CatalogProvider.Research.Collection.Count(), "Research unlock failed.");
            timerObject = new GameObject("TrainerSmokeTimer");
            var timer = timerObject.AddComponent<GameTimer>();
            timer.GameSpeedMultiplier = 2;
            speed.Value = 5;
            Call(plugin, "Update");
            Check(timer.GameSpeedMultiplier == 10, "Game speed did not scale from its original multiplier.");
            Call(plugin, "DisableEffects");
            Check(timer.GameSpeedMultiplier == 2 && speed.Value == 1 && savedSettings.Keys.All(x => !x.Value), "Disable effects did not restore normal settings.");
            speed.Value = 3;
            Call(plugin, "Update");
            plugin.enabled = false;
            Check(timer.GameSpeedMultiplier == 2, "Disabling the plugin did not restore game speed.");
            Check((bool)Patch("PingPrefix"), "Disabled plugin still overrides ping.");
            plugin.enabled = true;
        }
        finally
        {
            foreach (var setting in savedSettings) setting.Key.Value = setting.Value;
            speed.Value = savedSpeed;
            plugin.Config.Save();
            if (development != null) { development.Unregistered(); UnityEngine.Object.DestroyImmediate(development); }
            presentation.UnpatchSelf();
            if (controllerObject != null) UnityEngine.Object.DestroyImmediate(controllerObject);
            if (ping != null) UnityEngine.Object.DestroyImmediate(ping);
            if (bugs != null) UnityEngine.Object.DestroyImmediate(bugs);
            if (uptime != null) UnityEngine.Object.DestroyImmediate(uptime);
            // GameTimer.OnDestroy disposes the isolated database. No timer was ever resumed.
            if (timerObject != null) UnityEngine.Object.DestroyImmediate(timerObject);
            else if (state != null) Database.Dispose();
        }
        Check(Database.Disposed && Database.State == null, "Isolated state was not disposed before quitting.");
        UnityEngine.Object.DestroyImmediate(plugin);
        Check(Plugin.Instance == null, "Plugin singleton survived destruction.");
        Check(Harmony.GetPatchInfo(AccessTools.Method(typeof(PingSimulation), "OnUpdateSimulation"))?.Owners.Contains(Plugin.PluginGuid) != true,
            "Harmony hooks survived plugin destruction.");
    }
}
