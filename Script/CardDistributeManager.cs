using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System;
using TMPro;

public class CardDistributeManager : MonoBehaviourPunCallbacks
{
    public static CardDistributeManager Instance { get; private set; }
    [SerializeField] private Transform cardParent;  // カードを並べる親（空オブジェクトなど）

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    public void CalledOnClickStartButton()
    {
        Debug.Log("CalledOnClickStartButton called");
        // カード画像の読み込み（裏面を除く）
        Sprite[] allSprites = Resources.LoadAll<Sprite>("image/Cards");
        string[] frontImageNames = allSprites
            .Where(sprite => sprite.name != "裏面")
            .Select(sprite => sprite.name)
            .ToArray();

        // シャッフル
        frontImageNames = frontImageNames.OrderBy(x => UnityEngine.Random.Range(0, 10000)).ToArray();
        photonView.RPC("DeleteCards", RpcTarget.AllBuffered);
        photonView.RPC("GenerateCards", RpcTarget.AllBuffered, frontImageNames);
        photonView.RPC("FirstSetupCards", RpcTarget.AllBuffered);
    }

    // カードの削除:PhotonNetworkで生成したカードを削除する
    [PunRPC]
    void DeleteCards()
    {
        Debug.Log("DeleteCards called");
        // 既存のカードを削除
        CardList.Clear();
        foreach (Transform child in cardParent)
        {
            Destroy(child.gameObject);
        }
    }

    // カード生成:Instantiateで生成するため、１人が実行で全員に同期される
    [PunRPC]
    void GenerateCards(string[] shuffledImageNames)
    {
        Debug.Log("GenerateCards called");
        SetModeCustom();
        for (int i = 0; i < shuffledImageNames.Length; i++)
        {
            // 画像名からカード情報を取得
            string[] parts = shuffledImageNames[i].Split('_');
            string cardColor = parts[0];
            string cardNumber = parts[1][0].ToString();

            // カード生成
            Vector3 pos = new Vector3(0, 0, -0.02f * i + 2f);  // 今後変更の可能性あり
            GameObject cardPrefab = Resources.Load<GameObject>("Prefab/Card");
            GameObject card = Instantiate(cardPrefab, pos, Quaternion.identity);
            card.name = $"Card_{shuffledImageNames[i]}";
            CardList.deck.Add(card);
            // カードの初期化
            card.GetComponent<Card>().Initialize(shuffledImageNames[i], cardNumber, cardColor, i);
        }
    }

    // カードの配置
    [PunRPC]
    void FirstSetupCards()
    {
        Debug.Log("FirstSetupCards called");
        SetHandleAreas();
        //var room = PhotonNetwork.CurrentRoom;
        int[] seatActive = RoomManager.ParseIntArray(RoomManager.SEAT_ACTIVE, RoomManager.MaxSeats, -1);
        List<int> activeSeatIdx = Enumerable.Range(0, seatActive.Length).Where(i => seatActive[i] == 1).ToList();
        int cardsPerPlayer = (activeSeatIdx.Count == 4) ? 4 : 5;  // 2, 3人は5枚、4人は4枚
        Debug.Assert(GameManager.basePositions.Count == activeSeatIdx.Count, $"ポジション数:{GameManager.basePositions.Count}とアクティブ座席数:{activeSeatIdx.Count}が不一致です。");

        // 各プレイヤー（座席）にカードを配る
        foreach (int seat in activeSeatIdx)
        {
            // プレイヤーのカードを山札から取得
            List<GameObject> playerCards = CardList.deck.GetRange(0, cardsPerPlayer); // 先頭からN枚取り出す
            CardList.deck.RemoveRange(0, cardsPerPlayer); // 取り出したカードをデッキから削除

            // 各カードをプレイヤーの手札に配置
            Vector3 basePosition = GameManager.basePositions[seat];
            //Vector3 basePosition = new Vector3(-18f, -40f, -1f);  // 配置の基準位置
            Vector3 offset = RoomManager.worldPositions["Offset"]; // 2枚目以降、どれだけずらすか
            //float rotationAngle = RoomManager.seatAngles[seat]; // プレイヤーごとの回転角度を取得
            //Quaternion rotation = Quaternion.Euler(0f, 0f, rotationAngle);
            for (int j = 0; j < playerCards.Count; j++)
            {
                var gameObject = playerCards[j];
                var card = gameObject.GetComponent<Card>();
                card.SetOwnerId(seat); // カードの所有者座席を設定
                card.indexInOwner = j; // 所有者内でのインデックスを設定
                CardList.seats[seat].Add(gameObject);
                //gameObject.transform.SetParent(parent, true); // 親オブジェクトを設定

                // 原点(0,0,0)中心に回転させた位置を計算
                gameObject.transform.position = basePosition + offset * j;
                //Vector3 localPosition = basePosition + offset * j;
                //Vector3 worldPosition = rotation * localPosition;
                //gameObject.transform.SetPositionAndRotation(worldPosition, rotation);
            }
        }
    }

    private void SetHandleAreas()
    {
        Debug.Log("SetHandleAreas called");
        List<Vector3> positions = new List<Vector3>();

        int[] seatActors = RoomManager.ParseIntArray(RoomManager.SEAT_ACTORS, RoomManager.MaxSeats, -1);
        int otherIndex = 1;
        int mySeat = Math.Max(RoomManager.GetActorSeat(PhotonNetwork.LocalPlayer.ActorNumber), 0); // 観戦者ならプレイヤー1の配置を参照

        for (int i = 0; i < seatActors.Length; i++)
        {
            Player player = PhotonNetwork.PlayerList.FirstOrDefault(p => p.ActorNumber == seatActors[i]);
            // 座席のプレイヤー = 自分ならば、myselfをセット
            if (i == mySeat)
            {
                TextMeshPro nameText = GameObject.Find($"Myself_NameHolder").GetComponent<TextMeshPro>();
                nameText.text = $"Player{i + 1}：{player.NickName}";
                //positions.Add(RoomManager.worldPositions["Myself"]);
                GameManager.basePositions[i] = RoomManager.worldPositions["Myself"];
            }
            else
            {
                TextMeshPro nameText = GameObject.Find($"Other{otherIndex}_NameHolder").GetComponent<TextMeshPro>();
                //参加者がいない
                if (player == null)
                {
                    nameText.text = $"Player{i + 1}：なし";
                }
                else
                {
                    nameText.text = $"Player{i + 1}：{player.NickName}";
                    //positions.Add(RoomManager.worldPositions[$"Other{otherIndex}"]);
                    GameManager.basePositions[i] = RoomManager.worldPositions[$"Other{otherIndex}"];
                }
                otherIndex++;
            }
        }
    }

    private void SetModeCustom()
    {
        if (PhotonNetwork.CurrentRoom == null ||
            !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomManager.MODE, out object obj) ||
            obj == null)
        {
            return;
        }
        string modeName = obj.ToString();
        if (modeName == "Normal")
        {
            GameObject.Find("置き場_Rainbow").SetActive(false);
            GameObject.Find("置き場_Black").SetActive(false);
        }
        else if (modeName == "Rainbow")
        {
            GameObject.Find("置き場_Black").SetActive(false);
        }
        else if (modeName == "Black")
        {
            GameObject.Find("置き場_Rainbow").SetActive(false);
        }
        else
        {
            
        }

    }
}
