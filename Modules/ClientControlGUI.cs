using System;
using EHR.Modules;
using EHR.Patches;
using UnityEngine;
using UnityEngine.SceneManagement;
using static EHR.Translator;
// ReSharper disable InconsistentNaming

namespace EHR;

public class ClientControlGUI : MonoBehaviour
{
    public static ClientControlGUI Instance;

    public bool IsOpen;           // whether the panel is visible

    private Vector2 _scroll;      // current scroll position inside the panel
    private float _contentH;      // total height of all drawn buttons, updated each frame

    private Rect _windowRect;     // position and size of the floating window
    private bool _dragging;       // true while the user is dragging the title bar
    private Vector2 _dragOffset;  // cursor offset from window corner when drag started
    private bool _windowInitialized;

    // Zoom slider state - tracks Camera.main.orthographicSize so the slider doesn't reset when the user zooms with the scroll wheel or pinch gesture
    // Range matches Zoom.cs: 3.0 (default) to 18.0 (max out)
    private float _zoomValue = 3.0f;

    // Scale helpers - everything is relative to a 1080px-wide reference screen
    // On PC the UI is scaled down to 60% but on Android we keep it at full size for better readability
    private static float PS  => OperatingSystem.IsAndroid() ? 1.0f : 0.5f;  // PS: platform scale multiplier (Android full size, PC smaller)
    private static float S   => Screen.width / 1080f * PS;                  // S: global scale factor
    private static int   FS  => Mathf.Max(12, Mathf.RoundToInt(21f * S));   // FS: font size
    private static float BH  => 66f * S;                                    // BH: button height
    private static float BW  => 340f * S;                                   // BW: button width
    private static float P   => 10f * S;                                    // P: padding
    private static int ChipFS => Mathf.Max(10, FS - 4);                     // Shortcut chip font size

    // Scrollbar column width - kept intentionally small so it never causes horizontal overflow
    private static float SBW => 22f * S;

    // Used to detect when a rebuild is needed
    private float _lastS = -1f;
    private string _lastScene = "";

    // GUIStyle holds font, colors, and the background texture for each widget type
    private GUIStyle _sAction, _sHost, _sDanger, _sSection, _sToggle, _sWindow, _sTitleBar, _sDragHint;

    // Credit: Xtracube for pointing out the add_sceneLoaded workaround
    private void Awake()
    {
        Instance = this;
        SceneManager.add_sceneLoaded((Action<Scene, LoadSceneMode>)OnSceneLoaded);
        Logger.Info("ClientControlGUI initialised", "ClientControlGUI");
    }

    private void OnDestroy()
    {
        SceneManager.remove_sceneLoaded((Action<Scene, LoadSceneMode>)OnSceneLoaded);
    }

    // Called by Unity whenever a scene loads (e.g. lobby -> game)
    // Resetting _lastS forces a full style rebuild on the next OnGUI call
    // Textures use HideFlags.HideAndDontSave so Unity manages their lifetime automatically
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _lastS = -1f;
        NullStyles();
    }

    // Nulls out every GUIStyle so StylesValid() returns false and RebuildStyles() is triggered next frame
    private void NullStyles()
    {
        _sAction = _sHost = _sDanger = _sSection = _sToggle = _sWindow = _sTitleBar = _sDragHint = null;
    }

    // Called once, sets the initial window position to the left side, vertically centred
    private void InitWindowRect()
    {
        // Window width accounts for button width + padding on both sides + scrollbar column
        float w = BW + P * 4f + SBW;
        float h = Screen.height * (OperatingSystem.IsAndroid() ? 0.78f : 0.65f);
        _windowRect = new Rect(20f * S, (Screen.height - h) * 0.5f, w, h);
        _windowInitialized = true;
    }

    // Creates all GUIStyles. Called on first draw and after every scene change
    private void RebuildStyles()
    {
        _lastS = S;
        NullStyles();

        int toggleSize = Mathf.Max(1, Mathf.RoundToInt(48f * S));
        int toggleRadius = toggleSize / 4; // gentler radius for the toggle square

        // Open/close toggle button (the "=" / "X" button)
        _sToggle = new GUIStyle
        {
            fontSize  = FS + 4,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { background = T(toggleSize, toggleSize, toggleRadius, new Color(0.10f, 0.10f, 0.28f, 1f), new Color(0.20f, 0.36f, 0.60f, 1f)), textColor = Color.white },
            hover     = { background = T(toggleSize, toggleSize, toggleRadius, new Color(0.18f, 0.18f, 0.40f, 1f), new Color(0.30f, 0.48f, 0.72f, 1f)), textColor = Color.white },
            active    = { background = T(toggleSize, toggleSize, toggleRadius, new Color(0.07f, 0.07f, 0.18f, 1f), new Color(0.12f, 0.26f, 0.46f, 1f)), textColor = Color.white }
        };

        int winW = Mathf.Max(1, Mathf.RoundToInt(BW + P * 4f + SBW));
        int winH = Mathf.Max(1, Mathf.RoundToInt(Screen.height * (OperatingSystem.IsAndroid() ? 0.78f : 0.65f)));

        // Floating window background
        _sWindow = new GUIStyle
        {
            normal = { background = T(winW, winH, 22, new Color(0.06f, 0.07f, 0.15f, 1f), new Color(0.10f, 0.16f, 0.30f, 1f)) }
        };

        // "EHR Client Controls" heading
        _sTitleBar = new GUIStyle
        {
            fontSize  = FS + 3,
            fontStyle = FontStyle.BoldAndItalic,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.72f, 0.90f, 1.00f, 1f) }
        };

        // Small "drag to move" hint under the title
        _sDragHint = new GUIStyle
        {
            fontSize  = Mathf.Max(10, FS - 5),
            fontStyle = FontStyle.Italic,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.45f, 0.58f, 0.72f, 1f) }
        };

        // Section label (e.g. "Lobby", "Host Controls")
        _sSection = new GUIStyle
        {
            fontSize  = FS,
            fontStyle = FontStyle.BoldAndItalic,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.58f, 0.82f, 1.00f, 1f) }
        };

        // Blue-grey: general actions available to everyone
        _sAction = MakeBtn(
            new Color(0.15f, 0.19f, 0.35f, 1f),
            new Color(0.24f, 0.30f, 0.52f, 1f),
            new Color(0.09f, 0.12f, 0.22f, 1f)
        );
        // Darker blue: host-only actions
        _sHost = MakeBtn(
            new Color(0.07f, 0.22f, 0.40f, 1f),
            new Color(0.12f, 0.36f, 0.62f, 1f),
            new Color(0.04f, 0.14f, 0.28f, 1f)
        );
        // Red: destructive or irreversible actions
        _sDanger = MakeBtn(
            new Color(0.38f, 0.07f, 0.07f, 1f),
            new Color(0.60f, 0.12f, 0.12f, 1f),
            new Color(0.24f, 0.04f, 0.04f, 1f)
        );
    }

// Creates a button GUIStyle with normal, hover, and pressed states using a high-res, pixel-drawn rounded rectangle for smooth corners.
    private GUIStyle MakeBtn(Color normal, Color hover, Color active)
    {
        int w = Mathf.Max(1, Mathf.RoundToInt(BW));
        int h = Mathf.Max(1, Mathf.RoundToInt(BH));
        // Radius is BH * 0.38 so corners are visibly rounded, not just slightly chamfered
        int r = Mathf.Max(1, Mathf.RoundToInt(BH * 0.38f));
        return new GUIStyle
        {
            fontSize  = FS,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap  = true,
            richText  = true,
            normal    = { background = T(w, h, r, normal,  Lift(normal,  0.10f)), textColor = Color.white },
            hover     = { background = T(w, h, r, hover,   Lift(hover,   0.10f)), textColor = Color.white },
            active    = { background = T(w, h, r, active,  Lift(active,  0.06f)), textColor = Color.white }
        };
    }

    // Slightly brightens a color for the edge highlight on rounded textures
    private static Color Lift(Color c, float v) =>
        new(Mathf.Clamp01(c.r + v), Mathf.Clamp01(c.g + v), Mathf.Clamp01(c.b + v), 1f);

    // Creates a rounded-rect texture with HideAndDontSave so Unity handles cleanup automatically
    private static Texture2D T(int w, int h, int r, Color fill, Color edge)
    {
        var tex = RoundedTex(w, h, r, fill, edge);
        tex.hideFlags = HideFlags.HideAndDontSave;
        return tex;
    }

    // Draws a rounded-corner texture with per-pixel distance checks, using 'fill' for interior and 'edge' for a subtle 1px anti-aliased rim
    private static Texture2D RoundedTex(int w, int h, int radius, Color fill, Color edge)
    {
        w = Mathf.Max(1, w);
        h = Mathf.Max(1, h);
        radius = Mathf.Clamp(radius, 0, Mathf.Min(w, h) / 2);
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false)
        {
            filterMode = FilterMode.Bilinear
        };

        for (int py = 0; py < h; py++)
        {
            for (int px = 0; px < w; px++)
            {
                float a = CornerAlpha(px, py, w, h, radius);
                Color c = a <= 0f ? Color.clear          // outside rounded corner = transparent
                        : a >= 1f ? fill                 // solid interior
                        : Color.Lerp(edge, fill, a);     // 1px anti-aliased border
                tex.SetPixel(px, py, c);
            }
        }
        tex.Apply();
        return tex;
    }

    // Returns 0 if outside (transparent), 1 if inside, or a small blend value for the smooth edge
    private static float CornerAlpha(int px, int py, int w, int h, int r)
    {
        // Find which corner circle center this pixel belongs to, if any
        int cx, cy;
        if      (px < r     && py < r    ) { cx = r;     cy = r;     }
        else if (px >= w-r  && py < r    ) { cx = w - r; cy = r;     }
        else if (px < r     && py >= h-r ) { cx = r;     cy = h - r; }
        else if (px >= w-r  && py >= h-r ) { cx = w - r; cy = h - r; }
        else return 1f; // not near any corner, always solid

        float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
        if (d >= r + 1f) return 0f;  // outside arc
        if (d <= r - 1f) return 1f;  // inside arc
        return r + 0.5f - d;         // on the edge - fractional for smoothing
    }

    // Returns false if any style or its background texture is missing
    // This happens after a scene load, triggering RebuildStyles() on the next OnGUI call
    private bool StylesValid() =>
        _sAction != null
        && _sAction.normal.background
        && _sHost  != null
        && _sHost.normal.background
        && _sDanger != null
        && _sDanger.normal.background
        && _sToggle != null
        && _sToggle.normal.background
        && _sWindow != null
        && _sWindow.normal.background;

    // OnGUI is called by Unity every frame (and sometimes multiple times per frame)
    private void OnGUI()
    {
        if (!HudManager.InstanceExists) return;
        if (!_windowInitialized) InitWindowRect();

        // Rebuild styles if the screen scale changed or a scene transition wiped the styles
        string scene = SceneManager.GetActiveScene().name;
        if (Math.Abs(_lastS - S) > 0.01f || scene != _lastScene || !StylesValid())
        {
            _lastScene = scene;
            RebuildStyles();
        }

        HandleDrag();
        DrawToggle();
        if (IsOpen) DrawWindow();
    }

    // Draws the small toggle button for the panel; fades to 10% opacity during gameplay
    private void DrawToggle()
    {
        float size = 48f * S;
        float x, y;

        if (IsOpen)
        {
            // Sits just to the right of the window, vertically centred on it
            x = _windowRect.x + _windowRect.width + 8f * S;
            y = _windowRect.y + (_windowRect.height - size) * 0.5f;
        }
        else
        {
            // Bottom-left center: horizontally centred on the left quarter of the screen
            x = Screen.width * 0.3f - size * 0.5f;
            y = Screen.height - size - 10f * S;
        }

        // 90% transparent when in game and panel is closed, full opacity otherwise
        bool fadeOut = !IsOpen && (GameStates.IsInGame || GameSettingMenu.Instance);
        Color prev = GUI.color;
        if (fadeOut) GUI.color = new Color(1f, 1f, 1f, 0.10f);

        if (GUI.Button(new Rect(x, y, size, size), IsOpen ? "X" : "=", _sToggle))
            IsOpen = !IsOpen;

        if (fadeOut) GUI.color = prev;
    }

    // Handles dragging the window by the title bar using mouse or touch input
    private void HandleDrag()
    {
        if (!IsOpen) return;

        Event e = Event.current;
        float titleH = BH * 0.80f + P;
        var titleRect = new Rect(_windowRect.x, _windowRect.y, _windowRect.width, titleH);

        switch (e.type)
        {
            case EventType.MouseDown when titleRect.Contains(e.mousePosition):
            {
                _dragging = true;
                _dragOffset = e.mousePosition - new Vector2(_windowRect.x, _windowRect.y);
                e.Use(); // consume the event so nothing else receives it
                break;
            }
            case EventType.MouseDrag when _dragging:
            {
                float nx = Mathf.Clamp(e.mousePosition.x - _dragOffset.x, 0, Screen.width  - _windowRect.width);
                float ny = Mathf.Clamp(e.mousePosition.y - _dragOffset.y, 0, Screen.height - _windowRect.height);
                _windowRect.x = nx;
                _windowRect.y = ny;
                e.Use();
                break;
            }
            case EventType.MouseUp:
            {
                _dragging = false;
                break;
            }
        }
    }

    // Draws the window background, title, and the scrollable button list
    private void DrawWindow()
    {
        // GUI.Box draws a background panel using the given style
        GUI.Box(_windowRect, "", _sWindow);

        float titleH = BH * 0.80f + P;

        GUI.Label(
            new Rect(_windowRect.x, _windowRect.y + P * 0.6f, _windowRect.width, BH * 0.55f),
            "EHR Client Controls",
            _sTitleBar
        );

        GUI.Label(
            new Rect(_windowRect.x, _windowRect.y + BH * 0.58f + P * 0.4f, _windowRect.width, BH * 0.38f),
            "drag to move",
            _sDragHint
        );

        float scrollY   = _windowRect.y + titleH + P * 0.4f;
        float scrollH   = _windowRect.height - titleH - P;
        float visibleW  = _windowRect.width - P * 2f;

    // outerRect: the visible scroll area on screen
    // innerRect: the full content area (taller than outerRect when there are enough buttons)
    // Content width is set slightly less than outerRect.width to prevent horizontal overflow and hide the horizontal scrollbar
        float contentW = visibleW - SBW - 1f;
        var outerRect = new Rect(_windowRect.x + P, scrollY, visibleW, scrollH);
        var innerRect = new Rect(0, 0, contentW, _contentH);

        GUI.skin.verticalScrollbar.fixedWidth      = SBW;
        GUI.skin.verticalScrollbarThumb.fixedWidth = SBW;

    // Horizontal scroll is disabled; vertical scroll appears when _contentH > scrollH
        _scroll = GUI.BeginScrollView(outerRect, _scroll, innerRect, false, false);
        float y = P * 0.5f;
        DrawButtons(ref y, contentW);
        _contentH = y + P; // record total height so innerRect stays accurate next frame
        GUI.EndScrollView();
    }

    // Formats a button label with an optional shortcut chip below the text; hidden on Android (no keyboard); uses IMGUI rich text (richText = true)
    private string L(string label, string shortcut = null)
    {
        if (OperatingSystem.IsAndroid() || shortcut == null) return label;
        return $"{label}\n<color=#7a9cbf><size={ChipFS}>{shortcut}</size></color>";
    }

    // Draws buttons based on game state; 'y' moves down per button; 'w' is the available width from DrawWindow
    private void DrawButtons(ref float y, float w)
    {
        bool amHost     = AmongUsClient.Instance && AmongUsClient.Instance.AmHost;
        bool inGame     = GameStates.IsInGame;
        bool inLobby    = GameStates.IsLobby;
        bool inMeeting  = GameStates.IsMeeting;
        bool countdown  = GameStates.IsCountDown;
        bool notJoined  = GameStates.IsNotJoined;
        bool localAlive = PlayerControl.LocalPlayer && PlayerControl.LocalPlayer.IsAlive();
        bool canMove    = GameStates.IsCanMove;
        bool noGameEnd  = Options.NoGameEnd.GetBool();

        Section(ref y, "General");

        Btn(ref y, L("Dump Log", "CTRL + F1"), _sAction, () =>
        {
            Logger.Info("Log dumped", "ClientControlGUI");
            Utils.DumpLog();
        });
        Btn(ref y, L("Reload Translations", "F5 + T"), _sAction, () =>
        {
            Logger.Info("Reloading Custom Translation File", "ClientControlGUI");
            LoadLangs();
            Logger.SendInGame("Reloaded Custom Translation File");
        });
        Btn(ref y, L("Export Translations", "F5 + X"), _sAction, () =>
        {
            Logger.Info("Exported Custom Translation File", "ClientControlGUI");
            ExportCustomTranslation();
            Logger.SendInGame("Exported Custom Translation File");
        });
        if (!notJoined)
            Btn(ref y, L("Copy Settings", "ALT + C"), _sAction, Utils.CopyCurrentSettings);

        Btn(ref y, L("Fix Button Positions", "ALT + ENTER"), _sAction, () =>
            LateTask.New(SetResolutionManager.Postfix, 0.01f, "Fix Button Position")
        );

        if (inGame || inMeeting)
            Btn(ref y, L("Fix Blackscreen", "SHIFT + CTRL + X"), _sAction, () =>
                ExileController.Instance?.ReEnableGameplay()
            );

        if (inGame && (canMove || inMeeting))
            Btn(ref y, L(InGameRoleInfoMenu.Showing ? "Hide Role Info" : "Show Role Info", "F1 (hold)"), _sAction, () =>
            {
                if (InGameRoleInfoMenu.Showing)
                    InGameRoleInfoMenu.Hide();
                else
                {
                    InGameRoleInfoMenu.SetRoleInfoRef(PlayerControl.LocalPlayer);
                    InGameRoleInfoMenu.Show();
                }
            });

        if (inLobby || (inGame && !inMeeting && canMove && (!localAlive || GameStates.IsFreePlay || DebugModeManager.AmDebugger)))
        {
            Section(ref y, "Camera");

            // Sync slider to actual camera value so external changes (scroll wheel, touch pinch) are reflected
            var cam = Camera.main;
            if (cam) _zoomValue = cam.orthographicSize;

            float newZoom = Slider(ref y, $"Zoom  {_zoomValue:F1}x", _zoomValue, 3.0f, 18.0f, w);
            if (Mathf.Abs(newZoom - _zoomValue) > 0.01f)
            {
                _zoomValue = newZoom;
                Zoom.SetZoomSize(reset: false);
                if (cam) cam.orthographicSize = _zoomValue;
                if (HudManager.InstanceExists) HudManager.Instance.UICamera.orthographicSize = _zoomValue;
            }

            if (GUI.Button(new Rect(0, y, w, BH), "Reset Zoom", _sAction))
            {
                Zoom.SetZoomSize(reset: true);
                _zoomValue = 3.0f;
            }
            y += BH + P * 0.7f;

            bool canNoclip = PlayerControl.LocalPlayer
                && PlayerControl.LocalPlayer.CanMove
                && (!AmongUsClient.Instance.IsGameStarted || !GameStates.IsOnlineGame);

            if (canNoclip)
            {
                // Reads live state every frame for correct label/colour; lambda also reads it on click to avoid stale values
                bool noclipOn = ControllerManagerUpdatePatch.NoClipEnabled;
                Btn(ref y, noclipOn ? "No-clip: ON" : "No-clip: OFF", noclipOn ? _sHost : _sAction, () =>
                {
                    ControllerManagerUpdatePatch.NoClipEnabled = !ControllerManagerUpdatePatch.NoClipEnabled;
                });
            }
        }

        if (inLobby)
        {
            Section(ref y, "Lobby");

            if (amHost && !countdown)
                Btn(ref y, L("Start Game", "ENTER"), _sHost, () =>
                {
                    if (GameStartManager.InstanceExists)
                    {
                        Logger.Info("Start game via ClientControlGUI", "ClientControlGUI");
                        GameStartManager.Instance.BeginGame();
                    }
                });

            if (amHost && countdown)
            {
                Btn(ref y, L("Start Immediately", "SHIFT"), _sHost, () =>
                {
                    Logger.Info("Starting game immediately via ClientControlGUI", "ClientControlGUI");
                    GameStartManager.Instance.countDownTimer = 0;
                });
                Btn(ref y, L("Cancel Countdown", "C"), _sDanger, () =>
                {
                    GameStartManager.Instance.ResetStartState();
                    Logger.SendInGame(GetString("CancelStartCountDown"));
                });
            }

            if (amHost)
            {
                Btn(ref y, L("Show Active Settings", "CTRL + N"), _sHost, () =>
                {
                    Main.IsChatCommand = true;
                    Utils.ShowActiveSettings();
                });
                Btn(ref y, L("Show Settings Help", "CTRL + SHIFT + N"), _sHost, () =>
                {
                    Main.IsChatCommand = true;
                    Utils.ShowActiveSettingsHelp();
                });
                Btn(ref y, L("Reset All Options", "CTRL + SHIFT + DEL"), _sDanger, () =>
                    Prompt.Show(GetString("Promt.ResetAllOptions"), () =>
                    {
                        foreach (var opt in OptionItem.AllOptions)
                            if (opt.Id > 0) opt.SetValue(opt.DefaultValue, false, false);
                        OptionItem.SyncAllOptions();
                        OptionSaver.Save();
                    }, () => { })
                );
            }
        }

        if (inGame)
        {
            Section(ref y, "In Game");

            if (amHost && localAlive)
                Btn(ref y, L("Kill Self", "SHIFT + ENTER + E"), _sDanger, () =>
                {
                    var state = Main.PlayerStates[PlayerControl.LocalPlayer.PlayerId];
                    state.deathReason = PlayerState.DeathReason.etc;
                    PlayerControl.LocalPlayer.RpcExileV2();
                    PlayerControl.LocalPlayer.Data.IsDead = true;
                    state.SetDead();
                    Utils.AfterPlayerDeathTasks(PlayerControl.LocalPlayer, inMeeting);
                    Utils.SendMessage(
                        GetString("HostKillSelfByCommand"),
                        title: $"<color=#ff0000>{GetString("DefaultSystemMessageTitle")}</color>"
                    );
                });

            if (amHost)
            {
                Section(ref y, "Host Controls");

                if (!inMeeting)
                    Btn(ref y, L("Call Meeting", "SHIFT + ENTER + M"), _sHost, () =>
                        PlayerControl.LocalPlayer.NoCheckStartMeeting(null, true)
                    );
                else
                    Btn(ref y, L("End Meeting", "SHIFT + ENTER + M"), _sHost, () =>
                    {
                        MeetingHudRpcClosePatch.AllowClose = true;
                        MeetingHud.Instance.RpcClose();
                    });

                Btn(ref y, L("Open Your Chat", "SHIFT + ENTER + C"), _sHost, () =>
                    HudManager.Instance.Chat.SetVisible(true)
                );
                Btn(ref y, L("Open Chat for All", "CTRL + SHIFT + ENTER + C"), _sHost, Utils.SetChatVisibleForAll);

                if (noGameEnd)
                    Btn(ref y, L("Force Game End", "SHIFT + ENTER + L"), _sDanger, () =>
                    {
                        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Draw);
                        GameEndChecker.CheckCustomEndCriteria();
                    });
            }
        }

        return;

        // Draws a section heading with spacing above it
        void Section(ref float sy, string title)
        {
            sy += P * 2f;
            GUI.Label(new Rect(0, sy, w, BH * 0.50f), title, _sSection);
            sy += BH * 0.52f + P * 0.4f;
        }

            // Draws a button and moves the cursor; try/catch stops one bad action from crashing the GUI
        void Btn(ref float by, string label, GUIStyle style, Action action)
        {
            if (GUI.Button(new Rect(0, by, w, BH), label, style))
            {
                try { action(); }
                catch (Exception e) { Logger.Error(e.ToString(), "ClientControlGUI"); }
            }
            by += BH + P * 0.7f;
        }

        // Draws a labeled horizontal slider and returns the new value
        float Slider(ref float sy, string label, float value, float min, float max, float sw)
        {
            GUI.Label(new Rect(0, sy, sw, BH * 0.45f), label, _sSection);
            sy += BH * 0.48f;
            float result = GUI.HorizontalSlider(new Rect(0, sy, sw, BH * 0.52f), value, min, max);
            sy += BH * 0.52f + P * 0.7f;
            return result;
        }
    }
}
