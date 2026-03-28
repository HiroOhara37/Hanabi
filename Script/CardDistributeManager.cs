using static Config;
using static Property;
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
        InitProp();
    }

    // カード生成:Instantiateで生成するため、１人が実行で全員に同期される
    [PunRPC]
    void GenerateCards(string[] shuffledImageNames)
    {
        Debug.Log("GenerateCards called");
        shuffledImageNames = SetModeCustom(shuffledImageNames);
        for (int i = 0; i < shuffledImageNames.Length; i++)
        {
            // 画像名からカード情報を取得
            string[] parts = shuffledImageNames[i].Split('_');
            string cardColor = parts[0];
            string cardNumber = parts[1][0].ToString();

            // カード生成
            Vector3 pos = worldPositions["Deck"] + new Vector3(0, 0, -0.02f * i - 1f);
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
        int[] seatActive = RoomManager.ParseIntArray(SEAT_ACTIVE, MAX_SEATS, -1);
        Debug.Log($"[FirstSetupCards] 取得したSEAT_ACTIVE配列: {string.Join(",", seatActive)}");
        List<int> activeSeatIdx = Enumerable.Range(0, seatActive.Length).Where(i => seatActive[i] == 1).ToList();
        Debug.Log($"[FirstSetupCards] アクティブシート一覧: {string.Join(",", activeSeatIdx)}");
        int cardsPerPlayer = (activeSeatIdx.Count >= 4) ? 4 : 5;  // プレイヤー数：2or3人は5枚、4or5人は4枚
        Debug.Log($"[FirstSetupCards] アクティブプレイヤー数: {activeSeatIdx.Count}, 条件判定: activeSeatIdx.Count({activeSeatIdx.Count}) >= 4 ? 4 枚 : 5 枚, 実際の配付: {cardsPerPlayer} 枚");
        Debug.Assert(GameManager.basePositions.Count == activeSeatIdx.Count, $"ポジション数:{GameManager.basePositions.Count}とアクティブ座席数:{activeSeatIdx.Count}が不一致です。");

        // 各プレイヤー（座席）にカードを配る
        foreach (int seat in activeSeatIdx)
        {
            // プレイヤーのカードを山札から取得
            List<GameObject> playerCards = CardList.deck.GetRange(0, cardsPerPlayer); // 先頭からN枚取り出す
            CardList.deck.RemoveRange(0, cardsPerPlayer); // 取り出したカードをデッキから削除

            // 各カードをプレイヤーの手札に配置
            Vector3 basePosition = GameManager.basePositions[seat];
            Vector3 offset = worldPositions["Offset"]; // 2枚目以降、どれだけずらすか
            for (int j = 0; j < playerCards.Count; j++)
            {
                var gameObject = playerCards[j];
                var card = gameObject.GetComponent<Card>();
                card.SetOwnerId(seat); // カードの所有者座席を設定
                card.indexInOwner = j; // 所有者内でのインデックスを設定
                CardList.seats[seat].Add(gameObject);

                gameObject.transform.position = basePosition + offset * j;  // 初期手札移動はアニメーションにしない
            }
        }
    }

    private void SetHandleAreas()
    {
        Debug.Log("SetHandleAreas called");
        List<Vector3> positions = new List<Vector3>();

        int[] seatActors = RoomManager.ParseIntArray(SEAT_ACTORS, MAX_SEATS, -1);
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
                GameManager.basePositions[i] = worldPositions["Myself"];
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
                    GameManager.basePositions[i] = worldPositions[$"Other{otherIndex}"];
                }
                otherIndex++;
            }
        }
    }

    private string[] SetModeCustom(string[] shuffledImageNames)
    {
        if (PhotonNetwork.CurrentRoom == null ||
            !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(MODE, out object obj) ||
            obj == null)
        {
            Debug.Assert(false, "予期せぬエラー：SetModeCustom");
            return new string[1];
        }
        string modeName = obj.ToString();
        if (modeName == "Normal")
        {
            RAINBOW_AREA.SetActive(false);
            BLACK_AREA.SetActive(false);
            // カードからRainbowとBlackを除く
            shuffledImageNames = shuffledImageNames
                .Where(name => !name.Contains("Rainbow") && !name.Contains("Black"))
                .ToArray();
        }
        else if (modeName == "Rainbow")
        {
            BLACK_AREA.SetActive(false);
            // カードからBlackを除く
            shuffledImageNames = shuffledImageNames
                .Where(name => !name.Contains("Black"))
                .ToArray();
        }
        else if (modeName == "Black")
        {
            RAINBOW_AREA.SetActive(false);
            // カードからRainbowを除く
            shuffledImageNames = shuffledImageNames
                .Where(name => !name.Contains("Rainbow"))
                .ToArray();
        }
        else
        {
        }

        return shuffledImageNames;
    }
}
