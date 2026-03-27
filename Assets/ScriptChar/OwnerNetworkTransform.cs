using Unity.Netcode.Components;
using UnityEngine;

// Script này giúp trao quyền di chuyển cho chính người chơi (Owner)
[DisallowMultipleComponent]
public class OwnerNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative()
    {
        return false; // Tắt quyền tối cao của Server, cho phép Client tự di chuyển
    }
}