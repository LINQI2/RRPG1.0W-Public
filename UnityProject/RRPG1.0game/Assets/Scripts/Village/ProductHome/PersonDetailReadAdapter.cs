using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

public enum PersonDetailTab { Overview, Life, Growth, Relationships, LifeHistory }

/// <summary>Copied display values only. No writable person, world or persistence references.</summary>
public sealed class PersonDetailSnapshot
{
    public string PersonId { get; }
    public string Name { get; }
    public bool Exists { get; }
    public bool CanOpenGrowth { get; }
    public IReadOnlyList<string> Pages { get; }
    public IReadOnlyList<PersonRelationSnapshot> Relations { get; }
    public PersonDetailSnapshot(string id,string name,bool exists,bool growth,IEnumerable<string> pages,IEnumerable<PersonRelationSnapshot> relations)
    { PersonId=id;Name=name;Exists=exists;CanOpenGrowth=growth;Pages=Array.AsReadOnly(pages.ToArray());Relations=Array.AsReadOnly(relations.ToArray()); }
}
public sealed class PersonRelationSnapshot
{
    public string PersonId { get; }
    public string Name { get; }
    public string Text { get; }
    public bool CanNavigate { get; }
    public PersonRelationSnapshot(string id,string name,string text,bool navigate){PersonId=id;Name=name;Text=text;CanNavigate=navigate;}
}

/// <summary>Existing queries compose the five tabs; reads never seed growth, tick or drain a world.</summary>
public static class PersonDetailReadAdapter
{
    public static IReadOnlyList<string> Roster(PlayerCollection source,string search)
    {
        var persons=new List<IPersonGrowth>();
        if(source?.OwnedHeroes!=null) persons.AddRange(source.OwnedHeroes.Where(p=>p!=null));
        if(source?.OwnedCharacters!=null) persons.AddRange(source.OwnedCharacters.Where(p=>p!=null));
        return Array.AsReadOnly(persons.Where(p=>string.IsNullOrEmpty(search)||(p.DisplayName??"").IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0)
            .OrderBy(p=>p.DisplayName,StringComparer.Ordinal).ThenBy(p=>p.PersonId,StringComparer.Ordinal).Select(p=>p.PersonId).ToArray());
    }
    public static PersonDetailSnapshot Read(PlayerCollection source,string id,ProductHomeSnapshot home)
    {
        var person=source?.FindPersonByPersonId(id);
        if(person==null) return new PersonDetailSnapshot(id,"人物资料暂不可用",false,false,Enumerable.Repeat("人物资料暂不可用，请从名册选择当前人物。",5),new PersonRelationSnapshot[0]);
        var character=person as CharacterInstance;
        var auth=GrowthAuthority.Resolve(person);
        var p=home?.Person(id);
        string profession=character==null?"Hero（沿用现有英雄成长）":character.ActiveProfessionProgress?.ProfessionId??"尚未形成职业";
        string availability=p==null?"现场资料暂缺":p.Availability==VillageAvailability.InVillage?"在村":p.Availability==VillageAvailability.Expedition?"Away / 远征中":"不可用";
        string overview=$"{person.DisplayName}\n人物身份：{(character==null?"Hero":"普通人物")}\n职业：{profession}\n状态：{availability}\n主要职责：{p?.AssignmentSummary??"资料暂缺"}\n临时状态：{Empty(p?.OccupancySummary,"无临时占用")}\n\n人物标识：{id}";
        var activityId=ActivityRuntimeBridge.QueryClaim(id);
        var activity=activityId==null?null:ActivityRuntimeBridge.QueryActivityInstance(activityId);
        var rest=VillageRuntimeBridge.PersonRest?.GetLatestRest(id);
        var clock=VillageRuntimeBridge.Time;
        var nutrition=person as IFulfillmentHost;
        string restText=rest==null?"暂无恢复期记录":$"至 {rest.UntilYear}/{rest.UntilMonth}/{rest.UntilDay}"+(clock==null?"":VillageRuntimeBridge.PersonRest.IsRestBlocked(id,clock.Year,clock.Month,clock.Day)?"（当前恢复期）":"（已结束）");
        string life=$"当前在场状态：{p?.SimulationPresence??"现场资料暂缺"}\n当前活动：{p?.ActivitySummary??"现场资料暂缺"}\n活动记录：{(activity==null?"无进行中的活动":activity.Candidate.ActivityType+" · "+activity.State)}\n当前地点：{Place(home,p?.CurrentPlaceId)}\n目的地：{Place(home,p?.DestinationPlaceId)}\n通勤：{(p?.Traveling==true?"途中，尚未到达":"当前无通勤事实")}\n主要职责：{p?.AssignmentSummary??"资料暂缺"}\n实际劳动：{(p==null?"现场资料暂缺":VillageRuntimeBridge.GetWorkExecutionLabel(id))}\n恢复期：{restText}\n饥饿：{(nutrition==null?"资料暂缺":nutrition.Hungry?"未满足":"已满足")}\n连续不足天数：{(nutrition==null?"—":nutrition.ConsecutiveUnderfedDays.ToString())}\n\n职责绑定不等于正在劳动，活动与到场信息读取当前世界事实。";
        ProfessionDefinition definition=null;
        if(character?.ActiveProfessionProgress!=null) DataManager.Instance?.ProfessionRegistry?.TryGetById(character.ActiveProfessionProgress.ProfessionId,out definition);
        string growth=auth==null||!auth.IsAvailable?"尚未形成职业，暂不可执行成长。\n\nStarter：\n"+Starter(person.StarterSlots):
            $"职业：{profession}\n等级：{auth.Level}\n经验：{auth.Exp}\nTrait：{Empty(definition?.ProfessionTraitId??auth.TraitId,"暂无定义")} · Lv.{auth.TraitLevel}\n\n个人 Unlocked Pool（已解锁）：\n{Cards(auth.UnlockedPoolCardIds)}\n\nStarter（下次远征起始配置）：\n{Starter(auth.StarterSlots)}\n\nFull Pool（职业理论池，不是已解锁候选）：\n{(definition==null?"暂无职业理论池资料":Cards(definition.FullCardPool))}";
        var facts=SocialRuntimeBridge.QueryFacts().Where(f=>f.ActorPersonId==id||f.TargetPersonId==id).ToArray();
        var relations=new List<PersonRelationSnapshot>();
        foreach(string target in facts.Select(f=>f.ActorPersonId==id?f.TargetPersonId:f.ActorPersonId).Where(t=>t!=id&&!string.IsNullOrEmpty(t)).Distinct().OrderBy(t=>t,StringComparer.Ordinal))
        {
            var other=source.FindPersonByPersonId(target);
            string a=SocialRuntimeBridge.QueryRelationship(id,target).Affinity.ToString("0.##",CultureInfo.InvariantCulture);
            string b=SocialRuntimeBridge.QueryRelationship(target,id).Affinity.ToString("0.##",CultureInfo.InvariantCulture);
            var pair=facts.Where(f=>f.ActorPersonId==target||f.TargetPersonId==target).ToArray();
            string factText=string.Join("\n",pair.Select(f=>$"{f.Year}/{f.Month}/{f.Day} · {f.FactType} · {(f.ActorPersonId==id?"本人 → 对方":"对方 → 本人")}"));
            relations.Add(new PersonRelationSnapshot(target,other?.DisplayName??target,$"本人 → 对方：{a}；对方 → 本人：{b}\n{factText}",other!=null));
        }
        string social=relations.Count==0?"尚无记录": "仅列已有真实互动事实的参与对象。两个方向独立查询；0 可能为 neutral fallback，不表示确立关系。\n数值通道沿用现有原型，不代表友情等级。";
        string history="尚无记录";
        if(character?.LifeHistory?.Events!=null&&character.LifeHistory.Events.Any(e=>e!=null)) history=string.Join("\n\n",character.LifeHistory.Events.Where(e=>e!=null).OrderBy(e=>e.Year).ThenBy(e=>e.Month).ThenBy(e=>e.Day).Select(e=>$"{e.Year}/{e.Month}/{e.Day} · "+(e.Type==PersonLifeEventType.ProfessionFormed?"职业形成："+e.ProfessionId:e.Type==PersonLifeEventType.ExpeditionReturned?"远征返回："+(e.Outcome==0?"安全返回":e.Outcome==1?"撤退返回":e.Outcome==2?"失败返回":"返回结果资料暂缺"):"经历类型资料暂缺")));
        bool can=auth?.IsAvailable==true&&p?.Availability==VillageAvailability.InVillage;
        return new PersonDetailSnapshot(id,person.DisplayName,true,can,new[]{overview,life,growth,social,history},relations);
    }
    static string Empty(string s,string fallback)=>string.IsNullOrEmpty(s)?fallback:s;
    static string Place(ProductHomeSnapshot home,string id)=>string.IsNullOrEmpty(id)?"暂无地点事实":home?.Place(id)?.DisplayName??id;
    static string Card(string id)=>DataManager.Instance?.AllBattleCards?.FirstOrDefault(c=>c!=null&&c.CardId==id)?.DisplayName??id;
    static string Cards(IEnumerable<string> ids)=>ids==null||!ids.Any()?"（空）":string.Join("、",ids.Select(Card));
    static string Starter(IList<StarterSlotEntry> slots)=>slots==null||slots.Count==0?"（无槽）":string.Join("\n",slots.Select((s,i)=>$"槽 {i+1}："+(s==null||s.IsEmpty?"（空）":Card(s.CardId)+" · 熟练 "+s.Proficiency)));
}
