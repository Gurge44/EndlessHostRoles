using System;
using TMPro;
using UnityEngine;

namespace EHR;

public class SimpleButton
{
    private static PassiveButton BaseButton;
    private readonly BoxCollider2D buttonCollider;
    private float _fontSize;
    private Vector2 _scale;

    /// <summary>Creates a new button</summary>
    /// <param name="parent">Parent object</param>
    /// <param name="name">Object name</param>
    /// <param name="normalColor">Background color in normal state</param>
    /// <param name="hoverColor">Background color when mouse hovers</param>
    /// <param name="action">Action triggered on click</param>
    /// <param name="label">Button label</param>
    /// <param name="localPosition">Button position</param>
    /// <param name="isActive">Whether to be active initially (default true)</param>
    public SimpleButton(
        Transform parent,
        string name,
        Vector3 localPosition,
        Color32 normalColor,
        Color32 hoverColor,
        Action action,
        string label,
        bool isActive = true)
    {
        if (!BaseButton) throw new InvalidOperationException("baseButton is null or destroyed");

        Button = Object.Instantiate(BaseButton, parent);
        Label = Button.transform.Find("FontPlacer/Text_TMP").GetComponent<TextMeshPro>();
        NormalSprite = Button.inactiveSprites.GetComponent<SpriteRenderer>();
        HoverSprite = Button.activeSprites.GetComponent<SpriteRenderer>();
        buttonCollider = Button.GetComponent<BoxCollider2D>();

        // Center the label
        Transform container = Label.transform.parent;
        Object.Destroy(Label.GetComponent<AspectPosition>());
        container.SetLocalX(0f);
        Label.transform.SetLocalX(0f);
        Label.horizontalAlignment = HorizontalAlignmentOptions.Center;

        Button.name = name;
        Button.transform.localPosition = localPosition;
        NormalSprite.color = normalColor;
        HoverSprite.color = hoverColor;
        Button.OnClick.AddListener(action);
        Label.text = label;
        Button.gameObject.SetActive(isActive);
    }

    public PassiveButton Button { get; }
    public TextMeshPro Label { get; }
    public SpriteRenderer NormalSprite { get; }
    public SpriteRenderer HoverSprite { get; }

    public Vector2 Scale
    {
        get => _scale;
        set => _scale = NormalSprite.size = HoverSprite.size = buttonCollider.size = value;
    }

    public float FontSize
    {
        get => _fontSize;
        set => _fontSize = Label.fontSize = Label.fontSizeMin = Label.fontSizeMax = value;
    }

    public static void SetBase(PassiveButton passiveButton)
    {
        if (BaseButton || !passiveButton) return;

        BaseButton = Object.Instantiate(passiveButton);
        var label = BaseButton.transform.Find("FontPlacer/Text_TMP").GetComponent<TextMeshPro>();
        BaseButton.gameObject.SetActive(false);

        Object.DontDestroyOnLoad(BaseButton);
        BaseButton.name = "EHR_SimpleButtonBase";

        Object.Destroy(BaseButton.GetComponent<AspectPosition>());
        label.DestroyTranslator();
        label.fontSize = label.fontSizeMax = label.fontSizeMin = 3.5f;
        label.enableWordWrapping = false;
        label.text = "EHR SIMPLE BUTTON BASE";

        var buttonCollider = BaseButton.GetComponent<BoxCollider2D>();
        buttonCollider.offset = new(0f, 0f);
        BaseButton.OnClick = new();
    }

/*
    public static bool IsNullOrDestroyed(SimpleButton button)
    {
        return button == null || button.Button == null;
    }
*/
}