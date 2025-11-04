using UnityEngine;

public enum ObjectCategory
{
    Furniture,
    Decor,
    KitchenEssentials,
    Electronics,
    Books,
    Plants,
    Building 
}

public class ObjectMetadata : MonoBehaviour
{
    public ObjectCategory category;
}
