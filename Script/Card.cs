using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using System.Linq;
using UnityEngine.EventSystems;

public static class CardOwner
{
    // 0..3 は Seat(=プレイヤー) を表す
    public const int Deck = -1;
    public const int Discard = 100;
}

public class Card : MonoBehaviourPun, IPointerClickHandler
{
    private SpriteRenderer spriteRenderer;
    public Sprite frontSprite;
    public Sprite backSprite;
    private bool isSelected = false;
    public int cardNumber;
    public string cardColor;
    public int cardIndex;
    public int ownerId;  // 所有者：Seat番号（0..3）または山札、捨て札など
    public int indexInOwner; // 所有者内でのインデックス（0..3など）
    public List<GameObject> hintChips = new List<GameObject>();

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Initialize(string frontImageName, string cardNumber, string cardColor, int cardIndex)
    {
        frontSprite = Resources.Load<Sprite>($"image/Cards/{frontImageName}");
        backSprite = Resources.Load<Sprite>("image/Cards/裏面");
        this.cardNumber = int.Parse(cardNumber); // カードに書かれた数字 
        this.cardColor = cardColor; // カードの色
        this.cardIndex = cardIndex; // カードの通し番号
        ShowBack();  // 初期表示は裏面
    }

    public void SetOwnerId(int seat)
    {
        ownerId = seat;
        if (!IsMine)
        {
            ShowFront(); // 自分のカードでない場合は表面を表示
            SetSelected(false);
        }
        if (ownerId == CardOwner.Discard)
        {
            // ヒントチップを削除
            ClearHints();
        }
    }

    // 自分の座席のカードかどうかを判定
    public bool IsMine
    {
        get
        {
            if (!PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("GameSeat", out object seatObj))
                return false; // まだ決まっていない / 観戦など
            int mySeat = (int)seatObj;
            return mySeat >= 0 && mySeat == ownerId;
        }
    }

    // 左クリック判定
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            CardSelectManager.Instance?.Select(this);
        }
    }

    // 選択状態の設定
    // @return {bool} 自分のカードかどうか
    public bool SetSelected(bool selected)
    {
        Debug.Log($"SetSelected called. selected:{selected}");
        if (isSelected == selected) return IsMine; // 状態が変わらない場合は何もしない

        isSelected = selected;
        spriteRenderer.color = isSelected ? Color.gray : Color.white;

        return IsMine;
    }

    public (int cardIndex, int ownerId, int indexInOwner) GetCardInfo()
    {
        Debug.Log("GetCardInfo called");
        if (!isSelected) return (-1, -1, -1);
        SetSelected(false);
        return (cardIndex, ownerId, indexInOwner);
    }

    public (int ownerId, int indexInOwner, int cardNumber, string cardColor) GetHintInfo()
    {
        Debug.Log("GetHintInfo called");
        SetSelected(false);
        return (ownerId, indexInOwner, cardNumber, cardColor);
    }

    public void ClearHints()
    {
        foreach (var hint in hintChips)
        {
            if (hint != null) Destroy(hint);
        }
        hintChips.Clear();
    }

    public void ShowFront()
    {
        spriteRenderer.sprite = frontSprite;
    }

    public void ShowBack()
    {
        spriteRenderer.sprite = backSprite;
    }
}