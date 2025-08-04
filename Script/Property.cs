using UnityEngine;
using System.Linq;
using System.Collections;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public static class Property
{
    // オブジェクト
    public static CardDistributeManager cardDistributeManager;

    public static LogManager LOGGER;
    public static GameObject YOUR_TURN;  // あなたのターンです表示用
    public static GameObject NOT_YOUR_TURN;  // あなたのターンではありません表示用
    public static GameObject MODE_PANEL;
    public static GameObject RAINBOW_AREA;
    public static GameObject BLACK_AREA;

    public static bool isModePanelOpen = false;
    public static Dictionary<string, Vector3> worldPositions = new Dictionary<string, Vector3>();

    // Initで初期化するもの
    public static Dictionary<string, int> state;
    public static int discardCount; // 捨て札の枚数
    public static int hintCount; // ヒントの残数
    public static int errorCount; // 失敗数

    static Property() // ← アプリ実行時に最初に1回だけ呼ばれる
    {
        Debug.Log("[Config] static constructor called");

        LOGGER = UnityEngine.Object.FindAnyObjectByType<LogManager>();
        cardDistributeManager = UnityEngine.Object.FindAnyObjectByType<CardDistributeManager>();

        MODE_PANEL = GameObject.Find("ModePanel");
        MODE_PANEL.SetActive(false);
        YOUR_TURN = GameObject.Find("YourTurn");
        YOUR_TURN.SetActive(false);
        NOT_YOUR_TURN = GameObject.Find("NotYourTurn");
        NOT_YOUR_TURN.SetActive(false);
        RAINBOW_AREA = GameObject.Find("置き場_Rainbow");
        BLACK_AREA = GameObject.Find("置き場_Black");

        // 置き場
        worldPositions["Blue"] = GameObject.Find("置き場_Blue").transform.position;
        worldPositions["Green"] = GameObject.Find("置き場_Green").transform.position;
        worldPositions["White"] = GameObject.Find("置き場_White").transform.position;
        worldPositions["Yellow"] = GameObject.Find("置き場_Yellow").transform.position;
        worldPositions["Red"] = GameObject.Find("置き場_Red").transform.position;
        worldPositions["Rainbow"] = GameObject.Find("置き場_Rainbow").transform.position;
        worldPositions["Black"] = GameObject.Find("置き場_Black").transform.position;
        worldPositions["Discard"] = GameObject.Find("捨て札").transform.position + new Vector3(0f, 0f, -1f);
        worldPositions["Deck"] = GameObject.Find("山札").transform.position + new Vector3(0f, 0f, -1f);
        // 手札
        worldPositions["Myself"] = GameObject.Find("HandArea_Myself").transform.position + new Vector3(-25f, 0f, -1f);
        worldPositions["Other1"] = GameObject.Find("HandArea_Other_1").transform.position + new Vector3(-25f, 0f, -1f);
        worldPositions["Other2"] = GameObject.Find("HandArea_Other_2").transform.position + new Vector3(-25f, 0f, -1f);
        worldPositions["Other3"] = GameObject.Find("HandArea_Other_3").transform.position + new Vector3(-25f, 0f, -1f);
        worldPositions["Other4"] = GameObject.Find("HandArea_Other_4").transform.position + new Vector3(-25f, 0f, -1f);
        worldPositions["NumberHint"] = new Vector3(-1f, 6f, -1f); // カードの位置に対するヒントの差分位置
        worldPositions["ColorHint"] = new Vector3(3f, 6f, -1f); // カードの位置に対するヒントの差分位置
        worldPositions["Offset"] = new Vector3(12f, 0f, 0f); // カード1枚のoffset
        worldPositions["DiscardOffset"] = new Vector3(3f, 0f, -0.01f);

        // ゲームパラメータ初期化
        InitProp();
    }

    public static void InitProp()
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
}