using TMPro;
using UnityEngine;

/// <summary>Narrow programmatic whitebox skin, using the current shared font.</summary>
public static class HomeProductUI
{
    public static RectTransform Rect(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
        var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=anchor;rt.pivot=anchor;rt.anchoredPosition=position;rt.sizeDelta=size;
        return rt;
    }
    public static RectTransform Panel(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size,Color color)
    {
        var rt=Rect(parent,name,anchor,position,size);var image=rt.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;
        return rt;
    }
    public static TMP_Text Text(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size,string value,int fontSize=20)
    {
        var rt=Rect(parent,name,anchor,position,size);var text=rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.font=CurrentUIFontAuthority.Default;text.fontSize=fontSize;text.text=value;text.color=new Color(.9f,.94f,.91f);
        text.raycastTarget=false;text.enableWordWrapping=true;text.overflowMode=TextOverflowModes.Ellipsis;text.alignment=TextAlignmentOptions.TopLeft;
        return text;
    }
    public static UnityEngine.UI.Button Button(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size,string label,System.Action action)
    {
        var rt=Panel(parent,name,anchor,position,size,new Color(.14f,.24f,.24f,.94f));
        var button=rt.gameObject.AddComponent<UnityEngine.UI.Button>();button.onClick.AddListener(()=>action?.Invoke());
        var text=Text(rt,"Label",Vector2.zero,Vector2.zero,size,label);text.alignment=TextAlignmentOptions.Center;
        return button;
    }
}
