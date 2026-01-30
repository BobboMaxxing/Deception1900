using Mirror;
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
    public string targetCountry;
    public Vector3 targetPosition;
    public MainUnit supportedUnit;
}
