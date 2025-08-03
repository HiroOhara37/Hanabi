using UnityEngine;
using Photon.Pun;
using TMPro;
using System.Linq;

public class LogManager : MonoBehaviourPun
{

    [SerializeField] public TextMeshPro logText;

    [PunRPC]
    public void WriteLog(string newLog)
    {
        Debug.Log($"WriteLog called. text = {newLog}");
        string currentLog = logText.text;
        string[] splitLogs = currentLog.Split("\n");
        // 最新14件を取得（10件以上あれば末尾から9件、そうでなければ全部）
        string[] latestLogs = (splitLogs.Length >= 10)
            ? splitLogs.Skip(splitLogs.Length - 9).ToArray()
            : splitLogs;

        logText.text = newLog + "\n" + string.Join("\n", latestLogs);
    }

}
