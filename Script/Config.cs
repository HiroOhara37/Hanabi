using System.Collections.Generic;
using UnityEngine;

public static class Config
{
    public const int MAX_SEATS = 5;
    public const string MODE = "MODE";
    public const string SEAT_ACTORS = "SEAT_ACTORS";  // CSV: "3,-1,-1,-1" (seatIndex -> actorNumber / -1 = empty)
    public const string SEAT_ACTIVE = "SEAT_ACTIVE";  // CSV: "1,1,0,0"   (seatIndex -> 1=有効席, 0=無効席)
    public const string TURN_SEAT = "TURN_SEAT";

    // デフォルトタイマー秒数なども定義可能
    public const float MOVE_SPEED = 0.5f;

    // オブジェクト
    public static LogManager LOGGER;
    public static GameObject NOT_YOUR_TURN;

    // 色
    public static readonly Dictionary<string, Color> COLOR_DICT = new Dictionary<string, Color>()
    {
        {"Red", Color.red},
        {"Green", Color.green},
        {"White", Color.white},
        {"Blue", Color.blue},
        {"Yellow", Color.yellow}
    };

    public static readonly Dictionary<string, string> COLOR_NAME = new Dictionary<string, string>()
    {
        {"Red", "赤"},
        {"Green", "緑"},
        {"White", "白"},
        {"Blue", "青"},
        {"Yellow", "黄"},
        {"Rainbow", "虹"},
        {"Black", "黒"}
    };

    static Config() // ← アプリ実行時に最初に1回だけ呼ばれる
    {
        Debug.Log("[Config] static constructor called");
        LOGGER = UnityEngine.Object.FindAnyObjectByType<LogManager>();
        NOT_YOUR_TURN = GameObject.Find("NotYourTurn");
        NOT_YOUR_TURN.SetActive(false);
    }
}