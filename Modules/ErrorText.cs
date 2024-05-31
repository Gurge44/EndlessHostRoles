using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

public class ErrorText : MonoBehaviour
{
    public TextMeshPro Text;
    public Camera Camera;
    public Vector3 TextOffset = new(0, 0.3f, -1000f);

    public bool HnSFlag;
    public bool CheatDetected;
    public bool SBDetected;
    private Camera _camera;
    private Camera _camera1;
    public List<ErrorData> AllErrors = [];

    public void Update()
    {
        AllErrors.ForEach(err => err.IncreaseTimer());
        var ToRemove = AllErrors.Where(err => err.ErrorLevel <= 1 && 30f < err.Timer).ToArray();
        if (ToRemove.Length > 0)
        {
            AllErrors.RemoveAll(err => ToRemove.Contains(err));
            UpdateText();
            if (HnSFlag)
                Destroy(gameObject);
        }
    }

    public void LateUpdate()
    {
        if (!Text.enabled) return;

        if (!Camera)
            Camera = !HudManager.InstanceExists ? _camera : _camera1;
        if (Camera)
        {
            transform.position = AspectPosition.ComputeWorldPosition(Camera, AspectPosition.EdgeAlignments.Top, TextOffset);
        }
    }

    public static void Create(TextMeshPro baseText)
    {
        var Text = Instantiate(baseText);
        var instance = Text.gameObject.AddComponent<ErrorText>();
        instance.Text = Text;
        instance.name = "ErrorText";

        Text.enabled = false;
        Text.text = "NO ERROR";
        Text.color = Color.red;
        Text.outlineColor = Color.black;
        Text.alignment = TextAlignmentOptions.Top;
    }

    public void AddError(ErrorCode code)
    {
        var error = new ErrorData(code);
        if (0 < error.ErrorLevel)
            Logger.Error($"Error: {error}: {error.Message}", "ErrorText");

        if (AllErrors.All(e => e.Code != code))
        {
            AllErrors.Add(error);
        }

        UpdateText();
    }

    public void UpdateText()
    {
        try
        {
            string text = string.Empty;
            int maxLevel = 0;
            foreach (ErrorData err in AllErrors)
            {
                text += $"{err}: {err.Message}\n";
                if (maxLevel < err.ErrorLevel) maxLevel = err.ErrorLevel;
            }

            if (maxLevel == 0)
            {
                Text.enabled = false;
            }
            else
            {
                if (!HnSFlag)
                    text += $"{GetString($"ErrorLevel{maxLevel}")}";
                if (CheatDetected)
                    text = SBDetected ? GetString("EAC.CheatDetected.HighLevel") : GetString("EAC.CheatDetected.LowLevel");
                Text.enabled = true;
            }

            if (GameStates.IsInGame && maxLevel != 3 && !CheatDetected)
                text += $"\n{GetString("TerminateCommand")}: Shift+L+Enter";
            Text.text = text;
        }
        catch (NullReferenceException)
        {
        }
        catch (Exception e)
        {
            Logger.Error(e.ToString(), "ErrorText.UpdateText");
        }
    }

    public void Clear()
    {
        AllErrors.RemoveAll(err => err.ErrorLevel != 3);
        UpdateText();
    }

    public class ErrorData
    {
        public readonly ErrorCode Code;
        public readonly int ErrorLevel;
        public readonly int ErrorType1;
        public readonly int ErrorType2;

        public ErrorData(ErrorCode code)
        {
            Code = code;
            ErrorType1 = (int)code / 10000;
            ErrorType2 = (int)code / 10 - ErrorType1 * 1000; // xxxyyy - xxx000
            ErrorLevel = (int)code - (int)code / 10 * 10;
            Timer = 0f;
        }

        public float Timer { get; private set; }
        public string Message => GetString(ToString());

        public override string ToString()
        {
            // ERR-xxx-yyy-z
            return $"ERR-{ErrorType1:000}-{ErrorType2:000}-{ErrorLevel:0}";
        }

        public void IncreaseTimer() => Timer += Time.deltaTime;
    }

    #region Singleton

    public static ErrorText Instance { get; private set; }

    private void Start()
    {
        _camera1 = HudManager.Instance.PlayerCam.GetComponent<Camera>();
        _camera = Camera.main;
    }

    private void Awake()
    {
        if (Instance)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }
    }

    #endregion
}

public enum ErrorCode
{
    //xxxyyyz: ERR-xxx-yyy-z
    //  xxx: エラー大まかなの種類 (HUD関連, 追放処理関連など)
    //  yyy: エラーの詳細な種類 (BoutyHunterの処理, SerialKillerの処理など)
    //  z:   深刻度
    //    0: 処置不要 (非表示)
    //    1: 正常に動作しなければ廃村 (一定時間で非表示)
    //    2: 廃村を推奨 (廃村で非表示)
    //    3: ユーザー側では対処不能 (消さない)
    // ==========
    // 001 Main
    Main_DictionaryError = 0010003, // 001-000-3 Main Dictionary Error

    // 002 Support related
    UnsupportedVersion = 002_000_1, // 002-000-1 AmongUs version is outdated

    // ==========
    // 000 Test
    NoError = 0000000, // 000-000-0 No Error
    TestError0 = 0009000, // 000-900-0 Test Error 0
    TestError1 = 0009101, // 000-910-1 Test Error 1
    TestError2 = 0009202, // 000-920-2 Test Error 2
    TestError3 = 0009303, // 000-930-3 Test Error 3
    HnsUnload = 000_804_1, // 000-804-1 Unloaded By HnS
    CheatDetected = 000_666_2, // 000-666-2 疑似存在作弊玩家
    SBDetected = 000_666_1, // 000-666-1 傻逼外挂司马东西
}