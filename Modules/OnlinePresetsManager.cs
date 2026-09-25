using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

#if IL2CPP
using System.Text.Json;
#else
using Newtonsoft.Json;
#endif

namespace EHR.Modules;

public static class OnlinePresetsManager
{
    private static readonly Dictionary<int, StringOption> OptionBehaviourCache = [];
    private static readonly Dictionary<int, CategoryHeaderMasked> HeaderCache = [];
    public static List<PresetMeta> CachedPresets = [];
    public static bool PresetsLoaded = false;

    public static void CreatePresetExplorerUI(GameOptionsMenu menu)
    {
        if (!PresetsLoaded) return;

        float y = 2.0f;

        {
            CategoryHeaderMasked header = HeaderCache.TryGetValue(0, out CategoryHeaderMasked cache) ? cache : ModGameOptionsMenu.Track(Object.Instantiate(
                menu.categoryHeaderOrigin,
                Vector3.zero,
                Quaternion.identity,
                menu.settingsContainer
            ));
            HeaderCache[0] = header;

            header.SetHeader(StringNames.RolesCategory, 20);
            header.Title.DestroyTranslator();
            header.Title.text = Translator.GetString("UploadPresetHeader");
            header.transform.localScale = Vector3.one * 0.63f;
            header.transform.localPosition = new(-0.903f, y, -2f);

            y -= 0.8f;

            StringOption upload = OptionBehaviourCache.TryGetValue(-1, out StringOption uploadCache) ? uploadCache : ModGameOptionsMenu.Track(Object.Instantiate(menu.stringOptionOrigin, Vector3.zero, Quaternion.identity, menu.settingsContainer));
            upload.name = nameof(OnlinePresetsManager) + ";" + Translator.GetString("UploadPreset");
            upload.transform.localPosition = new Vector3(0.952f, y, -2f);
            upload.SetClickMask(menu.ButtonClickMask);
            upload.SetUpFromData(null, 20);
            
            Object.Destroy(upload.transform.Find("Value_TMP (1)").gameObject);
            Object.Destroy(upload.transform.Find("ValueBox").gameObject);
            Object.Destroy(upload.PlusBtn.gameObject);

#if IL2CPP
            upload.OnValueChanged = (Il2CppSystem.Action<OptionBehaviour>)(menu.ValueChanged);
#else
            upload.OnValueChanged = menu.ValueChanged;
#endif
            upload.MinusBtn.OnClick = new();
            upload.MinusBtn.OnClick.AddListener(() => Main.Instance.StartCoroutine(UploadCurrentPreset()));
            TextMeshPro text = upload.MinusBtn.GetComponentInChildren<TextMeshPro>();
            text.DestroyTranslator();
            text.text = "↸";
            upload.TitleText.DestroyTranslator();
            upload.gameObject.SetActive(true);
            
            menu.Children.Add(upload);
            OptionBehaviourCache[-1] = upload;

            y -= 0.6f;
        }

        {
            CategoryHeaderMasked header = HeaderCache.TryGetValue(1, out CategoryHeaderMasked cache) ? cache : ModGameOptionsMenu.Track(Object.Instantiate(
                menu.categoryHeaderOrigin,
                Vector3.zero,
                Quaternion.identity,
                menu.settingsContainer
            ));
            HeaderCache[1] = header;

            header.SetHeader(StringNames.RolesCategory, 20);
            header.Title.DestroyTranslator();
            header.Title.text = Translator.GetString("TabGroup.PresetExplorer");
            header.transform.localScale = Vector3.one * 0.63f;
            header.transform.localPosition = new(-0.903f, y, -2f);

            y -= 0.8f;
        }

        for (var index = 0; index < CachedPresets.Count; index++)
        {
            PresetMeta preset = CachedPresets[index];
            StringOption row = OptionBehaviourCache.TryGetValue(index, out StringOption cache) ? cache : ModGameOptionsMenu.Track(Object.Instantiate(menu.stringOptionOrigin, Vector3.zero, Quaternion.identity, menu.settingsContainer));
            row.name = $"{nameof(OnlinePresetsManager)};{string.Format(Translator.GetString("OnlinePresetInfo"), preset.name, preset.author, (Utils.TimeStamp - (long)preset.created_at) / 86400, preset.downloads)}";
            row.transform.localPosition = new Vector3(0.952f, y, -2f);
            row.SetClickMask(menu.ButtonClickMask);
            row.SetUpFromData(null, 20);

            Object.Destroy(row.transform.Find("Value_TMP (1)").gameObject);
            Object.Destroy(row.transform.Find("ValueBox").gameObject);

#if IL2CPP
            row.OnValueChanged = (Il2CppSystem.Action<OptionBehaviour>)(menu.ValueChanged);
#else
            row.OnValueChanged = menu.ValueChanged;
#endif
            row.LabelBackground.transform.localScale += new Vector3(1f, 0f, 0f);
            row.TitleText.GetComponent<RectTransform>().sizeDelta = new(5.7f, 0.37f);

            TextMeshPro plusText = row.PlusBtn.GetComponentInChildren<TextMeshPro>();
            plusText.DestroyTranslator();
            plusText.text = "ⓘ";
            row.PlusBtn.OnClick = new();
            row.PlusBtn.OnClick.AddListener(() =>
            {
                bool b = plusText.text == "ⓘ";
                GameObject.Find("PlayerOptionsMenu(Clone)").transform.Find("What Is This?").gameObject.SetActive(b);
                GameSettingMenuPatch.GMButtons.Values.Do(x => x.gameObject.SetActive(!b));
                if (b) GameSettingMenu.Instance.MenuDescriptionText.text = preset.description;
                plusText.text = b ? "∅" : "ⓘ";
            });

            row.MinusBtn.transform.localPosition += new Vector3(1.7f, 0f, 0f);
            row.MinusBtn.OnClick = new();
            row.MinusBtn.OnClick.AddListener(() =>
            {
                LateTask.New(() => { }, 0.01f);
                GameSettingMenu.Instance.Close();

                Prompt.Show(Translator.GetString("Promt.ApplyPreset"), () =>
                {
                    Logger.SendInGame(Translator.GetString("DownloadingPreset"));

                    Main.Instance.StartCoroutine(
                        DownloadPreset(preset.id, downloadedPreset =>
                        {
                            foreach ((int id, OptionItem optionItem) in OptionItem.FastOptions)
                            {
                                if (optionItem.IsSingleValue) continue;
                                if (!downloadedPreset.TryGetValue(id, out int newValue)) continue;
                                optionItem.SetValue(newValue, doSave: false, doSync: false);
                            }

                            OptionItem.SyncAllOptions();
                            OptionSaver.Save();

                            Logger.SendInGame(Translator.GetString("PresetApplied"), Color.green);
                        })
                    );
                }, () =>
                {
                    LateTask.New(() =>
                    {
                        if (!GameStates.IsLobby) return;
                        GameObject.Find("Host Buttons").transform.Find("Edit").GetComponent<PassiveButton>().ReceiveClickDown();
                    }, 0.1f);

                    LateTask.New(() =>
                    {
                        if (!GameStates.IsLobby || !GameSettingMenu.Instance) return;
                        const int tabIndex = (int)TabGroup.PresetExplorer + 3;
                        GameSettingMenu.Instance.ChangeTab(tabIndex, Controller.currentTouchType == Controller.TouchType.Joystick);
                        ModGameOptionsMenu.TabIndex = tabIndex;
                    }, 0.4f);
                }, showBackButton: false);
            });
            TextMeshPro minusText = row.MinusBtn.GetComponentInChildren<TextMeshPro>();
            minusText.DestroyTranslator();
            minusText.text = "▶";
            row.TitleText.DestroyTranslator();
            row.gameObject.SetActive(true);

            menu.Children.Add(row);
            OptionBehaviourCache[index] = row;

            y -= 0.6f;
        }

        menu.scrollBar.SetYBoundsMax(-y - 1.65f);
        
        menu.ControllerSelectable.Clear();

        foreach (UiElement x in menu.scrollBar.GetComponentsInChildren<UiElement>())
            menu.ControllerSelectable.Add(x);
    }

    private static IEnumerator UploadCurrentPreset()
    {
        Dictionary<int, int> preset = OptionItem.AllOptions.Where(x => !x.IsSingleValue).ToDictionary(x => x.Id, x => x.CurrentValue);
        
        PresetDraftRequest body = new PresetDraftRequest
        {
            friend_code = PlayerControl.LocalPlayer.Data.FriendCode,
            puid = PlayerControl.LocalPlayer.GetClient().ProductUserId,
            preset = preset
        };

#if IL2CPP
        string json = JsonSerializer.Serialize(body);
#else
        string json = JsonConvert.SerializeObject(body);
#endif

        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        UnityWebRequest request = new UnityWebRequest("https://app.gurge44.eu/presets/draft", "POST")
        {
            uploadHandler = new UploadHandlerRaw(bodyRaw),
            downloadHandler = new DownloadHandlerBuffer()
        };

        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Logger.SendInGame($"Preset upload failed: {request.error}");
            yield break;
        }

        PresetDraftResponse response;

        try
        {
#if IL2CPP
            response = JsonSerializer.Deserialize<PresetDraftResponse>(request.downloadHandler.text);
#else
            response = JsonConvert.DeserializeObject<PresetDraftResponse>(request.downloadHandler.text);
#endif
        }
        catch
        {
            Logger.SendInGame("Preset upload response parse failed");
            yield break;
        }

        if (!string.IsNullOrEmpty(response.error))
        {
            Logger.SendInGame($"Preset upload rejected: {response.error}");
            yield break;
        }

        if (string.IsNullOrEmpty(response.draft_id))
        {
            Logger.SendInGame("Preset upload failed: no draft_id");
            yield break;
        }

        OpenPublishPage(response.draft_id);
    }
    
    static void OpenPublishPage(string draftId)
    {
        string url = $"https://app.gurge44.eu/publish?preset={draftId}";
        
        if (OperatingSystem.IsAndroid()) 
        {
            Constants.OpenURL(url);
            return;
        }
    
        try
        {
            Process.Start(url);
        }
        catch
        {
            Application.OpenURL(url);
        }
    }

    private static IEnumerator DownloadPreset(string presetId, Action<Dictionary<int,int>> onSuccess)
    {
        UnityWebRequest request = UnityWebRequest.Get($"https://app.gurge44.eu/presets/{presetId}");

        request.timeout = 5;

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Logger.SendInGame($"Preset download failed: {request.error}");
            yield break;
        }

        PresetDownloadResponse response;

        try
        {
#if IL2CPP
            response = JsonSerializer.Deserialize<PresetDownloadResponse>(request.downloadHandler.text);
#else
            response = JsonConvert.DeserializeObject<PresetDownloadResponse>(request.downloadHandler.text);
#endif
        }
        catch
        {
            Logger.SendInGame("Preset JSON parse failed");
            yield break;
        }

        if (!string.IsNullOrEmpty(response.error))
        {
            Logger.SendInGame($"Preset rejected: {response.error}");
            yield break;
        }

        if (response.preset == null || string.IsNullOrEmpty(response.hash))
        {
            Logger.SendInGame("Preset invalid response");
            yield break;
        }

        onSuccess?.Invoke(response.preset);
    }
    
    public static IEnumerator FetchPresetList(Action<List<PresetMeta>> onSuccess)
    {
        UnityWebRequest request = UnityWebRequest.Get("https://app.gurge44.eu/presets/list");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Logger.SendInGame($"Preset list fetch failed: {request.error}");
            yield break;
        }

        PresetListResponse response;

        try
        {
#if IL2CPP
            response = JsonSerializer.Deserialize<PresetListResponse>(request.downloadHandler.text.Trim());
#else
            response = JsonConvert.DeserializeObject<PresetListResponse>(request.downloadHandler.text.Trim());
#endif
        }
        catch (Exception ex)
        {
            Logger.SendInGame("Preset list parse failed");
            Logger.Info(request.downloadHandler.text.Trim(), "Response");
            Utils.ThrowException(ex);
            yield break;
        }

        if (response?.presets == null)
        {
            Logger.SendInGame("Preset list empty or invalid");
            yield break;
        }

        onSuccess?.Invoke(response.presets.OrderByDescending(x => x.downloads).ToList());
    }
    
    // ReSharper disable all
    public class PresetDraftRequest
    {
        public string friend_code { get; set; }
        public string puid { get; set; }
        public Dictionary<int, int> preset { get; set; }
    }

    public class PresetDraftResponse
    {
        public string draft_id { get; set; }
        public string error { get; set; }
    }
    
    public class PresetDownloadResponse
    {
        public Dictionary<int, int> preset { get; set; }
        public string hash { get; set; }
        public string error { get; set; }
    }
    
    public class PresetMeta
    {
        public string id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string author { get; set; }
        public string hash { get; set; }
        public double created_at { get; set; }
        public int downloads { get; set; }
    }

    public class PresetListResponse
    {
        public List<PresetMeta> presets { get; set; }
    }
}