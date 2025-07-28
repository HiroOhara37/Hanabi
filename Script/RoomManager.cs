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
    private const string GAME_SEAT = "GameSeat"; // 座席情報 int: 0..3 / -1 = 観戦者

    private void Start()
    {
        cardDistributeManager = FindAnyObjectByType<CardDistributeManager>();
    }

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

    [PunRPC]
    public void StartButtonProcess()
    {
        Debug.Log("StartButtonProcess called");
        // 入室順（PlayerListの順）でそのまま席を割り当て
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
    private bool HasSeatTable()
    {
        if (PhotonNetwork.CurrentRoom == null) return false;
        var props = PhotonNetwork.CurrentRoom.CustomProperties;
        return props.ContainsKey(SEAT_ACTORS) && props.ContainsKey(SEAT_ACTIVE);
    }

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

    // プレイヤーがルームを離れたときに呼ばれる
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"OnPlayerLeftRoom called: {otherPlayer.NickName}");
        // RoomSelectManagerのスロット情報を開放
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

        int[] seatActors = ParseIntArray(SEAT_ACTORS, MaxSeats, -1);
        bool changed = false;
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

    public override void OnRoomPropertiesUpdate(PhotonHashtable changedProp)
    {
        Debug.Log("OnRoomPropertiesUpdate called");
        // 席テーブルが変わったら、自分の GameSeat を seatActors から決め直す
        if (changedProp.ContainsKey(SEAT_ACTORS) || changedProp.ContainsKey(SEAT_ACTIVE))
        {
            int[] seatActors = ParseIntArray(SEAT_ACTORS, MaxSeats, -1);
            int myActor = PhotonNetwork.LocalPlayer.ActorNumber;

            // 1) まず seatActors から自分の席を逆引き
            int seat = -1;
            for (int i = 0; i < seatActors.Length; i++)
            {
                if (seatActors[i] == myActor)
                {
                    seat = i;
                    break;
                }
            }

            // 2) 見つかったらそれを使う。見つからないなら空いてる有効席を探す（= TryOccupySeatForSelfIfPossible の簡易版）
            if (seat >= 0)
            {
                SetMyGameSeat(seat);
            }
            else
            {
                TryOccupySeatForSelfIfPossible(); // 空き有効席があればここで座る
            }

            //ApplyMySeatState();
            //if (inStartButtonProceed)
            //{
            //    cardManager.CalledOnClickStartButton();
            //    inStartButtonProceed = false;
            //}
        }
    }

    public override void OnPlayerPropertiesUpdate(Player target, PhotonHashtable changedProps)
    {
        if (!target.IsLocal) return;  // 変更されたプレイヤープロパティが自分でなければスキップ
        if (changedProps.ContainsKey(GAME_SEAT))
        {
            Debug.Log("[OnPlayerPropertiesUpdate] my GAME_SEAT changed -> ApplyMySeatState");
            ApplyMySeatState();

            // 生成処理をここで実行する
            if (inStartButtonProceed)
            {
                inStartButtonProceed = false;
                cardDistributeManager.CalledOnClickStartButton();
            }
        }
    }

    // =========================
    // 自分の席/観戦状態反映
    // =========================
    private void SetMyGameSeat(int seat)
    {
        Debug.Log($"SetMyGameSeat called: seat={seat}");
        PhotonNetwork.LocalPlayer.SetCustomProperties(new PhotonHashtable
            {
                { GAME_SEAT, seat }
            });
    }

    public static int GetMySeat()
    {
        int myActor = PhotonNetwork.LocalPlayer.ActorNumber;
        int[] seatActors = ParseIntArray("SEAT_ACTORS", MaxSeats, -1);
        int seat = -1;
        for (int i = 0; i < seatActors.Length; i++)
        {
            if (seatActors[i] == myActor)
            {
                seat = i;
                break;
            }
        }

        return seat;
    }

    private void TryOccupySeatForSelfIfPossible()
    {
        Debug.Log("TryOccupySeatForSelfIfPossible called");
        if (!HasSeatTable()) return;

        // 既に席が割当済みなら不要
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(GAME_SEAT, out object seatObj) &&
            seatObj is int seatIndex && seatIndex >= 0)
        {
            return;
        }

        int[] seatActors = ParseIntArray(SEAT_ACTORS, MaxSeats, -1);
        int[] seatActive = ParseIntArray(SEAT_ACTIVE, MaxSeats, 0);

        int myActor = PhotonNetwork.LocalPlayer.ActorNumber;
        // 1) すでに seatActors に自分が入っていれば、それを採用
        for (int i = 0; i < MaxSeats; i++)
        {
            if (seatActors[i] == myActor)
            {
                SetMyGameSeat(i);
                return;
            }
        }

        // 2) 入っていなければ、空いている有効席を探す
        for (int i = 0; i < MaxSeats; i++)
        {
            if (seatActive[i] == 1 && seatActors[i] == -1)
            {
                seatActors[i] = myActor;
                PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
                    {
                        { SEAT_ACTORS, string.Join(",", seatActors) }
                    });
                SetMyGameSeat(i);
                return;
            }
        }

        // 3) どこにも入れなければ観戦
        SetMyGameSeat(-1);
    }

    // 「自分がプレイヤー席か観戦者か」を判定して、ローカル側の見た目／操作可否を切り替える
    private void ApplyMySeatState()
    {
        Debug.Log("ApplyMySeatState called");
        // ゲームが開始されていない状態
        if (!HasSeatTable())
        {
            Debug.Log("ApplyMySeatState: No SeatTable");
            EnablePlayerInput(false); // 操作を受け付けない
            return;
        }

        // 自分のGameSeatがまだ書き込まれていない
        if (!PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(GAME_SEAT, out object seatObj))
        {
            Debug.Log("ApplyMySeatState: No GameSeat property");
            SetMyGameSeat(-1);
            EnablePlayerInput(false);
            return;
        }

        int mySeat = (int)seatObj;
        // 観戦者として入室した場合=ゲーム開始後の入室
        if (mySeat < 0)
        {
            Debug.Log("ApplyMySeatState: Spectator mode");
            // 観戦者
            EnablePlayerInput(false);
            return;
        }

        float cameraAngle = seatAngles[Mathf.Clamp(mySeat, 0, seatAngles.Length - 1)];
        RotateMyCamera(cameraAngle);
        RotateCardPlace(cameraAngle);
        EnablePlayerInput(true); // 操作を受け付ける
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

    private void EnablePlayerInput(bool enable)
    {
        // TODO: 観戦者用の入力/UI無効化をここでまとめて制御
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