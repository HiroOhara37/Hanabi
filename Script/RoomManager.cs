using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
using System.Linq;
using System.Collections;
using TMPro;
using System.Collections.Generic;

public static class CardList
{
    public static List<GameObject> deck = new List<GameObject>();
    public static List<GameObject> discard = new List<GameObject>();
    public static List<GameObject>[] seats = new List<GameObject>[]
    {
        new List<GameObject>(),
        new List<GameObject>(),
        new List<GameObject>(),
        new List<GameObject>()
    };

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
    public const int MaxSeats = 4; // 最大プレイヤー数
    public static float[] seatAngles = { 0f, 180f, -90f, 90f };
    public static Dictionary<string, Vector3> worldPositions = new Dictionary<string, Vector3>
    {
        ["Blue"] = new Vector3(-15f, 15f, 0f),
        ["Green"] = new Vector3(0f, 15f, 0f),
        ["White"] = new Vector3(15f, 15f, 0f),
        ["Yellow"] = new Vector3(-15f, -15f, 0f),
        ["Red"] = new Vector3(15f, -15f, 0f),
        ["Discard"] = new Vector3(0f, -15f, 0f),
        ["NumberHint"] = new Vector3(-19f, -34f, -2f),
        ["ColorHint"] = new Vector3(-15f, -34f, -2f),
        ["Base"] = new Vector3(-18f, -40f, -1f),
        ["Offset"] = new Vector3(12f, 0f, 0f)
    };

    // ---- Room Property Keys ----
    private const string SEAT_ACTORS = "SEAT_ACTORS"; // CSV: "3,-1,-1,-1" (seatIndex -> actorNumber / -1 = empty)
    private const string SEAT_ACTIVE = "SEAT_ACTIVE"; // CSV: "1,1,0,0"   (seatIndex -> 1=有効席, 0=無効席)

    // ---- Player Property Keys ----
    //private const string GAME_SEAT = "GameSeat"; // 座席情報 int: 0..3 / -1 = 観戦者

    private void Start()
    {
        cardDistributeManager = FindAnyObjectByType<CardDistributeManager>();
    }

    // ゲーム開始処理の呼び出し
    public void OnClickStartButton()
    {
        Debug.Log("OnClickStartButton called");
        if (PhotonNetwork.IsMasterClient)
        {
            StartButtonProcess(); // 直呼び
        }
        else
        {
            photonView.RPC(nameof(StartButtonProcess), RpcTarget.MasterClient);
        }
    }

    // ゲーム開始処理(マスタークライアントのみ実行)
    [PunRPC]
    public void StartButtonProcess()
    {
        Debug.Log("StartButtonProcess called");
        // 入室順（PlayerListの順）でそのまま席を割り当て TODO* ランダムに
        var players = PhotonNetwork.PlayerList.ToList();
        int playerCount = Mathf.Min(players.Count, MaxSeats);
        int[] seatActors = Enumerable.Repeat(-1, MaxSeats).ToArray(); // 初期化: -1 = 空席
        int[] seatActive = Enumerable.Repeat(0, MaxSeats).ToArray(); // 初期化: 0 = 無効席

        for (int seat = 0; seat < playerCount; seat++)
        {
            var p = players[seat];
            seatActors[seat] = p.ActorNumber; // プレイヤーのIDを座席に割り当て
            seatActive[seat] = 1; // 有効席に設定
            Debug.Log($"Seat {seat} assigned to Player {p.NickName} (Actor: {p.ActorNumber})");
        }

        // 名前設定
        var playerNames = players.Select(p => p.NickName).ToArray();
        photonView.RPC(nameof(SetNameHolder), RpcTarget.AllBuffered, (object)playerNames);

        inStartButtonProceed = true;
        // ルームプロパティ更新 → OnRoomPropertiesUpdateが呼ばれる
        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
            {
                { SEAT_ACTORS, string.Join(",", seatActors) },
                { SEAT_ACTIVE, string.Join(",", seatActive) }
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
        for (int i = 1; i <= MaxSeats; i++)
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

    private void RotateCardPlace(float rotationAngle)
    {
        Debug.Log("RotateCardPlace called");
        // 中央のアイテムを回転させる処理
        // リストにする
        var cardPlaces = new[]
        {
                GameObject.Find("置き場_Red"),
                GameObject.Find("置き場_Blue"),
                GameObject.Find("置き場_Green"),
                GameObject.Find("置き場_Yellow"),
                GameObject.Find("置き場_White"),
                GameObject.Find("捨て札")
            };

        // 座席によって、各置き場の回転角度を設定
        Quaternion rotation = Quaternion.Euler(0f, 0f, rotationAngle);
        // 各置き場のpositionを取得
        foreach (var place in cardPlaces)
        {
            if (place == null)
            {
                Debug.LogWarning("Card place not found: " + place.name);
            }
            Vector3 pos = place.transform.position;
            Vector3 newPos = rotation * pos;
            place.transform.SetPositionAndRotation(newPos, rotation);
        }
    }


    // =========================
    // 途中参加・途中離脱
    // =========================
    // プレイヤーがルームを離れたときに呼ばれる
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"OnPlayerLeftRoom called: {otherPlayer.NickName}");
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
        int[] seatActors = ParseIntArray(SEAT_ACTORS, MaxSeats, -1);
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
    public override void OnJoinedRoom()
    {
        Debug.Log("OnJoinedRoom called");
        // 開始済み（席テーブルあり）なら空いている有効席へ自動割当を試みる
        if (HasSeatTable())
        {
            TryOccupySeatForSelfIfPossible();
        }
        ApplyMySeatState();
    }


    // =========================
    // 自分の席/観戦状態反映
    // =========================
    public static int GetActorSeat(int actorNumber)
    {
        int[] seatActors = ParseIntArray("SEAT_ACTORS", MaxSeats, -1);
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

    // 未割り当ての座席を調査し、新規参加者を割り当てる
    private void TryOccupySeatForSelfIfPossible()
    {
        Debug.Log("TryOccupySeatForSelfIfPossible called");
        int myActor = PhotonNetwork.LocalPlayer.ActorNumber;
        int mySeat = GetActorSeat(myActor);
        // 既に席が割当済みなら不要
        if (mySeat >= 0) return;

        // 空いている有効席を探す
        int[] seatActors = ParseIntArray(SEAT_ACTORS, MaxSeats, -1);
        int[] seatActive = ParseIntArray(SEAT_ACTIVE, MaxSeats, 0);
        for (int i = 0; i < MaxSeats; i++)
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

    // 「自分がプレイヤー席か観戦者か」を判定して、ローカル側の見た目／操作可否を切り替える
    private void ApplyMySeatState()
    {
        Debug.Log("ApplyMySeatState called");
        // ゲームが開始されていない状態
        if (!HasSeatTable())
        {
            Debug.Log("ApplyMySeatState: No SeatTable");
            return;
        }

        // 観戦者として入室した場合=ゲーム開始後の入室
        int mySeat = GetActorSeat(PhotonNetwork.LocalPlayer.ActorNumber);
        if (mySeat < 0)
        {
            Debug.Log("ApplyMySeatState: Spectator mode");
            // 観戦者
            return;
        }

        float cameraAngle = seatAngles[Mathf.Clamp(mySeat, 0, seatAngles.Length - 1)];
        RotateMyCamera(cameraAngle);
        RotateCardPlace(cameraAngle);
    }

    private void RotateMyCamera(float zAngle)
    {
        Debug.Log($"RotateMyCamera called: zAngle={zAngle}");
        var cam = Camera.main;
        if (cam == null) return;
        var e = cam.transform.eulerAngles;
        e.z = zAngle;
        cam.transform.eulerAngles = e;
    }

    // Room.CustomProperties に文字列（例: "3,-1,-1,-1"）として入れてある配列風データを、
    // int[] に変換して返す。指定長に満たない場合は defaultValue で埋める。
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