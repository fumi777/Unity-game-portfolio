using UnityEngine;

public class PickUpItem1 : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 90f;

    public event System.Action Collected;

    private bool isCollected;

    private void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 接触したオブジェクトのタグでプレイヤーを判定する。
        if (isCollected || !other.CompareTag("Player"))
        {
            return;
        }

        isCollected = true;
        Collected?.Invoke();
        Destroy(gameObject);
    }
}
