using UnityEngine;
using TMPro;

public class NameInputWatcher : MonoBehaviour
{
    private RoomSelectManager roomSelectManager;
    private TMP_InputField input;
    private string roomName;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        roomSelectManager = FindFirstObjectByType<RoomSelectManager>();
        input = GetComponent<TMP_InputField>();
        input.onValueChanged.AddListener(OnNameChanged);

        // オブジェクト名から roomName を抽出（例: "RoomA_Name_Player1" → "RoomA"）
        string inputName = gameObject.name;
        roomName = inputName.Split('_')[0];
        if (string.IsNullOrEmpty(roomName))
        {
            Debug.LogWarning($"{gameObject.name} から roomName を取得できませんでした。");
        }
    }

    void OnNameChanged(string value)
    {
        if (!string.IsNullOrEmpty(roomName) && roomSelectManager != null)
        {
            roomSelectManager.RefreshRoomButtons(roomName);
        }
    }
    
}
