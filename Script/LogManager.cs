using UnityEngine;
using Photon.Pun;
using TMPro;
using System.Linq;

public class LogManager : MonoBehaviourPun
{

    public TextMeshPro logText;

    [PunRPC]
    public void WriteLog(string newLog)
    {
        string currentLog = logText.text;
        string[] splitLogs = currentLog.Split("\n");
        // 最新14件を取得（15件以上あれば末尾から14件、そうでなければ全部）
        string[] latestLogs = (splitLogs.Length >= 15)
            ? splitLogs.Skip(splitLogs.Length - 14).ToArray()
            : splitLogs;

        logText.text = string.Join("\n", latestLogs) + "\n" + newLog;
    }

}
