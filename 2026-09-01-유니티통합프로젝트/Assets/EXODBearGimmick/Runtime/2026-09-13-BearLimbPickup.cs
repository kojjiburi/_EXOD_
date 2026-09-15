using UnityEngine;

internal sealed class BearLimbPickup : MonoBehaviour
{
    private BearLimbGimmickManager manager;
    private string itemId;

    internal void Configure(BearLimbGimmickManager owner, string id)
    {
        manager = owner;
        itemId = id;
    }

    public void Pickup()
    {
        manager?.TryCollect(itemId);
    }
}
