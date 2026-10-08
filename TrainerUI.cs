using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class Plugin : InputListener.IHandler
{
    private const float WindowWidth = 600f, WindowHeight = 660f;
    private Rect windowRect = new Rect(100, 100, WindowWidth, WindowHeight);
    private bool showMenu;
    private int selectedTab;
    private static readonly string[] Tabs = { "Resources", "Progression", "Simulation", "Settings" };
    private static readonly float[] SpeedPresets = { 0.5f, 1f, 2f, 5f, 10f, 25f, 50f };
    private string moneyInput = "1000", playersInput = "1000", fansInput = "1000", dataInput = "1000";
    private GameObject nativeRoot, previousSelection;
    private RectTransform nativeWindow;
    private Canvas nativeCanvas;
    private CanvasScaler nativeScaler;
    private TrainerNativeTheme nativeTheme;
    private TrainerNativeControls controls;
    private InputListener registeredInput;
    private float nextNativeSearch, nextNativeRefresh;
    private readonly List<Action> nativeBindings = new List<Action>();
    private readonly List<TMP_InputField> resourceInputs = new List<TMP_InputField>();
    private readonly List<GameObject> nativePages = new List<GameObject>();
    private readonly List<Button> nativeTabs = new List<Button>();

    // InputListener orders handlers from lowest to highest priority. A visible trainer is modal.
    int InputListener.IHandler.Priority => int.MinValue;
    void InputListener.IHandler.Handle(InputEvent context)
    {
        if (!showMenu || nativeRoot == null || !nativeRoot.activeInHierarchy) return;
        context.Consume();
        if (context.Input == InputEvent.Key.Cancel) SetMenuVisible(false);
    }

    private void UpdateNativeUI()
    {
        if (nativeRoot == null)
        {
            if (Time.unscaledTime < nextNativeSearch) return;
            nextNativeSearch = Time.unscaledTime + 1f;
            // Share the game's Input System event module; never create a competing EventSystem.
            if (EventSystem.current == null || (nativeTheme = TrainerNativeTheme.Capture()) == null) return;
            BuildNativeUI();
            SetMenuVisible(showMenu);
        }
        if (!showMenu) return;
        if (!nativeRoot.activeSelf) SetMenuVisible(true);
        UpdateInputRegistration();
        float scale = Mathf.Min(TrainerNumbers.ClampScale(uiScale.Value),
            Mathf.Min(Screen.width / WindowWidth, Screen.height / WindowHeight));
        if (scale <= 0f) return;
        nativeScaler.scaleFactor = nativeCanvas.scaleFactor = scale;
        windowRect.x = ClampPosition(windowRect.x, Screen.width / scale - WindowWidth);
        windowRect.y = ClampPosition(windowRect.y, Screen.height / scale - WindowHeight);
        nativeWindow.anchoredPosition = new Vector2(windowRect.x, -windowRect.y);
        if (Time.unscaledTime >= nextNativeRefresh) RefreshNativeUI();
    }

    private static float ClampPosition(float value, float maximum) =>
        TrainerNumbers.IsFinite(value) ? Mathf.Clamp(value, 0f, Mathf.Max(0f, maximum)) : 0f;

    private void RefreshNativeUI()
    {
        nextNativeRefresh = Time.unscaledTime + 0.15f;
        foreach (var binding in nativeBindings) binding();
    }

    private void SetMenuVisible(bool visible)
    {
        showMenu = visible;
        if (nativeRoot == null) return;
        if (visible && isActiveAndEnabled)
        {
            if (!nativeRoot.activeSelf)
            {
                previousSelection = EventSystem.current?.currentSelectedGameObject;
                EventSystem.current?.SetSelectedGameObject(null);
                nativeRoot.SetActive(true);
            }
            UpdateInputRegistration();
            RefreshNativeUI();
        }
        else HideNativeUI();
    }

    private void UpdateInputRegistration()
    {
        var listener = InputListener.HasInstance ? InputListener.Instance : null;
        if (registeredInput == listener) return;
        if (registeredInput != null) registeredInput.Unregister(this);
        registeredInput = listener;
        if (registeredInput != null) registeredInput.Register(this);
    }

    private void HideNativeUI()
    {
        if (registeredInput != null) registeredInput.Unregister(this);
        registeredInput = null;
        if (nativeRoot == null || !nativeRoot.activeSelf) return;
        var selected = EventSystem.current?.currentSelectedGameObject;
        bool restoreSelection = selected == null || selected.transform.IsChildOf(nativeRoot.transform);
        if (selected != null && selected.transform.IsChildOf(nativeRoot.transform))
            EventSystem.current.SetSelectedGameObject(null);
        nativeRoot.SetActive(false);
        if (restoreSelection && previousSelection != null && previousSelection.activeInHierarchy)
            EventSystem.current?.SetSelectedGameObject(previousSelection);
        previousSelection = null;
    }

    private void DestroyNativeUI()
    {
        HideNativeUI();
        if (nativeRoot != null) Destroy(nativeRoot);
        nativeRoot = null;
        nativeBindings.Clear();
        resourceInputs.Clear();
        nativePages.Clear();
        nativeTabs.Clear();
    }

    private void BuildNativeUI()
    {
        controls = new TrainerNativeControls(nativeTheme);
        var canvasRect = TrainerNativeControls.Rect(transform, "MMO98 Trainer Canvas", 0, 0, 0, 0);
        nativeRoot = canvasRect.gameObject;
        nativeRoot.SetActive(false);
        nativeCanvas = nativeRoot.AddComponent<Canvas>();
        nativeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        nativeCanvas.sortingOrder = 31000;
        nativeScaler = nativeRoot.AddComponent<CanvasScaler>();
        nativeScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        nativeRoot.AddComponent<GraphicRaycaster>();
        var blocker = TrainerNativeControls.Rect(canvasRect, "Modal Blocker", 0, 0, 0, 0);
        TrainerNativeControls.Stretch(blocker);
        controls.Image(blocker, null, new Color(0, 0, 0, 0.32f));
        nativeWindow = TrainerNativeControls.Rect(canvasRect, "Trainer Window", windowRect.x, windowRect.y, WindowWidth, WindowHeight);
        controls.Image(nativeWindow, nativeTheme.Window);
        var title = TrainerNativeControls.Rect(nativeWindow, "Title Bar", 4, 4, WindowWidth - 48, 36);
        controls.Image(title, null, Color.clear);
        controls.Label(title, "Title", $"{PluginName} {PluginVersion}", 14, 0, WindowWidth - 86, 36, 23, Color.white).font = nativeTheme.Font;
        var drag = title.gameObject.AddComponent<TrainerWindowDrag>();
        drag.Window = nativeWindow;
        drag.CanvasRect = canvasRect;
        drag.Changed = () => {
            windowRect.position = new Vector2(nativeWindow.anchoredPosition.x, -nativeWindow.anchoredPosition.y);
            windowRect.x = ClampPosition(windowRect.x, Screen.width / nativeCanvas.scaleFactor - WindowWidth);
            windowRect.y = ClampPosition(windowRect.y, Screen.height / nativeCanvas.scaleFactor - WindowHeight);
            nativeWindow.anchoredPosition = new Vector2(windowRect.x, -windowRect.y);
        };
        drag.Finished = () => { windowX.Value = windowRect.x; windowY.Value = windowRect.y; };
        var close = controls.Button(nativeWindow, "Close", "", WindowWidth - 40, 7, 30, 30, () => SetMenuVisible(false));
        controls.Image(TrainerNativeControls.Rect(close.transform, "Icon", 6, 6, 18, 18), nativeTheme.CloseIcon, raycast: false);
        for (int i = 0; i < Tabs.Length; i++)
        {
            int tab = i;
            var button = controls.Button(nativeWindow, "Tab " + Tabs[i], Tabs[i], 20 + i * 140, 52, 140, 36,
                () => SelectNativeTab(tab), nativeTheme.Tab);
            button.transition = Selectable.Transition.ColorTint;
            nativeTabs.Add(button);
            var content = controls.ScrollPage(nativeWindow, Tabs[i] + " Page", 24, 124, WindowWidth - 48, 414, out var scroll);
            nativePages.Add(scroll.gameObject);
            switch (i)
            {
                case 0: BuildResources(content); break;
                case 1: BuildProgression(content); break;
                case 2: BuildSimulation(content); break;
                case 3: BuildSettings(content); break;
            }
        }
        BindLabel(nativeWindow, "Profile Status", 24, 90, 552, 28, () => IsGameReady ?
            $"Profile {Database.Profile}  |  {(Database.State.Studio.Paused.Value ? "Paused" : "Running")}  |  {simulationSpeed.Value:0.#}x speed" :
            "Load a profile to use game actions.", 16);
        BindLabel(nativeWindow, "Action Status", 24, 548, 552, 42, () => statusMessage, 16);
        var reset = controls.Button(nativeWindow, "Disable Effects", "", 24, 594, 552, 32, DisableEffects);
        nativeBindings.Add(() => reset.GetComponentInChildren<TMP_Text>().text = $"Disable all trainer effects ({resetKey.Value})");
        BindLabel(nativeWindow, "Shortcuts", 24, 629, 552, 22,
            () => $"{menuKey.Value}: menu   |   {pauseKey.Value}: pause   |   Esc: close   |   by Official B", 14);
        SelectNativeTab(selectedTab);
        Logger.LogInfo("Native trainer UI created using " + nativeTheme.Font.name + " and the game's window sprites.");
    }

    private void SelectNativeTab(int tab)
    {
        selectedTab = Mathf.Clamp(tab, 0, Tabs.Length - 1);
        var selected = EventSystem.current?.currentSelectedGameObject;
        if (selected != null && nativePages.Exists(page => selected.transform.IsChildOf(page.transform)))
            EventSystem.current.SetSelectedGameObject(null);
        for (int i = 0; i < nativePages.Count; i++)
        {
            nativePages[i].SetActive(i == selectedTab);
            ((Image)nativeTabs[i].targetGraphic).sprite = i == selectedTab ?
                (nativeTheme.SelectedTab ?? nativeTheme.Tab) : nativeTheme.Tab;
        }
    }

    private TMP_Text BindLabel(Transform parent, string name, float x, float y, float width, float height,
        Func<string> value, float size = 18)
    {
        var label = controls.Label(parent, name, "", x, y, width, height, size);
        nativeBindings.Add(() => label.text = value());
        return label;
    }

    private void Gate(Selectable control, Func<bool> available) => nativeBindings.Add(() => control.interactable = available());

    private void BuildResources(RectTransform content)
    {
        controls.Label(content, "Help", "Add increases a balance; Set replaces it. Use zero or scientific notation, such as 1e6.", 0, 0, 520, 52, 16);
        BuildResourceRow(content, "Money", 60, () => moneyInput, x => moneyInput = x, () => Database.State.Resources.Money);
        BuildResourceRow(content, "Players", 142, () => playersInput, x => playersInput = x, () => Database.State.Resources.Players);
        BuildResourceRow(content, "Fans", 224, () => fansInput, x => fansInput = x, () => Database.State.Prestige.Fans);
        BuildResourceRow(content, "Data", 306, () => dataInput, x => dataInput = x, () => Database.State.Prestige.Data);
        controls.Label(content, "Quick Input", "Quick input", 0, 392, 520);
        controls.Button(content, "Input 1000", "All inputs: 1,000", 0, 428, 252, 36, () => SetResourceInputs("1000"));
        controls.Button(content, "Input 1000000", "All inputs: 1,000,000", 264, 428, 252, 36, () => SetResourceInputs("1000000"));
        content.sizeDelta = new Vector2(content.sizeDelta.x, 480);
    }

    private void BuildResourceRow(RectTransform content, string name, float y, Func<string> value,
        Action<string> changed, Func<ReactiveProperty<double>> resource)
    {
        BindLabel(content, name + " Balance", 0, y, 520, 30, () => name + ": " + (IsGameReady ? resource().Value.ToString("N2") : "—"));
        var input = controls.Input(content, name + " Input", value(), 0, y + 34, 300, 36, x => changed(x));
        resourceInputs.Add(input);
        Gate(input, () => IsGameReady);
        foreach (bool add in new[] { true, false })
        {
            bool increase = add;
            var button = controls.Button(content, name + (add ? " Add" : " Set"), add ? "Add" : "Set",
                add ? 312 : 420, y + 34, 96, 36,
                () => { if (IsGameReady) ChangeResource(name, value(), resource(), increase); });
            Gate(button, () => IsGameReady);
        }
    }

    private void SetResourceInputs(string value)
    {
        moneyInput = playersInput = fansInput = dataInput = value;
        foreach (var input in resourceInputs) input.SetTextWithoutNotify(value);
    }

    private void BuildProgression(RectTransform content)
    {
        controls.Label(content, "Help", "Unlocks and resource changes are kept in the game's normal saves.", 0, 0, 520, 42, 16);
        string[] labels = { "Unlock all upgrades", "Unlock all research", "Unlock all datacenters" };
        string[] methods = { "UnlockAllUpgrades", "UnlockAllResearch", "UnlockAllDatacenters" };
        for (int i = 0; i < methods.Length; i++)
        {
            string method = methods[i];
            var button = controls.Button(content, method, labels[i], 0, 50 + i * 42, 516, 36, () => UnlockAll(method));
            Gate(button, () => IsGameReady && controller != null && unlockMethods.ContainsKey(method));
        }
        controls.Label(content, "Development", "Development", 0, 186, 520);
        BindLabel(content, "Development Status", 0, 220, 520, 56, () => {
            if (!IsGameReady) return "Load a profile to view development.";
            var sequel = Database.State.Sequel;
            return $"Round {sequel.Round.Value}  |  Quality {sequel.Progress.Normalized:P0}\n" +
                (sequel.Developing.Value ? $"Developing: {sequel.Time.Value:0.0} / {sequel.Duration.Value:0.0} s" : "No cycle currently running.");
        }, 16);
        var cycle = controls.Button(content, "Complete Cycle", "Complete a free development cycle", 0, 284, 516, 36, InstantDevelopmentCycle);
        Gate(cycle, () => IsGameReady && developmentSupported && developmentSimulation != null);
        var quality = controls.Button(content, "Maximize Quality", "Set all sequel quality to 100%", 0, 326, 516, 36, MaximizeSequelQuality);
        Gate(quality, () => IsGameReady);
        var repair = controls.Button(content, "Repair Datacenters", "Repair degraded / critical datacenters", 0, 382, 516, 36, RepairDatacenters);
        Gate(repair, () => IsGameReady);
        content.sizeDelta = new Vector2(content.sizeDelta.x, 434);
    }

    private void BuildSimulation(RectTransform content)
    {
        BuildEffectToggle(content, "No Bugs", "No bugs", 0, noBugs, () => bugsSupported);
        BuildEffectToggle(content, "Zero Ping", "Zero ping", 40, noPing, () => pingSupported);
        BuildEffectToggle(content, "Full Uptime", "100% uptime", 80, fullUptime, () => uptimeSupported);
        BindLabel(content, "Performance", 0, 126, 520, 56, () => IsGameReady ?
            $"Bugs {Database.State.Resources.Bugs.Value:N1}  |  Ping {Database.State.Resources.Ping.Value:N1} ms\n" +
            $"Load {Database.State.Resources.Load.Value:P1}  |  Uptime {Database.State.Resources.Uptime.Value:P1}" : "Load a profile to view performance.", 16);
        BindLabel(content, "Speed Label", 0, 196, 520, 30, () => $"Simulation speed: {simulationSpeed.Value:0.#}x");
        var slider = controls.Slider(content, "Speed", 0, 234, 516, 0.1f, 50f, simulationSpeed.Value,
            x => simulationSpeed.Value = Mathf.Round(x * 10f) / 10f);
        nativeBindings.Add(() => slider.SetValueWithoutNotify(simulationSpeed.Value));
        Gate(slider, () => IsGameReady && gameTimer != null);
        for (int i = 0; i < SpeedPresets.Length; i++)
        {
            float speed = SpeedPresets[i];
            var button = controls.Button(content, "Speed " + speed, $"{speed:0.#}x", i * 74, 280, 70, 36, () => simulationSpeed.Value = speed);
            Gate(button, () => IsGameReady && gameTimer != null);
        }
        controls.Label(content, "Speed Help", "Speed affects all game timers and simulations. The game's pause state is respected.", 0, 332, 520, 52, 16);
        var pause = controls.Button(content, "Pause", "", 0, 392, 516, 36, TogglePause);
        nativeBindings.Add(() => pause.GetComponentInChildren<TMP_Text>().text = IsGameReady && Database.State.Studio.Paused.Value ? "Resume game" : "Pause game");
        Gate(pause, () => IsGameReady);
        content.sizeDelta = new Vector2(content.sizeDelta.x, 444);
    }

    private void BuildEffectToggle(RectTransform content, string name, string label, float y, ConfigEntry<bool> setting, Func<bool> supported)
    {
        var toggle = controls.Toggle(content, name, label, y, setting.Value, x => setting.Value = x, 516);
        nativeBindings.Add(() => toggle.SetIsOnWithoutNotify(setting.Value));
        Gate(toggle, () => IsGameReady && supported());
        nativeBindings.Add(() => toggle.GetComponentInChildren<TMP_Text>().text = supported() ? label : label + " (unavailable)");
    }

    private void BuildSettings(RectTransform content)
    {
        var startup = controls.Toggle(content, "Show On Startup", "Show menu on startup", 0, showOnStartup.Value, x => showOnStartup.Value = x, 516);
        nativeBindings.Add(() => startup.SetIsOnWithoutNotify(showOnStartup.Value));
        BindLabel(content, "Scale Label", 0, 50, 520, 30, () => $"Menu scale: {uiScale.Value:0.00}x");
        var slider = controls.Slider(content, "Menu Scale", 0, 88, 516, 0.75f, 2f, uiScale.Value,
            x => uiScale.Value = Mathf.Round(x * 20f) / 20f);
        nativeBindings.Add(() => slider.SetValueWithoutNotify(uiScale.Value));
        controls.Button(content, "Reset Layout", "Reset menu position and scale", 0, 132, 516, 36, () => {
            uiScale.Value = 1f;
            windowRect.position = new Vector2(100, 100);
            windowX.Value = windowY.Value = 100f;
        });
        controls.Label(content, "Shortcuts", "Shortcuts", 0, 188, 520);
        BindLabel(content, "Shortcut Values", 0, 224, 520, 78,
            () => $"Menu: {menuKey.Value}\nPause: {pauseKey.Value}\nDisable effects: {resetKey.Value}", 16);
        controls.Label(content, "Config Help", "Change shortcuts in BepInEx/config/com.mmo98.trainer.cfg while the game is closed. Settings save automatically.", 0, 306, 520, 62, 16);
        controls.Label(content, "Compatibility", "Compatibility", 0, 384, 520);
        BindLabel(content, "Versions", 0, 420, 520, 56, () => $"Game: {Application.version}\nTrainer: {PluginVersion}", 16);
        BindLabel(content, "Hooks", 0, 480, 520, 100, () => unavailableFeatures.Count == 0 ?
            "All trainer hooks and unlock actions are available." : "Unavailable in this build:\n" + string.Join("\n", unavailableFeatures), 16);
        content.sizeDelta = new Vector2(content.sizeDelta.x, 596);
    }
}
