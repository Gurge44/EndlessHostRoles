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

    /// <summary>
    /// Whether the game HUD has been hidden
    /// <remarks>Restored automatically on scene change</remarks>
    /// </summary>
    public static bool HudHidden;

    /// <summary>
    /// Whether the panel is currently visible or should be visible
    /// </summary>
    public bool IsOpen;

    /// <summary>
    /// Current scroll position inside the panel
    /// </summary>
    private Vector2 _scroll;
    
    /// <summary>
    /// Total height of all drawn buttons, updated each frame
    /// </summary>
    private float _contentH;
    
    /// <summary>
    /// Position and size of the floating window
    /// </summary>
    private Rect _windowRect;
    
    /// <summary>
    /// True while the user is dragging the title bar
    /// </summary>
    private bool _dragging;
    
    /// <summary>
    /// Cursor offset from window corner when the drag started
    /// </summary>
    private Vector2 _dragOffset;
    
    /// <summary>
    /// Whether the window has been initialized
    /// </summary>
    private bool _windowInitialized;

    /// <summary>
    /// Zoom slider state.
    /// <remarks> Tracks <c>Camera.main.orthographicSize</c> so the slider doesn't reset when the user zooms with the scroll wheel or pinch gesture. </remarks>
    /// <returns> Range matching Zoom.cs: 3.0 (default) to 18.0 (max out) </returns>
    /// </summary>
    private float _zoomValue = 3.0f;

    // Scale helpers - everything is relative to a 1080px-wide reference screen
    // On PC the UI is scaled down to 50% but on Android we keep it slightly larger (60%) for better readability
    private static float PlatformScale  => OperatingSystem.IsAndroid() ? 0.6f : 0.5f;
    private static float Scale   => Screen.width / 1080f * PlatformScale;
    private static int   FontSize  => Mathf.Max(12, Mathf.RoundToInt(21f * Scale));
    private static float ButtonHeight  => 66f * Scale;
    private static float ButtonWidth  => (OperatingSystem.IsAndroid() ? 360f : 340f) * Scale;
    private static float Padding   => 10f * Scale;
    private static int ChipFontSize => Mathf.Max(10, FontSize - 4);

    // Slightly thicker on Android for touch support
    private static float ScrollbarColumnWidth => (OperatingSystem.IsAndroid() ? 42f : 22f) * Scale;

    /// <summary>
    /// The value of <see cref="Scale"/> in the last frame
    /// <remarks>Used to detect when a rebuild is needed (screen resize)</remarks>
    /// </summary>
    private float _lastScale = -1f;

    // GUIStyle holds font, colors, and the background texture for each widget type
    private GUIStyle _sAction, _sHost, _sDanger, _sSection, _sToggle, _sWindow, _sTitleBar, _sDragHint;
    private Camera _cam;

    // Credits: Xtracube (add_sceneLoaded workaround), astra1dev (HideFlags.HideAndDontSave suggestion)
    private void Awake()
    {
        _cam = Camera.main;
        Instance = this;
        SceneManager.add_sceneLoaded((Action<Scene, LoadSceneMode>)OnSceneLoaded);
        Logger.Info("ClientControlGUI initialised", "ClientControlGUI");
    }

    private void OnDestroy()
    {
        SceneManager.remove_sceneLoaded((Action<Scene, LoadSceneMode>)OnSceneLoaded);
    }

    // Resets HUD visibility flag on scene change; HudManager is recreated each load so its new instance is always active
    // Textures use HideFlags.HideAndDontSave so they survive scene transitions without any rebuild needed
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _cam = Camera.main;
        HudHidden = false;
    }

    // Creates all GUIStyles; only called on first draw or if the screen scale changes
    // Textures use HideFlags.HideAndDontSave so Unity keeps them alive across scene loads without rebuilding
    private void RebuildStyles()
    {
        _lastScale = Scale;

        int toggleSize = Mathf.Max(1, Mathf.RoundToInt(48f * Scale));
        int toggleRadius = toggleSize / 4; // gentler radius for the toggle square

        // Open/close toggle button (the "=" / "X" button)
        _sToggle = new GUIStyle
        {
            fontSize  = FontSize + 4,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { background = RoundedTexture(toggleSize, toggleSize, toggleRadius, new Color(0.10f, 0.10f, 0.28f, 1f), new Color(0.20f, 0.36f, 0.60f, 1f)), textColor = Color.white },
            hover     = { background = RoundedTexture(toggleSize, toggleSize, toggleRadius, new Color(0.18f, 0.18f, 0.40f, 1f), new Color(0.30f, 0.48f, 0.72f, 1f)), textColor = Color.white },
            active    = { background = RoundedTexture(toggleSize, toggleSize, toggleRadius, new Color(0.07f, 0.07f, 0.18f, 1f), new Color(0.12f, 0.26f, 0.46f, 1f)), textColor = Color.white }
        };

        int winW = Mathf.Max(1, Mathf.RoundToInt(ButtonWidth + Padding * 4f + ScrollbarColumnWidth));
        int winH = Mathf.Max(1, Mathf.RoundToInt(Screen.height * (OperatingSystem.IsAndroid() ? 0.82f : 0.65f)));

        // Floating window background
        _sWindow = new GUIStyle
        {
            normal = { background = RoundedTexture(winW, winH, 22, new Color(0.06f, 0.07f, 0.15f, 1f), new Color(0.10f, 0.16f, 0.30f, 1f)) }
        };

        // "EHR Client Controls" heading
        _sTitleBar = new GUIStyle
        {
            fontSize  = FontSize + 3,
            fontStyle = FontStyle.BoldAndItalic,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.72f, 0.90f, 1.00f, 1f) }
        };

        // Small "drag to move" hint under the title
        _sDragHint = new GUIStyle
        {
            fontSize  = Mathf.Max(10, FontSize - 5),
            fontStyle = FontStyle.Italic,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.45f, 0.58f, 0.72f, 1f) }
        };

        // Section label (e.g. "Lobby", "Host Controls")
        _sSection = new GUIStyle
        {
            fontSize  = FontSize,
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

    /// <summary>
    /// Creates a button GUIStyle with normal, hover, and pressed states using a high-res, pixel-drawn rounded rectangle for smooth corners
    /// </summary>
    /// <param name="normal">Default color</param>
    /// <param name="hover">Color on hover</param>
    /// <param name="active">Color when clicked</param>
    /// <returns></returns>
    private static GUIStyle MakeBtn(Color normal, Color hover, Color active)
    {
        int width = Mathf.Max(1, Mathf.RoundToInt(ButtonWidth));
        int height = Mathf.Max(1, Mathf.RoundToInt(ButtonHeight));
        int radius = Mathf.Max(1, Mathf.RoundToInt(ButtonHeight * 0.38f)); // so corners are visibly rounded, not just slightly chamfered
        return new GUIStyle
        {
            fontSize  = FontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap  = true,
            richText  = true,
            normal    = { background = RoundedTexture(width, height, radius, normal,  Lift(normal,  0.10f)), textColor = Color.white },
            hover     = { background = RoundedTexture(width, height, radius, hover,   Lift(hover,   0.10f)), textColor = Color.white },
            active    = { background = RoundedTexture(width, height, radius, active,  Lift(active,  0.06f)), textColor = Color.white }
        };
    }

    /// <summary>
    /// Slightly brightens a color for the edge highlight on rounded textures
    /// </summary>
    /// <param name="color">The original color</param>
    /// <param name="add">How much to add to the byte value of each color part (RGB)</param>
    /// <returns></returns>
    private static Color Lift(Color color, float add) =>
        new(Mathf.Clamp01(color.r + add), Mathf.Clamp01(color.g + add), Mathf.Clamp01(color.b + add), 1f);

    /// <summary>
    /// Creates a rounded-rect texture with HideAndDontSave so Unity handles cleanup automatically.
    /// Draws a rounded-corner texture with per-pixel distance checks, using 'fill' for interior and 'edge' for a subtle 1px anti-aliased rim.
    /// </summary>
    private static Texture2D RoundedTexture(int width, int height, int radius, Color fill, Color edge)
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        radius = Mathf.Clamp(radius, 0, Mathf.Min(width, height) / 2);
        var tex = new Texture2D(width, height, TextureFormat.ARGB32, false)
        {
            filterMode = FilterMode.Bilinear
        };

        for (int py = 0; py < height; py++)
        {
            for (int px = 0; px < width; px++)
            {
                float a = CornerAlpha(px, py, width, height, radius);
                Color c = a <= 0f ? Color.clear          // outside rounded corner = transparent
                    : a >= 1f ? fill                 // solid interior
                    : Color.Lerp(edge, fill, a);     // 1px anti-aliased border
                tex.SetPixel(px, py, c);
            }
        }
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        return tex;
    }

    /// <summary>
    /// Returns 0 if outside (transparent), 1 if inside, or a small blend value for the smooth edge
    /// </summary>
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

    // Called once, sets the initial window position to the left side, vertically centred
    private void InitWindowRect()
    {
        // Window width accounts for button width + padding on both sides + scrollbar column
        float w = ButtonWidth + Padding * 4f + ScrollbarColumnWidth;
        float h = Screen.height * (OperatingSystem.IsAndroid() ? 0.82f : 0.65f);
        _windowRect = new Rect(20f * Scale, (Screen.height - h) * 0.5f, w, h);
        _windowInitialized = true;
    }

    // OnGUI is called by Unity every frame (and sometimes multiple times per frame)
    private void OnGUI()
    {
        if (!HudManager.InstanceExists) return;

        if (!_windowInitialized) InitWindowRect();

        // Rebuild styles only when screen scale changes; HideAndDontSave keeps textures alive across scene transitions
        if (Math.Abs(_lastScale - Scale) > 0.01f) RebuildStyles();

        HandleDrag();
        DrawToggle();
        if (IsOpen) DrawWindow();
    }

    // Draws the small toggle button for the panel; fades to 10% opacity during gameplay
    private void DrawToggle()
    {
        float size = 48f * Scale;
        float x, y;

        if (IsOpen)
        {
            // Sits just to the right of the window, vertically centred on it
            x = _windowRect.x + _windowRect.width + 8f * Scale;
            y = _windowRect.y + (_windowRect.height - size) * 0.5f;
        }
        else
        {
            // Bottom-left center: horizontally centred on the left quarter of the screen
            x = Screen.width * 0.3f - size * 0.5f;
            y = Screen.height - size - 10f * Scale;
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
        float titleH = ButtonHeight * 0.80f + Padding;
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

        float titleH = ButtonHeight * 0.80f + Padding;

        GUI.Label(
            new Rect(_windowRect.x, _windowRect.y + Padding * 0.6f, _windowRect.width, ButtonHeight * 0.55f),
            "EHR Client Controls",
            _sTitleBar
        );

        GUI.Label(
            new Rect(_windowRect.x, _windowRect.y + ButtonHeight * 0.58f + Padding * 0.4f, _windowRect.width, ButtonHeight * 0.38f),
            "drag to move",
            _sDragHint
        );

        float scrollY   = _windowRect.y + titleH + Padding * 0.4f;
        float scrollH   = _windowRect.height - titleH - Padding;
        float visibleW  = _windowRect.width - Padding * 2f;

        // outerRect: the visible scroll area on screen
        // innerRect: the full content area (taller than outerRect when there are enough buttons)
        // Content width is set slightly less than outerRect.width to prevent horizontal overflow and hide the horizontal scrollbar
        float contentW = visibleW - ScrollbarColumnWidth - 1f;
        var outerRect = new Rect(_windowRect.x + Padding, scrollY, visibleW, scrollH);
        var innerRect = new Rect(0, 0, contentW, _contentH);

        GUI.skin.verticalScrollbar.fixedWidth      = ScrollbarColumnWidth;
        GUI.skin.verticalScrollbarThumb.fixedWidth = ScrollbarColumnWidth;

        // Horizontal scroll is disabled; vertical scroll appears when _contentH > scrollH
        _scroll = GUI.BeginScrollView(outerRect, _scroll, innerRect, false, false);
        float y = Padding * 0.5f;
        DrawButtons(ref y, contentW);
        _contentH = y + Padding; // record total height so innerRect stays accurate next frame
        GUI.EndScrollView();
    }

    // Formats a button label with an optional shortcut chip below the text; hidden on Android (no keyboard); uses IMGUI rich text (richText = true)
    private static string Label(string label, string shortcut = null)
    {
        if (OperatingSystem.IsAndroid() || shortcut == null) return label;
        return $"{label}\n<color=#7a9cbf><size={ChipFontSize}>{shortcut}</size></color>";
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

        Btn(ref y, Label("Dump Log", "CTRL + F1"), _sAction, () =>
        {
            Logger.Info("Log dumped", "ClientControlGUI");
            Utils.DumpLog();
        });
        Btn(ref y, Label("Reload Translations", "F5 + T"), _sAction, () =>
        {
            Logger.Info("Reloading Custom Translation File", "ClientControlGUI");
            LoadLangs();
            Logger.SendInGame("Reloaded Custom Translation File");
        });
        Btn(ref y, Label("Export Translations", "F5 + X"), _sAction, () =>
        {
            Logger.Info("Exported Custom Translation File", "ClientControlGUI");
            ExportCustomTranslation();
            Logger.SendInGame("Exported Custom Translation File");
        });
        if (!notJoined)
            Btn(ref y, Label("Copy Settings", "ALT + C"), _sAction, Utils.CopyCurrentSettings);

        Btn(ref y, Label("Fix Button Positions", "ALT + ENTER"), _sAction, () =>
            LateTask.New(SetResolutionManager.Postfix, 0.01f, "Fix Button Position")
        );

        if (inGame || inMeeting)
            Btn(ref y, Label("Fix Blackscreen", "SHIFT + CTRL + X"), _sAction, () =>
                ExileController.Instance?.ReEnableGameplay()
            );

        if (inGame && (canMove || inMeeting))
            Btn(ref y, Label(InGameRoleInfoMenu.Showing ? "Hide Role Info" : "Show Role Info", "F1 (hold)"), _sAction, () =>
            {
                if (InGameRoleInfoMenu.Showing)
                    InGameRoleInfoMenu.Hide();
                else
                {
                    InGameRoleInfoMenu.SetRoleInfoRef(PlayerControl.LocalPlayer);
                    InGameRoleInfoMenu.Show();
                }
            });

        bool canZoom = Zoom.CanZoom;
        bool canNoClip = canMove && (!AmongUsClient.Instance.IsGameStarted || !GameStates.IsOnlineGame);
        bool canToggleHud = Main.IntroDestroyed && !inMeeting && !ExileController.Instance && !ReportDeadBodyPatch.MeetingStarted;

        if (canZoom || canNoClip || canToggleHud)
        {
            Section(ref y, "Camera");

            if (canZoom)
            {
                // Sync slider to actual camera value so external changes (scroll wheel, touch pinch) are reflected
                if (_cam) _zoomValue = _cam.orthographicSize;

                float newZoom = Slider(ref y, $"Zoom  {_zoomValue:F1}x", _zoomValue, 3.0f, 18.0f, w);
                if (Mathf.Abs(newZoom - _zoomValue) > 0.01f)
                {
                    _zoomValue = newZoom;
                    Zoom.SetZoomSize(reset: false);
                    if (_cam) _cam.orthographicSize = _zoomValue;
                    if (HudManager.InstanceExists) HudManager.Instance.UICamera.orthographicSize = _zoomValue;
                }

                if (GUI.Button(new Rect(0, y, w, ButtonHeight), "Reset Zoom", _sAction))
                {
                    Zoom.SetZoomSize(reset: true);
                    _zoomValue = 3.0f;
                }
                y += ButtonHeight + Padding * 0.7f;
            }
            else if (!Mathf.Approximately(_zoomValue, 3.0f))
            {
                Zoom.SetZoomSize(reset: true);
                _zoomValue = 3.0f;
            }

            if (canNoClip)
            {
                // Reads live state every frame for correct label/colour; lambda also reads it on click to avoid stale values
                bool noclipOn = ControllerManagerUpdatePatch.NoClipEnabled;
                Btn(ref y, noclipOn ? "No-clip: ON" : "No-clip: OFF", noclipOn ? _sHost : _sAction, () =>
                {
                    ControllerManagerUpdatePatch.NoClipEnabled = !ControllerManagerUpdatePatch.NoClipEnabled;
                    if (OperatingSystem.IsAndroid()) PlayerControl.LocalPlayer.Collider.offset = ControllerManagerUpdatePatch.NoClipEnabled ? new Vector2(0f, 127f) : new Vector2(0f, -0.3636f);
                });
            }
            else if (OperatingSystem.IsAndroid() && PlayerControl.LocalPlayer) PlayerControl.LocalPlayer.Collider.offset = new Vector2(0f, -0.3636f);

            if (canToggleHud)
            {
                Btn(ref y, HudHidden ? "Show HUD" : "Hide HUD", HudHidden ? _sHost : _sAction, () =>
                {
                    HudHidden = !HudHidden;
                    if (HudManager.InstanceExists)
                        HudManager.Instance.gameObject.SetActive(!HudHidden);
                });
            }
            else if (HudHidden)
            {
                HudHidden = false;
                if (HudManager.InstanceExists)
                    HudManager.Instance.gameObject.SetActive(true);
            }
        }

        if (inLobby)
        {
            Section(ref y, "Lobby");

            if (amHost && !countdown)
                Btn(ref y, Label("Start Game", "ENTER"), _sHost, () =>
                {
                    if (GameStartManager.InstanceExists)
                    {
                        Logger.Info("Start game via ClientControlGUI", "ClientControlGUI");
                        GameStartManager.Instance.BeginGame();
                    }
                });

            if (amHost && countdown)
            {
                Btn(ref y, Label("Start Immediately", "SHIFT"), _sHost, () =>
                {
                    Logger.Info("Starting game immediately via ClientControlGUI", "ClientControlGUI");
                    GameStartManager.Instance.countDownTimer = 0;
                });
                Btn(ref y, Label("Cancel Countdown", "C"), _sDanger, () =>
                {
                    GameStartManager.Instance.ResetStartState();
                    Logger.SendInGame(GetString("CancelStartCountDown"));
                });
            }

            if (amHost)
            {
                Btn(ref y, Label("Show Active Settings", "CTRL + N"), _sHost, () =>
                {
                    Main.IsChatCommand = true;
                    Utils.ShowActiveSettings();
                });
                Btn(ref y, Label("Show Settings Help", "CTRL + SHIFT + N"), _sHost, () =>
                {
                    Main.IsChatCommand = true;
                    Utils.ShowActiveSettingsHelp();
                });
                Btn(ref y, Label("Reset All Options", "CTRL + SHIFT + DEL"), _sDanger, () =>
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
                Btn(ref y, Label("Kill Self", "SHIFT + ENTER + E"), _sDanger, () =>
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
                    Btn(ref y, Label("Call Meeting", "SHIFT + ENTER + M"), _sHost, () =>
                        PlayerControl.LocalPlayer.NoCheckStartMeeting(null, true)
                    );
                else
                {
                    Btn(ref y, Label("End Meeting", "SHIFT + ENTER + M"), _sHost, () =>
                    {
                        MeetingHudRpcClosePatch.AllowClose = true;
                        MeetingHud.Instance.RpcClose();
                    });
                    Btn(ref y, Label("End By Votes", "F6"), _sHost, () =>
                    {
                        CheckForEndVotingPatch.ShouldSkip = true;
                        MeetingHud.Instance.CheckForEndVoting();
                    });
                }

                Btn(ref y, Label("Open Your Chat", "SHIFT + ENTER + C"), _sHost, () =>
                    HudManager.Instance.Chat.SetVisible(true)
                );
                Btn(ref y, Label("Open Chat for All", "CTRL + SHIFT + ENTER + C"), _sHost, Utils.SetChatVisibleForAll);

                if (noGameEnd)
                    Btn(ref y, Label("Force Game End", "SHIFT + ENTER + L"), _sDanger, () =>
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
            sy += Padding * 2f;
            GUI.Label(new Rect(0, sy, w, ButtonHeight * 0.50f), title, _sSection);
            sy += ButtonHeight * 0.52f + Padding * 0.4f;
        }

        // Draws a button and moves the cursor; try/catch stops one bad action from crashing the GUI
        void Btn(ref float by, string label, GUIStyle style, Action action)
        {
            if (GUI.Button(new Rect(0, by, w, ButtonHeight), label, style))
            {
                try { action(); }
                catch (Exception e) { Logger.Error(e.ToString(), "ClientControlGUI"); }
            }
            by += ButtonHeight + Padding * 0.7f;
        }

        // Draws a labeled horizontal slider and returns the new value
        float Slider(ref float sy, string label, float value, float min, float max, float sw)
        {
            GUI.Label(new Rect(0, sy, sw, ButtonHeight * 0.45f), label, _sSection);
            sy += ButtonHeight * 0.48f;
            float result = GUI.HorizontalSlider(new Rect(0, sy, sw, ButtonHeight * 0.52f), value, min, max);
            sy += ButtonHeight * 0.52f + Padding * 0.7f;
            return result;
        }
    }
}
