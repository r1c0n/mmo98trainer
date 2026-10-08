using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Pointer coordinates are converted through the canvas, so dragging works at every menu scale.
public sealed class TrainerWindowDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    internal RectTransform Window;
    internal RectTransform CanvasRect;
    internal Action Changed, Finished;
    private Vector2 offset;
    private bool dragging;

    public void OnBeginDrag(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(CanvasRect, data.position, data.pressEventCamera, out var point)) return;
        offset = Window.anchoredPosition - (point - new Vector2(CanvasRect.rect.xMin, CanvasRect.rect.yMax));
        dragging = true;
    }
    public void OnDrag(PointerEventData data)
    {
        if (!dragging || !RectTransformUtility.ScreenPointToLocalPointInRectangle(CanvasRect, data.position, data.pressEventCamera, out var point)) return;
        Window.anchoredPosition = point - new Vector2(CanvasRect.rect.xMin, CanvasRect.rect.yMax) + offset;
        Changed?.Invoke();
    }
    public void OnEndDrag(PointerEventData data)
    {
        if (!dragging) return;
        dragging = false;
        Finished?.Invoke();
    }
}
