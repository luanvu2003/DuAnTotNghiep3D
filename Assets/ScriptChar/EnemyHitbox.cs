using Unity.Netcode;
using UnityEngine;

public class EnemyHitbox : NetworkBehaviour
{
    public int damage = 5;
    public float damageInterval = 1.0f;
    private float lastDamageTime = -1f; // Khởi tạo âm để cắn được ngay phát đầu

    private void OnTriggerStay(Collider other)
    {
        if (!IsServer) return;

        // CHỖ NÀY QUAN TRỌNG: Kiểm tra tag trước
        if (other.CompareTag("Player"))
        {
            // Dùng GetComponentInParent để an toàn hơn
            PlayerController player = other.GetComponentInParent<PlayerController>();
            
            if (player != null)
            {
                if (Time.time - lastDamageTime >= damageInterval)
                {
                    player.TakeDamage(damage);
                    lastDamageTime = Time.time;
                    Debug.Log($"[SERVER] Đang cắn {other.name}. HP: {player.currentHP.Value}");
                }
            }
        }
    }
}