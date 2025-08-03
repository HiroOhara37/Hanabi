using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController2D : MonoBehaviour
{
    public float zoomSpeed = 240f;
    public float minZoom = 10f;
    public float maxZoom = 150f;

    private Vector3 lastMousePos;

    void Update()
    {
        HandleZoom();
        HandleDrag();
    }

    void HandleZoom()
    {
        // マウスのスクロール入力を取得（新InputSystem）
        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.001f)
        {
            if (Camera.main != null)
            {
                Camera.main.orthographicSize -= scroll * zoomSpeed * Time.deltaTime;
                Camera.main.orthographicSize = Mathf.Clamp(Camera.main.orthographicSize, minZoom, maxZoom);
            }
        }
    }

    void HandleDrag()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            lastMousePos = Mouse.current.position.ReadValue();
        }

        if (Mouse.current.rightButton.isPressed)
        {
            Vector3 currentMousePos = Mouse.current.position.ReadValue();
            Vector3 delta = currentMousePos - lastMousePos;

            Vector3 move = Camera.main.ScreenToWorldPoint(lastMousePos) - Camera.main.ScreenToWorldPoint(lastMousePos + delta);
            move.z = 0;

            Camera.main.transform.position += move;
            lastMousePos = currentMousePos;
        }
    }
}
