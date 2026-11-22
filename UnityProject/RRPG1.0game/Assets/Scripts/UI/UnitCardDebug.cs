using UnityEngine;

/// <summary>
/// 运行时调试信息 — 挂载到 UnitCard Prefab 上
/// 在 Inspector 中查看角色所在排、列、属性
/// </summary>
public class UnitCardDebug : MonoBehaviour
{
    [Header("运行时调试信息（Play 模式下查看）")]
    [SerializeField] private string unitName;
    [SerializeField] private string row;
    [SerializeField] private string column;
    [SerializeField] private int currentHP;
    [SerializeField] private int maxHP;
    [SerializeField] private int actionPoints;
    [SerializeField] private int attack;
    [SerializeField] private bool isDown;
    [SerializeField] private bool inDeathDoor;

    /// <summary>由 BattleUI 调用，更新数据（调试查看用；不承担玩家可见 HUD 职责）</summary>
    public void UpdateInfo(string name, string r, string c, int chp, int mhp, int ap, int atk, bool down, bool deathDoor)
    {
        unitName = name;
        row = r;
        column = c;
        currentHP = chp;
        maxHP = mhp;
        actionPoints = ap;
        attack = atk;
        isDown = down;
        inDeathDoor = deathDoor;
    }
}