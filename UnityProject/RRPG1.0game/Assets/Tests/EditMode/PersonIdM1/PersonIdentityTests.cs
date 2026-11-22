using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// M1 §15.1：两个 CharacterId 完全相同的普通人物必须拥有不同且非空的 PersonId。
/// </summary>
public class PersonIdentityUniquenessTests
{
    [Test]
    public void TwoCharacters_SameCharacterId_HaveDistinctPersonIds()
    {
        object p1 = M1TestKit.NewCharacter("char_001", "战士");
        object p2 = M1TestKit.NewCharacter("char_001", "战士");

        string pid1 = M1TestKit.PersonIdOf(p1);
        string pid2 = M1TestKit.PersonIdOf(p2);

        Assert.IsFalse(string.IsNullOrEmpty(pid1), "P1.PersonId 不得为空");
        Assert.IsFalse(string.IsNullOrEmpty(pid2), "P2.PersonId 不得为空");
        Assert.AreNotEqual(pid1, pid2, "同模板两个实例必须拥有不同 PersonId");
        Assert.AreNotEqual(pid1, (string)M1TestKit.Get(p1, "CharacterId"), "PersonId 不得等于模板 CharacterId");
        Assert.AreNotEqual(pid2, (string)M1TestKit.Get(p2, "CharacterId"), "PersonId 不得等于模板 CharacterId");
    }

    [Test]
    public void NewHero_HasNonEmptyPersonId_DifferentFromHeroId()
    {
        object h = M1TestKit.NewHero();
        string pid = M1TestKit.PersonIdOf(h);
        Assert.IsFalse(string.IsNullOrEmpty(pid), "Hero PersonId 不得为空");
        Assert.AreNotEqual(pid, (string)M1TestKit.Get(h, "HeroId"), "Hero PersonId 不得等于模板 HeroId");
    }

    [Test]
    public void CreatedInstances_AreGloballyUniqueEnough()
    {
        var seen = new HashSet<string>();
        for (int i = 0; i < 50; i++)
        {
            string pid = M1TestKit.PersonIdOf(M1TestKit.NewCharacter("char_001"));
            Assert.IsTrue(seen.Add(pid), "重复生成 PersonId 不应出现");
        }
    }
}

/// <summary>
/// M1 §15.2：PlayerCollection 按 PersonId 查询必须精确命中各自实例（同模板不返回错实例）。
/// </summary>
public class PlayerCollectionPersonLookupTests
{
    [Test]
    public void GetCharacterByPersonId_ReturnsExactInstance_ForDuplicateTemplates()
    {
        object collection = M1TestKit.NewPlayerCollection();
        object p1 = M1TestKit.NewCharacter("char_001", "战士甲");
        object p2 = M1TestKit.NewCharacter("char_001", "战士乙");
        M1TestKit.OwnedCharacters(collection).Add(p1);
        M1TestKit.OwnedCharacters(collection).Add(p2);

        string pid1 = M1TestKit.PersonIdOf(p1);
        string pid2 = M1TestKit.PersonIdOf(p2);

        object r1 = M1TestKit.InvokeInstance(collection, "GetCharacterByPersonId", pid1);
        object r2 = M1TestKit.InvokeInstance(collection, "GetCharacterByPersonId", pid2);
        Assert.AreSame(p1, r1, "GetCharacterByPersonId(P1) 必须返回 P1");
        Assert.AreSame(p2, r2, "GetCharacterByPersonId(P2) 必须返回 P2");
    }

    [Test]
    public void FindPersonByPersonId_ReturnsCharacterAndHero_ByInstanceId()
    {
        object collection = M1TestKit.NewPlayerCollection();
        object p1 = M1TestKit.NewCharacter("char_001", "战士");
        object h1 = M1TestKit.NewHero();
        M1TestKit.OwnedCharacters(collection).Add(p1);
        M1TestKit.OwnedHeroes(collection).Add(h1);

        Assert.AreSame(p1, M1TestKit.InvokeInstance(collection, "FindPersonByPersonId", M1TestKit.PersonIdOf(p1)));
        Assert.AreSame(h1, M1TestKit.InvokeInstance(collection, "FindPersonByPersonId", M1TestKit.PersonIdOf(h1)));
        Assert.IsNull(M1TestKit.InvokeInstance(collection, "FindPersonByPersonId", "char_001"),
            "模板 CharacterId 不是合法 PersonId，不得命中具体人物");
    }

    [Test]
    public void CardResolve_ReturnsItsOwnInstance_ForDuplicateTemplates()
    {
        object collection = M1TestKit.NewPlayerCollection();
        object p1 = M1TestKit.NewCharacter("char_001", "战士甲");
        object p2 = M1TestKit.NewCharacter("char_001", "战士乙");
        object card1 = M1TestKit.NewCharacterCard("ccard_dup_1", "char_001");
        object card2 = M1TestKit.NewCharacterCard("ccard_dup_2", "char_001");
        M1TestKit.OwnedCharacters(collection).Add(p1);
        M1TestKit.OwnedCharacters(collection).Add(p2);
        M1TestKit.OwnedCharacterCards(collection).Add(card1);
        M1TestKit.OwnedCharacterCards(collection).Add(card2);

        M1TestKit.InvokeInstance(collection, "BindCharacterCardOwner",
            M1TestKit.Get(card1, "CardId"), M1TestKit.PersonIdOf(p1));
        M1TestKit.InvokeInstance(collection, "BindCharacterCardOwner",
            M1TestKit.Get(card2, "CardId"), M1TestKit.PersonIdOf(p2));

        object r1 = M1TestKit.InvokeInstance(collection, "GetCharacterInstanceForCard", card1);
        object r2 = M1TestKit.InvokeInstance(collection, "GetCharacterInstanceForCard", card2);
        Assert.AreSame(p1, r1, "卡1 必须解析到 P1");
        Assert.AreSame(p2, r2, "卡2 必须解析到 P2");
    }
}
