using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class DrawerReturnedItemUi : MonoBehaviour, IPointerClickHandler
{
    private WorldItemPickup pickup;

    public void Configure(WorldItemPickup targetPickup)
    {
        pickup = targetPickup;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || pickup == null)
            return;

        if (pickup.TryPickup())
            Destroy(gameObject);
    }
}
