using UnityEngine;

public class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance;

    [Header("Audio Source")]
    [SerializeField] private AudioSource audioSource;

    [Header("Unit Sounds")]
    [Tooltip("Plays when a land unit moves")]
    public AudioClip unitMoveLand;
    [Tooltip("Plays when a boat unit moves")]
    public AudioClip unitMoveBoat;
    [Tooltip("Plays when a plane unit moves")]
    public AudioClip unitMovePlane;

    [Header("Order Sounds")]
    [Tooltip("Plays when a valid move order is placed")]
    public AudioClip moveValid;
    [Tooltip("Plays when an illegal move is attempted")]
    public AudioClip moveIllegal;
    [Tooltip("Plays when a support order is placed")]
    public AudioClip supportQueued;

    [Header("Build Sounds")]
    [Tooltip("Plays when a unit is successfully built")]
    public AudioClip unitBuilt;
    [Tooltip("Plays when a build fails")]
    public AudioClip buildFailed;

    [Header("UI Sounds")]
    [Tooltip("Plays when any UI button is clicked")]
    public AudioClip buttonClick;
    [Tooltip("Plays when a country is selected during country selection phase")]
    public AudioClip countrySelected;
    [Tooltip("Plays when a choice is confirmed")]
    public AudioClip confirmSound;
    [Tooltip("Plays when a choice is cancelled")]
    public AudioClip cancelSound;

    [Header("Turn Sounds")]
    [Tooltip("Plays when turns are resolved")]
    public AudioClip turnResolved;
    [Tooltip("Plays when build phase starts")]
    public AudioClip buildPhaseStart;

    void Awake()
    {
        Instance = this;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    public static void Play(AudioClip clip)
    {
        if (Instance == null) return;
        if (clip == null) return;

        Instance.audioSource.PlayOneShot(clip);
    }

    public static void PlayMoveValid() => Play(Instance?.moveValid);
    public static void PlayMoveIllegal() => Play(Instance?.moveIllegal);
    public static void PlaySupportQueued() => Play(Instance?.supportQueued);
    public static void PlayUnitBuilt() => Play(Instance?.unitBuilt);
    public static void PlayBuildFailed() => Play(Instance?.buildFailed);
    public static void PlayButtonClick() => Play(Instance?.buttonClick);
    public static void PlayCountrySelected() => Play(Instance?.countrySelected);
    public static void PlayConfirm() => Play(Instance?.confirmSound);
    public static void PlayCancel() => Play(Instance?.cancelSound);
    public static void PlayTurnResolved() => Play(Instance?.turnResolved);
    public static void PlayBuildPhaseStart() => Play(Instance?.buildPhaseStart);

    [Header("Volume")]
    [Range(0f, 1f)] public float unitMoveVolume = 0.5f;

    public static void Play(AudioClip clip, float volumeScale)
    {
        if (Instance == null) return;
        if (clip == null) return;

        Instance.audioSource.PlayOneShot(clip, volumeScale);
    }

    public static void PlayUnitMove(UnitType unitType)
    {
        if (Instance == null) return;

        float vol = Instance.unitMoveVolume;
        switch (unitType)
        {
            case UnitType.Land: Play(Instance.unitMoveLand, vol); break;
            case UnitType.Boat: Play(Instance.unitMoveBoat, vol); break;
            case UnitType.Plane: Play(Instance.unitMovePlane, vol); break;
        }
    }
}
