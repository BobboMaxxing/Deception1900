using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;
    public static bool IsActive => Instance != null;

    [Header("References")]
    public CameraMovment cameraMovment;
    public Transform supplyCenterCameraPoint;

    [Header("Country Setup")]
    [Tooltip("Tag of the player country (only this one flashes)")]
    public string playerCountryTag = "Germany";
    [Tooltip("Tag of the enemy country")]
    public string enemyCountryTag = "France";
    [Tooltip("How many enemy units to spawn")]
    public int enemyUnitCount = 2;

    [Header("Timing")]
    [Tooltip("Seconds to wait for the map flip animation before starting")]
    public float mapFlipDelay = 3f;
    [Tooltip("Seconds to wait after player completes an input check")]
    public float inputConfirmDelay = 2f;
    public float dialogPause = 1.5f;
    public float longPause = 2.5f;

    private MainPlayerController localPlayer;
    private bool firstBuildDone;
    private bool enemySpawned;

    // Camera input tracking
    private bool hasMovedCamera;
    private bool hasZoomed;
    private bool hasDragged;

    // Confirm press tracking
    private bool hasConfirmed;

    enum TutorialStep
    {
        WaitForMapFlip,
        Welcome,
        ExplainCameraMove,
        ExplainCameraZoom,
        ExplainCameraDrag,
        PickCountry,
        ExplainUnitTypes,
        ExplainBuildKey,
        WaitForBuild,
        SpawnEnemy,
        AttackOrder,
        WaitForAttack,
        AttackResult,
        ExplainSupplyCenters,
        RemindBuild,
        WaitForSecondBuild,
        ExplainSupport,
        SecondAttack,
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

    void Update()
    {
        // Track camera inputs for tutorial progression
        if (currentStep == TutorialStep.ExplainCameraMove)
        {
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) ||
                Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D) ||
                Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) ||
                Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow))
            {
                hasMovedCamera = true;
            }
        }

        if (currentStep == TutorialStep.ExplainCameraZoom)
        {
            if (Input.GetAxis("Mouse ScrollWheel") != 0f)
            {
                hasZoomed = true;
            }
        }

        if (currentStep == TutorialStep.ExplainCameraDrag)
        {
            if (Input.GetMouseButton(2)) // middle mouse
            {
                hasDragged = true;
            }
        }
    }

    void OnEnable()
    {
        MainPlayerController.OnMovesConfirmed += HandleMovesConfirmed;
    }

    void OnDisable()
    {
        MainPlayerController.OnMovesConfirmed -= HandleMovesConfirmed;
    }

    private void HandleMovesConfirmed()
    {
        hasConfirmed = true;
    }

    private IEnumerator WaitForConfirmPress()
    {
        hasConfirmed = false;
        while (!hasConfirmed)
            yield return null;
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
        float timeout = 15f;
        float elapsed = 0f;
        while (DialogManager.Instance != null && DialogManager.Instance.gameObject.activeSelf
            && DialogManager.Instance.dialogPanel != null && DialogManager.Instance.dialogPanel.activeSelf)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= timeout) break;
            yield return null;
        }
        yield return new WaitForSeconds(0.3f);
    }

    private IEnumerator ShowAndWait(string message)
    {
        DialogManager.TutorialShow(message);
        yield return WaitForDialog();
    }

    private IEnumerator RunTutorial()
    {
        // Wait for networking and player to be ready
        yield return WaitForLocalPlayer();

        // === WAIT FOR MAP FLIP ANIMATION ===
        currentStep = TutorialStep.WaitForMapFlip;
        yield return new WaitForSeconds(mapFlipDelay);

        // === WELCOME ===
        currentStep = TutorialStep.Welcome;
        yield return ShowAndWait("Hello Commander. I will be your second in command.");
        yield return ShowAndWait("I'll walk you through the basics of warfare. Pay attention.");

        // === CAMERA MOVE ===
        currentStep = TutorialStep.ExplainCameraMove;
        yield return ShowAndWait("First, let's get you oriented. Move the camera with WASD or the Arrow Keys. Try it now.");
        hasMovedCamera = false;

        while (!hasMovedCamera)
            yield return null;

        yield return new WaitForSeconds(inputConfirmDelay);

        // === CAMERA ZOOM ===
        currentStep = TutorialStep.ExplainCameraZoom;
        yield return ShowAndWait("Good! Now try zooming in and out with the Scroll Wheel.");
        hasZoomed = false;

        while (!hasZoomed)
            yield return null;

        yield return new WaitForSeconds(inputConfirmDelay);

        // === CAMERA DRAG ===
        currentStep = TutorialStep.ExplainCameraDrag;
        yield return ShowAndWait("You can also hold down the Scroll Wheel and drag to pan around the map. Try it.");
        hasDragged = false;

        while (!hasDragged)
            yield return null;

        yield return new WaitForSeconds(inputConfirmDelay);
        yield return ShowAndWait("Excellent! You've got the hang of it.");

        // === PICK COUNTRY (only Germany flashes) ===
        currentStep = TutorialStep.PickCountry;
        PulseCountry(playerCountryTag);
        yield return ShowAndWait("Now, pick your country. Click on the flashing territory to claim it.");
        yield return ShowAndWait("Then press Confirm to lock in your choice.");

        // Wait for player to actually confirm their country pick
        while (localPlayer != null && !localPlayer.hasChosenCountry)
            yield return null;

        StopAllCountryPulses();
        yield return new WaitForSeconds(dialogPause);

        // === EXPLAIN UNIT TYPES (before opening build table) ===
        currentStep = TutorialStep.ExplainUnitTypes;
        yield return ShowAndWait("Well done.");
        yield return ShowAndWait("Before you build, let me explain the unit types.");
        yield return ShowAndWait("Tanks move on land and claim new territory for your empire. They are the backbone of your army.");
        yield return ShowAndWait("Boats patrol the oceans. They can support land units and block sea routes.");
        yield return ShowAndWait("Planes provide support from the air, but can only be built when you control 30% of supply centers.");
        yield return ShowAndWait("Tanks are the best choice right now, but boats are fine too if you want to try them.");

        // === EXPLAIN BUILD KEY ===
        currentStep = TutorialStep.ExplainBuildKey;
        yield return ShowAndWait("Now press F to open the build menu and place your units.");

        while (localPlayer != null && !IsBuildTableOpen())
            yield return null;

        yield return ShowAndWait("Click a unit pile to select it, then click your territory to place it.");

        // === WAIT FOR BUILD ===
        currentStep = TutorialStep.WaitForBuild;
        while (localPlayer != null)
        {
            if (!IsBuildPhaseActive())
            {
                firstBuildDone = true;
                break;
            }
            yield return null;
        }

        yield return new WaitForSeconds(dialogPause);

        // === SPAWN ENEMY IN FRANCE ===
        currentStep = TutorialStep.SpawnEnemy;
        SpawnEnemyUnits();
        enemySpawned = true;

        yield return ShowAndWait("Enemy forces have appeared in France! They threaten your borders.");

        // === ATTACK ORDER ===
        currentStep = TutorialStep.AttackOrder;
        PulseCountry(enemyCountryTag);
        yield return ShowAndWait("Select your tank and click France to attack it.");
        yield return ShowAndWait("Then press Confirm to execute your orders.");

        // === WAIT FOR ATTACK (wait for confirm press) ===
        currentStep = TutorialStep.WaitForAttack;
        yield return WaitForConfirmPress();

        yield return new WaitForSeconds(dialogPause);
        StopAllCountryPulses();

        // === ATTACK RESULT (player loses — equal forces) ===
        currentStep = TutorialStep.AttackResult;
        yield return ShowAndWait("The attack was repelled! When forces are equal, the defender holds.");
        yield return ShowAndWait("You need more strength. Let me explain how to get it.");

        // === PAN TO SUPPLY CENTERS ===
        currentStep = TutorialStep.ExplainSupplyCenters;
        if (supplyCenterCameraPoint != null && cameraMovment != null)
        {
            cameraMovment.SetManualInputLocked(true);
            Vector3 panTarget = supplyCenterCameraPoint.position;
            var posField = typeof(CameraMovment).GetField("targetPosition",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (posField != null)
                posField.SetValue(cameraMovment, panTarget);
        }

        yield return ShowAndWait("See these territories with factories? Those are Supply Centers.");
        yield return ShowAndWait("Capture them to earn Build Points. More build points means more units.");
        yield return ShowAndWait("Control 75% of all supply centers and you win the war.");
        yield return ShowAndWait("Press Space to reset the camera back to your territory. You can use this anytime.");

        if (cameraMovment != null)
        {
            cameraMovment.SetManualInputLocked(false);
            cameraMovment.ResetCamera();
        }

        yield return new WaitForSeconds(dialogPause);

        // === CLAIM SUPPLY CENTER ===
        currentStep = TutorialStep.RemindBuild;
        yield return ShowAndWait("Move one of your units to a nearby territory with a factory to claim it.");
        yield return ShowAndWait("Then press Confirm to end your turn.");

        // Wait for player to confirm the move
        yield return WaitForConfirmPress();
        yield return new WaitForSeconds(dialogPause);

        yield return ShowAndWait("Now press F to build more tanks with your new build points.");

        // === WAIT FOR SECOND BUILD ===
        currentStep = TutorialStep.WaitForSecondBuild;
        // Wait for build phase to start
        while (localPlayer != null && !IsBuildPhaseActive())
            yield return null;
        // Wait for build phase to end
        while (localPlayer != null && IsBuildPhaseActive())
            yield return null;

        yield return new WaitForSeconds(dialogPause);

        // === EXPLAIN SUPPORT ===
        currentStep = TutorialStep.ExplainSupport;
        yield return ShowAndWait("Now for the key to victory: Support Orders.");
        yield return ShowAndWait("Hold SHIFT and click another friendly unit to support its attack.");
        yield return ShowAndWait("A supported attack has more strength. Two units attacking one will break through!");
        yield return ShowAndWait("Try it now. Select a unit, hold SHIFT, and click another unit to support its attack on France.");

        // === SECOND ATTACK ===
        currentStep = TutorialStep.SecondAttack;
        PulseCountry(enemyCountryTag);
        yield return ShowAndWait("Give your orders and press Confirm when ready.");

        // === WAIT FOR SECOND ATTACK (wait for confirm press) ===
        currentStep = TutorialStep.WaitForSecondAttack;
        yield return WaitForConfirmPress();

        yield return new WaitForSeconds(dialogPause);
        StopAllCountryPulses();

        // === TUTORIAL COMPLETE ===
        currentStep = TutorialStep.TutorialComplete;
        yield return ShowAndWait("Excellent work, Commander!");
        yield return ShowAndWait("You now know the basics: claim territory, build units, attack, and use support orders.");
        yield return ShowAndWait("Remember: capture 75% of supply centers to win. Good luck out there.");

        yield return new WaitForSeconds(longPause);
        SceneManager.LoadScene("MainMenu");
    }

    private void PulseCountry(string countryTag)
    {
        if (string.IsNullOrEmpty(countryTag)) return;

        GameObject countryObj = GameObject.FindWithTag(countryTag);
        if (countryObj == null) return;

        Renderer rend = countryObj.GetComponentInChildren<Renderer>();
        if (rend == null) return;

        PulsingHighlighter highlighter =
            rend.GetComponent<PulsingHighlighter>() ??
            rend.gameObject.AddComponent<PulsingHighlighter>();

        highlighter.StartPulse();
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

        int enemyPlayerID = 99;
        Color enemyColor = Color.red;

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
