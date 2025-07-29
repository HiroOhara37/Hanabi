using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

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
        frontImageNames = frontImageNames.OrderBy(x => Random.Range(0, 10000)).ToArray();
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
        var room = PhotonNetwork.CurrentRoom;
        int[] seatActive = ParseCsvInt(room.CustomProperties["SEAT_ACTIVE"] as string, 4, 0);
        var activeSeatIdx = Enumerable.Range(0, seatActive.Length).Where(i => seatActive[i] == 1).ToList();
        int cardsPerPlayer = (activeSeatIdx.Count == 4) ? 4 : 5;  // 2, 3人は5枚、4人は4枚

        // 各プレイヤー（座席）にカードを配る
        foreach (int seat in activeSeatIdx)
        {
            // プレイヤーのカードを山札から取得
            List<GameObject> playerCards = CardList.deck.GetRange(0, cardsPerPlayer); // 先頭からN枚取り出す
            CardList.deck.RemoveRange(0, cardsPerPlayer); // 取り出したカードをデッキから削除

            // 各カードをプレイヤーの手札に配置
            Vector3 basePosition = new Vector3(-18f, -40f, -1f);  // 配置の基準位置
            Vector3 offset = new Vector3(12f, 0f, 0f); // 2枚目以降、どれだけずらすか
            float rotationAngle = RoomManager.seatAngles[seat]; // プレイヤーごとの回転角度を取得
            Quaternion rotation = Quaternion.Euler(0f, 0f, rotationAngle);

            //Transform parent = GameObject.Find($"手札置き場_Player{seat + 1}")?.transform;
            for (int j = 0; j < playerCards.Count; j++)
            {
                var gameObject = playerCards[j];
                var card = gameObject.GetComponent<Card>();
                card.SetOwnerId(seat); // カードの所有者座席を設定
                card.indexInOwner = j; // 所有者内でのインデックスを設定
                CardList.seats[seat].Add(gameObject);
                //gameObject.transform.SetParent(parent, true); // 親オブジェクトを設定

                // 原点(0,0,0)中心に回転させた位置を計算
                Vector3 localPosition = basePosition + offset * j;
                Vector3 worldPosition = rotation * localPosition;
                gameObject.transform.SetPositionAndRotation(worldPosition, rotation);
            }
        }
    }

    int[] ParseCsvInt(string csv, int expected, int defVal)
    {
        if (string.IsNullOrEmpty(csv)) return Enumerable.Repeat(defVal, expected).ToArray();
        var parts = csv.Split(',');
        var r = new int[expected];
        for (int i = 0; i < expected; i++)
            r[i] = (i < parts.Length && int.TryParse(parts[i], out var v)) ? v : defVal;
        return r;
    }
}
