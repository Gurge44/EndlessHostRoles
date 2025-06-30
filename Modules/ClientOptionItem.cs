using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace EHR;

// Credit: https://github.com/tukasa0001/TownOfHost/pull/1265
public class ClientOptionItem
{
    public static SpriteRenderer CustomBackground;
    private static List<ToggleButtonBehaviour> OptionButtons;
    private readonly ConfigEntry<bool> Config;
    public readonly ToggleButtonBehaviour ToggleButton;

    private ClientOptionItem(
        string name,
        ConfigEntry<bool> config,
        OptionsMenuBehaviour optionsMenuBehaviour,
        Action additionalOnClickAction = null)
    {
        try
        {
            Config = config;

            ToggleButtonBehaviour mouseMoveToggle = optionsMenuBehaviour.DisableMouseMovement;

            if (CustomBackground == null)
            {
                CustomBackground = Object.Instantiate(optionsMenuBehaviour.Background, optionsMenuBehaviour.transform);
                CustomBackground.name = "CustomBackground";
                CustomBackground.transform.localScale = new(0.9f, 0.9f, 1f);
                CustomBackground.transform.localPosition += Vector3.back * 8;
                CustomBackground.size += new Vector2(3f, 0f);
                CustomBackground.gameObject.SetActive(false);

                ToggleButtonBehaviour closeButton = Object.Instantiate(mouseMoveToggle, CustomBackground.transform);
                closeButton.transform.localPosition = new(2.6f, -2.3f, -6f);
                closeButton.name = "Back";
                closeButton.Text.text = Translator.GetString("Back");
                closeButton.Background.color = Palette.DisabledGrey;
                var closePassiveButton = closeButton.GetComponent<PassiveButton>();
                closePassiveButton.OnClick = new();
                closePassiveButton.OnClick.AddListener(new Action(() => { CustomBackground.gameObject.SetActive(false); }));

                UiElement[] selectableButtons = optionsMenuBehaviour.ControllerSelectable.ToArray();
                PassiveButton leaveButton = null;
                PassiveButton returnButton = null;

                foreach (UiElement button in selectableButtons)
                {
                    if (button == null) continue;

                    switch (button.name)
                    {
                        case "LeaveGameButton":
                            leaveButton = button.GetComponent<PassiveButton>();
                            break;
                        case "ReturnToGameButton":
                            returnButton = button.GetComponent<PassiveButton>();
                            break;
                    }
                }

                Transform generalTab = mouseMoveToggle.transform.parent.parent.parent;

                ToggleButtonBehaviour modOptionsButton = Object.Instantiate(mouseMoveToggle, generalTab);
                modOptionsButton.transform.localPosition = leaveButton?.transform.localPosition ?? new(0f, -2.4f, 1f);
                modOptionsButton.name = "EHROptions";
                modOptionsButton.Text.text = Translator.GetString("EHROptions");
                modOptionsButton.Background.color = new Color32(0, 165, 255, byte.MaxValue);
                var modOptionsPassiveButton = modOptionsButton.GetComponent<PassiveButton>();
                modOptionsPassiveButton.OnClick = new();
                modOptionsPassiveButton.OnClick.AddListener(new Action(() =>
                {
                    AdjustButtonPositions();
                    CustomBackground.gameObject.SetActive(true);
                }));

                if (leaveButton != null && leaveButton.transform != null) leaveButton.transform.localPosition = new(-1.35f, -2.411f, -1f);
                if (returnButton != null) returnButton.transform.localPosition = new(1.35f, -2.411f, -1f);
                OptionButtons = [];
            }

            ToggleButton = Object.Instantiate(mouseMoveToggle, CustomBackground.transform);
            OptionButtons.Add(ToggleButton);

            ToggleButton.transform.localPosition = new(
                (OptionButtons.Count - 1) % 3 == 0 ? -2.6f : ((OptionButtons.Count - 1) % 3 == 1 ? 0f : 2.6f),
                           2.2f - (0.5f * ((OptionButtons.Count - 1) / 3)),
                           -6f);

            ToggleButton.name = name;
            ToggleButton.Text.text = Translator.GetString(name);
            var passiveButton = ToggleButton.GetComponent<PassiveButton>();
            passiveButton.OnClick = new();

            passiveButton.OnClick.AddListener(new Action(() =>
            {
                if (config != null) config.Value = !config.Value;

                UpdateToggle();
                additionalOnClickAction?.Invoke();
            }));

            UpdateToggle();
        }
        catch (Exception e)
        {
            Logger.Error(e.ToString(), "ClientOptionItem.Create");
        }
    }

    public static ClientOptionItem Create(
        string name,
        ConfigEntry<bool> config,
        OptionsMenuBehaviour optionsMenuBehaviour,
        Action additionalOnClickAction = null)
    {
        return new(name, config, optionsMenuBehaviour, additionalOnClickAction);
    }

    private static void AdjustButtonPositions()
    {
        if (OptionButtons == null || OptionButtons.Count == 0) return;

        int totalRows = (OptionButtons.Count + 2) / 3;

        float topPosition = 2.2f;
        float bottomLimit = -1.6f;
        float availableHeight = topPosition - bottomLimit;
        float rowSpacing = totalRows > 1 ? availableHeight / (totalRows - 1) : 0f;

        for (int i = 0; i < OptionButtons.Count; i++)
        {
            var button = OptionButtons[i];
            if (button == null) continue;

            int row = i / 3;
            int col = i % 3;

            float xPos = col switch
            {
                0 => -2.6f,
                1 => 0f,
                _ => 2.6f
            };

            float yPos = topPosition - (row * rowSpacing);

            button.transform.localPosition = new Vector3(xPos, yPos, -6f);
        }
    }

    public void UpdateToggle()
    {
        if (ToggleButton == null) return;

        Color32 color = Config is { Value: true } ? new(0, 165, 255, byte.MaxValue) : new Color32(77, 77, 77, byte.MaxValue);
        ToggleButton.Background.color = color;
        ToggleButton.Rollover?.ChangeOutColor(color);
    }
}
