using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    [Header("References")]
    public CameraMovment cameraMovment;
    public Transform supplyCenterCameraPoint;

    [Header("Enemy Setup")]
    [Tooltip("Tag of the country where enemy units spawn (e.g. France)")]
    public string enemyCountryTag;
    [Tooltip("How many enemy units to spawn")]
    public int enemyUnitCount = 2;

    [Header("Timing")]
    public float dialogPause = 1.5f;
    public float longPause = 2.5f;

    private MainPlayerController localPlayer;
    private bool triedLandUnit;
    private bool triedBoatUnit;
    private bool firstBuildDone;
    private bool enemySpawned;
    private int buildPhaseCount;

    enum TutorialStep
    {
        Welcome,
        PickCountry,
        ExplainBuildKey,
        ExplainUnitTypes,
        WaitForBuild,
        ClickUnit,
        ExplainUnit,
        SpawnEnemy,
        AttackOrder,
        WaitForAttack,
        AttackResult,
        ExplainSupplyCenters,
        SecondBuildPhase,
        ExplainSupport,
        WaitForSecondAttack,
        TutorialComplete
    }

    private TutorialStep currentStep;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        StartCoroutine(RunTutorial());
    }

    private MainPlayerController FindLocalPlayer()
    {
        foreach (var p in MainPlayerController.allPlayers)
        {
            if (p != null && p.isLocalPlayer)
                return p;
        }
        return null;
    }

    private IEnumerator WaitForLocalPlayer()
    {
        while (localPlayer == null)
        {
            localPlayer = FindLocalPlayer();
            yield return new WaitForSeconds(0.5f);
        }
    }

    private IEnumerator WaitForDialog()
    {
        yield return new WaitForSeconds(0.3f);
        while (DialogManager.Instance != null && DialogManager.Instance.gameObject.activeSelf
            && DialogManager.Instance.dialogPanel != null && DialogManager.Instance.dialogPanel.activeSelf)
        {
            yield return null;
        }
        yield return new WaitForSeconds(0.3f);
    }

    private IEnumerator ShowAndWait(string message)
    {
        DialogManager.Show(message);
        yield return WaitForDialog();
    }

    private IEnumerator RunTutorial()
    {
        // Wait for networking and player to be ready
        yield return WaitForLocalPlayer();
        yield return new WaitForSeconds(dialogPause);

        // === WELCOME ===
        currentStep = TutorialStep.Welcome;
        yield return ShowAndWait("Hello Commander. I will be your second in command.");
        yield return ShowAndWait("I'll walk you through the basics of warfare. Pay attention.");

        // === PICK COUNTRY ===
        currentStep = TutorialStep.PickCountry;
        PulseAllStarterCountries();
        yield return ShowAndWait("First, pick a country. Click on one of the flashing territories to claim it as yours.");
        yield return ShowAndWait("Then press Confirm to lock in your choice.");

        // Wait for player to choose a country
        while (localPlayer != null && !localPlayer.hasChosenCountry)
            yield return null;

        StopAllCountryPulses();
        yield return new WaitForSeconds(dialogPause);

        // === EXPLAIN BUILD KEY ===
        currentStep = TutorialStep.ExplainBuildKey;
        yield return ShowAndWait("Good choice, Commander.");
        yield return ShowAndWait("Now it's time to build your forces. Press F to open the unit piles.");

        // Wait for player to open build table
        while (localPlayer != null && !IsBuildTableOpen())
            yield return null;

        // === EXPLAIN UNIT TYPES ===
        currentStep = TutorialStep.ExplainUnitTypes;
        yield return ShowAndWait("You have three types of units to choose from.");
        yield return ShowAndWait("Tanks move on land and claim new territory for your empire.");
        yield return ShowAndWait("Boats patrol the oceans. They can support land units and help them cross water.");
        yield return ShowAndWait("Planes provide support from the air, but can only be built when you control 30% of supply centers.");
        yield return ShowAndWait("For now, I recommend building tanks. Click on the tank pile to select it, then click your territory to place it.");

        // === WAIT FOR BUILD ===
        currentStep = TutorialStep.WaitForBuild;
        firstBuildDone = false;

        while (localPlayer != null && localPlayer.hasChosenCountry)
        {
            // Check if build phase ended
            if (!IsBuildPhaseActive())
            {
                firstBuildDone = true;
                break;
            }
            yield return null;
        }

        yield return new WaitForSeconds(dialogPause);

        // === CLICK UNIT ===
        currentStep = TutorialStep.ClickUnit;
        yield return ShowAndWait("Your forces are ready, Commander.");
        yield return ShowAndWait("Click on one of your units to select it. You'll see which territories it can move to.");

        // Wait for player to select a unit
        MainUnit selectedUnit = null;
        while (selectedUnit == null)
        {
            if (localPlayer != null)
            {
                var field = typeof(MainPlayerController).GetField("selectedUnit",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                    selectedUnit = field.GetValue(localPlayer) as MainUnit;
            }
            yield return null;
        }

        // === EXPLAIN UNIT ===
        currentStep = TutorialStep.ExplainUnit;
        if (selectedUnit.unitType == UnitType.Land)
        {
            triedLandUnit = true;
            yield return ShowAndWait("A tank. Strong on land. Click an adjacent flashing territory to issue a move order.");
        }
        else if (selectedUnit.unitType == UnitType.Boat)
        {
            triedBoatUnit = true;
            yield return ShowAndWait("A boat. It moves on ocean tiles. Click an adjacent ocean to issue a move order.");
        }

        yield return ShowAndWait("After giving orders to your units, press the Confirm button to end your turn.");
        yield return ShowAndWait("But first... the enemy approaches.");

        // === SPAWN ENEMY ===
        currentStep = TutorialStep.SpawnEnemy;
        yield return new WaitForSeconds(dialogPause);

        SpawnEnemyUnits();
        enemySpawned = true;

        yield return ShowAndWait("Enemy forces have appeared! They threaten your borders.");

        // === ATTACK ORDER ===
        currentStep = TutorialStep.AttackOrder;
        yield return ShowAndWait("Select your tank and click the enemy territory to attack it.");
        yield return ShowAndWait("Then press Confirm to execute your orders.");

        // === WAIT FOR ATTACK ===
        currentStep = TutorialStep.WaitForAttack;

        // Wait for player to confirm moves
        while (localPlayer != null)
        {
            var readyField = typeof(MainPlayerController).GetField("isReady",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (readyField != null)
            {
                // isReady is a SyncVar, check it
                bool ready = (bool)readyField.GetValue(localPlayer);
                if (ready) break;
            }
            yield return null;
        }

        // Wait for turn to resolve
        yield return new WaitForSeconds(longPause);

        // === ATTACK RESULT ===
        currentStep = TutorialStep.AttackResult;
        yield return ShowAndWait("The attack was repelled! When forces are equal, neither side moves.");
        yield return ShowAndWait("You need more strength. Let me show you how.");

        // === EXPLAIN SUPPLY CENTERS ===
        currentStep = TutorialStep.ExplainSupplyCenters;

        if (supplyCenterCameraPoint != null && cameraMovment != null)
        {
            cameraMovment.SetManualInputLocked(true);
            Vector3 panTarget = supplyCenterCameraPoint.position;
            // Temporarily move camera to supply center
            var posField = typeof(CameraMovment).GetField("targetPosition",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (posField != null)
                posField.SetValue(cameraMovment, panTarget);
        }

        yield return ShowAndWait("See these territories with stars? Those are Supply Centers.");
        yield return ShowAndWait("Capture them to earn Build Points. More build points means more units.");
        yield return ShowAndWait("Control 75% of all supply centers and you win the war.");

        if (cameraMovment != null)
            cameraMovment.SetManualInputLocked(false);

        yield return new WaitForSeconds(dialogPause);

        // === SECOND BUILD PHASE ===
        currentStep = TutorialStep.SecondBuildPhase;
        yield return ShowAndWait("Capture nearby supply centers to get more units. Move your units to unclaimed territories.");

        if (!triedLandUnit || !triedBoatUnit)
        {
            string missing = !triedLandUnit ? "tank" : "boat";
            yield return ShowAndWait($"Remember to press F to build. Try building a {missing} this time.");
        }

        // === EXPLAIN SUPPORT ===
        currentStep = TutorialStep.ExplainSupport;
        yield return ShowAndWait("Now for the key to victory: Support Orders.");
        yield return ShowAndWait("Hold SHIFT and click another friendly unit to support it.");
        yield return ShowAndWait("A supported attack has more strength. Two units attacking one will break through.");
        yield return ShowAndWait("Try it now. Select a unit, then hold SHIFT and click another unit to support its attack on the enemy.");

        // === WAIT FOR SECOND ATTACK ===
        currentStep = TutorialStep.WaitForSecondAttack;
        yield return ShowAndWait("Give your orders and press Confirm when ready.");

        while (localPlayer != null)
        {
            var readyField = typeof(MainPlayerController).GetField("isReady",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (readyField != null)
            {
                bool ready = (bool)readyField.GetValue(localPlayer);
                if (ready) break;
            }
            yield return null;
        }

        yield return new WaitForSeconds(longPause);

        // === TUTORIAL COMPLETE ===
        currentStep = TutorialStep.TutorialComplete;
        yield return ShowAndWait("Excellent work, Commander!");
        yield return ShowAndWait("You now know the basics: claim territory, build units, attack, and use support orders.");
        yield return ShowAndWait("Remember: capture 75% of supply centers to win. Good luck out there.");

        yield return new WaitForSeconds(longPause);

        // Return to main menu
        SceneManager.LoadScene("MainMenu");
    }

    private void PulseAllStarterCountries()
    {
        Country[] allCountries = Object.FindObjectsByType<Country>(FindObjectsSortMode.None);
        for (int i = 0; i < allCountries.Length; i++)
        {
            if (allCountries[i] == null) continue;
            if (!allCountries[i].isStarterCountry) continue;

            Renderer rend = allCountries[i].GetComponentInChildren<Renderer>();
            if (rend == null) continue;

            PulsingHighlighter highlighter =
                rend.GetComponent<PulsingHighlighter>() ??
                rend.gameObject.AddComponent<PulsingHighlighter>();

            highlighter.StartPulse();
        }
    }

    private void StopAllCountryPulses()
    {
        PulsingHighlighter[] all = Object.FindObjectsByType<PulsingHighlighter>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null)
                all[i].StopPulse();
        }
    }

    private bool IsBuildTableOpen()
    {
        if (localPlayer == null) return false;
        var field = typeof(MainPlayerController).GetField("buildTableSelectionOpen",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null) return false;
        return (bool)field.GetValue(localPlayer);
    }

    private bool IsBuildPhaseActive()
    {
        if (localPlayer == null) return false;
        var field = typeof(MainPlayerController).GetField("buildPhaseActiveLocal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null) return false;
        return (bool)field.GetValue(localPlayer);
    }

    private void SpawnEnemyUnits()
    {
        if (string.IsNullOrEmpty(enemyCountryTag)) return;
        if (MainUnitManager.Instance == null) return;

        // Use a fake enemy player ID
        int enemyPlayerID = 99;
        Color enemyColor = Color.red;

        // Set the enemy country owner
        GameObject countryObj = GameObject.FindWithTag(enemyCountryTag);
        if (countryObj != null)
        {
            Country country = countryObj.GetComponent<Country>();
            if (country != null)
                country.SetOwner(enemyPlayerID);
        }

        MainUnitManager.Instance.SpawnUnitsForCountryServer(
            enemyCountryTag, enemyPlayerID, enemyColor, enemyUnitCount, UnitType.Land
        );
    }
}
