using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

public class RoomSelectPropManager : MonoBehaviourPunCallbacks
{
    private static RoomSelectPropManager _instance;

    private void Awake()
    {
        if (_instance != null)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject); // どのシーンでも死なない
    }

    public override void OnJoinedRoom()
    {
        // 自分自身の参加時（2人目以降もここで確実に叩ける）
        SetMyNameIntoRoomSlot();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        // 念のためマスター側でも穴埋め（どちらかが成功すればOK）
        if (!PhotonNetwork.IsMasterClient) return;

        if (newPlayer.CustomProperties.TryGetValue("PlayerSlot", out object slotObj) && slotObj is int slot)
        {
            string roomName = PhotonNetwork.CurrentRoom.Name;
            string slotKey = $"{roomName}_Slot{slot}";
            PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
            {
                { slotKey, newPlayer.NickName }
            });
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        // 抜けたら空文字に戻す（ロビー表示を正しく保つ）
        if (!PhotonNetwork.IsMasterClient) return;

        if (otherPlayer.CustomProperties.TryGetValue("PlayerSlot", out object slotObj) && slotObj is int slot)
        {
            string roomName = PhotonNetwork.CurrentRoom.Name;
            string slotKey = $"{roomName}_Slot{slot}";
            PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
            {
                { slotKey, "" }
            });
        }
    }

    private void SetMyNameIntoRoomSlot()
    {
        string roomName = PhotonNetwork.CurrentRoom.Name;
        string playerName = PhotonNetwork.LocalPlayer.NickName;
        if (!PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerSlot", out object slotObj) || !(slotObj is int slot))
        {
            Debug.LogWarning("PlayerSlot が取れませんでした");
            return;
        }

        string slotKey = $"{roomName}_Slot{slot}";
        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
        {
            { slotKey, playerName }
        });

        Debug.Log($"[LobbyRoomPropWriter] OnJoinedRoom -> {slotKey} = {playerName} を書き込みました。");
    }
}