using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>现有 BattleUI 的表现消费接缝，Gameplay 仍由既有同步路径确定。</summary>
public partial class BattleUI
{
    public BattleFeedbackProfile feedbackProfile = new BattleFeedbackProfile();
    public bool feedbackEnabled = true;
    public BattleFeedbackPlayer feedbackPlayer;
    public BattleValuePresenter actionPointPresenter;
    public bool IsFeedbackPlaying => _feedbackBusy;
    public IReadOnlyList<BattleFeedbackRecord> LastFeedbackRecords => _lastFeedback.AsReadOnly();
    private readonly List<BattleFeedbackRecord> _lastFeedback = new List<BattleFeedbackRecord>();
    private bool _feedbackBusy, _feedbackFinalizing;
    private IDisposable _feedbackBatch;
    private RectTransform _selectedFeedbackCard;
    private Vector3 _selectedFeedbackScale;
    private Vector2 _selectedFeedbackPosition;
    private readonly Dictionary<Image, Color> _targetColors = new Dictionary<Image, Color>();

    private BattleFeedbackPlayer EnsureFeedbackPlayer()
    {
        if (feedbackPlayer == null) feedbackPlayer = GetComponent<BattleFeedbackPlayer>();
        if (feedbackPlayer == null)
        {
            feedbackPlayer = gameObject.AddComponent<BattleFeedbackPlayer>();
            feedbackPlayer.Instant = BattleFeedbackPlayer.DefaultInstant;
        }
        if (feedbackProfile == null) feedbackProfile = new BattleFeedbackProfile();
        return feedbackPlayer;
    }

    private GameObject FeedbackUnitView(string id)
    {
        if (_rowCards == null || string.IsNullOrEmpty(id)) return null;
        foreach (var row in _rowCards.Values)
        {
            var card = row.Find(c => c != null && c.name == "Card_" + id);
            if (card != null) return card;
        }
        return null;
    }

    private void RestoreFeedbackSelection()
    {
        if (_selectedFeedbackCard != null)
        {
            _selectedFeedbackCard.localScale = _selectedFeedbackScale;
            _selectedFeedbackCard.anchoredPosition = _selectedFeedbackPosition;
        }
        _selectedFeedbackCard = null;
        foreach (var entry in _targetColors) if (entry.Key != null) entry.Key.color = entry.Value;
        _targetColors.Clear();
    }

    private void SelectFeedbackCard(BattleCard card)
    {
        RestoreFeedbackSelection();
        if (!feedbackEnabled || card == null || handContainer == null) return;
        EnsureFeedbackPlayer();
        _selectedFeedbackCard = handContainer.Find("V1Card_" + card.CardId) as RectTransform;
        if (_selectedFeedbackCard != null)
        {
            _selectedFeedbackScale = _selectedFeedbackCard.localScale;
            _selectedFeedbackPosition = _selectedFeedbackCard.anchoredPosition;
            _selectedFeedbackCard.localScale *= feedbackProfile.CardSelectScale;
            _selectedFeedbackCard.anchoredPosition += feedbackProfile.CardSelectOffset;
        }
        var context = EnsureRuleContext();
        if (context == null) return;
        var legal = BattleCardRuleResolver.GetLegalManualTargets(card, context);
        foreach (var target in legal)
        {
            if (target.Kind != BattleRuleTargetKind.Unit) continue;
            var unit = FindBattleUnit(target.UnitId);
            if (!CanV1CardReachTarget(card, unit)) continue;
            var view = FeedbackUnitView(target.UnitId);
            var image = view != null ? view.transform.Find(HostAnchorVisual + "/" + HostBodyVisual)?.GetComponent<Image>() : null;
            if (image == null) continue;
            _targetColors[image] = image.color;
            image.color = feedbackProfile.TargetHighlight;
        }
    }

    private void ExecuteCardWithFeedback(BattleCard card, Action<List<BattleFeedbackRecord>> execute)
    {
        if (_feedbackBusy) return;
        RestoreFeedbackSelection();
        _lastFeedback.Clear();
        _feedbackBatch = v1Runtime.BeginPresentationBatch();
        _feedbackBusy = true;
        try
        {
            int apBefore = v1Runtime.ActionPoints.Current;
            if (!v1Runtime.PayCardCost(card)) { CompleteFeedback(false); return; }
            var caster = GetV1CardCaster(card);
            string source = caster != null ? caster.UnitId : null;
            _lastFeedback.Add(new BattleFeedbackRecord { Type = BattleFeedbackType.CardCommit,
                CardInstanceId = card.CardId, CardTemplateId = card.TemplateId, SourceUnitId = source });
            _lastFeedback.Add(new BattleFeedbackRecord { Type = BattleFeedbackType.ActionPoints,
                APBefore = apBefore, APAfter = v1Runtime.ActionPoints.Current, APMax = v1Runtime.ActionPoints.Max });
            _lastFeedback.Add(new BattleFeedbackRecord { Type = BattleFeedbackType.ActorAction, SourceUnitId = source });
            execute(_lastFeedback);
            v1Runtime.ResolvePlayedCard(card);
            _v1PendingCard = null;
            ClearRuleCellMarkers();
            SyncDownedPersonCards();
            StartFeedback(false);
        }
        catch { CompleteFeedback(false); throw; }
    }

    private void AddRuleFeedback(BattleCard card, BattleCardRuleResolutionResult resolution, List<BattleFeedbackRecord> records)
    {
        // 使用 Commit 时已经捕获的 source；结算后牌主可能已 Down，不能再次按存活规则查找。
        string source = records.First(r => r.Type == BattleFeedbackType.CardCommit).SourceUnitId;
        foreach (var outcome in resolution.Outcomes)
        {
            BattleFeedbackType type;
            switch (outcome.Operation)
            {
                case BattleRuleOperation.Heal: type = BattleFeedbackType.Heal; break;
                case BattleRuleOperation.Shield: type = BattleFeedbackType.Shield; break;
                case BattleRuleOperation.Move: type = BattleFeedbackType.Move; break;
                case BattleRuleOperation.Swap: type = BattleFeedbackType.Swap; break;
                default: type = BattleFeedbackType.Damage; break;
            }
            foreach (var delta in outcome.FactChanges)
                records.Add(BattleFeedbackRecord.From(type, source, delta, outcome.Value, card));
        }
    }

    private void ExecuteEnemyWithFeedback()
    {
        RestoreFeedbackSelection();
        _lastFeedback.Clear();
        _feedbackBatch = v1Runtime != null ? v1Runtime.BeginPresentationBatch() : null;
        _feedbackBusy = true;
        try
        {
            if (v1Runtime != null) v1Runtime.EndPlayerTurn();
            var data = _battleSystem.GetBattleData();
            var lastFacts = data.PlayerUnits.Concat(data.EnemyUnits).ToDictionary(u => u.UnitId, BattleSystem.CaptureUnitFact);
            _battleSystem.ExecuteTurnPart2();
            foreach (var action in _battleSystem.LastEnemyActions)
            {
                _lastFeedback.Add(new BattleFeedbackRecord { Type = BattleFeedbackType.EnemyAction,
                    SourceUnitId = action.EnemyUnitId, TargetUnitId = action.TargetUnitId,
                    IntentType = action.IntentType, Applied = action.Applied, RequestedValue = action.RequestedValue });
                if (action.Before == null || action.After == null) continue;
                _lastFeedback.Add(BattleFeedbackRecord.From(BattleFeedbackType.Damage, action.EnemyUnitId,
                    new BattleRuleFactDelta { Before = action.Before, After = action.After }, action.RequestedValue));
                lastFacts[action.TargetUnitId] = action.After;
            }
            // DOT/回合尾段必须独立于最后一个敌人的 action。
            foreach (var unit in data.PlayerUnits.Concat(data.EnemyUnits))
            {
                var before = lastFacts[unit.UnitId]; var after = BattleSystem.CaptureUnitFact(unit);
                if (before.HP != after.HP || before.Shield != after.Shield)
                    _lastFeedback.Add(BattleFeedbackRecord.From(BattleFeedbackType.TurnEnd, null,
                        new BattleRuleFactDelta { Before = before, After = after }));
            }
            SyncDownedPersonCards();
            StartFeedback(true);
        }
        catch { CompleteFeedback(true); throw; }
    }

    private void StartFeedback(bool endTurn)
    {
        _lastFeedback.Add(new BattleFeedbackRecord { Type = BattleFeedbackType.Formation });
        if (!feedbackEnabled || !Application.isPlaying) { CompleteFeedback(endTurn); return; }
        EnsureFeedbackPlayer().Play(_lastFeedback, PresentFeedback, () => CompleteFeedback(endTurn));
    }

    private void CompleteFeedback(bool endTurn)
    {
        _feedbackBusy = false;
        try { _feedbackBatch?.Dispose(); }
        finally
        {
            _feedbackBatch = null;
            if (_battleSystem != null) { RepositionAllUnitCards(); RefreshFieldUI(); }
            if (endTurn) _actionTaken = true;
        }
    }

    private void OnDisable()
    {
        RestoreFeedbackSelection();
        if (feedbackPlayer != null) feedbackPlayer.CompleteImmediately();
        CompleteFeedback(false);
    }

    private IEnumerator PresentFeedback(BattleFeedbackRecord record)
    {
        var player = feedbackPlayer; var profile = feedbackProfile;
        if (record.Type == BattleFeedbackType.Formation)
        {
            var views = _rowCards != null ? _rowCards.Values.SelectMany(r => r).Where(c => c != null).Distinct().ToList() : new List<GameObject>();
            var starts = views.ToDictionary(c => c, c => c.transform.position);
            _feedbackFinalizing = true;
            try { RepositionAllUnitCards(); RefreshFieldUI(); }
            finally { _feedbackFinalizing = false; }
            var ends = views.ToDictionary(c => c, c => c.transform.position);
            player.RestoreOnFinish(() => { foreach (var c in views) if (c != null) c.transform.position = ends[c]; });
            yield return player.Tween(profile.FormationMoveDuration, t =>
            { foreach (var c in views) if (c != null && c.activeSelf) c.transform.position = Vector3.Lerp(starts[c], ends[c], t); });
            yield break;
        }
        if (record.Type == BattleFeedbackType.CardCommit)
        {
            var view = handContainer != null ? handContainer.Find("V1Card_" + record.CardInstanceId) as RectTransform : null;
            if (view != null)
            {
                var start = view.localScale;
                player.RestoreOnFinish(() => { if (view != null) view.localScale = start; });
                yield return player.Tween(profile.CardCommitDuration, t => { if (view != null) view.localScale = start * (1 - t); });
            }
            yield break;
        }
        if (record.Type == BattleFeedbackType.ActionPoints)
        {
            var host = actionPointPresenter;
            if (host == null && v1Runtime != null && v1Runtime.txtSharedActionPoint != null)
                host = ValueHost(v1Runtime.txtSharedActionPoint.transform.parent.gameObject, v1Runtime.txtSharedActionPoint, true);
            if (host != null)
            {
                host.SetImmediate(record.APBefore, record.APMax);
                yield return host.AnimateTo(record.APAfter, record.APMax, profile.HpTweenDuration, player);
            }
            yield break;
        }
        if (record.Type == BattleFeedbackType.ActorAction || record.Type == BattleFeedbackType.EnemyAction)
        {
            var actor = FeedbackUnitView(record.SourceUnitId);
            var rt = actor != null ? actor.transform.Find(HostAnchorVisual) as RectTransform : null;
            if (rt != null)
            {
                var start = rt.anchoredPosition;
                player.RestoreOnFinish(() => { if (rt != null) rt.anchoredPosition = start; });
                yield return player.Tween(profile.ActorMoveDuration, t => { if (rt != null) rt.anchoredPosition = start + Vector2.right * profile.ActorMoveDistance * Mathf.Sin(t * Mathf.PI); });
                rt.anchoredPosition = start;
            }
            yield break;
        }
        var target = FeedbackUnitView(record.TargetUnitId);
        if (target == null) yield break;
        if (record.HPDelta != 0 || record.ShieldDelta != 0)
        {
            var rt = target.transform.Find(HostAnchorVisual) as RectTransform;
            if (rt != null)
            {
                var start = rt.localScale;
                player.RestoreOnFinish(() => { if (rt != null) rt.localScale = start; });
                yield return player.Tween(profile.HitDuration, t => { if (rt != null) rt.localScale = start * (1 + profile.HitScalePunch * Mathf.Sin(t * Mathf.PI)); });
                rt.localScale = start;
            }
            yield return FloatDelta(target, record);
            var hp = ValueHost(target, target.transform.Find(HostTextHP)?.GetComponent<TMP_Text>(), true);
            hp.SetImmediate(record.HPBefore, record.MaxHP);
            yield return hp.AnimateTo(record.HPAfter, record.MaxHP, profile.HpTweenDuration, player);
            var shieldRoot = target.transform.Find(HostAnchorShield);
            if (shieldRoot != null)
            {
                shieldRoot.gameObject.SetActive(record.ShieldBefore > 0 || record.ShieldAfter > 0);
                var shield = ValueHost(shieldRoot.gameObject, shieldRoot.Find(HostTextShield)?.GetComponent<TMP_Text>(), false);
                shield.SetImmediate(record.ShieldBefore, Mathf.Max(record.ShieldBefore, record.ShieldAfter));
                yield return shield.AnimateTo(record.ShieldAfter, Mathf.Max(record.ShieldBefore, record.ShieldAfter), profile.ShieldTweenDuration, player);
                shieldRoot.gameObject.SetActive(record.ShieldAfter > 0);
            }
        }
        if (record.BecameDown)
        {
            var rt = target.transform as RectTransform;
            var start = rt.localScale;
            player.RestoreOnFinish(() => { if (rt != null) rt.localScale = start; });
            yield return player.Tween(profile.DownDuration, t => { if (rt != null) rt.localScale = start * (1 - .9f * t); });
            target.SetActive(false); rt.localScale = start;
        }
    }

    internal static BattleValuePresenter ValueHost(GameObject root, TMP_Text number, bool showMax)
    {
        var presenter = root.GetComponent<BattleValuePresenter>();
        if (presenter == null) presenter = root.AddComponent<BattleValuePresenter>();
        if (presenter.Number == null) presenter.Number = number;
        presenter.ShowMaximum = showMax;
        if (presenter.Linear == null) presenter.Linear = root.GetComponentInChildren<Slider>(true);
        if (presenter.Radial == null)
            presenter.Radial = root.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.type == Image.Type.Filled);
        return presenter;
    }

    private IEnumerator FloatDelta(GameObject target, BattleFeedbackRecord record)
    {
        var go = new GameObject("BattleFeedbackNumber", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(target.transform.Find(HostAnchorEffect) ?? target.transform, false);
        var text = go.GetComponent<TMP_Text>();
        text.font = feedbackPlayer.FloatingFont(CurrentUIFontAuthority.Default); text.fontSize = 24; text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.text = (record.HPDelta == 0 ? "" : (record.HPDelta > 0 ? "+" : "") + record.HPDelta)
            + (record.ShieldDelta == 0 ? "" : "  护盾" + (record.ShieldDelta > 0 ? "+" : "") + record.ShieldDelta);
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(240, 60);
        feedbackPlayer.RestoreOnFinish(() => { if (go != null) Destroy(go); });
        yield return feedbackPlayer.Tween(feedbackProfile.FloatingNumberDuration, t =>
        { if (rt != null) rt.anchoredPosition = feedbackProfile.FloatingNumberOffset * t; });
        Destroy(go);
    }
}
