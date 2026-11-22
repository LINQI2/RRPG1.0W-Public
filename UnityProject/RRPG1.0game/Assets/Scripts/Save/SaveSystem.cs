using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// P0 local single-slot save system.
///
/// Saves PlayerCollection long-term data as JSON. Runtime battle state, map
/// progress, UI objects, DataManager caches, and ScriptableObject objects are
/// intentionally excluded.
/// </summary>
public static class SaveSystem
{
    public const int CurrentSaveVersion = 1;
    public const string CurrentGameVersion = "0.2.3";
    private const string SaveFileName = "rrpg_save_p0.json";

    public static string GetSavePath()
    {
        return Path.Combine(Application.persistentDataPath, SaveFileName);
    }

    public static bool HasSave()
    {
        return File.Exists(GetSavePath());
    }

    /// <summary>
    /// 【Test / Not Frozen】Save 结果注入点（WP-B Expedition Return Integrity｜§14 Case C）。
    /// 非 null 且返回 true → 本次 Save 不写盘并返回 false，用于验证
    /// 「最终 Return Save false → Return 未 Completed → 再次触发只重试 Save → Completed」。
    /// 默认 null = 正常写入；不改变 SaveData / SaveVersion / 任何产品写入路径；测试负责在使用后清空。
    /// </summary>
    public static System.Func<bool> ForceSaveFailureForTests;

    public static bool Save(PlayerCollection collection)
    {
        // Stage V1：任何保存入口都确保 Village 快照存在（未初始化时以默认状态起始，幂等）。
        // 保证 MainMenuUI.NewGame / HeroSelectionUI.OnConfirm / TownUI.OnReturnFromExpedition /
        // AdventureManager.EndRun / CharacterGrowthUI 等所有真实保存调用链都捕获最新 VillageTime/State。
        VillageRuntimeBridge.EnsureInitialized(null);
        return SaveCore(collection, VillageRuntimeBridge.BuildSaveData());
    }

    /// <summary>
    /// Stage V1：带指定 Village 区块保存（独立方法名，避免与 Save 形成重载歧义，
    /// 保护既有测试/调用方的反射 GetMethod("Save")）。village 为 null 时不含 Village 区块。
    /// </summary>
    public static bool SaveWithVillage(PlayerCollection collection, VillageSaveData village)
    {
        return SaveCore(collection, village);
    }

    internal static bool SaveReturnFinal(PlayerCollection collection)
    {
        if (!VillageRuntimeBridge.CanSaveReturnFinal) return false;
        return SaveCore(collection, VillageRuntimeBridge.BuildSaveData(), VillageRuntimeBridge.ActiveReturnRecord.SessionIdentity);
    }

    private static bool SaveCore(PlayerCollection collection, VillageSaveData village, string returnIdentity = null)
    {
        if (returnIdentity == null ? !VillageRuntimeBridge.CanSaveWorld :
            (!VillageRuntimeBridge.CanSaveReturnFinal || VillageRuntimeBridge.ActiveReturnRecord.SessionIdentity != returnIdentity))
        { Debug.LogWarning("[SaveSystem] Save rejected: world advance/expedition is not at a permitted save boundary."); return false; }
        if (collection == null)
        {
            Debug.LogWarning("[SaveSystem] Save failed: PlayerCollection is null.");
            return false;
        }

        // WP-B（Test / Not Frozen）：Save 失败注入点 —— 仅用于「Final Save false → 只允许 retry Save」
        // 的定向契约测试；默认 null = 正常写入，不影响任何产品路径、SaveData 结构或 SaveVersion。
        if (ForceSaveFailureForTests != null && ForceSaveFailureForTests())
        {
            Debug.LogWarning("[SaveSystem] Save forced to fail by ForceSaveFailureForTests (Test seam).");
            return false;
        }

        try
        {
            var saveData = BuildSaveData(collection, village);
            if(!VillageRuntimeBridge.TryPrepareBuildings(village?.Buildings,out _,out string buildingsSaveDiagnostic))
            {Debug.LogWarning("[SaveSystem] Save rejected: "+buildingsSaveDiagnostic);return false;}
            bool timed=saveData.AutonomousDecisionWorld.HasLastOpportunity
                || (saveData.ActivityWorld.ActiveInstances?.Length ?? 0)>0
                || (saveData.ActivityWorld.PersonActivityStates ?? new RRPG.AutonomousActivity.PersonActivitySaveData[0]).Any(p=>p.HasLastCompletionMinute || p.HasPresence || p.HasMissedVisit);
            if(timed && (village?.Time?.IntradayTime==null
                || !VillageTime.TryPrepareRestore(village.Time,true,out var snapshotInstant,out _,out _,out _)
                || !VillageRuntimeBridge.ValidateActivityRestoreAt(saveData.ActivityWorld,snapshotInstant)
                || saveData.AutonomousDecisionWorld.HasLastOpportunity && saveData.AutonomousDecisionWorld.LastOpportunityMinute>snapshotInstant.Minute))
            { Debug.LogWarning("[SaveSystem] Save rejected: timed Activity/Decision state requires its matching VillageTime snapshot.");return false; }
            string json = JsonUtility.ToJson(saveData, true);
            // This engine serializes null nested classes as zero-filled objects.
            // Emit actual null for absent time blocks; schema-zero objects remain invalid inputs.
            json = PreserveNullableTimeBlocks(json, village);
            File.WriteAllText(GetSavePath(), json);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SaveSystem] Save failed: " + ex.Message);
            return false;
        }
    }

    public static PlayerCollection Load()
    {
        return LoadCore(false);
    }

    public static PlayerCollection AbortAdvanceAndLoad()
    {
        return LoadCore(true);
    }

    private static PlayerCollection LoadCore(bool abortAdvance)
    {
        if (!abortAdvance && !VillageRuntimeBridge.CanSaveWorld) return null;
        string path = GetSavePath();
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            var saveData = JsonUtility.FromJson<SaveData>(json);
            if (saveData == null)
            {
                Debug.LogWarning("[SaveSystem] Load failed: save data is empty or invalid.");
                return null;
            }

            if (saveData.SaveVersion != CurrentSaveVersion)
            {
                Debug.LogWarning("[SaveSystem] Unsupported save version: " + saveData.SaveVersion);
                return null;
            }

            // JsonUtility materializes absent/null class fields in this engine version.
            // Require an actual root Player object before any world restore can run.
            if (saveData.Player == null || !HasRootPlayerObject(json))
            {
                Debug.LogWarning("[SaveSystem] Load failed: Player data is missing.");
                return null;
            }

            // JsonUtility may materialize absent/null nested classes. Inspect the actual object path.
            if (!TryReadObjectMember(json, "Village", out string villageJson)
                || !TryReadObjectMember(villageJson, "Time", out string timeJson)
                || !TryReadObjectMember(timeJson, "IntradayTime", out string minuteJson)) return null;
            if(!TryReadObjectMember(villageJson,"Buildings",out string buildingsJson)) return null;
            if(!VillageRuntimeBridge.TryPrepareBuildings(buildingsJson==null ? null : saveData.Village?.Buildings,out var preparedBuildings,out string buildingsDiagnostic))
            { Debug.LogWarning("[SaveSystem] Load rejected: "+buildingsDiagnostic);return null; }
            VillageTimeSaveData preparedTime = timeJson == null ? null : saveData.Village?.Time;
            bool hasMinutes = minuteJson != null;
            if (!VillageTime.TryPrepareRestore(preparedTime, hasMinutes, out var restoredInstant, out _, out _, out string timeDiagnostic))
            { Debug.LogWarning("[SaveSystem] Load rejected: " + timeDiagnostic); return null; }
            if (preparedTime != null && !hasMinutes) preparedTime.IntradayTime = null;
            if (!TryReadObjectMember(villageJson, "IntradayWork", out string workJson)) return null;
            if (saveData.Village != null && workJson == null) saveData.Village.IntradayWork = null;
            if (!VillageIntradayWork.Validate(saveData.Village?.IntradayWork, out string workDiagnostic))
            { Debug.LogWarning("[SaveSystem] Load rejected: " + workDiagnostic); return null; }
            if(!VillageRuntimeBridge.TryPrepareBuildingWorld(saveData.Village,preparedBuildings,out var preparedBuildingWorld,out string buildingWorldDiagnostic))
            {Debug.LogWarning("[SaveSystem] Load rejected: "+buildingWorldDiagnostic);return null;}
            if(!VillageRuntimeBridge.ValidateActivityRestoreAt(saveData.ActivityWorld,restoredInstant))
            { Debug.LogWarning("[SaveSystem] Load rejected: invalid active ActivityWorld.");return null; }

            // Decision exactly-once data must be valid before any world restore.
            if (!RRPG.AutonomousSelection.AutonomousDecisionWorldPersistence.IsValid(saveData.AutonomousDecisionWorld))
            {
                Debug.LogWarning("[SaveSystem] Load failed: invalid AutonomousDecisionWorld.");
                return null;
            }
            if(saveData.AutonomousDecisionWorld != null && saveData.AutonomousDecisionWorld.HasLastOpportunity
                && saveData.AutonomousDecisionWorld.LastOpportunityMinute>restoredInstant.Minute)
            { Debug.LogWarning("[SaveSystem] Load rejected: future opportunity marker.");return null; }

            if (!RRPG.AutonomousSocialFeedback.SocialFeedbackWorldPersistence.IsValid(saveData.SocialFeedbackWorld))
            {
                Debug.LogWarning("[SaveSystem] Load failed: invalid SocialFeedbackWorld.");
                return null;
            }

            // Stage V1：恢复 Village 数据（旧档缺 Village 区块时以兼容默认状态初始化）。
            var collection = RestorePlayerCollection(saveData.Player);
            if (collection == null) return null;
            VillageRuntimeBridge.RestoreVillageWithoutTime(saveData.Village);

            // WP-Event-1D：恢复 Event World（旧档缺该区块 → 空默认；Completed Instance 不持久化）。
            EventRuntimeBridge.Restore(saveData.EventWorld);

            // WP-Event-1D §21（Gate D1 · B4）：旧档 Legacy Pending → Unified Event Pending 的收敛迁移。
            // 旧 Pending 是**已经发生的事实**：不重跑 Resolver / Weight / EventCondition；
            // 未知 Pattern 保留 Legacy 兼容路径（不静默删除）；诊断见 VillageEventFlow.LastDiagnostic。
            // Legacy Event migration runs below after the prepared time replacement.

            // Restore Social only after Player restore succeeds; failed Load retains the prior Social world.
            SocialRuntimeBridge.Restore(saveData.SocialWorld);
            if(ActivityRuntimeBridge.Restore(saveData.ActivityWorld)<0) return null;
            if (!AutonomousSelectionRuntimeBridge.Restore(saveData.AutonomousDecisionWorld))
            {
                Debug.LogWarning("[SaveSystem] Load failed: AutonomousDecisionWorld restore rejected.");
                return null;
            }
            if (!AutonomousSocialFeedbackRuntimeBridge.Restore(saveData.SocialFeedbackWorld)) return null;
            if (abortAdvance) VillageRuntimeBridge.AbortAdvanceAfterSuccessfulWorldRestore();
            if (!VillageRuntimeBridge.Time.TryRestore(preparedTime, hasMinutes)) return null;
            VillageEventFlow.ResetSessionState();
            string ignoredMigrationDiagnostic;
            VillageEventFlow.TryMigrateLegacyPending(out ignoredMigrationDiagnostic);
            VillageRuntimeBridge.CommitBuildingWorld(preparedBuildingWorld);
            return collection;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SaveSystem] Load failed: " + ex.Message);
            return null;
        }
    }

    public static PlayerCollection NewGame()
    {
        if (!VillageRuntimeBridge.CanSaveWorld) return null;
        var collection = new PlayerCollection();
        collection.InitializeDefaultCollection();
        SocialRuntimeBridge.ResetForNewGame();
        ActivityRuntimeBridge.ResetForNewGame();
        AutonomousSelectionRuntimeBridge.ResetForNewGame();
        AutonomousSocialFeedbackRuntimeBridge.ResetForNewGame();
        VillageRuntimeBridge.ResetTimeForNewGame();
        VillageRuntimeBridge.ResetBuildingsForNewGame();
        return collection;
    }

    private static string ReplaceObjectValue(string json, string name, string replacement)
    {
        if (!TryReadObjectMember(json, name, out string value) || value == null) return json;
        int key = json.IndexOf("\""+name+"\"", StringComparison.Ordinal);
        int start = json.IndexOf(':', key) + 1;
        while (start < json.Length && char.IsWhiteSpace(json[start])) start++;
        return json.Substring(0, start) + replacement + json.Substring(start + value.Length);
    }
    private static string PreserveNullableTimeBlocks(string json, VillageSaveData village)
    {
        if (village == null) return ReplaceObjectValue(json, "Village", "null");
        if (!TryReadObjectMember(json, "Village", out string v) || v == null) return json;
        if(village.Buildings==null) {v=ReplaceObjectValue(v,"Buildings","null");json=ReplaceObjectValue(json,"Village",v);}
        if (village.IntradayWork == null)
        {
            v = ReplaceObjectValue(v, "IntradayWork", "null");
            json = ReplaceObjectValue(json, "Village", v);
        }
        if (village.Time == null) return ReplaceObjectValue(json, "Village", ReplaceObjectValue(v, "Time", "null"));
        if (village.Time.IntradayTime != null) return json;
        if (!TryReadObjectMember(v, "Time", out string time) || time == null) return json;
        string updated = ReplaceObjectValue(time, "IntradayTime", "null");
        return ReplaceObjectValue(json, "Village", ReplaceObjectValue(v, "Time", updated));
    }

    // Read just an object property at the current root, preserving absent/null distinction.
    // No alternate serializer, regex search in string values, or duplicated property ambiguity.
    private static bool TryReadObjectMember(string json, string name, out string objectJson)
    {
        objectJson = null;
        if (json == null) return true;
        int depth = 0; bool found = false;
        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            if (c == '"')
            {
                int start = i++;
                for (; i < json.Length; i++) { if (json[i] == '\\') { i++; continue; } if (json[i] == '"') break; }
                if (i >= json.Length) return false;
                int next = i + 1;
                while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                if (depth != 1 || next >= json.Length || json[next] != ':') continue;
                var member = JsonUtility.FromJson<JsonMemberName>("{\"Name\":" + json.Substring(start, i - start + 1) + "}");
                if (member.Name != name) continue;
                if (found) return false;
                found = true; next++;
                while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                if (next >= json.Length) return false;
                if (json[next] == 'n')
                {
                    if (next + 4 > json.Length || json.Substring(next, 4) != "null") return false;
                    continue;
                }
                if (json[next] != '{') return false;
                int end = next, level = 0; bool quoted = false;
                for (; end < json.Length; end++)
                {
                    char v = json[end];
                    if (quoted && v == '\\') { end++; continue; }
                    if (v == '"') { quoted = !quoted; continue; }
                    if (quoted) continue;
                    if (v == '{') level++;
                    if (v == '}' && --level == 0) { end++; break; }
                }
                if (level != 0) return false;
                objectJson = json.Substring(next, end - next);
            }
            else if (c == '{' || c == '[') depth++;
            else if (c == '}' || c == ']') depth--;
        }
        return true;
    }

    [Serializable]
    private sealed class JsonMemberName
    {
        public string Name;
    }

    private static bool HasRootPlayerObject(string json)
    {
        int depth = 0;
        bool found = false;
        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            if (c == '"')
            {
                int start = i++;
                for (; i < json.Length; i++)
                {
                    if (json[i] == '\\') { i++; continue; }
                    if (json[i] == '"') break;
                }
                if (i >= json.Length) return false;
                int next = i + 1;
                while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                if (depth != 1 || next >= json.Length || json[next] != ':') continue;
                // Decode escaped property names with the existing serializer, not a new JSON dependency.
                string token = json.Substring(start, i - start + 1);
                var member = JsonUtility.FromJson<JsonMemberName>("{\"Name\":" + token + "}");
                if (member.Name != "Player") continue;
                if (found) return false; // Ambiguous duplicate root Player members fail closed.
                found = true;
                next++;
                while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                if (next >= json.Length || json[next] != '{') return false;
            }
            else if (c == '{' || c == '[') depth++;
            else if (c == '}' || c == ']') depth--;
        }
        return found;
    }

    public static bool DeleteSave()
    {
        if (!VillageRuntimeBridge.CanSaveWorld) return false;
        string path = GetSavePath();
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SaveSystem] DeleteSave failed: " + ex.Message);
            return false;
        }
    }

    private static SaveData BuildSaveData(PlayerCollection collection, VillageSaveData village)
    {
        var player = new PlayerCollectionSaveData
        {
            Gold = collection.Gold
        };

        foreach (var hero in collection.OwnedHeroes)
        {
            if (hero == null || string.IsNullOrEmpty(hero.HeroId)) continue;
            if (string.IsNullOrEmpty(hero.PersonId)) hero.PersonId = PersonIdentity.NewPersonId(); // M1 防御兜底
            player.Heroes.Add(new HeroSaveData
            {
                HeroId = hero.HeroId,
                PersonId = hero.PersonId,
                Level = hero.Level,
                Exp = hero.Exp,
                Injury = hero.Injury,
                MaxHP = hero.MaxHP,
                StarterSlots = SaveStarterSlots(hero.StarterSlots),
                UnlockedPoolCardIds = hero.UnlockedPoolCardIds != null ? new List<string>(hero.UnlockedPoolCardIds) : null,
                TraitId = hero.TraitId,
                TraitLevel = hero.TraitLevel,
                // WP-3（additive）：Fulfillment / Hunger 最小状态（SaveVersion 保持 1）。
                Hungry = hero.Hungry,
                ConsecutiveUnderfedDays = hero.ConsecutiveUnderfedDays > 0 ? hero.ConsecutiveUnderfedDays : 0
            });
        }

        foreach (var character in collection.OwnedCharacters)
        {
            if (character == null || string.IsNullOrEmpty(character.CharacterId)) continue;
            if (string.IsNullOrEmpty(character.PersonId)) character.PersonId = PersonIdentity.NewPersonId(); // M1 防御兜底
            player.Characters.Add(new CharacterSaveData
            {
                CharacterId = character.CharacterId,
                PersonDefinitionId = character.PersonDefinitionId,
                DisplayName = character.DisplayName,
                PersonId = character.PersonId,
                Level = character.Level,
                Exp = character.Exp,
                CurrentAttack = character.CurrentAttack,
                Injury = character.Injury,
                IsDead = character.IsDead,
                MaxHP = character.MaxHP,
                StarterSlots = SaveStarterSlots(character.StarterSlots),
                UnlockedPoolCardIds = character.UnlockedPoolCardIds != null ? new List<string>(character.UnlockedPoolCardIds) : null,
                TraitId = character.TraitId,
                TraitLevel = character.TraitLevel,
                HasActiveProfessionProgress = character.ActiveProfessionProgress != null,
                ActiveProfessionProgress = SaveProfessionProgress(character.ActiveProfessionProgress),
                // M6（additive）：Formation 派生状态 + 最小 Life History（SaveVersion 保持 1；旧档缺失 → 空状态）
                FormationState = SaveFormationState(character.FormationState),
                LifeHistory = SaveLifeHistory(character.LifeHistory),
                // WP-3（additive）：Fulfillment / Hunger 最小状态（SaveVersion 保持 1）。
                Hungry = character.Hungry,
                ConsecutiveUnderfedDays = character.ConsecutiveUnderfedDays > 0 ? character.ConsecutiveUnderfedDays : 0
            });
        }

        // 保存所有角色卡 ID（包含运行时招募创建的临时卡，不按 SO 过滤）
        foreach (var card in collection.OwnedCharacterCards)
        {
            if (card == null || string.IsNullOrEmpty(card.CardId)) continue;
            player.OwnedCharacterCardIds.Add(card.CardId);
        }

        // M1：持久化“角色卡 → 具体人物 PersonId”绑定（卡到实例的唯一映射；旧档缺失时 Load 按顺序补建）
        player.OwnedCharacterCardOwners = collection.BuildCharacterCardOwnerSaveData();

        // WP-C：Current Save 不再依赖 PlayerCollection.OwnedItemCards runtime container。
        // OwnedItemCardIds 保留为兼容 no-op 字段（DTO 字段继续存在，SaveVersion 不变），
        // 当前写入空列表；旧档中的旧 Item ids 由 Load 安全忽略（见 RestoreItemCards）。
        player.OwnedItemCardIds.Clear();

        return new SaveData
        {
            SaveVersion = CurrentSaveVersion,
            GameVersion = CurrentGameVersion,
            SavedAtUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Player = player,
            Village = village,
            // WP-Event-1D：Event World 区块（Flags / World Event History / Pending Village Event）。
            // 与 Village 区块同一原则：可空增量，旧档缺失 → 空默认，不提升 SaveVersion。
            EventWorld = EventRuntimeBridge.BuildSaveData(),
            SocialWorld = SocialRuntimeBridge.BuildSaveData(),
            ActivityWorld = ActivityRuntimeBridge.BuildSaveData(),
            AutonomousDecisionWorld = AutonomousSelectionRuntimeBridge.BuildSaveData(),
            SocialFeedbackWorld = AutonomousSocialFeedbackRuntimeBridge.BuildSaveData()
        };
    }

    private static PlayerCollection RestorePlayerCollection(PlayerCollectionSaveData playerData)
    {
        var collection = new PlayerCollection();
        collection.Gold = playerData.Gold;

        RestoreHeroes(collection, playerData);
        RestoreCharacters(collection, playerData);
        RestoreCharacterCards(collection, playerData);
        RestoreItemCards(collection, playerData);

        // M1 / Foundation Identity Cleanup：恢复“角色卡 → PersonId”绑定。
        // 只接受存档中的显式绑定（OwnedCharacterCardOwners）；
        // 旧档（无该字段）中缺绑定的卡保持 unresolved（解析返回 null + 告警），
        // 不按模板候选唯一 / 卡册顺序猜测补绑（Rebuild… 仅告警，不写绑定）。
        collection.RestoreCharacterCardOwners(playerData.OwnedCharacterCardOwners);
        collection.RebuildCharacterCardOwnersFromCardOrder();

        return collection;
    }

    private static List<StarterSlotSaveData> SaveStarterSlots(List<StarterSlotEntry> slots)
    {
        if (slots == null) return null;
        var result = new List<StarterSlotSaveData>();
        foreach (var s in slots)
        {
            result.Add(new StarterSlotSaveData
            {
                CardId = s != null ? s.CardId : string.Empty,
                Proficiency = s != null ? s.Proficiency : 0
            });
        }
        return result;
    }

    /// <summary>
    /// 把 Person-owned ProfessionProgress 转为存档 DTO；null（尚未形成 Profession）→ null（合法）。
    /// UnlockedPool 做快照拷贝，保证同一 Person 后续成长不会反向污染已构建的存档数据。
    /// </summary>
    private static ProfessionProgressSaveData SaveProfessionProgress(ProfessionProgress progress)
    {
        if (progress == null) return null;

        return new ProfessionProgressSaveData
        {
            ProfessionId = progress.ProfessionId,
            ProfessionLevel = progress.ProfessionLevel,
            ProfessionExp = progress.ProfessionExp,
            UnlockedPoolCardIds = progress.UnlockedPoolCardIds != null
                ? new List<string>(progress.UnlockedPoolCardIds)
                : new List<string>(),
            ProfessionTraitLevel = progress.ProfessionTraitLevel
        };
    }

    /// <summary>
    /// M6：把 Person-owned Formation 派生状态转为存档 DTO（null → null）。
    /// 快照拷贝 Strength/Stability/天数等值；绝不写入任何 ProfessionDefinition 引用。
    /// </summary>
    private static FormationStateSaveData SaveFormationState(FormationState state)
    {
        if (state == null) return null;
        var dto = new FormationStateSaveData
        {
            FormationWindowOpen = state.FormationWindowOpen,
            FormationTargetProfessionId = state.FormationTargetProfessionId,
            IsAmbiguous = state.IsAmbiguous,
            Entries = new List<ProfessionFormationEntrySaveData>()
        };
        if (state.Entries != null)
        {
            foreach (var e in state.Entries)
            {
                if (e == null || string.IsNullOrEmpty(e.ProfessionId)) continue;
                dto.Entries.Add(new ProfessionFormationEntrySaveData
                {
                    ProfessionId = e.ProfessionId,
                    Strength = e.Strength,
                    ActiveCareerDays = e.ActiveCareerDays,
                    InactiveDays = e.InactiveDays,
                    Stability = e.Stability,
                    IsCandidate = e.IsCandidate
                });
            }
        }
        return dto;
    }

    /// <summary>
    /// M6：从存档 DTO 恢复 Formation 派生状态（null / 旧档缺失 → 空状态）。
    /// 数值防御性下限钳制（不为负）；unknown Profession 条目保留原样（不改投其它职业）。
    /// </summary>
    private static FormationState RestoreFormationState(FormationStateSaveData dto)
    {
        var state = new FormationState();
        if (dto == null) return state;

        state.FormationWindowOpen = dto.FormationWindowOpen;
        state.FormationTargetProfessionId = dto.FormationTargetProfessionId;
        state.IsAmbiguous = dto.IsAmbiguous;
        if (dto.Entries != null)
        {
            foreach (var e in dto.Entries)
            {
                if (e == null || string.IsNullOrEmpty(e.ProfessionId)) continue;
                state.Entries.Add(new ProfessionFormationEntry
                {
                    ProfessionId = e.ProfessionId,
                    Strength = e.Strength > 0f ? e.Strength : 0f,
                    ActiveCareerDays = e.ActiveCareerDays > 0 ? e.ActiveCareerDays : 0,
                    InactiveDays = e.InactiveDays > 0 ? e.InactiveDays : 0,
                    Stability = e.Stability > 0f ? e.Stability : 0f,
                    IsCandidate = e.IsCandidate
                });
            }
        }
        return state;
    }

    /// <summary>M6：把 Person Life History 转为存档 DTO（null → null）。</summary>
    private static PersonLifeHistorySaveData SaveLifeHistory(PersonLifeHistory history)
    {
        if (history == null) return null;
        var dto = new PersonLifeHistorySaveData { Events = new List<PersonLifeEventSaveData>() };
        if (history.Events != null)
        {
            foreach (var e in history.Events)
            {
                if (e == null) continue;
                dto.Events.Add(new PersonLifeEventSaveData
                {
                    Type = (int)e.Type,
                    Year = e.Year,
                    Month = e.Month,
                    Day = e.Day,
                    ProfessionId = e.ProfessionId,
                    // V4 Gate 2（additive）：ExpeditionReturned 的 Return Outcome。
                    Outcome = e.Outcome
                });
            }
        }
        return dto;
    }

    /// <summary>M6：从存档 DTO 恢复 Person Life History（null / 旧档缺失 → 空 History）。</summary>
    private static PersonLifeHistory RestoreLifeHistory(PersonLifeHistorySaveData dto)
    {
        var history = new PersonLifeHistory();
        if (dto == null || dto.Events == null) return history;

        foreach (var e in dto.Events)
        {
            if (e == null) continue;
            if (!Enum.IsDefined(typeof(PersonLifeEventType), e.Type)) continue; // 未知事件类型不猜、不丢已记录事实外的推断
            history.Events.Add(new PersonLifeEvent
            {
                Type = (PersonLifeEventType)e.Type,
                Year = e.Year,
                Month = e.Month,
                Day = e.Day,
                ProfessionId = e.ProfessionId,
                // V4 Gate 2（additive）：旧档缺失 → 0（NotSet）。
                Outcome = e.Outcome
            });
        }
        return history;
    }

    /// <summary>恢复人物长期成长状态；缺失（旧存档）时按模板 ID 播种演示/测试种子（M1：模板与 PersonId 分离）。</summary>
    private static void RestoreGrowthData(IPersonGrowth person,
        List<StarterSlotSaveData> slots, List<string> pool, string traitId, int traitLevel,
        string templateId)
    {
        if (person == null) return;

        if (slots != null)
        {
            foreach (var s in slots)
            {
                if (s != null) person.StarterSlots.Add(new StarterSlotEntry(s.CardId, s.Proficiency));
            }
        }

        if (pool != null)
        {
            foreach (var id in pool)
            {
                if (!string.IsNullOrEmpty(id) && !person.UnlockedPoolCardIds.Contains(id))
                    person.UnlockedPoolCardIds.Add(id);
            }
        }

        person.TraitId = traitId;
        person.TraitLevel = traitLevel;

        GrowthDefaults.EnsureSeeded(person, templateId); // 旧存档/缺失长期状态时：按模板 ID 播种初始 Starter/Unlocked Pool 测试种子
    }

    private static void RestoreHeroes(PlayerCollection collection, PlayerCollectionSaveData playerData)
    {
        if (playerData.Heroes == null) return;

        foreach (var heroSave in playerData.Heroes)
        {
            if (heroSave == null || string.IsNullOrEmpty(heroSave.HeroId)) continue;

            var heroData = DataManager.Instance.GetHeroById(heroSave.HeroId);
            if (heroData == null)
            {
                Debug.LogWarning("[SaveSystem] Missing HeroData id: " + heroSave.HeroId);
                continue;
            }

            var hero = new HeroInstance(heroData.HeroId, heroData.DisplayName, heroData.BaseHP)
            {
                Level = heroSave.Level,
                Exp = heroSave.Exp,
                Injury = heroSave.Injury,
                MaxHP = heroSave.MaxHP > 0 ? heroSave.MaxHP : heroData.BaseHP,
                CurrentHP = heroSave.MaxHP > 0 ? heroSave.MaxHP : heroData.BaseHP
            };
            // M1：恢复既有 PersonId（旧档缺失时保留构造生成的唯一新 ID，并在下次保存时写入）
            if (!string.IsNullOrEmpty(heroSave.PersonId))
                hero.PersonId = heroSave.PersonId;
            RestoreGrowthData(hero, heroSave.StarterSlots, heroSave.UnlockedPoolCardIds,
                heroSave.TraitId, heroSave.TraitLevel, heroSave.HeroId);
            // WP-3（additive）：恢复 Fulfillment / Hunger 最小状态（旧档缺失 → false / 0）。
            hero.Hungry = heroSave.Hungry;
            hero.ConsecutiveUnderfedDays = heroSave.ConsecutiveUnderfedDays > 0 ? heroSave.ConsecutiveUnderfedDays : 0;
            collection.OwnedHeroes.Add(hero);
        }
    }

    private static void RestoreCharacters(PlayerCollection collection, PlayerCollectionSaveData playerData)
    {
        if (playerData.Characters == null) return;

        foreach (var characterSave in playerData.Characters)
        {
            if (characterSave == null || string.IsNullOrEmpty(characterSave.CharacterId)) continue;

            // Initial Person &amp; Profession Bootstrap（additive，不提升 SaveVersion）：
            // 优先用存档里的 PersonDefinitionId 恢复人物表现名与战斗定义；旧档缺失该字段 → 走 Legacy 回退
            // （CharacterId → CharacterData），不猜人物定义、不把模板名当人物名。
            PersonDefinition personDefinition = !string.IsNullOrEmpty(characterSave.PersonDefinitionId)
                ? DataManager.Instance.GetPersonDefinitionById(characterSave.PersonDefinitionId)
                : null;
            if (personDefinition == null && !string.IsNullOrEmpty(characterSave.PersonDefinitionId))
            {
                Debug.LogWarning("[SaveSystem] 找不到 PersonDefinition id: " + characterSave.PersonDefinitionId
                    + "（内容/配置已变更）；本次 Load 回退到 Legacy 表现，不猜人物定义。");
            }

            string combatDefinitionId = personDefinition != null && !string.IsNullOrEmpty(personDefinition.CombatDefinitionId)
                ? personDefinition.CombatDefinitionId
                : characterSave.CharacterId;

            var characterData = DataManager.Instance.GetCharacterById(combatDefinitionId);
            if (characterData == null)
            {
                Debug.LogWarning("[SaveSystem] Missing CharacterData id: " + combatDefinitionId);
                continue;
            }

            // 显示名优先级：存档中的运行时人物名（表现事实）→ PersonDefinition.DisplayName → 战斗定义名（Legacy）。
            // 人物显示名绝不因为内容变更/定义缺失而退化为战斗定义名（职业词不得成为人物身份表达）。
            string personDisplayName = !string.IsNullOrEmpty(characterSave.DisplayName)
                ? characterSave.DisplayName
                : (personDefinition != null && !string.IsNullOrEmpty(personDefinition.DisplayName)
                    ? personDefinition.DisplayName
                    : characterData.DisplayName);

            var character = new CharacterInstance(combatDefinitionId, personDisplayName,
                characterData.BaseHP, characterData.BaseAttack)
            {
                Level = characterSave.Level,
                Exp = characterSave.Exp,
                CurrentAttack = characterSave.CurrentAttack,
                Injury = characterSave.Injury,
                IsDead = characterSave.IsDead,
                MaxHP = characterSave.MaxHP > 0 ? characterSave.MaxHP : characterData.BaseHP,
                CurrentHP = characterSave.MaxHP > 0 ? characterSave.MaxHP : characterData.BaseHP
            };
            // M1：恢复既有 PersonId；旧档缺失时保留本实例构造生成的唯一新 ID（同模板两实例必不同），
            // 并在下一次保存时写入。不能用 CharacterId 充当 PersonId。
            if (!string.IsNullOrEmpty(characterSave.PersonId))
                character.PersonId = characterSave.PersonId;
            // Bootstrap provenance：字面保留（即使当前内容已找不到该定义），不参与身份判定。
            character.PersonDefinitionId = characterSave.PersonDefinitionId;
            // M3：恢复 Active Profession Progress（Person-owned，普通 Character）。
            // 顺序说明：必须在 RestoreGrowthData 之前恢复，这样 Legacy 播种（GrowthDefaults.EnsureSeeded）
            // 才能识别「该人物已有 Active Profession」并跳过 Legacy Demo 种子（不污染 Profession 初始/成长内容）。
            // 以显式哨兵 HasActiveProfessionProgress 判定（本引擎 JsonUtility 会把 null/缺失 class 字段物化为默认实例，
            // 不能用 DTO 引用是否为 null 区分）。旧档无该字段 → false → 保持 null（合法）；
            // 只做字面恢复，绝不根据 CharacterId / Assignment / 旧 Level/Exp 自动推断 ProfessionId，也不自动 Formation；
            // Load 绝不重新执行 New Game Bootstrap（不覆盖已成长的 Profession 进度）。
            if (characterSave.HasActiveProfessionProgress && characterSave.ActiveProfessionProgress != null)
            {
                character.ActiveProfessionProgress = new ProfessionProgress
                {
                    ProfessionId = characterSave.ActiveProfessionProgress.ProfessionId,
                    ProfessionLevel = characterSave.ActiveProfessionProgress.ProfessionLevel,
                    ProfessionExp = characterSave.ActiveProfessionProgress.ProfessionExp,
                    UnlockedPoolCardIds = characterSave.ActiveProfessionProgress.UnlockedPoolCardIds != null
                        ? new List<string>(characterSave.ActiveProfessionProgress.UnlockedPoolCardIds)
                        : new List<string>(),
                    ProfessionTraitLevel = characterSave.ActiveProfessionProgress.ProfessionTraitLevel
                };
            }
            RestoreGrowthData(character, characterSave.StarterSlots, characterSave.UnlockedPoolCardIds,
                characterSave.TraitId, characterSave.TraitLevel, characterSave.CharacterId);
            // M6（additive）：恢复 Formation 派生状态与最小 Life History。
            // 旧档缺失（FormationState / LifeHistory 为 null 或空）→ 空状态（合法）。
            // 绝不根据 CharacterId / Assignment / 旧 Level/Exp / TotalExecutedDays 推断 Formation；
            // unknown Profession 条目（含存档恢复）保留原样、不改投其它职业。
            character.FormationState = RestoreFormationState(characterSave.FormationState);
            character.LifeHistory = RestoreLifeHistory(characterSave.LifeHistory);
            // WP-3（additive）：恢复 Fulfillment / Hunger 最小状态（旧档缺失 → false / 0）。
            character.Hungry = characterSave.Hungry;
            character.ConsecutiveUnderfedDays = characterSave.ConsecutiveUnderfedDays > 0 ? characterSave.ConsecutiveUnderfedDays : 0;
            collection.OwnedCharacters.Add(character);
        }
    }

    private static void RestoreCharacterCards(PlayerCollection collection, PlayerCollectionSaveData playerData)
    {
        if (playerData.OwnedCharacterCardIds == null) return;

        // Gate Correction：同一 PlayerCollection 内 OwnedCharacterCards 的 CardId 必须唯一
        // （绑定键 = 集合内唯一 CardId → PersonId）。异常旧档若重复同一 CardId，
        // 将第二条及之后的 owned 卡归一化为独立实例条目（实例化 + 新 CardId），
        // 避免“两张 owned 卡共享同一静态 CardId”导致卡→Person 绑定塌缩。
        var seenCardIds = new HashSet<string>();

        foreach (var cardId in playerData.OwnedCharacterCardIds)
        {
            if (string.IsNullOrEmpty(cardId)) continue;

            bool duplicate = !seenCardIds.Add(cardId);
            string effectiveCardId = cardId;
            string templateCharacterId = null;
            if (duplicate)
            {
                Debug.LogWarning("[SaveSystem] 检测到重复角色卡 CardId=" + cardId
                    + "：同模板多 owned 卡不得共享同一 CardId，已实例化归一化为独立 owned 条目。");
            }

            var card = DataManager.Instance.GetCardById(cardId);
            if (card != null)
            {
                if (!duplicate)
                {
                    collection.OwnedCharacterCards.Add(card);
                    continue;
                }
                templateCharacterId = card.CharacterId;
            }
            else
            {
                // SO 中找不到该卡 → 可能是运行时招募创建的临时卡
                // 遍历已恢复的角色实例，用 CharacterId 匹配重建角色卡（模板语义；不绑定 Person）
                foreach (var character in collection.OwnedCharacters)
                {
                    if (character == null || string.IsNullOrEmpty(character.CharacterId)) continue;
                    if (character.IsDead) continue;
                    if (cardId.Contains(character.CharacterId))
                    {
                        templateCharacterId = character.CharacterId;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(templateCharacterId))
            {
                Debug.LogWarning("[SaveSystem] 无法恢复角色卡（无匹配模板）：" + cardId);
                continue;
            }

            if (duplicate)
            {
                // 归一化 CardId 保留模板子串，保证下次 Load 的“临时卡重建”仍可识别模板
                effectiveCardId = "card_" + templateCharacterId + "_dup_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            }

            var rebuilt = ScriptableObject.CreateInstance<CharacterCardData>();
            rebuilt.CardId = effectiveCardId;
            rebuilt.CharacterId = templateCharacterId;
            collection.OwnedCharacterCards.Add(rebuilt);
        }
    }

    /// <summary>
    /// WP-C：旧档兼容入口 —— 旧存档中的 OwnedItemCardIds 只做安全忽略（no-op）。
    /// 独立 Item Card 产品语义已退出正式路径，Load 不再据此重建道具卡册，
    /// 也不查找 / 实例化 ItemCardData（旧档含该字段时不会抛异常，SaveVersion 不变）。
    /// 字段本身继续保留在 PlayerCollectionSaveData 中以保证旧档可反序列化。
    /// </summary>
    private static void RestoreItemCards(PlayerCollection collection, PlayerCollectionSaveData playerData)
    {
        if (playerData == null || playerData.OwnedItemCardIds == null || playerData.OwnedItemCardIds.Count == 0)
            return;

        Debug.LogWarning("[SaveSystem] 旧档包含 Legacy OwnedItemCardIds（" + playerData.OwnedItemCardIds.Count
            + " 项）：独立 Item Card 产品语义已退出，已按兼容 no-op 忽略，不重建道具卡册。");
    }
}
