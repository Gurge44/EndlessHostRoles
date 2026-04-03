using System;
using System.Collections.Generic;
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

    // Scale helpers - everything is relative to a 1080px-wide reference screen.
    // On PC the UI is scaled down to 60% but on Android we keep it at full size for better readability.
    private static float PS  => OperatingSystem.IsAndroid() ? 1.0f : 0.5f;  // PS: platform scale multiplier (Android full size, PC smaller)
    private static float S   => Screen.width / 1080f * PS;                  // S: global scale factor
    private static int   FS  => Mathf.Max(12, Mathf.RoundToInt(21f * S));   // FS: font size
    private static float BH  => 66f * S;                                    // BH: button height
    private static float BW  => 340f * S;                                   // BW: button width
    private static float P   => 10f * S;                                    // P: padding

    // Scrollbar column width - kept intentionally small so it never causes horizontal overflow
    private static float SBW => 22f * S;

    // Used to detect when a rebuild is needed
    private float _lastS = -1f;
    private string _lastScene = "";

    // GUIStyle holds font, colors, and the background texture for each widget type.
    private GUIStyle _sAction, _sHost, _sDanger, _sSection, _sToggle, _sWindow, _sTitleBar, _sDragHint;

    // All textures created go here so we can destroy them properly on scene change
    private readonly List<Texture2D> _textures = [];
    private Action<Scene, LoadSceneMode> _sceneLoadedHandler;

    // Credit: Xtracube for pointing out add_sceneLoaded workaround.
    // Subscribes to scene load events so we can rebuild styles after transitions.
    private void Awake()
    {
        Instance = this;
        _sceneLoadedHandler = OnSceneLoaded;
        SceneManager.add_sceneLoaded(_sceneLoadedHandler);
        Logger.Info("ClientControlGUI initialised", "ClientControlGUI");
    }

    // Unsubscribes the scene load handler and cleans up textures when the component is removed.
    private void OnDestroy()
    {
        if (_sceneLoadedHandler != null)
            SceneManager.remove_sceneLoaded(_sceneLoadedHandler);

        DestroyTextures();
    }

    // Called by Unity whenever a scene loads (e.g. lobby -> game).
    // Scene transitions destroy GPU-side texture data, so we reset the scale tracker to force a full style rebuild on the next OnGUI call.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _lastS = -1f;
        DestroyTextures();
    }

    // Destroys all tracked textures and nulls out every GUIStyle.
    // Nulling the styles causes StylesValid() to return false, which triggers RebuildStyles() on the next OnGUI call.
    private void DestroyTextures()
    {
        foreach (var t in _textures)
            if (t) Destroy(t);
        _textures.Clear();

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

    // Creates all GUIStyles. Called on first draw and after every scene change.
    private void RebuildStyles()
    {
        _lastS = S;
        DestroyTextures();

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

    // Builds a button GUIStyle with three states: normal, hovered, pressed.
    // The background texture is a rounded rectangle drawn pixel-by-pixel.
    // Uses a higher-res texture and larger radius for visibly smooth corners.
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
            normal    = { background = T(w, h, r, normal,  Lift(normal,  0.10f)), textColor = Color.white },
            hover     = { background = T(w, h, r, hover,   Lift(hover,   0.10f)), textColor = Color.white },
            active    = { background = T(w, h, r, active,  Lift(active,  0.06f)), textColor = Color.white }
        };
    }

    // Slightly brightens a color for the edge highlight on rounded textures
    private static Color Lift(Color c, float v) =>
        new(Mathf.Clamp01(c.r + v), Mathf.Clamp01(c.g + v), Mathf.Clamp01(c.b + v), 1f);

    // Creates a rounded-rect texture and registers it for cleanup
    private Texture2D T(int w, int h, int r, Color fill, Color edge)
    {
        var tex = RoundedTex(w, h, r, fill, edge);
        _textures.Add(tex);
        return tex;
    }

    // Draws a texture where corners are rounded using per-pixel distance checks.
    // 'fill' is the interior color, 'edge' is used for a subtle 1px anti-aliased rim.
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

    // Returns 0 (transparent) if the pixel is outside a corner arc, 1 if inside, or a
    // fractional value for the single-pixel anti-aliased transition on the edge.
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

    // Returns false if any style or its background texture is missing.
    // This happens after a scene load destroys the textures.
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

    // OnGUI is called by Unity every frame (and sometimes multiple times per frame).
    private void OnGUI()
    {
        if (!HudManager.InstanceExists) return;
        if (!_windowInitialized) InitWindowRect();

        // Rebuild styles if the screen scale changed or a scene transition wiped the textures
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

    // Draws the small square button that opens/closes the panel.
    // Fades to 10% opacity during gameplay
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
            // Bottom-left center: horizontally centred on the left quarter of the screen,
            x = Screen.width * 0.3f - size * 0.5f;
            y = Screen.height - size - 10f * S;
        }

        // 90% transparent when in game and panel is closed, full opacity otherwise
        bool fadeOut = !IsOpen && GameStates.IsInGame;
        Color prev = GUI.color;
        if (fadeOut) GUI.color = new Color(1f, 1f, 1f, 0.10f);

        if (GUI.Button(new Rect(x, y, size, size), IsOpen ? "X" : "=", _sToggle))
            IsOpen = !IsOpen;

        if (fadeOut) GUI.color = prev;
    }

    // Handles dragging the window by its title bar area.
    // ImGUI doesn't have built-in window dragging, so we do it manually with mouse events.
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
        // Content width is set to outerRect.width - SBW - 1
        // Keeping it strictly less than outerRect.width guarantees Unity never calculates horizontal overflow, which is what
        // causes the horizontal scrollbar to appear regardless of the alwaysShowHorizontal flag.
        float contentW = visibleW - SBW - 1f;
        var outerRect = new Rect(_windowRect.x + P, scrollY, visibleW, scrollH);
        var innerRect = new Rect(0, 0, contentW, _contentH);

        GUI.skin.verticalScrollbar.fixedWidth      = SBW;
        GUI.skin.verticalScrollbarThumb.fixedWidth = SBW;

        // Both horizontal flags false, so no horizontal scrollbar.
        // Vertical scrollbar appears automatically when _contentH > scrollH.
        _scroll = GUI.BeginScrollView(outerRect, _scroll, innerRect, false, false);
        float y = P * 0.5f;
        DrawButtons(ref y, contentW);
        _contentH = y + P; // record total height so innerRect stays accurate next frame
        GUI.EndScrollView();
    }

    // Draws all buttons. Only shows buttons relevant to the current game state.
    // 'y' is passed by ref so each button advances the cursor downward automatically.
    // 'w' is the available width, passed in from DrawWindow to match innerRect exactly.
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

        Btn(ref y, "Dump Log", _sAction, () =>
        {
            Logger.Info("Log dumped", "ClientControlGUI");
            Utils.DumpLog();
        });
        Btn(ref y, "Reload Translations", _sAction, () =>
        {
            Logger.Info("Reloading Custom Translation File", "ClientControlGUI");
            LoadLangs();
            Logger.SendInGame("Reloaded Custom Translation File");
        });
        Btn(ref y, "Export Translations", _sAction, () =>
        {
            Logger.Info("Exported Custom Translation File", "ClientControlGUI");
            ExportCustomTranslation();
            Logger.SendInGame("Exported Custom Translation File");
        });
        if (!notJoined)
            Btn(ref y, "Copy Settings", _sAction, Utils.CopyCurrentSettings);

        Btn(ref y, "Fix Button Positions", _sAction, () =>
            LateTask.New(SetResolutionManager.Postfix, 0.01f, "Fix Button Position")
        );

        if (inGame || inMeeting)
            Btn(ref y, "Fix Blackscreen", _sAction, () =>
                ExileController.Instance?.ReEnableGameplay()
            );

        if (inGame && (canMove || inMeeting))
            Btn(ref y, InGameRoleInfoMenu.Showing ? "Hide Role Info" : "Show Role Info", _sAction, () =>
            {
                if (InGameRoleInfoMenu.Showing)
                    InGameRoleInfoMenu.Hide();
                else
                {
                    InGameRoleInfoMenu.SetRoleInfoRef(PlayerControl.LocalPlayer);
                    InGameRoleInfoMenu.Show();
                }
            });

        if (inLobby)
        {
            Section(ref y, "Lobby");

            if (amHost && !countdown)
                Btn(ref y, "Start Game", _sHost, () =>
                {
                    if (GameStartManager.InstanceExists)
                    {
                        Logger.Info("Start game via ClientControlGUI", "ClientControlGUI");
                        GameStartManager.Instance.BeginGame();
                    }
                });

            if (amHost && countdown)
            {
                Btn(ref y, "Start Immediately", _sHost, () =>
                {
                    Logger.Info("Starting game immediately via ClientControlGUI", "ClientControlGUI");
                    GameStartManager.Instance.countDownTimer = 0;
                });
                Btn(ref y, "Cancel Countdown", _sDanger, () =>
                {
                    GameStartManager.Instance.ResetStartState();
                    Logger.SendInGame(GetString("CancelStartCountDown"));
                });
            }

            if (amHost)
            {
                Btn(ref y, "Show Active Settings", _sHost, () =>
                {
                    Main.IsChatCommand = true;
                    Utils.ShowActiveSettings();
                });
                Btn(ref y, "Show Settings Help", _sHost, () =>
                {
                    Main.IsChatCommand = true;
                    Utils.ShowActiveSettingsHelp();
                });
                Btn(ref y, "Reset All Options", _sDanger, () =>
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
                Btn(ref y, "Kill Self", _sDanger, () =>
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
                    Btn(ref y, "Call Meeting", _sHost, () =>
                        PlayerControl.LocalPlayer.NoCheckStartMeeting(null, true)
                    );
                else
                    Btn(ref y, "End Meeting", _sHost, () =>
                    {
                        MeetingHudRpcClosePatch.AllowClose = true;
                        MeetingHud.Instance.RpcClose();
                    });

                Btn(ref y, "Open Your Chat", _sHost, () =>
                    HudManager.Instance.Chat.SetVisible(true)
                );
                Btn(ref y, "Open Chat for All", _sHost, Utils.SetChatVisibleForAll);

                if (noGameEnd)
                    Btn(ref y, "Force Game End", _sDanger, () =>
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

        // Draws a button and advances the cursor. try/catch prevents one bad action from crashing the GUI.
        void Btn(ref float by, string label, GUIStyle style, Action action)
        {
            if (GUI.Button(new Rect(0, by, w, BH), label, style))
            {
                try { action(); }
                catch (Exception e) { Logger.Error(e.ToString(), "ClientControlGUI"); }
            }
            by += BH + P * 0.7f;
        }
    }

}
