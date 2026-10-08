using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Uses the game's loaded UI assets without cloning its scene controllers or changing their listeners.
internal sealed class TrainerNativeTheme
{
    internal TMP_FontAsset Font, BodyFont;
    internal Sprite Window, Button, Input, Checkmark, SliderTrack, SliderHandle, Tab, SelectedTab, CloseIcon;
    internal SpriteState ButtonStates;
    internal Selectable.Transition ButtonTransition;
    internal ColorBlock ButtonColors;
    internal readonly Color TextColor = new Color32(50, 50, 50, 255);

    internal static TrainerNativeTheme Capture()
    {
        var labels = Resources.FindObjectsOfTypeAll<TMP_Text>().Where(x => x.gameObject.scene.IsValid() &&
            !x.transform.IsChildOf(Plugin.Instance.transform) && x.font != null).ToArray();
        var label = labels.FirstOrDefault(x => x.font.name == "Latin-Main") ?? labels.FirstOrDefault();
        if (label == null) return null;
        // Access serialized images first: inactive popup sprites may not have managed wrappers yet.
        var sceneSprites = Resources.FindObjectsOfTypeAll<Image>().Where(x => x.gameObject.scene.IsValid() &&
            !x.transform.IsChildOf(Plugin.Instance.transform)).Select(x => x.sprite).Where(x => x != null).ToArray();
        var sprites = sceneSprites.Concat(Resources.FindObjectsOfTypeAll<Sprite>()).Distinct().ToArray();
        Sprite Find(string name) => sprites.FirstOrDefault(x => x.name == name);
        var buttons = Resources.FindObjectsOfTypeAll<Button>().Where(x => x.gameObject.scene.IsValid() &&
            !x.transform.IsChildOf(Plugin.Instance.transform)).ToArray();
        var button = buttons.FirstOrDefault(x => x.targetGraphic is Image image && image.sprite?.name == "Windows_Button");
        var tab = buttons.FirstOrDefault(x => x.targetGraphic is Image image && image.sprite?.name == "Tab_Inactive");
        if (Find("Window_Header") == null || Find("Windows_Button") == null || Find("Toggle_Active") == null ||
            Find("Windows_Icons_4") == null || Find("Windows_Slider_Background") == null ||
            Find("Windows_Slider_Handle") == null || Find("Tab_Inactive") == null) return null;
        return new TrainerNativeTheme {
            Font = label.font, BodyFont = labels.FirstOrDefault(x => x.font.name == "English-Main")?.font ?? label.font,
            Window = Find("Window_Header"), Button = Find("Windows_Button"),
            Input = Find("Toggle_Active"), Checkmark = Find("Windows_Icons_4"),
            SliderTrack = Find("Windows_Slider_Background"), SliderHandle = Find("Windows_Slider_Handle"),
            Tab = Find("Tab_Inactive"), SelectedTab = tab?.spriteState.selectedSprite ?? Find("Tab_Active"), CloseIcon = Find("Windows_Icons_3"),
            ButtonStates = button != null ? button.spriteState : default,
            ButtonTransition = button != null ? button.transition : Selectable.Transition.ColorTint,
            ButtonColors = button != null ? button.colors : ColorBlock.defaultColorBlock
        };
    }
}

internal sealed class TrainerNativeControls
{
    private readonly TrainerNativeTheme theme;
    internal TrainerNativeControls(TrainerNativeTheme theme) { this.theme = theme; }

    internal static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    internal static void Stretch(RectTransform rect, float left = 0, float top = 0, float right = 0, float bottom = 0)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    internal Image Image(RectTransform rect, Sprite sprite, Color? color = null, bool raycast = true)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite == null ? UnityEngine.UI.Image.Type.Simple : UnityEngine.UI.Image.Type.Sliced;
        image.color = color ?? Color.white;
        image.raycastTarget = raycast;
        return image;
    }

    internal TextMeshProUGUI Label(Transform parent, string name, string value, float x, float y, float width,
        float height = 30, float size = 18, Color? color = null, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        var text = Rect(parent, name, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = theme.BodyFont;
        text.fontSize = size;
        text.color = color ?? theme.TextColor;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        // The game's pixel fonts have tight line metrics; add spacing in font units.
        text.lineSpacing = text.font.faceInfo.pointSize * 0.6f / Mathf.Max(0.001f, text.font.faceInfo.scale);
        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = false;
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    internal Button Button(Transform parent, string name, string title, float x, float y, float width, float height,
        UnityAction callback, Sprite sprite = null)
    {
        var rect = Rect(parent, name, x, y, width, height);
        var image = Image(rect, sprite ?? theme.Button);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = theme.ButtonTransition;
        button.spriteState = theme.ButtonStates;
        button.colors = theme.ButtonColors;
        var text = Label(rect, "Label", title, 6, 0, width - 12, height, 18, alignment: TextAlignmentOptions.Center);
        text.font = theme.Font;
        Stretch(text.rectTransform, 6, 0, 6, 0);
        if (callback != null) button.onClick.AddListener(callback);
        return button;
    }

    internal TMP_InputField Input(Transform parent, string name, string value, float x, float y, float width, float height, UnityAction<string> changed)
    {
        var rect = Rect(parent, name, x, y, width, height);
        var image = Image(rect, theme.Input);
        var viewport = Rect(rect, "TextArea", 0, 0, width, height);
        Stretch(viewport, 8, 4, 8, 4);
        viewport.gameObject.AddComponent<RectMask2D>();
        var text = Label(viewport, "Text", "", 0, 0, width - 16, height - 8, 18);
        Stretch(text.rectTransform);
        var placeholder = Label(viewport, "Placeholder", "Amount", 0, 0, width - 16, height - 8, 18,
            new Color(0.196f, 0.196f, 0.196f, 0.5f));
        Stretch(placeholder.rectTransform);
        var input = rect.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = image;
        input.textViewport = viewport;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.contentType = TMP_InputField.ContentType.Standard;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 40;
        input.customCaretColor = true;
        input.caretColor = theme.TextColor;
        input.selectionColor = new Color(0.15f, 0.35f, 0.7f, 0.4f);
        input.SetTextWithoutNotify(value);
        input.onValueChanged.AddListener(changed);
        return input;
    }

    internal Toggle Toggle(Transform parent, string name, string title, float y, bool value, UnityAction<bool> changed, float width)
    {
        var rect = Rect(parent, name, 0, y, width, 36);
        Image(rect, null, Color.clear);
        var background = Image(Rect(rect, "Background", 0, 3, 28, 28), theme.Input);
        var check = Image(Rect(background.transform, "Checkmark", 3, 3, 22, 22), theme.Checkmark, raycast: false);
        Label(rect, "Label", title, 40, 0, width - 40, 36);
        var toggle = rect.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = check;
        toggle.SetIsOnWithoutNotify(value);
        toggle.onValueChanged.AddListener(changed);
        return toggle;
    }

    internal Slider Slider(Transform parent, string name, float x, float y, float width, float minimum, float maximum, float value,
        UnityAction<float> changed)
    {
        var rect = Rect(parent, name, x, y, width, 34);
        Image(rect, null, Color.clear);
        Image(Rect(rect, "Track", 0, 10, width, 14), theme.SliderTrack, raycast: false);
        var area = Rect(rect, "HandleArea", 10, 0, width - 20, 34);
        var handle = Image(Rect(area, "Handle", 0, 2, 20, 30), theme.SliderHandle);
        var slider = rect.gameObject.AddComponent<Slider>();
        slider.targetGraphic = handle;
        slider.handleRect = handle.rectTransform;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(changed);
        return slider;
    }

    internal RectTransform ScrollPage(Transform parent, string name, float x, float y, float width, float height, out ScrollRect scroll)
    {
        var rect = Rect(parent, name, x, y, width, height);
        var viewport = Rect(rect, "Viewport", 0, 0, width - 20, height);
        viewport.gameObject.AddComponent<RectMask2D>();
        Image(viewport, null, new Color(0, 0, 0, 0), true);
        var content = Rect(viewport, "Content", 0, 0, width - 24, height);
        var scrollbarRect = Rect(rect, "Scrollbar", width - 18, 0, 18, height);
        Image(scrollbarRect, theme.Input);
        var handle = Image(Rect(scrollbarRect, "Handle", 0, 0, 18, height), theme.Button);
        Stretch(handle.rectTransform);
        var scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.targetGraphic = handle;
        scrollbar.handleRect = handle.rectTransform;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scroll = rect.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return content;
    }
}
