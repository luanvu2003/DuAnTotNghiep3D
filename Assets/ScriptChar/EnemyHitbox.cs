using Unity.Netcode;
using UnityEngine;

public class EnemyHitbox : NetworkBehaviour
{
    public int damage = 5;              // Sát thương mỗi lần "đớp"
    public float damageInterval = 1.0f; // Khoảng thời gian giữa mỗi lần mất máu (1 giây)
    private float lastDamageTime;

    // Hàm này chạy liên tục mỗi khi Player còn đứng TRONG vùng Trigger
    private void OnTriggerStay(Collider other)
    {
        // Chỉ Server mới có quyền trừ máu
        if (!IsServer) return;

        // Kiểm tra Cooldown để không bị trừ máu quá nhanh (60 lần/giây là chết luôn đấy!)
        if (Time.time - lastDamageTime < damageInterval) return;

        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(damage);
                lastDamageTime = Time.time; // Lưu lại thời điểm vừa gây sát thương
                
                Debug.Log($"[SERVER] Player đang đứng trong vùng nguy hiểm! HP còn: {player.currentHP.Value}");
            }
        }
    }

    // Vẫn nên giữ OnTriggerEnter để vừa chạm vào vùng là mất máu ngay phát đầu
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (other.CompareTag("Player") && Time.time - lastDamageTime >= damageInterval)
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(damage);
                lastDamageTime = Time.time;
            }
        }
    }
}