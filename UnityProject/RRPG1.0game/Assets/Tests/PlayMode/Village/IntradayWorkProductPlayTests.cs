using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public partial class VillageCommitmentProductIntegrationPlayTests
{
    [UnityTest] public IEnumerator IntradayWork_RealTownAssignment_CommutingLaborLunchVisible()
    {
        SceneManager.LoadScene("SampleScene");yield return null;yield return null;
        var town=EnterTown();yield return null;
        InvokeStatic(BridgeType,"SetSimulationActive",false);
        var clock=(VillageTime)GetBridgeField("Time");var ui=FindUi();InvokeInstance(ui,"Show");
        string person=FirstPersonId(town);InvokeInstance(ui,"OnPersonClicked",person);
        var container=(RectTransform)GetInstanceField(ui,"workplaceListContainer");
        var row=container.Find("WorkplaceRow_ws_farm_01");Assert.IsNotNull(row);
        var button=row.GetComponent<Button>()??row.GetComponentInChildren<Button>();Assert.IsNotNull(button);
        Assert.IsTrue(button.interactable);button.onClick.Invoke();
        var assignments=(VillageAssignmentSystem)GetBridgeField("Assignments");
        Assert.AreEqual("ws_farm_01",assignments.GetWorkplaceId(person));
        var resources=(VillageResourceState)GetBridgeField("Resources");resources.AddFood(100);
        Assert.AreEqual(WorldAdvanceOutcome.Completed,clock.AdvanceTo(new WorldInstant(480)).Outcome);
        InvokeInstance(ui,"Refresh");var selected=(TMP_Text)GetInstanceField(ui,"selectedInfoText");
        StringAssert.Contains("岗位劳动",selected.text);
        clock.AdvanceTo(new WorldInstant(720));InvokeInstance(ui,"Refresh");StringAssert.Contains("午休",selected.text);
        int ingredients=resources.Ingredients,food=resources.Food;
        clock.AdvanceTo(new WorldInstant(780));Assert.AreEqual(ingredients,resources.Ingredients);Assert.AreEqual(food,resources.Food);
        clock.AdvanceTo(new WorldInstant(1020));InvokeInstance(ui,"Refresh");StringAssert.Contains("下班 / 休息",selected.text);
        var root=(GameObject)GetInstanceField(ui,"root");Assert.IsTrue(root.activeInHierarchy);
        Assert.IsTrue(selected.enabled);Assert.IsFalse(string.IsNullOrEmpty(selected.text));
        yield return null;
    }
}
