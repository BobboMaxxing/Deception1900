using UnityEngine;

public enum OrderType
{
    None,
    Move,
    Support
}

[System.Serializable]
public class UnitOrder
{
    public OrderType type = OrderType.None;
    public string targetCountry; // for Move
    public Unit targetUnit;      // for Support

    public UnitOrder(OrderType type, string targetCountry = null, Unit targetUnit = null)
    {
        this.type = type;
        this.targetCountry = targetCountry;
        this.targetUnit = targetUnit;
    }
}
