using BepInEx;
using BepInEx.Unity.Mono;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

[BepInPlugin("com.mmo98.trainer", "MMO98 Trainer", "1.1.0")]
public class Plugin : BaseUnityPlugin
{
    private string moneyInput = "1000";
    private GameController controller;

    private void Update()
    {
        if (controller == null)
            controller = UnityEngine.Object.FindObjectOfType<GameController>();
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 250, 140), "MMO98 Trainer");

        GUI.Label(new Rect(20, 40, 100, 20), "Money:");
        moneyInput = GUI.TextField(new Rect(80, 40, 140, 20), moneyInput);

        if (GUI.Button(new Rect(20, 70, 200, 25), "Give Money"))
        {
            if (double.TryParse(moneyInput, out double amount))
            {
                GiveMoney(amount);
            }
        }

        if (GUI.Button(new Rect(20, 100, 200, 25), "Unlock All Upgrades"))
        {
            UnlockAllUpgrades();
        }
    }

    private void GiveMoney(double amount)
    {
        if (controller == null) return;

        MethodInfo method = typeof(GameController).GetMethod(
            "GiveMoney",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        method?.Invoke(controller, new object[] { amount });
    }

    private void UnlockAllUpgrades()
    {
        if (controller == null) return;

        MethodInfo method = typeof(GameController).GetMethod(
            "UnlockAllUpgrades",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        method?.Invoke(controller, null);
    }
}