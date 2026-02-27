using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace EHR.Modules;

public static class OnlinePresetsManager
{
    public static List<PresetMeta> CachedPresets = [];
    public static bool PresetsLoaded = false;

    public static IEnumerator CreatePresetExplorerUI(GameOptionsMenu menu)
    {
        float y = 2.0f;

        {
            CategoryHeaderMasked header = Object.Instantiate(
                menu.categoryHeaderOrigin,
                Vector3.zero,
                Quaternion.identity,
                menu.settingsContainer
            );

            header.SetHeader(StringNames.RolesCategory, 20);
            header.Title.DestroyTranslator();
            header.Title.text = "Upload & Publish";
            header.transform.localScale = Vector3.one * 0.63f;
            header.transform.localPosition = new(-0.903f, y, -2f);

            y -= 0.8f;

            StringOption upload = Object.Instantiate(menu.stringOptionOrigin, Vector3.zero, Quaternion.identity, menu.settingsContainer);
            upload.name = nameof(OnlinePresetsManager) + ";" + Translator.GetString("UploadPreset");
            upload.transform.localPosition = new Vector3(0.952f, y, -2f);
            upload.SetClickMask(menu.ButtonClickMask);
            upload.SetUpFromData(ScriptableObject.CreateInstance<StringGameSetting>(), 20);
            
            Object.Destroy(upload.transform.FindChild("Value_TMP (1)").gameObject);
            Object.Destroy(upload.transform.FindChild("ValueBox").gameObject);
            Object.Destroy(upload.PlusBtn.gameObject);

            upload.OnValueChanged = new Action<OptionBehaviour>(menu.ValueChanged);
            upload.MinusBtn.OnClick = new();
            upload.MinusBtn.OnClick.AddListener((UnityAction)(() => Main.Instance.StartCoroutine(UploadCurrentPreset())));
            TextMeshPro text = upload.MinusBtn.GetComponentInChildren<TextMeshPro>();
            text.DestroyTranslator();
            text.text = "↸";
            upload.TitleText.DestroyTranslator();
            upload.gameObject.SetActive(true);
            
            menu.Children.Add(upload);

            y -= 0.6f;
        }

        {
            CategoryHeaderMasked header = Object.Instantiate(
                menu.categoryHeaderOrigin,
                Vector3.zero,
                Quaternion.identity,
                menu.settingsContainer
            );

            header.SetHeader(StringNames.RolesCategory, 20);
            header.Title.DestroyTranslator();
            header.Title.text = "Preset Explorer";
            header.transform.localScale = Vector3.one * 0.63f;
            header.transform.localPosition = new(-0.903f, y, -2f);

            y -= 0.8f;
        }

        if (!PresetsLoaded) yield break;

        foreach (PresetMeta preset in CachedPresets)
        {
            StringOption row = Object.Instantiate(menu.stringOptionOrigin, Vector3.zero, Quaternion.identity, menu.settingsContainer);
            row.name = nameof(OnlinePresetsManager) + ";" + string.Format(Translator.GetString("OnlinePresetInfo"), preset.name, preset.author, preset.downloads);
            row.transform.localPosition = new Vector3(0.952f, y, -2f);
            row.SetClickMask(menu.ButtonClickMask);
            row.SetUpFromData(ScriptableObject.CreateInstance<StringGameSetting>(), 20);
            
            Object.Destroy(row.transform.FindChild("Value_TMP (1)").gameObject);
            Object.Destroy(row.transform.FindChild("ValueBox").gameObject);
            Object.Destroy(row.PlusBtn.gameObject);

            row.OnValueChanged = new Action<OptionBehaviour>(menu.ValueChanged);
            row.MinusBtn.OnClick = new();
            row.MinusBtn.OnClick.AddListener((UnityAction)(() =>
            {
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
                        GameObject.Find("Host Buttons").transform.FindChild("Edit").GetComponent<PassiveButton>().ReceiveClickDown();
                    }, 0.1f);
                    
                    LateTask.New(() =>
                    {
                        if (!GameStates.IsLobby || !GameSettingMenu.Instance) return;
                        const int index = (int)TabGroup.PresetExplorer + 3;
                        GameSettingMenu.Instance.ChangeTab(index, Controller.currentTouchType == Controller.TouchType.Joystick);
                        ModGameOptionsMenu.TabIndex = index;
                    }, 0.4f);
                });
                
            }));
            TextMeshPro text = row.MinusBtn.GetComponentInChildren<TextMeshPro>();
            text.DestroyTranslator();
            text.text = "▶";
            row.TitleText.DestroyTranslator();
            row.gameObject.SetActive(true);

            menu.Children.Add(row);

            y -= 0.6f;

            if (y < -10)
                yield return null;
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

        string json = JsonSerializer.Serialize(body);

        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        UnityWebRequest request = new UnityWebRequest("https://gurge44.pythonanywhere.com/presets/draft", "POST")
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
            response = JsonSerializer.Deserialize<PresetDraftResponse>(request.downloadHandler.text);
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
        string url = $"https://gurge44.pythonanywhere.com/publish?preset={draftId}";
    
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
        UnityWebRequest request = UnityWebRequest.Get($"https://gurge44.pythonanywhere.com/presets/{presetId}");

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
            response = JsonSerializer.Deserialize<PresetDownloadResponse>(request.downloadHandler.text);
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
        UnityWebRequest request = UnityWebRequest.Get("https://gurge44.pythonanywhere.com/presets/list");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Logger.SendInGame($"Preset list fetch failed: {request.error}");
            yield break;
        }

        PresetListResponse response;

        try
        {
            response = JsonSerializer.Deserialize<PresetListResponse>(request.downloadHandler.text.Trim());
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