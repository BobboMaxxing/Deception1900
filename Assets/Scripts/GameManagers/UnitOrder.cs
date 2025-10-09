[System.Serializable]
public class UnitOrder
{
    public OrderType orderType;
    public string targetCountry;

    public UnitOrder(OrderType type, string country)
    {
        orderType = type;
        targetCountry = country;
    }
}

public enum OrderType
{
    None,
    Move,
    Support
}

