using UnityEngine;

/// <summary>R2 candidate, not final art. Session-only ground shapes; no roads or navigation facts.</summary>
public sealed class HomeVisualPrototype : MonoBehaviour
{
    private Material _material;
    public void Initialize()
    {
        _material=new Material(Shader.Find("Sprites/Default"));
        Patch("Meadow",Vector2.zero,new Vector2(32,22),new Color(.43f,.54f,.37f),-20000);
        Patch("Village clearing",new Vector2(0,1),new Vector2(24,14),new Color(.57f,.62f,.43f),-19999);
        Patch("Living quarter",new Vector2(-1,4),new Vector2(22,4),new Color(.64f,.66f,.49f),-19998);
        Patch("Earth",new Vector2(-6,-2),new Vector2(8,6),new Color(.56f,.48f,.32f),-19997);
        Patch("Common green",new Vector2(1,-2),new Vector2(5,4),new Color(.46f,.58f,.39f),-19997);
        Patch("Visual path",new Vector2(0,1),new Vector2(22,1.0f),new Color(.76f,.69f,.49f),-19996);
        Patch("Visual path branch",new Vector2(3,-1.5f),new Vector2(1.15f,5),new Color(.76f,.69f,.49f),-19996);
    }
    private void Patch(string name,Vector2 center,Vector2 size,Color color,int order)
    {
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
        go.layer=ProductHomeWorldHost.WorldLayer;go.transform.SetParent(transform,false);
        go.transform.localPosition=new Vector3(center.x,center.y,0);
        float x=size.x*.5f,y=size.y*.5f,c=Mathf.Min(x,y)*.25f;
        var mesh=new Mesh();
        mesh.vertices=new[]{new Vector3(-x+c,-y),new Vector3(x-c,-y),new Vector3(x,-y+c),new Vector3(x,y-c),new Vector3(x-c,y),new Vector3(-x+c,y),new Vector3(-x,y-c),new Vector3(-x,-y+c)};
        mesh.triangles=new[]{0,1,2,0,2,3,0,3,4,0,4,5,0,5,6,0,6,7};
        var colors=new Color[8];for(int i=0;i<colors.Length;i++) colors[i]=QualitySettings.activeColorSpace==ColorSpace.Linear ? color.linear : color;mesh.colors=colors;
        mesh.RecalculateBounds();go.GetComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=_material;renderer.sortingOrder=order;
    }
    private void OnDestroy()
    {
        foreach(var filter in GetComponentsInChildren<MeshFilter>(true)) Destroy(filter.sharedMesh);
        if(_material!=null) Destroy(_material);
    }
}
