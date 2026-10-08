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
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[BepInPlugin("com.mmo98.trainer.smoke", "MMO98 Trainer Runtime Smoke", "1.0.0")]
[BepInDependency(Plugin.PluginGuid)]
public class RuntimeSmoke : BaseUnityPlugin
{
    private int checks;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private IEnumerator Start()
    {
        string mode = Environment.GetEnvironmentVariable("MMO98_TRAINER_SMOKE_TEST");
        if (mode != "1" && mode != "baseline" && mode != "ui-probe" && mode != "ui-preview") yield break;
        Application.runInBackground = true;
        yield return new WaitForSecondsRealtime(5f);
        if (mode == "ui-preview")
        {
            yield return PreviewUI();
            Application.Quit();
            yield break;
        }
        if (mode == "ui-probe")
        {
            ProbeUI();
            Application.Quit();
            yield break;
        }
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

    [Serializable] private class UISample
    {
        public string path, kind, text, font, sprite;
        public float fontSize;
        public Color color;
        public Vector2 size;
    }
    private static string PathOf(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null) { transform = transform.parent; path = transform.name + "/" + path; }
        return path;
    }
    private void ProbeUI()
    {
        var samples = new List<UISample>();
        foreach (var label in Resources.FindObjectsOfTypeAll<TMP_Text>().Where(x => x.gameObject.scene.IsValid()))
            samples.Add(new UISample { path = PathOf(label.transform), kind = "text", text = label.text, font = label.font?.name,
                fontSize = label.fontSize, color = label.color, size = label.rectTransform.rect.size });
        foreach (var image in Resources.FindObjectsOfTypeAll<Image>().Where(x => x.gameObject.scene.IsValid()))
            samples.Add(new UISample { path = PathOf(image.transform), kind = "image", sprite = image.sprite?.name,
                color = image.color, size = image.rectTransform.rect.size });
        string output = Environment.GetEnvironmentVariable("MMO98_TRAINER_UI_PROBE");
        using (var writer = new System.IO.StreamWriter(output))
        {
            writer.WriteLine("path,kind,text,font,fontSize,sprite,color,size");
            foreach (var sample in samples)
            {
                var values = new[] { sample.path, sample.kind, sample.text, sample.font, sample.fontSize.ToString(),
                    sample.sprite, sample.color.ToString("F3"), sample.size.ToString("F1") };
                writer.WriteLine(string.Join(",", values.Select(x => "\"" + (x ?? "").Replace("\"", "\"\"") + "\"")));
            }
        }
        Logger.LogInfo($"TRAINER_SMOKE_PASS: native UI probe written with {samples.Count} controls.");
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

    private IEnumerator PreviewUI()
    {
        string directory = Environment.GetEnvironmentVariable("MMO98_TRAINER_UI_CAPTURE");
        var plugin = Plugin.Instance;
        Check(Database.State == null && Database.Disposed, "Preview requires the main menu.");
        var state = new DatabaseState();
        Database.State = state;
        Database.Commands = new DatabaseCommands(state);
        Database.Modifiers = new DatabaseModifiers();
        Database.Disposed = false;
        state.Resources.Money.Value = 125000;
        state.Resources.Players.Value = 24350;
        state.Prestige.Fans.Value = 7800;
        state.Prestige.Data.Value = 1400;
        Setting<float>(plugin, "uiScale").Value = 1f;
        Setting<float>(plugin, "simulationSpeed").Value = 1f;
        Field("windowRect").SetValue(plugin, new Rect((Screen.width - 600f) / 2f, (Screen.height - 660f) / 2f, 600, 660));
        Call(plugin, "SetMenuVisible", true);
        Call(plugin, "Update");
        System.IO.Directory.CreateDirectory(directory);
        try
        {
            for (int i = 0; i < 4; i++)
            {
                Call(plugin, "SelectNativeTab", i);
                Call(plugin, "RefreshNativeUI");
                yield return new WaitForSecondsRealtime(0.3f);
                yield return new WaitForEndOfFrame();
                string path = System.IO.Path.Combine(directory, "Trainer-" + i + ".png");
                var texture = ScreenCapture.CaptureScreenshotAsTexture();
                System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.Destroy(texture);
            }
            Logger.LogInfo("TRAINER_SMOKE_PASS: four native UI screenshots captured; no profile loaded or saved.");
        }
        finally { Database.Dispose(); }
    }

    private static T Control<T>(GameObject root, string path) where T : Component => root.transform.Find(path).GetComponent<T>();
    private void VerifyNativeMenu(Plugin plugin)
    {
        Call(plugin, "SetMenuVisible", true);
        var root = (GameObject)Field("nativeRoot").GetValue(plugin);
        Check(root != null && root.activeInHierarchy, "Native trainer canvas was not built.");
        Check(root.GetComponent<Canvas>().renderMode == RenderMode.ScreenSpaceOverlay && root.GetComponent<GraphicRaycaster>() != null,
            "Native canvas or raycaster missing.");
        Check(Resources.FindObjectsOfTypeAll<EventSystem>().Count(x => x.gameObject.scene.IsValid()) == 1,
            "Trainer created a duplicate EventSystem.");
        Check(Control<Image>(root, "Trainer Window").sprite?.name == "Window_Header", "Native window sprite was not reused.");
        Check(Control<TMP_Text>(root, "Trainer Window/Title Bar/Title").font.name == "Latin-Main", "Native font was not reused.");
        Check(!Control<Button>(root, "Trainer Window/Resources Page/Viewport/Content/Money Add").interactable,
            "Resource action enabled without a profile.");
        if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            Canvas.ForceUpdateCanvases();
            var results = new List<RaycastResult>();
            var pointer = new PointerEventData(EventSystem.current) { position = new Vector2(1, 1) };
            EventSystem.current.RaycastAll(pointer, results);
            Check(results.Count > 0 && results[0].gameObject.name == "Modal Blocker", "Modal raycast failed: " +
                string.Join(", ", results.Select(x => x.gameObject.name)) + "; canvas=" + root.GetComponent<RectTransform>().rect +
                "; blocker=" + Control<Image>(root, "Modal Blocker").rectTransform.rect + "; depth=" + Control<Image>(root, "Modal Blocker").depth);
            pointer.position = RectTransformUtility.WorldToScreenPoint(null,
                root.transform.Find("Trainer Window/Resources Page/Viewport/Content/Money Add").position + new Vector3(10, -10));
            results.Clear();
            EventSystem.current.RaycastAll(pointer, results);
            Check(results.Count > 0 && results[0].gameObject.name == "Money Add", "Trainer button failed native UI raycasting.");
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, root.transform.Find("Trainer Window/Title Bar").position + new Vector3(30, -10));
            results.Clear();
            EventSystem.current.RaycastAll(pointer, results);
            Check(results.Count > 0 && results[0].gameObject.name == "Title Bar", "Title bar did not receive native drag input.");
        }
        Control<Button>(root, "Trainer Window/Tab Settings").onClick.Invoke();
        Check(root.transform.Find("Trainer Window/Settings Page").gameObject.activeSelf &&
            !root.transform.Find("Trainer Window/Resources Page").gameObject.activeSelf, "Tab callbacks failed.");
        Check(Control<Slider>(root, "Trainer Window/Settings Page/Viewport/Content/Menu Scale").interactable,
            "Menu settings disabled without a profile.");
        var context = new InputEvent { Input = InputEvent.Key.DashboardView };
        ((InputListener.IHandler)plugin).Handle(context);
        Check(context.Consumed, "Visible trainer allowed game view shortcuts through.");
        context = new InputEvent { Input = InputEvent.Key.Cancel };
        ((InputListener.IHandler)plugin).Handle(context);
        Check(context.Consumed && !root.activeSelf && Field("registeredInput").GetValue(plugin) == null,
            "Cancel did not close the menu and unregister its handler.");
        context = new InputEvent { Input = InputEvent.Key.Pause };
        ((InputListener.IHandler)plugin).Handle(context);
        Check(!context.Consumed, "Hidden trainer blocked game input.");
        Call(plugin, "SetMenuVisible", true);
        Call(plugin, "SelectNativeTab", 0);

    }

    private void VerifyNativeActions(Plugin plugin, DatabaseState state)
    {
        var root = (GameObject)Field("nativeRoot").GetValue(plugin);
        Call(plugin, "RefreshNativeUI");
        var money = Control<TMP_InputField>(root, "Trainer Window/Resources Page/Viewport/Content/Money Input");
        Check(money.interactable, "Resource input did not enable after state became ready.");
        Check(root.GetComponentsInChildren<Selectable>().All(x => x.navigation.mode == Navigation.Mode.Explicit &&
            new[] { x.navigation.selectOnUp, x.navigation.selectOnDown, x.navigation.selectOnLeft, x.navigation.selectOnRight }
                .All(next => next == null || next.transform.IsChildOf(root.transform))), "Keyboard navigation escaped the modal trainer.");
        var addButton = Control<Button>(root, "Trainer Window/Resources Page/Viewport/Content/Money Add");
        EventSystem.current.SetSelectedGameObject(addButton.gameObject);
        var move = new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Right, moveVector = Vector2.right };
        ExecuteEvents.Execute(addButton.gameObject, move, ExecuteEvents.moveHandler);
        Check(EventSystem.current.currentSelectedGameObject ==
            Control<Button>(root, "Trainer Window/Resources Page/Viewport/Content/Money Set").gameObject, "Native arrow navigation failed.");
        money.text = "25";
        state.Resources.Money.Value = 100;
        Control<Button>(root, "Trainer Window/Resources Page/Viewport/Content/Money Add").onClick.Invoke();
        Check(state.Resources.Money.Value == 125, "Native resource input and Add callback failed.");
        money.text = "0";
        Control<Button>(root, "Trainer Window/Resources Page/Viewport/Content/Money Set").onClick.Invoke();
        Check(state.Resources.Money.Value == 0, "Native Set callback failed.");
        Control<Button>(root, "Trainer Window/Resources Page/Viewport/Content/Input 1000000").onClick.Invoke();
        Check(root.GetComponentsInChildren<TMP_InputField>(true).All(x => x.text == "1000000") &&
            (string)Field("moneyInput").GetValue(plugin) == "1000000", "Quick inputs did not update controls and values together.");
        EventSystem.current.SetSelectedGameObject(money.gameObject);
        Call(plugin, "SetMenuVisible", false);
        Check(EventSystem.current.currentSelectedGameObject != money.gameObject, "Hiding the menu retained trainer text focus.");
        Call(plugin, "SetMenuVisible", true);
        Call(plugin, "SelectNativeTab", 2);
        var toggle = Control<Toggle>(root, "Trainer Window/Simulation Page/Viewport/Content/No Bugs");
        toggle.isOn = false;
        toggle.isOn = true;
        Check(Setting<bool>(plugin, "noBugs").Value, "Native toggle did not update the effect setting.");
        Setting<bool>(plugin, "noBugs").Value = false;
        Call(plugin, "RefreshNativeUI");
        Check(!toggle.isOn, "Native toggle did not reflect externally reset effects.");
        var speed = Control<Slider>(root, "Trainer Window/Simulation Page/Viewport/Content/Speed");
        speed.value = 2.3f;
        Check(Mathf.Approximately(Setting<float>(plugin, "simulationSpeed").Value, 2.3f), "Native speed slider failed.");
        ExecuteEvents.Execute(speed.gameObject, new AxisEventData(EventSystem.current) {
            moveDir = MoveDirection.Right, moveVector = Vector2.right }, ExecuteEvents.moveHandler);
        Check(Setting<float>(plugin, "simulationSpeed").Value > 2.3f, "Native slider keyboard adjustment failed.");
        var pause = Control<Button>(root, "Trainer Window/Simulation Page/Viewport/Content/Pause");
        pause.onClick.Invoke();
        Check(state.Studio.Paused.Value, "Native Pause callback failed.");
        pause.onClick.Invoke();
        Check(!state.Studio.Paused.Value, "Native Resume callback failed.");
        Call(plugin, "SelectNativeTab", 3);
        Canvas.ForceUpdateCanvases();
        var scroll = Control<ScrollRect>(root, "Trainer Window/Settings Page");
        var wheel = new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, -3) };
        Vector2 contentBefore = scroll.content.anchoredPosition;
        ExecuteEvents.Execute(scroll.gameObject, wheel, ExecuteEvents.scrollHandler);
        Check(scroll.content.anchoredPosition.y > contentBefore.y, "Native mouse-wheel scrolling failed.");
        Vector2 savedScroll = scroll.content.anchoredPosition;
        Call(plugin, "SelectNativeTab", 0);
        Call(plugin, "SelectNativeTab", 3);
        Check(scroll.content.anchoredPosition == savedScroll, "Switching tabs lost the native scroll position.");
        Control<Slider>(root, "Trainer Window/Settings Page/Viewport/Content/Menu Scale").value = 2;
        Call(plugin, "UpdateNativeUI");
        Canvas.ForceUpdateCanvases();
        var canvas = root.GetComponent<Canvas>();
        var window = Control<RectTransform>(root, "Trainer Window");
        Check(window.rect.width * canvas.scaleFactor <= Screen.width + 1 && window.rect.height * canvas.scaleFactor <= Screen.height + 1,
            "Menu did not fit the screen at maximum scale.");
        Control<Button>(root, "Trainer Window/Settings Page/Viewport/Content/Reset Layout").onClick.Invoke();
        Call(plugin, "UpdateNativeUI");
        Canvas.ForceUpdateCanvases();
        var drag = Control<TrainerWindowDrag>(root, "Trainer Window/Title Bar");
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, drag.transform.position) + new Vector2(30, -10) };
        Vector2 before = window.anchoredPosition;
        drag.OnBeginDrag(pointer);
        pointer.position += new Vector2(40, 20) * canvas.scaleFactor;
        drag.OnDrag(pointer);
        drag.OnEndDrag(pointer);
        Check(Mathf.Abs(window.anchoredPosition.x - before.x - 40) < 1 && Mathf.Abs(window.anchoredPosition.y - before.y - 20) < 1,
            "Window dragging did not account for canvas scaling.");
        Check(Mathf.Approximately(Setting<float>(plugin, "windowX").Value, window.anchoredPosition.x) &&
            Mathf.Approximately(Setting<float>(plugin, "windowY").Value, -window.anchoredPosition.y), "Drag position was not persisted.");
        Control<Button>(root, "Trainer Window/Close").onClick.Invoke();
        Check(!root.activeSelf, "Close button failed.");
        Call(plugin, "SetMenuVisible", true);
    }

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
        VerifyNativeMenu(plugin);

        var savedSettings = new Dictionary<ConfigEntry<bool>, bool>();
        foreach (string name in new[] { "noBugs", "noPing", "fullUptime", "showOnStartup" })
        {
            var setting = Setting<bool>(plugin, name);
            savedSettings.Add(setting, setting.Value);
        }
        var speed = Setting<float>(plugin, "simulationSpeed");
        float savedSpeed = speed.Value;
        var savedLayout = new Dictionary<ConfigEntry<float>, float>();
        foreach (string name in new[] { "uiScale", "windowX", "windowY" })
        {
            var setting = Setting<float>(plugin, name);
            savedLayout.Add(setting, setting.Value);
        }
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
            VerifyNativeActions(plugin, state);
            Call(plugin, "DisableEffects");
            Check(timer.GameSpeedMultiplier == 2 && speed.Value == 1 &&
                !Setting<bool>(plugin, "noBugs").Value && !Setting<bool>(plugin, "noPing").Value && !Setting<bool>(plugin, "fullUptime").Value,
                "Disable effects did not restore normal settings.");
            speed.Value = 3;
            Call(plugin, "Update");
            plugin.enabled = false;
            Check(timer.GameSpeedMultiplier == 2, "Disabling the plugin did not restore game speed.");
            Check((bool)Patch("PingPrefix"), "Disabled plugin still overrides ping.");
            Check(!((GameObject)Field("nativeRoot").GetValue(plugin)).activeSelf && Field("registeredInput").GetValue(plugin) == null,
                "Disabling the plugin left its UI or input handler active.");
            plugin.enabled = true;
        }
        finally
        {
            foreach (var setting in savedSettings) setting.Key.Value = setting.Value;
            foreach (var setting in savedLayout) setting.Key.Value = setting.Value;
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
        Check(Field("nativeRoot").GetValue(plugin) == null && Field("registeredInput").GetValue(plugin) == null,
            "Plugin destruction retained UI references or input registration.");
        Check(Harmony.GetPatchInfo(AccessTools.Method(typeof(PingSimulation), "OnUpdateSimulation"))?.Owners.Contains(Plugin.PluginGuid) != true,
            "Harmony hooks survived plugin destruction.");
    }
}
