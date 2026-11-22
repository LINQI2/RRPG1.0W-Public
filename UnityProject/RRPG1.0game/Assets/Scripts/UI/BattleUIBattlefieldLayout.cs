using UnityEngine;

public partial class BattleUI : MonoBehaviour
{
    public RectTransform GetRowRect(bool isPlayer, BattleRow row) => isPlayer
        ? (row == BattleRow.Front ? playerFrontRow : row == BattleRow.Middle ? playerMiddleRow : playerBackRow)
        : (row == BattleRow.Front ? enemyFrontRow : row == BattleRow.Middle ? enemyMiddleRow : enemyBackRow);

    private BattleRow RectToBattleRow(RectTransform row)
    {
        if (row == playerFrontRow || row == enemyFrontRow) return BattleRow.Front;
        if (row == playerMiddleRow || row == enemyMiddleRow) return BattleRow.Middle;
        return BattleRow.Back;
    }

    private int ColToIdx(BattleColumn col) => col == BattleColumn.Left ? 0 : col == BattleColumn.Right ? 2 : 1;
    private BattleColumn IdxToCol(int idx) => idx == 0 ? BattleColumn.Left : idx == 2 ? BattleColumn.Right : BattleColumn.Center;

    public RectTransform HitTestRow(Vector2 screenPos)
    {
        var corners = new Vector3[4];
        foreach (var row in _allRows)
        {
            if (row == null) continue;
            row.GetWorldCorners(corners);
            if (screenPos.x >= corners[0].x && screenPos.x <= corners[2].x &&
                screenPos.y >= corners[0].y && screenPos.y <= corners[2].y)
                return row;
        }
        return null;
    }

    /// <summary>
    /// 列视觉偏移（WP-D）：不再读取固定 Column Offset，统一转交 Formation Presentation，
    /// 使单位卡、目标标记与拖拽预览共用同一套排内视觉位置。
    /// </summary>
    private float GetColumnOffset(BattleColumn col)
    {
        return GetFormationColumnOffset(col);
    }

}
