using Unity.Netcode;
using UnityEngine;

public class SyncPushable : NetworkBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        // Nếu là Player chạm vào Cube
        if (collision.gameObject.CompareTag("Player"))
        {
            // Lấy ID của người vừa chạm
            var networkObject = collision.gameObject.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsOwner)
            {
                // Yêu cầu Server chuyển quyền sở hữu Cube cho người này
                RequestOwnershipServerRpc(networkObject.OwnerClientId);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestOwnershipServerRpc(ulong newOwnerId)
    {
        // Server thực hiện chuyển quyền
        GetComponent<NetworkObject>().ChangeOwnership(newOwnerId);
    }
}