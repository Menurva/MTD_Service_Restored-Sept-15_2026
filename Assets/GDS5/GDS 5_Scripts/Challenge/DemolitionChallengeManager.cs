using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DemolitionChallengeManager : MonoBehaviour
{
    // This component is the single controller for the complete demolition challenge.
    // It connects the timer, destructible houses, waypoint markers, result screen, and scene buttons.

    // ================================================================
    // SYSTEM 1: CHALLENGE SETTINGS AND SHARED DATA
    // These Inspector fields define the time limit, target group, camera, and marker position.
    // ================================================================
    [Header("Challenge")]
    [SerializeField, Range(1f, 300f)] private float challengeDurationSeconds = 300f;
    [SerializeField] private Transform destructibleHousesRoot;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Waypoint Display")]
    [SerializeField, Min(0f)] private float markerEdgePadding = 45f;

    [Header("Quest Receive Settings")]
    [SerializeField] private QuestReceiveTrigger questReceiveTrigger;
    [Tooltip("Leave at zero to mark the exact world-space centre of the quest collider.")]
    [SerializeField] private Vector3 questMarkerWorldOffset = Vector3.zero;

    [Header("HUD Scene References")]
    [SerializeField] private Canvas hudCanvas;
    [SerializeField] private Image countdownPanelBackground;
    [SerializeField] private Text challengeTitleText;
    [SerializeField] private Text timerText;
    [SerializeField] private Image houseCounterPanelBackground;
    [SerializeField] private Text objectiveText;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private Image resultPanelBackground;
    [SerializeField] private Text resultTitleText;
    [SerializeField] private Text resultDetailsText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private RectTransform markerContainer;
    [SerializeField] private RectTransform markerTemplate;
    [SerializeField] private RectTransform questMarker;
    [SerializeField] private Text questMarkerTextComponent;
    [SerializeField] private GameObject questPanel;
    [SerializeField] private Image questPanelBackground;
    [SerializeField] private Text questDetailsTextComponent;
    [SerializeField] private Button acceptQuestButton;
    [SerializeField] private Button rejectQuestButton;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Image pausePanelBackground;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button pauseMainMenuButton;
    [SerializeField] private Button quitButton;

    [Header("HUD Text Content")]
    // EDIT HERE - QUEST MARKER TEXT
    [SerializeField] private string questMarkerText = "!";
    // EDIT HERE - QUEST DETAILS
    [SerializeField, TextArea(3, 8)] private string questDetailsText = "[insert text]";
    [Tooltip("{0} is replaced by the current minutes and seconds.")]
    [SerializeField] private string timerTextFormat = "{0}";
    [Tooltip("{0} is houses remaining. {1} is the total number of houses.")]
    [SerializeField] private string houseCounterTextFormat = "HOUSES REMAINING: {0} / {1}";
    [SerializeField] private string winTitle = "CHALLENGE COMPLETE";
    [SerializeField] private string loseTitle = "TIME UP";
    [Tooltip("{0} is the total number of houses. {1} is the remaining time.")]
    [SerializeField] private string winDetailsFormat =
        "All {0} houses were destroyed with {1} remaining.";
    [Tooltip("{0} is houses remaining. {1} is the total number of houses.")]
    [SerializeField] private string loseDetailsFormat = "{0} of {1} houses remain.";

    [Header("HUD Runtime Colours")]
    [Tooltip("Disable this to keep the timer colour set directly on the Text component.")]
    [SerializeField] private bool useUrgentTimerColor = true;
    [SerializeField, Min(0f)] private float urgentTimeSeconds = 30f;
    [SerializeField] private Color normalTimerColor = Color.white;
    [SerializeField] private Color urgentTimerColor = new Color(1f, 0.25f, 0.15f);
    [Tooltip("Disable this to keep the result title colour set directly on the Text component.")]
    [SerializeField] private bool useResultTitleColors = true;
    [SerializeField] private Color winTitleColor = new Color(0.35f, 1f, 0.45f);
    [SerializeField] private Color loseTitleColor = new Color(1f, 0.3f, 0.2f);

    private readonly List<HouseTarget> houseTargets = new List<HouseTarget>();
    private readonly Dictionary<BreakableSecond, HouseTarget> targetByPart =
        new Dictionary<BreakableSecond, HouseTarget>();

    private ChallengeState state = ChallengeState.Initializing;
    private float remainingTime;
    private int remainingHouseCount;
    private int totalHouseCount;
    private ChallengeState stateBeforePause = ChallengeState.WaitingForQuest;
    private InputSystemUIInputModule gameplayUiInputModule;
    private InputActionMap gameplayUiActionMap;
    private PrometeoCarController playerCarController;
    private bool carAudioMutedByMenu;
    private bool carUseSoundsBeforeMenu;
    private bool engineMuteBeforeMenu;
    private bool tireMuteBeforeMenu;
    private float engineVolumeBeforeMenu;
    private float tireVolumeBeforeMenu;
    private bool engineWasPlayingBeforeMenu;
    private bool tireWasPlayingBeforeMenu;

    // The challenge can only be in one of these states at a time.
    // This prevents a win and a loss from being triggered together.
    private enum ChallengeState
    {
        Initializing,
        WaitingForQuest,
        QuestPrompt,
        Running,
        Paused,
        Won,
        Lost
    }

    private sealed class HouseTarget
    {
        // One HouseTarget groups a complete house, all of its breakable parts, and its UI marker.
        public Transform Root;
        public readonly List<BreakableSecond> Parts = new List<BreakableSecond>();
        public RectTransform Marker;
        public Vector3 MarkerWorldPosition;
        public bool IsComplete;
    }

    // ================================================================
    // SYSTEM 2: STARTUP AND CHALLENGE INITIALIZATION
    // ================================================================

    // Awake runs before Start. It accepts only the HUD objects assigned in the Inspector.
    // No runtime fallback is created, so the scene version remains the single source of truth.
    private void Awake()
    {
        Time.timeScale = 1f;

        if (!ValidateHudReferences())
        {
            enabled = false;
            return;
        }

        PrepareHudForPlay();
    }

    // Start registers the houses but waits for the player to accept the quest before starting.
    private void Start()
    {
        ResetUiInputForNewSceneEntry();
        ResolvePlayerCarAudio();

        if (destructibleHousesRoot == null)
        {
            destructibleHousesRoot = transform;
        }

        if (gameplayCamera == null)
        {
            Debug.LogError(
                $"{nameof(DemolitionChallengeManager)} requires the active gameplay camera.",
                this);
            enabled = false;
            return;
        }

        BuildHouseTargets();
        if (totalHouseCount == 0)
        {
            Debug.LogError(
                $"{nameof(DemolitionChallengeManager)} found no houses containing {nameof(BreakableSecond)} under '{destructibleHousesRoot.name}'.",
                this);
            enabled = false;
            return;
        }

        remainingTime = challengeDurationSeconds;
        state = ChallengeState.WaitingForQuest;
        UpdateTimerText();
        UpdateObjectiveText();
        UpdateQuestMarker();
        Debug.Log(
            $"Quest UI ready. Waiting for the player at '{questReceiveTrigger.name}'.",
            this);
    }

    // ================================================================
    // SYSTEM 3: COUNTDOWN TIMER
    // ================================================================

    // Update subtracts real frame time from the countdown and causes a loss at zero seconds.
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleEscapePressed();
        }

        SynchronizeCarAudioWithMenuPanels();

        if (IsMenuPanelActive() &&
            Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            LogPointerRaycast();
        }

        if (state != ChallengeState.Running)
        {
            return;
        }

        remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
        UpdateTimerText();

        if (remainingTime <= 0f && remainingHouseCount > 0)
        {
            FinishChallenge(false);
        }
    }

    // LateUpdate moves the markers after normal object and camera movement has finished for the frame.
    private void LateUpdate()
    {
        if (state == ChallengeState.Running)
        {
            UpdateMarkers();
        }
        else if (state == ChallengeState.WaitingForQuest)
        {
            UpdateQuestMarker();
        }
    }

    // ================================================================
    // SYSTEM 4: HOUSE REGISTRATION AND DESTRUCTION TRACKING
    // ================================================================

    // BuildHouseTargets treats every direct child of Destructible Houses as one complete house.
    // It collects each house's BreakableSecond parts and creates one numbered marker for that house.
    private void BuildHouseTargets()
    {
        houseTargets.Clear();
        targetByPart.Clear();

        for (int childIndex = 0; childIndex < destructibleHousesRoot.childCount; childIndex++)
        {
            Transform houseRoot = destructibleHousesRoot.GetChild(childIndex);
            BreakableSecond[] parts = houseRoot.GetComponentsInChildren<BreakableSecond>(true);

            if (parts.Length == 0)
            {
                Debug.LogWarning(
                    $"Challenge target '{houseRoot.name}' has no breakable sections and will not be counted.",
                    houseRoot);
                continue;
            }

            HouseTarget target = new HouseTarget
            {
                Root = houseRoot,
                MarkerWorldPosition = CalculateMarkerPosition(houseRoot)
            };

            foreach (BreakableSecond part in parts)
            {
                target.Parts.Add(part);
                targetByPart[part] = target;

                if (!part.IsBroken)
                {
                    // Listen for the moment this individual section successfully breaks.
                    part.Broken += HandlePartBroken;
                }
            }

            target.Marker = CreateMarker(); //this here add number to the marker//
            houseTargets.Add(target);
        }

        totalHouseCount = houseTargets.Count;
        remainingHouseCount = totalHouseCount;
    }

    // CalculateMarkerPosition finds the visual centre and top area of a house for its waypoint.
    // It falls back to three units above the house root when the house has no visible renderer.
    private Vector3 CalculateMarkerPosition(Transform houseRoot)
    {
        Renderer[] renderers = houseRoot.GetComponentsInChildren<Renderer>(false);
        bool hasBounds = false;
        Bounds combinedBounds = new Bounds(houseRoot.position, Vector3.zero);

        foreach (Renderer houseRenderer in renderers)
        {
            if (houseRenderer == null || !houseRenderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = houseRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(houseRenderer.bounds);
            }
        }

        if (!hasBounds)
        {
            return houseRoot.position + Vector3.up * 3f;
        }

        float heightOffset = Mathf.Max(1f, combinedBounds.extents.y * 0.25f);
        return combinedBounds.center + Vector3.up * heightOffset;
    }

    // HandlePartBroken is called by BreakableSecond after one house section breaks.
    // The whole house is only completed when none of its registered sections remain intact.
    private void HandlePartBroken(BreakableSecond brokenPart)
    {
        if (state != ChallengeState.Running ||
            !targetByPart.TryGetValue(brokenPart, out HouseTarget target) ||
            target.IsComplete)
        {
            return;
        }

        foreach (BreakableSecond part in target.Parts)
        {
            if (part != null && !part.IsBroken)
            {
                return;
            }
        }

        CompleteHouse(target);
    }

    // CompleteHouse removes one house from the counter, hides its marker, and checks for victory.
    private void CompleteHouse(HouseTarget target)
    {
        target.IsComplete = true;
        remainingHouseCount = Mathf.Max(0, remainingHouseCount - 1);

        if (target.Marker != null)
        {
            target.Marker.gameObject.SetActive(false);
        }

        UpdateObjectiveText();

        if (remainingHouseCount == 0)
        {
            FinishChallenge(true);
        }
    }

    // ================================================================
    // SYSTEM 5: QUEST RECEIVING
    // ================================================================

    // The trigger calls this only when the assigned player car enters it.
    public void TryOpenQuestPanel(QuestReceiveTrigger source)
    {
        if (source != questReceiveTrigger || state != ChallengeState.WaitingForQuest)
        {
            return;
        }

        state = ChallengeState.QuestPrompt;
        questMarker.gameObject.SetActive(false);
        questPanel.transform.SetAsLastSibling();
        questPanel.SetActive(true);
        PrepareMenuInteraction(acceptQuestButton, "QUEST");
        Time.timeScale = 0f;
        Debug.Log("Player entered the quest trigger. Quest panel opened.", this);
    }

    // Reject closes the panel but the trigger requires a complete exit before it can reopen.
    public void RejectQuest()
    {
        Debug.Log("UI BUTTON CLICKED: QUEST - REJECT", rejectQuestButton);

        if (state != ChallengeState.QuestPrompt)
        {
            return;
        }

        questPanel.SetActive(false);
        questReceiveTrigger.RequireExitBeforeReopening();
        questMarker.gameObject.SetActive(true);
        state = ChallengeState.WaitingForQuest;
        Time.timeScale = 1f;
        SetCarAudioMuted(false);
        SetCursorForMenu(false);
        UpdateQuestMarker();
        Debug.Log("Quest rejected. Exit and re-enter the trigger to open it again.", this);
    }

    // Accept removes the quest marker and trigger, then starts the existing demolition challenge.
    public void AcceptQuest()
    {
        Debug.Log("UI BUTTON CLICKED: QUEST - ACCEPT", acceptQuestButton);

        if (state != ChallengeState.QuestPrompt)
        {
            return;
        }

        questPanel.SetActive(false);
        questMarker.gameObject.SetActive(false);
        questReceiveTrigger.CompleteQuest();
        countdownPanelBackground.gameObject.SetActive(true);
        houseCounterPanelBackground.gameObject.SetActive(true);
        remainingTime = challengeDurationSeconds;
        state = ChallengeState.Running;
        Time.timeScale = 1f;
        SetCarAudioMuted(false);
        SetCursorForMenu(false);

        RefreshTargetsForChallengeStart();
        UpdateTimerText();
        UpdateObjectiveText();
        Debug.Log("Quest accepted. House markers and challenge timer started.", this);

        if (remainingHouseCount == 0)
        {
            FinishChallenge(true);
        }
    }

    // This notification leaves the quest ready for the next valid entry after a rejection.
    public void NotifyQuestTriggerExited(QuestReceiveTrigger source)
    {
        if (source == questReceiveTrigger && state == ChallengeState.WaitingForQuest)
        {
            questMarker.gameObject.SetActive(true);
        }
    }

    // Houses may be damaged before acceptance, so their true condition is checked at quest start.
    private void RefreshTargetsForChallengeStart()
    {
        remainingHouseCount = 0;

        foreach (HouseTarget target in houseTargets)
        {
            bool allPartsBroken = true;

            foreach (BreakableSecond part in target.Parts)
            {
                if (part != null && !part.IsBroken)
                {
                    allPartsBroken = false;
                    break;
                }
            }

            target.IsComplete = allPartsBroken;

            if (target.Marker != null)
            {
                target.Marker.gameObject.SetActive(!allPartsBroken);
            }

            if (!allPartsBroken)
            {
                remainingHouseCount++;
            }
        }
    }

    // ================================================================
    // SYSTEM 6: WIN, LOSS, AND RESULT SCREEN
    // ================================================================

    // FinishChallenge can run only once. It stops the challenge and displays the correct result.
    private void FinishChallenge(bool playerWon)
    {
        if (state != ChallengeState.Running)
        {
            return;
        }

        state = playerWon ? ChallengeState.Won : ChallengeState.Lost;

        foreach (HouseTarget target in houseTargets)
        {
            if (target.Marker != null)
            {
                target.Marker.gameObject.SetActive(false);
            }
        }

        questMarker.gameObject.SetActive(false);
        questPanel.SetActive(false);
        pausePanel.SetActive(false);
        resultPanel.transform.SetAsLastSibling();
        resultPanel.SetActive(true);
        resultTitleText.text = playerWon ? winTitle : loseTitle;

        if (useResultTitleColors)
        {
            resultTitleText.color = playerWon ? winTitleColor : loseTitleColor;
        }

        if (playerWon)
        {
            resultDetailsText.text = ApplyTextFormat(
                winDetailsFormat,
                "All {0} houses were destroyed with {1} remaining.",
                totalHouseCount,
                FormatTime(remainingTime));
        }
        else
        {
            resultDetailsText.text = ApplyTextFormat(
                loseDetailsFormat,
                "{0} of {1} houses remain.",
                remainingHouseCount,
                totalHouseCount);
        }

        PrepareMenuInteraction(restartButton, "RESULT");
        Time.timeScale = 0f;
    }

    // ================================================================
    // SYSTEM 7: PAUSE MENU AND SCENE NAVIGATION
    // ================================================================

    private void HandleEscapePressed()
    {
        if (state == ChallengeState.Paused)
        {
            ResumeGame();
        }
        else if (state == ChallengeState.WaitingForQuest || state == ChallengeState.Running)
        {
            OpenPauseMenu();
        }
    }

    private void OpenPauseMenu()
    {
        stateBeforePause = state;
        state = ChallengeState.Paused;
        pausePanel.transform.SetAsLastSibling();
        pausePanel.SetActive(true);
        PrepareMenuInteraction(resumeButton, "PAUSE");
        Time.timeScale = 0f;
        Debug.Log("Pause menu opened.", this);
    }

    public void ResumeGame()
    {
        if (state != ChallengeState.Paused)
        {
            return;
        }

        pausePanel.SetActive(false);
        state = stateBeforePause;
        Time.timeScale = 1f;
        SetCarAudioMuted(false);
        SetCursorForMenu(false);
        Debug.Log("Gameplay resumed from the pause menu.", this);
    }

    private void ResumeGameFromButton()
    {
        Debug.Log("UI BUTTON CLICKED: PAUSE - RESUME", resumeButton);
        ResumeGame();
    }

    public void QuitGame()
    {
        Debug.Log("UI BUTTON CLICKED: PAUSE - QUIT", quitButton);
        Time.timeScale = 1f;
        Application.Quit();
    }

    private void SetCursorForMenu(bool menuIsOpen)
    {
        Cursor.visible = menuIsOpen;
        Cursor.lockState = menuIsOpen ? CursorLockMode.None : CursorLockMode.Locked;
    }

    // RestartChallenge restores normal game speed and reloads the current gameplay scene.
    public void RestartChallenge()
    {
        Debug.Log("UI BUTTON CLICKED: RESULT - RESTART", restartButton);
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameObject.scene.name);
    }

    // ReturnToMainMenu restores normal game speed and loads the configured main-menu scene.
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SetCarAudioMuted(true);
        SetCursorForMenu(true);
        Debug.Log("Returning to the main menu with cursor and time state restored.", this);
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void ReturnToMainMenuFromResult()
    {
        Debug.Log("UI BUTTON CLICKED: RESULT - RETURN TO MAIN MENU", mainMenuButton);
        ReturnToMainMenu();
    }

    private void ReturnToMainMenuFromPause()
    {
        Debug.Log("UI BUTTON CLICKED: PAUSE - RETURN TO MAIN MENU", pauseMainMenuButton);
        ReturnToMainMenu();
    }

    // A newly loaded gameplay scene receives a fresh EventSystem and no selection from an old panel.
    private void ResetUiInputForNewSceneEntry()
    {
        EventSystem activeEventSystem = EventSystem.current;

        if (activeEventSystem == null)
        {
            Debug.LogError(
                "Gameplay UI requires one active EventSystem in the scene.",
                this);
            return;
        }

        if (!activeEventSystem.enabled)
        {
            activeEventSystem.enabled = true;
        }

        gameplayUiInputModule = activeEventSystem.GetComponent<InputSystemUIInputModule>();
        if (gameplayUiInputModule == null)
        {
            Debug.LogError(
                $"EventSystem '{activeEventSystem.name}' requires an InputSystemUIInputModule.",
                activeEventSystem);
            return;
        }

        if (!gameplayUiInputModule.enabled)
        {
            gameplayUiInputModule.enabled = true;
        }

        activeEventSystem.SetSelectedGameObject(null);
        EnsureUiActionsEnabled("GAMEPLAY SCENE START");
        StartCoroutine(ConfirmUiInputAfterSceneLoad());
    }

    // The main-menu scene can release shared UI actions during the scene change.
    // Rechecking one frame later guarantees that gameplay owns enabled Point and Click actions.
    private IEnumerator ConfirmUiInputAfterSceneLoad()
    {
        yield return null;
        EnsureUiActionsEnabled("GAMEPLAY FIRST FRAME");
    }

    private void PrepareMenuInteraction(Button firstSelectedButton, string panelName)
    {
        SetCursorForMenu(true);
        EnsureUiActionsEnabled($"{panelName} PANEL OPEN");
        SetCarAudioMuted(true);

        if (EventSystem.current != null && firstSelectedButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstSelectedButton.gameObject);
        }
    }

    private void EnsureUiActionsEnabled(string context)
    {
        if (gameplayUiInputModule == null)
        {
            gameplayUiInputModule = EventSystem.current != null
                ? EventSystem.current.GetComponent<InputSystemUIInputModule>()
                : null;
        }

        if (gameplayUiInputModule == null || gameplayUiInputModule.actionsAsset == null)
        {
            Debug.LogError($"UI INPUT CHECK [{context}]: input module or action asset is missing.", this);
            return;
        }

        gameplayUiActionMap = gameplayUiInputModule.actionsAsset.FindActionMap("UI", false);
        if (gameplayUiActionMap == null)
        {
            Debug.LogError($"UI INPUT CHECK [{context}]: action map 'UI' was not found.", this);
            return;
        }

        gameplayUiActionMap.Enable();
        EnableUiAction(gameplayUiInputModule.point);
        EnableUiAction(gameplayUiInputModule.leftClick);

        Debug.Log(
            $"UI INPUT CHECK [{context}]: " +
            $"map={gameplayUiActionMap.enabled}, " +
            $"point={IsUiActionEnabled(gameplayUiInputModule.point)}, " +
            $"click={IsUiActionEnabled(gameplayUiInputModule.leftClick)}, " +
            $"cursorVisible={Cursor.visible}, cursorLock={Cursor.lockState}.",
            gameplayUiInputModule);
    }

    private static void EnableUiAction(InputActionReference actionReference)
    {
        if (actionReference != null && actionReference.action != null)
        {
            actionReference.action.Enable();
        }
    }

    private static bool IsUiActionEnabled(InputActionReference actionReference)
    {
        return actionReference != null &&
               actionReference.action != null &&
               actionReference.action.enabled;
    }

    private bool IsMenuPanelActive()
    {
        return state == ChallengeState.QuestPrompt ||
               state == ChallengeState.Paused ||
               state == ChallengeState.Won ||
               state == ChallengeState.Lost;
    }

    // The visible panel objects are the final authority for whether player-car audio is muted.
    // This also covers panels opened or closed by another scene action instead of this manager.
    private void SynchronizeCarAudioWithMenuPanels()
    {
        bool anyMenuPanelIsVisible =
            (questPanel != null && questPanel.activeInHierarchy) ||
            (pausePanel != null && pausePanel.activeInHierarchy) ||
            (resultPanel != null && resultPanel.activeInHierarchy);

        SetCarAudioMuted(anyMenuPanelIsVisible);
    }

    // This diagnostic proves whether a physical click reaches a button or another UI graphic.
    private void LogPointerRaycast()
    {
        EventSystem activeEventSystem = EventSystem.current;
        if (activeEventSystem == null || Mouse.current == null)
        {
            Debug.LogWarning("RAW UI CLICK: EventSystem or mouse is unavailable.", this);
            return;
        }

        PointerEventData pointerData = new PointerEventData(activeEventSystem)
        {
            position = Mouse.current.position.ReadValue()
        };
        List<RaycastResult> raycastResults = new List<RaycastResult>();
        activeEventSystem.RaycastAll(pointerData, raycastResults);
        string topObjectName = raycastResults.Count > 0
            ? raycastResults[0].gameObject.name
            : "NONE";

        Debug.Log(
            $"RAW UI CLICK: position={pointerData.position}, topRaycast='{topObjectName}', " +
            $"point={IsUiActionEnabled(gameplayUiInputModule?.point)}, " +
            $"click={IsUiActionEnabled(gameplayUiInputModule?.leftClick)}.",
            this);
    }

    private void ResolvePlayerCarAudio()
    {
        Rigidbody playerRigidbody = questReceiveTrigger != null
            ? questReceiveTrigger.PlayerCarRigidbody
            : null;
        playerCarController = playerRigidbody != null
            ? playerRigidbody.GetComponent<PrometeoCarController>()
            : null;

        if (playerCarController == null)
        {
            Debug.LogWarning("Car menu audio could not find PrometeoCarController on the assigned player Rigidbody.", this);
        }
    }

    // Only the assigned player's engine and tire sources are controlled; other scene audio is unchanged.
    // Menu silence is re-applied every frame so the car controller cannot restart an audible source.
    private void SetCarAudioMuted(bool shouldMute)
    {
        if (playerCarController == null)
        {
            return;
        }

        AudioSource engineSource = playerCarController.carEngineSound;
        AudioSource tireSource = playerCarController.tireScreechSound;

        if (shouldMute)
        {
            bool menuJustOpened = !carAudioMutedByMenu;
            if (menuJustOpened)
            {
                carUseSoundsBeforeMenu = playerCarController.useSounds;
                CaptureAudioSourceState(
                    engineSource,
                    out engineMuteBeforeMenu,
                    out engineVolumeBeforeMenu,
                    out engineWasPlayingBeforeMenu);
                CaptureAudioSourceState(
                    tireSource,
                    out tireMuteBeforeMenu,
                    out tireVolumeBeforeMenu,
                    out tireWasPlayingBeforeMenu);
                carAudioMutedByMenu = true;
            }

            playerCarController.useSounds = false;
            EnforceSilentAudioSource(engineSource);
            EnforceSilentAudioSource(tireSource);

            if (menuJustOpened)
            {
                Debug.Log("CAR MENU AUDIO: enforced silence enabled.", this);
                LogCarAudioSourceState("ENGINE", engineSource, true);
                LogCarAudioSourceState("TIRE", tireSource, true);
                LogEveryActiveAudioSource();
            }

            return;
        }

        if (!carAudioMutedByMenu)
        {
            return;
        }

        playerCarController.useSounds = carUseSoundsBeforeMenu;
        RestoreAudioSourceState(
            engineSource,
            engineMuteBeforeMenu,
            engineVolumeBeforeMenu,
            engineWasPlayingBeforeMenu);
        RestoreAudioSourceState(
            tireSource,
            tireMuteBeforeMenu,
            tireVolumeBeforeMenu,
            tireWasPlayingBeforeMenu);
        carAudioMutedByMenu = false;

        Debug.Log("CAR MENU AUDIO: original car-audio state restored.", this);
        LogCarAudioSourceState("ENGINE", engineSource, false);
        LogCarAudioSourceState("TIRE", tireSource, false);
    }

    private static void CaptureAudioSourceState(
        AudioSource source,
        out bool wasMuted,
        out float previousVolume,
        out bool wasPlaying)
    {
        wasMuted = source != null && source.mute;
        previousVolume = source != null ? source.volume : 0f;
        wasPlaying = source != null && source.isPlaying;
    }

    private static void EnforceSilentAudioSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.mute = true;
        source.volume = 0f;

        if (source.isPlaying)
        {
            source.Pause();
        }
    }

    private static void RestoreAudioSourceState(
        AudioSource source,
        bool wasMuted,
        float previousVolume,
        bool wasPlaying)
    {
        if (source == null)
        {
            return;
        }

        source.volume = previousVolume;
        source.mute = wasMuted;

        if (wasPlaying)
        {
            source.UnPause();

            if (!source.isPlaying)
            {
                source.Play();
            }
        }
    }

    private void LogCarAudioSourceState(
        string label,
        AudioSource source,
        bool shouldBeSilent)
    {
        if (source == null)
        {
            Debug.LogWarning($"CAR AUDIO SOURCE [{label}]: reference is missing.", this);
            return;
        }

        Debug.Log(
            $"CAR AUDIO SOURCE [{label}]: expectedSilent={shouldBeSilent}, " +
            $"name='{source.name}', mute={source.mute}, volume={source.volume}, " +
            $"playing={source.isPlaying}, enabled={source.enabled}.",
            source);
    }

    private void LogEveryActiveAudioSource()
    {
        AudioSource[] activeSources =
            Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);

        foreach (AudioSource source in activeSources)
        {
            bool isControlledCarSource =
                source == playerCarController.carEngineSound ||
                source == playerCarController.tireScreechSound;
            string clipName = source.clip != null ? source.clip.name : "NONE";

            Debug.Log(
                $"ACTIVE AUDIO SOURCE: name='{source.name}', clip='{clipName}', " +
                $"controlledCarSource={isControlledCarSource}, mute={source.mute}, " +
                $"volume={source.volume}, playing={source.isPlaying}.",
                source);
        }
    }

    // UpdateTimerText replaces {0} with the current time.
    // The optional urgent colour change can be disabled in the Inspector.
    private void UpdateTimerText()
    {
        timerText.text = ApplyTextFormat(timerTextFormat, "{0}", FormatTime(remainingTime));

        if (useUrgentTimerColor)
        {
            timerText.color = remainingTime <= urgentTimeSeconds
                ? urgentTimerColor
                : normalTimerColor;
        }
    }

    // UpdateObjectiveText shows how many complete houses still need to be destroyed.
    private void UpdateObjectiveText()
    {
        objectiveText.text = ApplyTextFormat(
            houseCounterTextFormat,
            "HOUSES REMAINING: {0} / {1}",
            remainingHouseCount,
            totalHouseCount);
    }

    // FormatTime converts raw seconds into a clear minutes-and-seconds value such as 03:00.
    private string FormatTime(float timeInSeconds)
    {
        int displayedSeconds = Mathf.CeilToInt(Mathf.Max(0f, timeInSeconds));
        int minutes = displayedSeconds / 60;
        int seconds = displayedSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    // ApplyTextFormat safely inserts live values into the wording chosen in the Inspector.
    // If the braces are invalid, the default wording is used and the Console identifies the problem.
    private string ApplyTextFormat(string customFormat, string fallbackFormat, params object[] values)
    {
        try
        {
            return string.Format(customFormat, values);
        }
        catch (System.FormatException)
        {
            Debug.LogWarning(
                $"Invalid HUD text format '{customFormat}'. Using '{fallbackFormat}' instead.",
                this);
            return string.Format(fallbackFormat, values);
        }
    }

    // ================================================================
    // SYSTEM 7: HOUSE WAYPOINT MARKERS
    // ================================================================

    // UpdateMarkers changes each house's world position into a Canvas position.
    // Off-screen or behind-camera markers are kept inside the screen edge to guide the player.
    private void UpdateMarkers()
    {
        foreach (HouseTarget target in houseTargets)
        {
            if (target.IsComplete || target.Marker == null)
            {
                continue;
            }

            PositionMarkerOnScreen(target.Marker, target.MarkerWorldPosition);
        }
    }

    // The quest marker reads the collider's live world-space bounds centre every frame.
    // EDIT HERE - questMarkerWorldOffset can deliberately move it away from the exact centre.
    private void UpdateQuestMarker()
    {
        if (questMarker == null ||
            questReceiveTrigger == null ||
            questReceiveTrigger.TriggerCollider == null ||
            !questReceiveTrigger.TriggerCollider.enabled)
        {
            return;
        }

        Vector3 questWorldPosition =
            questReceiveTrigger.TriggerCollider.bounds.center + questMarkerWorldOffset;
        PositionMarkerOnScreen(questMarker, questWorldPosition);
    }

    private void PositionMarkerOnScreen(RectTransform marker, Vector3 worldPosition)
    {
        // EDIT HERE - markerEdgePadding controls the marker distance from the display edge.
        // Viewport coordinates remain correct when the camera renders to a low-resolution texture.
        Vector3 viewportPosition = gameplayCamera.WorldToViewportPoint(worldPosition);
        Vector2 viewportPoint = new Vector2(viewportPosition.x, viewportPosition.y);
        Vector2 viewportCenter = new Vector2(0.5f, 0.5f);

        if (viewportPosition.z <= 0f)
        {
            Vector2 direction = viewportPoint - viewportCenter;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = Vector2.up;
            }

            viewportPoint = viewportCenter - direction.normalized;
        }

        Rect displayRect = markerContainer.rect;
        Vector2 localPoint = new Vector2(
            Mathf.LerpUnclamped(displayRect.xMin, displayRect.xMax, viewportPoint.x),
            Mathf.LerpUnclamped(displayRect.yMin, displayRect.yMax, viewportPoint.y));

        localPoint.x = Mathf.Clamp(
            localPoint.x,
            displayRect.xMin + markerEdgePadding,
            displayRect.xMax - markerEdgePadding);
        localPoint.y = Mathf.Clamp(
            localPoint.y,
            displayRect.yMin + markerEdgePadding,
            displayRect.yMax - markerEdgePadding);

        marker.anchoredPosition = localPoint;
    }

    // ================================================================
    // SYSTEM 8: EDITABLE HUD CONNECTION
    // ================================================================

    // ValidateHudReferences checks the permanent scene objects before the challenge starts.
    // A missing reference stops this manager and reports the problem instead of replacing the design.
    private bool ValidateHudReferences()
    {
        bool allReferencesAssigned =
            hudCanvas != null &&
            countdownPanelBackground != null &&
            challengeTitleText != null &&
            timerText != null &&
            houseCounterPanelBackground != null &&
            objectiveText != null &&
            resultPanel != null &&
            resultPanelBackground != null &&
            resultTitleText != null &&
            resultDetailsText != null &&
            restartButton != null &&
            mainMenuButton != null &&
            markerContainer != null &&
            markerTemplate != null &&
            questReceiveTrigger != null &&
            questMarker != null &&
            questMarkerTextComponent != null &&
            questPanel != null &&
            questPanelBackground != null &&
            questDetailsTextComponent != null &&
            acceptQuestButton != null &&
            rejectQuestButton != null &&
            pausePanel != null &&
            pausePanelBackground != null &&
            resumeButton != null &&
            pauseMainMenuButton != null &&
            quitButton != null;

        if (!allReferencesAssigned)
        {
            Debug.LogError(
                $"{nameof(DemolitionChallengeManager)} has a missing HUD Scene Reference. " +
                "Select Destructible Houses and assign every HUD field in the Inspector.",
                this);
        }

        return allReferencesAssigned;
    }

    // PrepareHudForPlay hides preview panels and connects every runtime button once.
    private void PrepareHudForPlay()
    {
        resultPanel.SetActive(false);
        questPanel.SetActive(false);
        pausePanel.SetActive(false);
        countdownPanelBackground.gameObject.SetActive(false);
        houseCounterPanelBackground.gameObject.SetActive(false);
        questMarker.gameObject.SetActive(true);
        questMarker.SetAsLastSibling();
        questMarkerTextComponent.enabled = true;
        questMarkerTextComponent.text = questMarkerText;
        questDetailsTextComponent.text = questDetailsText;
        SetCursorForMenu(false);

        hudCanvas.enabled = true;
        acceptQuestButton.interactable = true;
        rejectQuestButton.interactable = true;
        resumeButton.interactable = true;
        pauseMainMenuButton.interactable = true;
        quitButton.interactable = true;

        if (markerTemplate != null)
        {
            markerTemplate.gameObject.SetActive(false);
        }

        restartButton.onClick.RemoveListener(RestartChallenge);
        restartButton.onClick.AddListener(RestartChallenge);

        mainMenuButton.onClick.RemoveListener(ReturnToMainMenuFromResult);
        mainMenuButton.onClick.AddListener(ReturnToMainMenuFromResult);

        acceptQuestButton.onClick.RemoveListener(AcceptQuest);
        acceptQuestButton.onClick.AddListener(AcceptQuest);

        rejectQuestButton.onClick.RemoveListener(RejectQuest);
        rejectQuestButton.onClick.AddListener(RejectQuest);

        resumeButton.onClick.RemoveListener(ResumeGameFromButton);
        resumeButton.onClick.AddListener(ResumeGameFromButton);

        pauseMainMenuButton.onClick.RemoveListener(ReturnToMainMenuFromPause);
        pauseMainMenuButton.onClick.AddListener(ReturnToMainMenuFromPause);

        quitButton.onClick.RemoveListener(QuitGame);
        quitButton.onClick.AddListener(QuitGame);
    }

    // CreateMarker copies the editable marker template for one registered house.
    // Only the displayed number changes; the template's image, font, colour, and children are preserved.
    private RectTransform CreateMarker()//int targetNumber//)
    {
        GameObject markerCopy = Instantiate(
            markerTemplate.gameObject,
            markerContainer,
            false);
        markerCopy.name = $"House Target {houseTargets.Count + 1} Marker";
        
        markerCopy.SetActive(false);

        return markerCopy.GetComponent<RectTransform>();
    }

    // OnDestroy removes every break notification subscription when this manager is destroyed.
    // This prevents destroyed scene objects from continuing to call the old manager.
    private void OnDestroy()
    {
        foreach (KeyValuePair<BreakableSecond, HouseTarget> pair in targetByPart)
        {
            if (pair.Key != null)
            {
                pair.Key.Broken -= HandlePartBroken;
            }
        }
    }

    // OnValidate keeps Inspector values inside safe limits while the scene is being edited.
    private void OnValidate()
    {
        challengeDurationSeconds = Mathf.Clamp(challengeDurationSeconds, 1f, 300f);
        markerEdgePadding = Mathf.Max(0f, markerEdgePadding);
        urgentTimeSeconds = Mathf.Clamp(urgentTimeSeconds, 0f, challengeDurationSeconds);
    }
}

// DemolitionChallengeManager controls quest receiving, live waypoint markers, pause flow, demolition timing, and the final result from one scene-owned source.
