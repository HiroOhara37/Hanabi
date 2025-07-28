using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ExitGames.Client.Photon;
using System.Collections;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
using System.Text.RegularExpressions;
using System.Linq;

public class RoomSelectManager : MonoBehaviourPunCallbacks
{
    private const int MaxPlayersPerRoom = 4;
    private Dictionary<string, RoomInfo> cachedRoomList = new Dictionary<string, RoomInfo>();

    void Start()
    {
        PhotonNetwork.NetworkingClient.LoadBalancingPeer.DisconnectTimeout = 300000;
        // Photonサーバーへ接続（PhotonServerSettingsに従う）
        PhotonNetwork.AutomaticallySyncScene = false;
        PhotonNetwork.ConnectUsingSettings();
        // ロビー接続完了までは入力をロック
        GameObject.Find("RoomA_Name_Player").GetComponent<TMP_InputField>().interactable = false;
        GameObject.Find("RoomB_Name_Player").GetComponent<TMP_InputField>().interactable = false;
        // 定期更新コルーチン開始
        StartCoroutine(AutoRefreshRoomInfo());
    }

    // 1秒ごとにUI再描画を試みる（RoomA / RoomB）
    private IEnumerator AutoRefreshRoomInfo()
    {
        while (true)
        {
            UpdateJoinButtonEnable("RoomA");
            UpdateJoinButtonEnable("RoomB");

            yield return new WaitForSeconds(1.0f);
        }
    }

    // Photonサーバーへの接続が完了して「Master Server」との接続が確立したときに呼ばれる
    public override void OnConnectedToMaster()
    {
        // 接続成功時にロビーに参加（ルーム一覧取得のため必須）
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        // ルームAとBの人数を更新
        UpdateJoinButtonEnable("RoomA");
        UpdateJoinButtonEnable("RoomB");
        // ロビー接続完了 → 入力を活性化
        GameObject.Find("RoomA_Name_Player").GetComponent<TMP_InputField>().interactable = true;
        GameObject.Find("RoomB_Name_Player").GetComponent<TMP_InputField>().interactable = true;
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        // 毎回最新のルーム情報で更新（ルームA/Bの存在チェックのため）
        cachedRoomList.Clear();
        foreach (RoomInfo room in roomList)
        {
            // 削除されたルームはリストに追加しない
            if (room.RemovedFromList) continue;
            cachedRoomList[room.Name] = room;
        }

        // ルームAとBの人数を更新
        UpdateJoinButtonEnable("RoomA");
        UpdateJoinButtonEnable("RoomB");
    }

    // 名前入力欄の変更通知（InputField.OnValueChangedから呼び出し）
    public void RefreshRoomButtons(string roomName)
    {
        UpdateJoinButtonEnable(roomName);
    }

    // 参加ボタン活性化とスロット表示を更新
    void UpdateJoinButtonEnable(string roomName)
    {
        // UI取得（名前欄・ボタン・プレイヤーリスト）
        TMP_InputField nameInput = GameObject.Find($"{roomName}_Name_Player")?.GetComponent<TMP_InputField>();
        Button joinButton = GameObject.Find($"{roomName}_Button_Player")?.GetComponent<Button>();
        TextMeshProUGUI listText = GameObject.Find($"{roomName}_PlayerList")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI countText = GameObject.Find($"{roomName}_CountText")?.GetComponent<TextMeshProUGUI>();
        if (nameInput == null || joinButton == null || listText == null || countText == null)
        {
            Debug.LogWarning($"UI要素が見つかりません: {roomName}");
            return;
        }

        Dictionary<int, string> usedSlots = new Dictionary<int, string>();
        if (cachedRoomList.TryGetValue(roomName, out RoomInfo info))
        {
            usedSlots = GetSlotDictionary(roomName, info.CustomProperties);
            // 人数表示
            countText.text = $"参加人数：{info.PlayerCount}/{info.MaxPlayers}";
        }
        else
        {
            countText.text = $"参加人数：0/{MaxPlayersPerRoom}";
        }

        // スロットリスト表示
        UpdatePlayerListText(usedSlots, listText);

        // ボタン活性条件：空きスロットあり && 入力欄に名前がある
        bool hasEmptySlot = usedSlots.Count < MaxPlayersPerRoom;
        bool hasName = !string.IsNullOrEmpty(nameInput.text.Trim());
        joinButton.interactable = hasEmptySlot && hasName;
    }

    // カスタムプロパティからスロット辞書（Slot番号 → 名前）を取得
    Dictionary<int, string> GetSlotDictionary(string roomName, PhotonHashtable props)
    {
        Dictionary<int, string> result = new Dictionary<int, string>();
        foreach (DictionaryEntry entry in props)
        {
            string key = entry.Key.ToString();
            string val = entry.Value?.ToString();
            Debug.Log($"[Debug]   key={key}, value={val}");
            if (key.StartsWith($"{roomName}_Slot") && !string.IsNullOrEmpty(val))
            {
                int index = int.Parse(Regex.Match(key, @"\d+").Value);
                result[index] = val;
            }
        }
        Debug.Log($"[Debug]   → result = {result}");
        return result;
    }

    // スロットリストを UI テキストに表示
    void UpdatePlayerListText(Dictionary<int, string> slots, TextMeshProUGUI listText)
    {
        // 表示内容を初期化
        string text = "参加ユーザー";

        // 使用中のスロット順にプレイヤー名を表示（Slot0〜順に並ぶ）
        foreach (var kvp in slots.OrderBy(pair => pair.Key))
        {
            string name = kvp.Value;
            if (!string.IsNullOrEmpty(name))
            {
                text += $"\n・{name}";
            }
        }

        listText.text = text;
    }

    // ルーム参加ボタンがクリックされたときの処理
    public void OnClickJoinRoomButton()
    {
        // ボタン名からルーム名を取得
        GameObject clickedButton = EventSystem.current.currentSelectedGameObject;
        string roomName = clickedButton.name.Split('_')[0];
        // 入力フィールドからプレイヤー名を取得
        TMP_InputField nameInput = GameObject.Find($"{roomName}_Name_Player")?.GetComponent<TMP_InputField>();
        if (nameInput == null || string.IsNullOrEmpty(nameInput.text.Trim()))
        {
            Debug.Assert(false, "プレイヤー名がnullまたは空文字です。");
        }
        ;
        string playerName = nameInput.text.Trim();
        PhotonNetwork.LocalPlayer.NickName = playerName;

        // 空いているスロット番号を探す（先頭から詰める）
        bool isExistingRoom = cachedRoomList.TryGetValue(roomName, out RoomInfo room);
        int assignedSlot = 0;
        if (isExistingRoom)
        {
            var slots = GetSlotDictionary(roomName, room.CustomProperties);
            // すべてのキーと値を出力
            foreach (var kvp in slots)
            {
                Debug.Log($"GetSlotDictionaryの返り値：Key = {kvp.Key}, Value = {kvp.Value}");
            }
            for (int i = 0; i < MaxPlayersPerRoom; i++)
            {
                // スロット[i]が空文字の場合、空きスロットとみなす
                if (!slots.ContainsKey(i))
                {
                    assignedSlot = i;
                    break;
                }
            }
        }
        Debug.Log($"空きスロット番号決定: {assignedSlot}");
        // 自プレイヤーのカスタムプロパティにスロット番号を保持
        PhotonNetwork.LocalPlayer.SetCustomProperties(new PhotonHashtable
        {
            { "PlayerSlot", assignedSlot }
        });

        // Room入室or作成
        if (isExistingRoom)
        {
            Debug.Log($"既存ルーム {roomName} に参加: Slot{assignedSlot}");
            PhotonNetwork.JoinRoom(roomName);
        }
        else
        {
            Debug.Log($"新規ルーム {roomName} を作成: Slot{assignedSlot}");
            // 各スロットキーを初期化（初回参加者が全Slotキーを作成）
            string slotKey = $"{roomName}_Slot{assignedSlot}";
            PhotonHashtable initProps = new PhotonHashtable();
            string[] lobbyVisibleKeys = new string[MaxPlayersPerRoom];
            for (int i = 0; i < MaxPlayersPerRoom; i++)
            {
                string key = $"{roomName}_Slot{i}";
                initProps[key] = "";
                lobbyVisibleKeys[i] = key;
            }
            // 現在参加者のスロットだけ名前を登録
            initProps[slotKey] = playerName;

            RoomOptions options = new RoomOptions
            {
                MaxPlayers = MaxPlayersPerRoom,
                CustomRoomProperties = initProps,
                CustomRoomPropertiesForLobby = lobbyVisibleKeys,
                BroadcastPropsChangeToAll = true
            };

            PhotonNetwork.JoinOrCreateRoom(roomName, options, TypedLobby.Default);
        }
    }

    // 入室成功時に自動で呼ばれる
    public override void OnJoinedRoom()
    {
        Debug.Log($"OnJoinedRoom called");
        string roomName = PhotonNetwork.CurrentRoom.Name;
        string playerName = PhotonNetwork.LocalPlayer.NickName;
        int slot = (int)PhotonNetwork.LocalPlayer.CustomProperties["PlayerSlot"];
        string slotKey = $"{roomName}_Slot{slot}";

        Debug.Log($"ルーム {roomName} に {playerName} (Slot{slot}) が参加しました。");

        // 入室後、スロット名を明示的にルームへ設定（ロビーのRoomInfoにも反映）
        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
        {
            { slotKey, playerName }
        });

        // シーン遷移
        PhotonNetwork.LoadLevel("Room");
    }
}
