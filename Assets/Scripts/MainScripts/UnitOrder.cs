using UnityEngine;

public enum UnitOrderType
{
    None,
    Move,
    Support
}

public class PlayerUnitOrder
{
    public UnitOrderType orderType = UnitOrderType.None;
    public string targetRegionId;
    public Vector3 targetPosition;
    public MainUnit supportedUnit;
}