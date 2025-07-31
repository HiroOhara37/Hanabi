using UnityEngine;
using Photon.Pun;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System;
using TMPro;
using DG.Tweening;

public class GameManager : MonoBehaviourPun
{
    // 場に出されたカードの状態を保存
    private Dictionary<string, int> state;
    private int discardCount; // 捨て札の枚数
    public TextMeshProUGUI hintCountText;
    public TextMeshProUGUI errorCountText;

    public static int hintCount; // ヒントの残数
    private int errorCount; // 失敗数
    public float moveSpeed = 0.5f;
    // 色
    private Dictionary<string, Color> myColors = new Dictionary<string, Color>()
    {
        {"Red", Color.red},
        {"Green", Color.green},
        {"White", Color.white},
        {"Blue", Color.blue},
        {"Yellow", Color.yellow}
    };
    // 手札のポジション
    public static Dictionary<int, Vector3> basePositions = new Dictionary<int, Vector3>();

    void Start()
    {
        Init();
    }

    void Update()
    {
        hintCountText.text = $"ヒント数：{hintCount}";
        errorCountText.text = $"エラー：{errorCount}";
    }

    void Init()
    {
        state = new Dictionary<string, int>()
        {
            {"Red", 0},
            {"Green", 0},
            {"White", 0},
            {"Blue", 0},
            {"Yellow", 0},
            {"Rainbow", 0},
            {"Black", 6}
        };
        discardCount = 0;
        hintCount = 8;
        errorCount = 0;
    }

    public void OnClickPlayButton()
    {
        Debug.Log("OnClickPlayButton called");
        var (cardIndex, ownerId, indexInOwner) = CardSelectManager.Instance?.CalledPlayOrDiscard() ?? (-1, -1, -1);
        Debug.Assert(cardIndex != -1, "選択対象がnullでした。");
        photonView.RPC("PlayRPC", RpcTarget.AllBuffered, cardIndex, ownerId, indexInOwner);
    }

    [PunRPC]
    public void PlayRPC(int cardIndex, int ownerId, int indexInOwner)
    {
        Debug.Log($"PlayRPC called. cardIndex:{cardIndex}, ownerId:{ownerId}, indexInOwner:{indexInOwner}");
        GameObject cardObject = CardList.seats[ownerId][indexInOwner];
        Card card = cardObject.GetComponent<Card>();
        bool isCorrected = CheckPlayAnswer(card);
        if (isCorrected)
        {
            // カードを該当のPlaceに移動
            StartCoroutine(PlayOrDiscardSequence(cardIndex, ownerId, indexInOwner, card.cardColor));
        }
        else
        {
            // カードを捨て札にして、エラーカウントを追加
            StartCoroutine(PlayOrDiscardSequence(cardIndex, ownerId, indexInOwner));
            errorCount++;
        }
    }

    public void OnClickDiscardButton()
    {
        Debug.Log("OnClickDiscardButton called");
        var (cardIndex, ownerId, indexInOwner) = CardSelectManager.Instance?.CalledPlayOrDiscard() ?? (-1, -1, -1);
        Debug.Assert(cardIndex != -1, "選択対象がnullでした。");
        photonView.RPC("DiscardRPC", RpcTarget.AllBuffered, cardIndex, ownerId, indexInOwner);
    }

    // 全員の画面でカードを捨て札に
    [PunRPC]
    public void DiscardRPC(int cardIndex, int ownerId, int indexInOwner)
    {
        Debug.Log($"DiscardRPC called cardIndex:{cardIndex}, ownerId:{ownerId}, indexInOwner:{indexInOwner}");
        StartCoroutine(PlayOrDiscardSequence(cardIndex, ownerId, indexInOwner));
        // ヒント数の追加
        hintCount = Math.Min(hintCount + 1, 8);
    }

    // 時間差演出のため、サブルーチンで捨て札処理を実行
    private System.Collections.IEnumerator PlayOrDiscardSequence(int cardIndex, int ownerId, int indexInOwner, string color = "")
    {
        Debug.Log($"PlayOrDiscardSequence called cardIndex:{cardIndex}, ownerId:{ownerId}, indexInOwner:{indexInOwner}, color:{color}");
        GameObject cardObject = CardList.seats[ownerId][indexInOwner]; // 選択カード
        CardList.seats[ownerId].RemoveAt(indexInOwner); // 選択カードを手札リストから削除
        Card card = cardObject.GetComponent<Card>();
        card.ShowFront();

        yield return new WaitForSeconds(0.8f);

        // 選択カードの配置を変更する
        Debug.Assert(card.ownerId >= 0 && card.ownerId <= 3, $"捨て札押下時の選択カードの所有者は0～3のはずですが、{card.ownerId}になっています。");
        Vector3 changedPosition;
        if (color == "")
        {
            //捨て札の位置を取得
            changedPosition = RoomManager.worldPositions["Discard"] + RoomManager.worldPositions["DiscardOffset"] * discardCount;
            discardCount++;
        }
        else
        {
            // 正解の色のPlaceの位置を取得
            state[color] += 1;
            int offsetNum = state[color];
            changedPosition = RoomManager.worldPositions[color] + new Vector3(0f, 0f, -1f - 0.01f * offsetNum);
        }

        cardObject.transform.DOMove(changedPosition, moveSpeed);

        // 選択カードのステータスを更新
        card.SetOwnerId(CardOwner.Discard); // これで所有者にも表が向く
        card.indexInOwner = discardCount; // 捨て札の最新indexにする

        yield return new WaitForSeconds(0.8f);

        // 山札があるならドロー
        if (CardList.deck.Count > 0)
        {
            SetAddCard(ownerId);
            yield return new WaitForSeconds(0.8f);
        }
        // カードとヒントの位置を調整
        SetCardsNewPositon(CardList.seats[ownerId], ownerId);

        // 完了SEを実行
        AudioPlayer.Instance.PlaySE(AudioPlayer.Instance.finishPlayng);
    }

    public void SetAddCard(int ownerId)
    {
        Debug.Log($"SetAddCard called, ownedId:{ownerId}");
        // 山札のトップを手札に追加し、山札のトップカードをRemove
        var cardObject = CardList.deck[0];
        CardList.seats[ownerId].Add(cardObject);
        CardList.deck.RemoveAt(0);
        Card card = cardObject.GetComponent<Card>();

        card.SetOwnerId(ownerId);
        card.indexInOwner = CardList.seats[ownerId].Count;

        Vector3 basePosition = basePositions[ownerId];
        Vector3 offset = RoomManager.worldPositions["Offset"];
        Vector3 addPosition = basePosition + offset * card.indexInOwner;

        cardObject.transform.DOMove(addPosition, moveSpeed);
    }

    public void OnClickNumberHintButton()
    {
        Debug.Log("OnClickNumberHintButton called");
        var (ownerId, indexInOwner, cardNumber, cardColor) = CardSelectManager.Instance?.CalledHint() ?? (-1, -1, -1, "");
        Debug.Assert(cardColor != "", "Error OnClickNumberHintButton");

        List<bool> hintTarget = new List<bool>();
        foreach (GameObject cardObj in CardList.seats[ownerId])
        {
            Card card = cardObj.GetComponent<Card>();
            int ithCardNum = card.cardNumber;
            // 選択したカードの数字と同じならtrue、異なるならfalseを入れる
            hintTarget.Add(ithCardNum == cardNumber);
        }

        photonView.RPC("SetHintChip", RpcTarget.AllBuffered, hintTarget.ToArray(), ownerId, "White", cardNumber.ToString());
    }

    public void OnClickColorHintButton()
    {
        Debug.Log("OnClickColorHintButton called");
        var (ownerId, indexInOwner, cardNumber, cardColor) = CardSelectManager.Instance?.CalledHint() ?? (-1, -1, -1, "");
        Debug.Assert(cardColor != "", "Error OnClickColorHintButton");

        List<bool> hintTarget = new List<bool>();
        foreach (GameObject cardObj in CardList.seats[ownerId])
        {
            Card card = cardObj.GetComponent<Card>();
            string ithCardColor = card.cardColor;
            // 選択したカードの色と同じかRainbowならtrue、異なるならfalseを入れる
            if (ithCardColor == cardColor || ithCardColor == "Rainbow")
            {
                hintTarget.Add(true);
            }
            else
            {
                hintTarget.Add(false);
            }
        }
        photonView.RPC("SetHintChip", RpcTarget.AllBuffered, hintTarget.ToArray(), ownerId, cardColor, "");
    }

    [PunRPC]
    public void SetHintChip(bool[] hintTarget, int ownerId, string color, string number)
    {
        Debug.Log($"SetHintChip called. ownerId:{ownerId}, color:{color}, number:{number}");
        hintCount -= 1;
        // ヒントチップの設定
        GameObject hintChipPrefab = Resources.Load<GameObject>("Prefab/HintChip");
        Vector3 basePosition;
        Vector3 offset = RoomManager.worldPositions["Offset"];
        if (number == "")
        {
            basePosition = basePositions[ownerId] + RoomManager.worldPositions["ColorHint"];
        }
        else
        {
            basePosition = basePositions[ownerId] + RoomManager.worldPositions["NumberHint"];
        }

        // ヒントチップを配置
        for (int i = 0; i < hintTarget.Length; i++)
        {
            if (!hintTarget[i]) continue;
            Vector3 setPosition = basePosition + offset * i;

            GameObject newHintChip = Instantiate(hintChipPrefab);
            // SpriteRendererのカラー設定
            var spriteRenderer = newHintChip.GetComponent<SpriteRenderer>();
            spriteRenderer.color = myColors[color];
            // 子オブジェクトのTextMeshProを取得してテキストを設定
            var textMesh = newHintChip.GetComponentInChildren<TextMeshPro>();
            textMesh.text = number;
            // 位置を設定
            newHintChip.transform.position = setPosition;

            // カードクラスに持たせる
            GameObject cardObj = CardList.seats[ownerId][i];
            Card card = cardObj.GetComponent<Card>();
            card.hintChips.Add(newHintChip);
        }

        // 完了SEを実行
        AudioPlayer.Instance.PlaySE(AudioPlayer.Instance.finishPlayng);
    }

    public bool CheckPlayAnswer(Card card)
    {
        Debug.Log("CheckPlayAnswer called");
        int cardNumber = card.cardNumber;
        string cardColor = card.cardColor;
        int correctNumber = state[cardColor];

        bool isCorrected = false;
        if (cardColor != "Black")
        {
            // 黒色以外は昇順で5がクリア
            isCorrected = cardNumber == correctNumber + 1;
            if (isCorrected && cardNumber == 5)
            {
                // cardNumberが5だったら、ヒントカードを1つ増やす
                hintCount = Math.Min(hintCount + 1, 8);
            }
        }
        else
        {
            // 黒色は降順で1がクリア
            isCorrected = cardNumber == correctNumber - 1;
            if (isCorrected && cardNumber == 1)
            {
                // cardNumberが1だったら、ヒントカードを1つ増やす
                hintCount = Math.Min(hintCount + 1, 8);
            }
        }

        return isCorrected;
    }

    public void SetCardsNewPositon(List<GameObject> cardList, int ownerId)
    {
        Debug.Log($"SetCardsNewPositon called. ownerId:{ownerId}");
        Vector3 offset = RoomManager.worldPositions["Offset"];

        for (int i = 0; i < cardList.Count; i++)
        {
            var cardObject = cardList[i];
            Card card = cardObject.GetComponent<Card>();
            int beforeIndexInOwner = card.indexInOwner;
            // 差分を求める
            int diff = i - card.indexInOwner;
            Debug.Assert(diff <= 0, $"diffが{diff}で異常値です。");
            // 差分だけoffsetを移動する
            Vector3 currentPos = cardObject.transform.position;
            Vector3 thisCardOffset = offset * diff;
            cardObject.transform.DOMove(currentPos + thisCardOffset, moveSpeed);
            card.indexInOwner = i;

            // ヒントも移動
            foreach (GameObject hintObj in card.hintChips)
            {
                Vector3 currentHintPos = hintObj.transform.position;
                hintObj.transform.DOMove(currentHintPos + thisCardOffset, moveSpeed);
            }
        }
    }

}
