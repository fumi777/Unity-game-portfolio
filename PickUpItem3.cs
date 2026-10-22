using UnityEngine;

public class PickUpItem3 : MonoBehaviour
{
    public event System.Action Collected;

    private bool isCollected;

    private void OnTriggerEnter(Collider other)
    {
        if (isCollected || !other.CompareTag("Player"))
        {
            return;
        }

        isCollected = true;
        Collected?.Invoke();
        Destroy(gameObject);
    }
}
