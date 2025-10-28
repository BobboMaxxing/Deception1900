public enum OrderType
{
    Move,
    Support
}

public class UnitOrder
{
    public OrderType orderType;
    public string targetCountry;
    public Unit supportedUnit; 

    public UnitOrder(OrderType type, string country)
    {
        orderType = type;
        targetCountry = country;
        supportedUnit = null;
    }

   
    public UnitOrder(OrderType type, Unit targetUnit)
    {
        orderType = type;
        supportedUnit = targetUnit;
        if (targetUnit != null)
            targetCountry = targetUnit.currentCountry;
        else
            targetCountry = "";
    }
}
