using static Config;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
using System.Linq;
using System.Collections;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public static class CardList
{
    public static List<GameObject> deck;
    public static List<GameObject> discard;
    public static List<GameObject>[] seats;

    static CardList()
    {
        deck = new List<GameObject>();
        discard = new List<GameObject>();
        seats = new List<GameObject>[MAX_SEATS];
        for (int i = 0; i < MAX_SEATS; i++)
        {
            seats[i] = new List<GameObject>();
        }
    }
    public static void Clear()
    {
        foreach (var d in deck)
        {
            Object.Destroy(d);
        }
        deck.Clear();
        foreach (var d in discard)
        {
            Object.Destroy(d);
        }
        discard.Clear();
        foreach (List<GameObject> seat in seats)
        {
            foreach (var s in seat)
            {
                Object.Destroy(s);
            }
            seat.Clear();
        }
    }
}

public class RoomManager : MonoBehaviourPunCallbacks
{
    private CardDistributeManager cardDistributeManager;

    private bool inStartButtonProceed = false;
    public static Dictionary<string, Vector3> worldPositions = new Dictionary<string, Vector3>();
    public GameObject modePanel;
    public bool isModePanelOpen = false;
    [SerializeField] public GameObject yourTurn;  // Inspectorで指定

    private void Start()
    {
        cardDistributeManager = FindAnyObjectByType<CardDistributeManager>();
        // 置き場
        worldPositions["Blue"] = GameObject.Find("置き場_Blue").transform.position;
        worldPositions["Green"] = GameObject.Find("置き場_Green").transform.position;
        worldPositions["White"] = GameObject.Find("置き場_White").transform.position;
        worldPositions["Yellow"] = GameObject.Find("置き場_Yellow").transform.position;
        worldPositions["Red"] = GameObject.Find("置き場_Red").transform.position;
        worldPositions["Rainbow"] = GameObject.Find("置き場_Rainbow").transform.position;
        worldPositions["Black"] = GameObject.Find("置き場_Black").transform.position;
        worldPositions["Discard"] = GameObject.Find("捨て札").transform.position + new Vector3(0f, 0f, -1f);
        worldPositions["Deck"] = GameObject.Find("山札").transform.position + new Vector3(0f, 0f, -1f);
        // 手札
        worldPositions["Myself"] = GameObject.Find("HandArea_Myself").transform.position + new Vector3(-25f, 0f, -1f);
        worldPositions["Other1"] = GameObject.Find("HandArea_Other_1").transform.position + new Vector3(-25f, 0f, -1f);
        worldPositions["Other2"] = GameObject.Find("HandArea_Other_2").transform.position + new Vector3(-25f, 0f, -1f);
        worldPositions["Other3"] = GameObject.Find("HandArea_Other_3").transform.position + new Vector3(-25f, 0f, -1f);
        worldPositions["Other4"] = GameObject.Find("HandArea_Other_4").transform.position + new Vector3(-25f, 0f, -1f);
        worldPositions["NumberHint"] = new Vector3(-1f, 6f, -1f); // カードの位置に対するヒントの差分位置
        worldPositions["ColorHint"] = new Vector3(3f, 6f, -1f); // カードの位置に対するヒントの差分位置
        worldPositions["Offset"] = new Vector3(12f, 0f, 0f); // カード1枚のoffset
        worldPositions["DiscardOffset"] = new Vector3(3f, 0f, -0.01f);

        modePanel = GameObject.Find("ModePanel");
        modePanel.SetActive(false);
        isModePanelOpen = false;
        FindAnyObjectByType<LogManager>().photonView.RPC("WriteLog", RpcTarget.AllBuffered, $"プレイヤー {PhotonNetwork.LocalPlayer.NickName} が入室しました。");
    }

    // 開始ボタン押下 -> モード選択パネル起動(またはクローズ)
    public void OnClickStartButton()
    {
        Debug.Log("OnClickStartButton called");
        if (isModePanelOpen)
        {
            modePanel.SetActive(false);
            isModePanelOpen = false;
        }
        else
        {
            modePanel.SetActive(true);
            isModePanelOpen = true;
        }
    }

    // ゲーム開始処理スタート
    public void OnClickModeButton()
    {
        modePanel.SetActive(false);
        isModePanelOpen = false;

        // 押したボタンを取得
        GameObject clickedButton = EventSystem.current.currentSelectedGameObject;
        string modeName = clickedButton.name.Split("_")[1];
        Debug.Log($"modeName:{modeName}");
        if (PhotonNetwork.IsMasterClient)
        {
            StartButtonProcess(modeName); // 直呼び
        }
        else
        {
            photonView.RPC(nameof(StartButtonProcess), RpcTarget.MasterClient, modeName);
        }
    }

    // ゲーム開始処理(マスタークライアントのみ実行)
    [PunRPC]
    public void StartButtonProcess(string modeName)
    {
        Debug.Log($"StartButtonProcess called. modeName:{modeName}");
        // プレイヤー順を決定
        var players = PhotonNetwork.PlayerList.OrderBy(_ => UnityEngine.Random.Range(0, 10000)).ToList();
        int playerCount = Mathf.Min(players.Count, MAX_SEATS);
        int[] seatActors = Enumerable.Repeat(-1, MAX_SEATS).ToArray(); // 初期化: -1 = 空席
        int[] seatActive = Enumerable.Repeat(0, MAX_SEATS).ToArray(); // 初期化: 0 = 無効席

        for (int seat = 0; seat < playerCount; seat++)
        {
            var p = players[seat];
            seatActors[seat] = p.ActorNumber;  // プレイヤーのIDを座席に割り当て
            seatActive[seat] = 1;  // 有効席に設定
            Debug.Log($"Seat {seat} assigned to Player {p.NickName} (Actor: {p.ActorNumber})");
        }

        inStartButtonProceed = true;
        // ルームプロパティ更新 → OnRoomPropertiesUpdateが呼ばれる
        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
            {
                { SEAT_ACTORS, string.Join(",", seatActors) },
                { SEAT_ACTIVE, string.Join(",", seatActive) },
                { MODE, modeName },
                { TURN_SEAT, seatActors[0]}
            });
    }

    public override void OnRoomPropertiesUpdate(PhotonHashtable changedProp)
    {
        Debug.Log("OnRoomPropertiesUpdate called");
        // 開始処理でのプロパティ変更ならカード生成処理
        if (inStartButtonProceed) // マスタークライアントしかtrueになりえない
        {
            inStartButtonProceed = false;
            CardDistributeManager.Instance.CalledOnClickStartButton();
        }

        // TurnSeatの更新で呼ばれた場合の処理
        // 自分のターンならYourTurnパネルを表示
        if (IsTurnSeat(PhotonNetwork.LocalPlayer.ActorNumber))
        {
            yourTurn.SetActive(true);
        }
        else
        {
            yourTurn.SetActive(false);
        }
    }

    // ルームプロパティが設定済みかどうか=ゲームが開始済みか
    private bool HasSeatTable()
    {
        if (PhotonNetwork.CurrentRoom == null) return false;
        var props = PhotonNetwork.CurrentRoom.CustomProperties;
        return props.ContainsKey(SEAT_ACTORS) && props.ContainsKey(SEAT_ACTIVE);
    }

    [PunRPC]
    private void SetNameHolder(string[] playerNames)
    {
        for (int i = 1; i <= MAX_SEATS; i++)
        {
            TextMeshPro nameText = GameObject.Find($"Player{i}_NameHolder").GetComponent<TextMeshPro>();
            if (i <= playerNames.Length)
            {
                nameText.text = $"Player{i}：{playerNames[i - 1]}";
            }
            else
            {
                nameText.text = $"Player{i}：なし";
            }
        }
    }


    // =========================
    // 途中参加・途中離脱
    // =========================

    // プレイヤーがルームを離れたときに呼ばれる
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"OnPlayerLeftRoom called: {otherPlayer.NickName}");
        if (PhotonNetwork.IsMasterClient) FindAnyObjectByType<LogManager>().photonView.RPC("WriteLog", RpcTarget.All, $"プレイヤー {otherPlayer.NickName} が退出しました。");

        // RoomSelectManager側のプロパティ:Slot情報を開放
        if (otherPlayer.CustomProperties.TryGetValue("PlayerSlot", out object slotObj))
        {
            string slot = slotObj.ToString();
            string roomName = PhotonNetwork.CurrentRoom.Name;
            string key = $"{roomName}_Slot{slot}";
            Debug.Log($"プレイヤー {otherPlayer.NickName} が退出。Slot{slot} を開放します。");

            // スロット情報を開放
            PhotonHashtable updatedProps = new PhotonHashtable
                {
                    { key, "" }
                };
            PhotonNetwork.CurrentRoom.SetCustomProperties(updatedProps);
        }

        // ゲーム開始後（席テーブルがある）ならば、座席状態を更新する
        if (!HasSeatTable()) return;

        bool changed = false;
        int[] seatActors = ParseIntArray(SEAT_ACTORS, MAX_SEATS, -1);
        // 退出したプレイヤーの座席を空席にする(観戦者なら何もしない)
        for (int i = 0; i < seatActors.Length; i++)
        {
            if (seatActors[i] == otherPlayer.ActorNumber)
            {
                seatActors[i] = -1; // 空席に変更（activeは維持）
                changed = true;
                break;
            }
        }
        if (changed)
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
                {
                    { SEAT_ACTORS, string.Join(",", seatActors) }
                });
        }
    }

    // プレイヤーがルームに参加した時
    /*public override void OnJoinedRoom()
    {
        Debug.Log("OnJoinedRoom called");
        FindAnyObjectByType<LogManager>().photonView.RPC("WriteLog", RpcTarget.All, $"プレイヤー {PhotonNetwork.LocalPlayer.NickName} が入室しました。");
        // 開始済み（席テーブルあり）なら空いている有効席へ自動割当を試みる
        if (HasSeatTable())
        {
            TryOccupySeatForSelfIfPossible();
        }
    }*/

    // =========================
    // 自分の席/観戦状態反映
    // =========================
    public static int GetActorSeat(int actorNumber)
    {
        int[] seatActors = ParseIntArray(SEAT_ACTORS, MAX_SEATS, -1);
        int seat = -1;
        for (int i = 0; i < seatActors.Length; i++)
        {
            if (seatActors[i] == actorNumber)
            {
                seat = i;
                break;
            }
        }

        return seat;
    }

    // actorNumberがターンの座席に座っているか
    public static bool IsTurnSeat(int actorNumber)
    {
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(TURN_SEAT, out object obj))
        {
            int turnSeat = (int)obj;
            int mySeat = GetActorSeat(actorNumber);
            return turnSeat == mySeat;
        }
        else
        {
            return false;
        }
    }

    // 未割り当ての座席を調査し、新規参加者を割り当てる
    private void TryOccupySeatForSelfIfPossible()
    {
        Debug.Log("TryOccupySeatForSelfIfPossible called");
        int myActor = PhotonNetwork.LocalPlayer.ActorNumber;
        int mySeat = GetActorSeat(myActor);
        // 既に席が割当済みなら不要
        if (mySeat >= 0) return;

        // 空いている有効席を探す
        int[] seatActors = ParseIntArray(SEAT_ACTORS, MAX_SEATS, -1);
        int[] seatActive = ParseIntArray(SEAT_ACTIVE, MAX_SEATS, 0);
        for (int i = 0; i < MAX_SEATS; i++)
        {
            if (seatActive[i] == 1 && seatActors[i] == -1)
            {
                seatActors[i] = myActor;
                PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
                    {
                        { SEAT_ACTORS, string.Join(",", seatActors) }
                    });
                return;
            }
        }
        // なければ何もしない
    }

    // Room.CustomProperties に文字列（例: "3,-1,-1,-1"）として入れてある配列風データをint[] に変換して返す。指定長に満たない場合は defaultValue で埋める。
    public static int[] ParseIntArray(string key, int expectedLength, int defaultValue)
    {
        Debug.Log($"ParseIntArray called: key={key}, expectedLength={expectedLength}, defaultValue={defaultValue}");
        if (PhotonNetwork.CurrentRoom == null ||
            !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object obj) ||
            obj == null)
        {
            return Enumerable.Repeat(defaultValue, expectedLength).ToArray();
        }

        var parts = obj.ToString().Split(',');
        var result = new int[expectedLength];
        for (int i = 0; i < expectedLength; i++)
        {
            if (i < parts.Length && int.TryParse(parts[i], out int v))
                result[i] = v;
            else
                result[i] = defaultValue;
        }
        Debug.Log($"ParseIntArray result: {result}");
        return result;
    }
}