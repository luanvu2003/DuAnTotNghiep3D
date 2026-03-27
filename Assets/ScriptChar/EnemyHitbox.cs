using Unity.Netcode;
using UnityEngine;

public class EnemyHitbox : NetworkBehaviour
{
    public int damage = 10;

    // KHI QUÁI VẬT LÀ VẬT THỂ RẮN (KHÔNG TRIGGER), DÙNG HÀM NÀY:
    private void OnCollisionEnter(Collision collision)
    {
        // Chỉ Server mới xử lý trừ máu
        if (!IsServer) return;

        // Kiểm tra xem thứ đâm vào mình có phải Player không
        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerController player = collision.gameObject.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(damage);
                Debug.Log($"[SERVER] Đã cắn {collision.gameObject.name}. HP còn: {player.currentHP.Value}");
            }
        }
    }
}