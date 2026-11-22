using UnityEngine;

/// <summary>Presentation camera only; no clock or save dependency.</summary>
public sealed class HomeWorldCamera : MonoBehaviour
{
    public Camera ViewCamera { get; private set; }
    private Vector3 _lastMouse;
    public bool PrototypeEnabled { get; private set; }
    public void SetPrototype(bool enabled)
    {
        PrototypeEnabled=enabled;
        ViewCamera.backgroundColor=enabled ? new Color(.24f,.34f,.29f) : new Color(.075f,.13f,.13f);
        Zoom(0);
    }
    public void Initialize()
    {
        ViewCamera=gameObject.AddComponent<Camera>();
        ViewCamera.orthographic=true; ViewCamera.orthographicSize=7.5f;
        ViewCamera.cullingMask=1<<ProductHomeWorldHost.WorldLayer; ViewCamera.depth=0;
        ViewCamera.clearFlags=CameraClearFlags.SolidColor; ViewCamera.backgroundColor=new Color(.075f,.13f,.13f);
        ViewCamera.nearClipPlane=.1f; ViewCamera.farClipPlane=100;
        transform.position=ProductHomeWorldHost.WorldOrigin+new Vector3(0,0,-10);
    }
    public void Focus(Vector3 point) => transform.position=new Vector3(point.x,point.y,transform.position.z);
    public void Pan(Vector2 delta)
    {
        var point=transform.position+new Vector3(delta.x,delta.y,0);
        if(PrototypeEnabled)
        {
            point.x=Mathf.Clamp(point.x,ProductHomeWorldHost.WorldOrigin.x-18,ProductHomeWorldHost.WorldOrigin.x+18);
            point.y=Mathf.Clamp(point.y,-12,12);
        }
        transform.position=point;
    }
    public void Zoom(float delta) => ViewCamera.orthographicSize=Mathf.Clamp(ViewCamera.orthographicSize+delta,PrototypeEnabled ? 4f : 2.5f,PrototypeEnabled ? 11f : 18f);
    public void ReadInput(bool overUI,bool suppressRightPan=false)
    {
        Vector3 mouse=Input.mousePosition;
        if (!overUI)
        {
            if ((!suppressRightPan && Input.GetMouseButton(1)) || Input.GetMouseButton(2))
            {
                if (!Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
                {
                    float scale=ViewCamera.orthographicSize*2/Mathf.Max(1,Screen.height);
                    Pan((Vector2)(_lastMouse-mouse)*scale);
                }
            }
            Zoom(-Input.mouseScrollDelta.y*.55f);
        }
        _lastMouse=mouse;
    }
}
