using UnityEngine;
using UnityEngine.UI;

public class CardSelectManager : MonoBehaviour
{
    public static CardSelectManager Instance { get; private set; }

    private Card current;
    public Card Current => current;
    private Button playButton;
    private Button discardButton;
    private Button numberHintButton;
    private Button colorHintButton;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        playButton = GameObject.Find("PlayButton").GetComponent<Button>();
        discardButton = GameObject.Find("DiscardButton").GetComponent<Button>();
        numberHintButton = GameObject.Find("NumberHintButton").GetComponent<Button>();
        colorHintButton = GameObject.Find("ColorHintButton").GetComponent<Button>();

        playButton.interactable = false;
        discardButton.interactable = false;
        numberHintButton.interactable = false;
        colorHintButton.interactable = false;
    }

    /// <summary>このカードを選択（以前の選択は解除）</summary>
    public void Select(Card card)
    {
        if (current == card) return;

        if (current != null) current.SetSelected(false);
        current = card;
        if (current != null)
        {
            bool isMine = current.SetSelected(true);
            if (isMine)
            {
                // 場に出す、捨てるボタンを活性、ヒントボタンを非活性
                playButton.interactable = true;
                discardButton.interactable = true;
                numberHintButton.interactable = false;
                colorHintButton.interactable = false;
            }
            else
            {
                // 場に出す、捨てるボタンを非活性、ヒントボタンを活性
                playButton.interactable = false;
                discardButton.interactable = false;
                if (GameManager.hintCount > 0)
                {
                    numberHintButton.interactable = true;
                    colorHintButton.interactable = true;
                }
                else
                {
                    numberHintButton.interactable = false;
                    colorHintButton.interactable = false;
                }
            }
        }
    }

    /// <summary>選択解除</summary>
    public void Clear()
    {
        if (current != null)
        {
            current.SetSelected(false);
            current = null;

            playButton.interactable = false;
            discardButton.interactable = false;
            numberHintButton.interactable = false;
            colorHintButton.interactable = false;
        }
    }

    // カードを捨てる処理
    public (int cardIndex, int ownerId, int indexInOwner) CalledPlayOrDiscard()
    {
        if (current != null)
        {
            var (cardIndex, ownerId, indexInOwner) = current.GetCardInfo();
            // 削除できていたら、該当カードの選択状態を解除
            Debug.Assert(cardIndex != -1, "削除対象のカードは選択状態ではありませんでした。");
            if (cardIndex != -1)
            {
                Clear();
            }

            return (cardIndex, ownerId, indexInOwner);
        }
        return (-1, -1, -1);
    }

    // ヒント用処理
    public (int ownerId, int indexInOwner, int cardNumber, string cardColor) CalledHint()
    {
        return current.GetHintInfo();
    }
}
