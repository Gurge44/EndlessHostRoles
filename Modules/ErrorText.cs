using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
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
    private readonly List<ErrorData> AllErrors = [];
    private Camera _camera;
    private Camera _camera1;

    public static bool HasHint => Instance.AllErrors.Any(err => err.Code == ErrorCode.LoadingHint);

    public void Update()
    {
        AllErrors.ForEach(err => err.IncreaseTimer());
        ErrorData[] ToRemove = AllErrors.Where(err => err.ErrorLevel <= 1 && 30f < err.Timer).ToArray();

        if (ToRemove.Length > 0)
        {
            AllErrors.RemoveAll(err => ToRemove.Contains(err));
            UpdateText();
            if (HnSFlag) Destroy(gameObject);
        }
    }

    public void LateUpdate()
    {
        if (!Text.enabled) return;
        if (!Camera) Camera = !HudManager.InstanceExists ? _camera : _camera1;
        if (Camera) transform.position = AspectPosition.ComputeWorldPosition(Camera, AspectPosition.EdgeAlignments.Top, TextOffset);
    }

    public static void Create(TextMeshPro baseText)
    {
        TextMeshPro Text = Instantiate(baseText);
        var instance = Text.gameObject.AddComponent<ErrorText>();
        instance.Text = Text;
        instance.name = "ErrorText";

        Text.enabled = false;
        Text.text = "NO ERROR";
        Text.color = Color.red;
        Text.outlineColor = Color.black;
        Text.alignment = TextAlignmentOptions.Top;
    }

    public static void RemoveHint()
    {
        Instance.AllErrors.RemoveAll(err => err.Code == ErrorCode.LoadingHint);
        Instance.UpdateText();
    }

    public void AddError(ErrorCode code)
    {
        var error = new ErrorData(code);
        if (0 < error.ErrorLevel && code != ErrorCode.LoadingHint) Logger.Error($"Error: {error}: {error.Message}", "ErrorText");

        if (AllErrors.All(e => e.Code != code)) AllErrors.Add(error);

        UpdateText();
    }

    public void UpdateText()
    {
        try
        {
            var text = string.Empty;
            var maxLevel = 0;
            var hint = false;

            foreach (ErrorData err in AllErrors)
            {
                if (err.Code == ErrorCode.LoadingHint) hint = true;

                text += hint ? LoadingScreen.Hint : $"{err}: {err.Message}\n";
                if (maxLevel < err.ErrorLevel) maxLevel = err.ErrorLevel;
            }

            if (maxLevel == 0)
                Text.enabled = false;
            else
            {
                if (!HnSFlag && !hint) text += $"{GetString($"ErrorLevel{maxLevel}")}";

                if (CheatDetected) text = SBDetected ? GetString("EAC.CheatDetected.HighLevel") : GetString("EAC.CheatDetected.LowLevel");

                Text.enabled = true;
            }

            if (GameStates.IsInGame && maxLevel != 3 && !CheatDetected) text += $"\n{GetString("TerminateCommand")}: Shift+L+Enter";

            Text.text = text;
        }
        catch (NullReferenceException) { }
        catch (Exception e) { Logger.Error(e.ToString(), "ErrorText.UpdateText"); }
    }

    public void Clear()
    {
        AllErrors.RemoveAll(err => err.ErrorLevel != 3);
        UpdateText();
    }

    private class ErrorData
    {
        public readonly ErrorCode Code;
        public readonly int ErrorLevel;
        private readonly int ErrorType1;
        private readonly int ErrorType2;

        public ErrorData(ErrorCode code)
        {
            Code = code;
            ErrorType1 = (int)code / 10000;
            ErrorType2 = ((int)code / 10) - (ErrorType1 * 1000); // xxxyyy - xxx000
            ErrorLevel = (int)code - ((int)code / 10 * 10);
            Timer = 0f;
        }

        public float Timer { get; private set; }
        public string Message => $"<b>{GetString(ToString())}</b>";

        public override string ToString()
        {
            // ERR-xxx-yyy-z
            return $"ERR-{ErrorType1:000}-{ErrorType2:000}-{ErrorLevel:0}";
        }

        public void IncreaseTimer()
        {
            Timer += Time.deltaTime;
        }
    }

    #region Singleton

    public static ErrorText Instance { get; private set; }

    private void Awake()
    {
        if (Instance)
            Destroy(gameObject);
        else
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }
    }

    private int Frame;

    private void FixedUpdate()
    {
        if (Frame++ < 40) return;

        Frame = 0;

        if (_camera != null && _camera1 != null) return;

        try
        {
            if (!HudManager.InstanceExists)
            {
                _camera = Camera.main;
                return;
            }
            
            _camera1 = HudManager.Instance.PlayerCam.GetComponent<Camera>();
            _camera = Camera.main;
        }
        catch { }
    }

    #endregion
}

public enum ErrorCode
{
    //xxxyyyz: ERR-xxx-yyy-z
    // xxx: General type of error (HUD-related, banishment-related, etc.)
    // yyy: Detailed type of error (BoutyHunter processing, Mercenary processing, etc.)
    // z: Severity
    //      0: No action required (hide)
    //      1: Abandon village if not working properly (hide after a certain period of time)
    //      2: Abandon village recommended (hide as abandoned village)
    //      3: Unable to handle on user side (do not delete)
    // ==========
    // 001 Main
    Main_DictionaryError = 0010003, // 001-000-3 Main Dictionary Error

    // 002 Support related
    UnsupportedVersion = 002_000_3, // 002-000-1 AmongUs version is outdated
    UnsupportedMap = 002_000_1, // 002-000-1 Unsupported Map

    // ==========
    // 000 Test
    NoError = 0000000, // 000-000-0 No Error
    TestError0 = 0009000, // 000-900-0 Test Error 0
    TestError1 = 0009101, // 000-910-1 Test Error 1
    TestError2 = 0009202, // 000-920-2 Test Error 2
    TestError3 = 0009303, // 000-930-3 Test Error 3
    HnsUnload = 000_804_1, // 000-804-1 Unloaded By HnS
    CheatDetected = 000_666_2, // 000-666-2
    SBDetected = 000_666_1, // 000-666-1

    // ==========
    LoadingHint = 000_999_3 // 000-999-3 Loading Hint
}