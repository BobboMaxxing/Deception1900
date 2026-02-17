using Mirror;
using UnityEngine;

public class AutoLoadLatestSave : NetworkBehaviour
{
    [SerializeField] private bool autoLoadOnServerStart = true;

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!autoLoadOnServerStart) return;
        if (!SaveLoadFlag.ShouldLoadLatest) return;
        SaveLoadFlag.ShouldLoadLatest = false;

        var data = SaveSystem.LoadLatest();
        if (data == null) return;

        var mgr = MainUnitManager.Instance;
        if (mgr == null) return;

        mgr.LoadFromSaveServer(data);
    }
}

public static class SaveLoadFlag
{
    public static bool ShouldLoadLatest;
}