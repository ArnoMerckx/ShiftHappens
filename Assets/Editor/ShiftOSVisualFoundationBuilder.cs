using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ShiftOSVisualFoundationBuilder
{
    private const string PrefabRoot = "Assets/Prefabs/ShiftOS/";
    private const string IconRoot = "Assets/Assets/ShiftOS/Icons/";

    [MenuItem("Shift Happens/Visual Design/Build Shared Foundation")]
    public static void BuildSharedFoundation()
    {
        StyleBaseWindow();
        StyleTaskbarButton();
        StyleDocumentRow();
        StyleDocumentReader();
        AssignIcons();
        AssetDatabase.SaveAssets();
        Debug.Log("ShiftOS shared visual foundation built.");
    }

    [MenuItem("Shift Happens/Visual Design/Build TickTock")]
    public static void BuildTickTock()
    {
        EditPrefab("TickTockContent.prefab", root =>
        {
            Image background = Require<Image>(root);
            background.color = ShiftOSVisualTokens.DesktopInk;

            VerticalLayoutGroup layout = Require<VerticalLayoutGroup>(root);
            layout.padding = new RectOffset(18, 18, 14, 14);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;

            AddOrUpdateHeader(root.transform, "TimerHeader", "DURATION TIMER", "ELAPSED REALITY IS NOT AVAILABLE HERE", ShiftOSVisualTokens.TickTock);

            TMP_Text remaining = Find(root.transform, "RemainingLabel").GetComponent<TMP_Text>();
            remaining.fontSize = ShiftOSVisualTokens.NumericSize;
            remaining.fontStyle = FontStyles.Bold;
            remaining.color = ShiftOSVisualTokens.TickTock;
            remaining.characterSpacing = 8f;
            remaining.alignment = TextAlignmentOptions.Center;
            SetLayout(remaining.gameObject, 84f, 1f);
            AddOrGet<Image>(remaining.gameObject).color = ShiftOSVisualTokens.Field;

            Transform durationRow = Find(root.transform, "DurationRow");
            Image durationPanel = AddOrGet<Image>(durationRow.gameObject);
            durationPanel.color = ShiftOSVisualTokens.PanelSlate;
            HorizontalLayoutGroup durationLayout = Require<HorizontalLayoutGroup>(durationRow.gameObject);
            durationLayout.padding = new RectOffset(14, 14, 12, 12);
            durationLayout.spacing = 8f;
            durationLayout.childAlignment = TextAnchor.MiddleCenter;
            SetLayout(durationRow.gameObject, 68f, 0f);

            StyleInput(Find(root.transform, "MinutesInput").GetComponent<TMP_InputField>(), "00", ShiftOSVisualTokens.TickTock);
            StyleInput(Find(root.transform, "SecondsInput").GetComponent<TMP_InputField>(), "00", ShiftOSVisualTokens.TickTock);
            StyleText(Find(root.transform, "MinutesUnit").GetComponent<TMP_Text>(), ShiftOSVisualTokens.TextMuted, ShiftOSVisualTokens.MetadataSize, FontStyles.UpperCase);
            StyleText(Find(root.transform, "SecondsUnit").GetComponent<TMP_Text>(), ShiftOSVisualTokens.TextMuted, ShiftOSVisualTokens.MetadataSize, FontStyles.UpperCase);

            Transform controls = Find(root.transform, "ControlsRow");
            HorizontalLayoutGroup controlsLayout = Require<HorizontalLayoutGroup>(controls.gameObject);
            controlsLayout.spacing = 8f;
            controlsLayout.padding = new RectOffset(0, 0, 2, 2);
            controlsLayout.childAlignment = TextAnchor.MiddleCenter;
            SetLayout(controls.gameObject, 46f, 0f);

            StyleButton(Find(root.transform, "StartButton").GetComponent<Button>(), ShiftOSVisualTokens.TickTock, true);
            StyleButton(Find(root.transform, "PauseResumeButton").GetComponent<Button>(), ShiftOSVisualTokens.RaisedPanel, false);
            StyleButton(Find(root.transform, "ResetButton").GetComponent<Button>(), ShiftOSVisualTokens.RaisedPanel, false);

            TMP_Text feedback = Find(root.transform, "FeedbackLabel").GetComponent<TMP_Text>();
            StyleText(feedback, ShiftOSVisualTokens.TextMuted, ShiftOSVisualTokens.MetadataSize, FontStyles.Normal);
            feedback.alignment = TextAlignmentOptions.Center;
            SetLayout(feedback.gameObject, 26f, 0f);

            AddOrUpdateFooter(root.transform, "TimerFooter", "DURATION ONLY  /  CHECK THE WALL CLOCK FOR REALITY", ShiftOSVisualTokens.TickTock);
        });

        AssetDatabase.SaveAssets();
        Debug.Log("TickTock visual pass built without changing serialized view references.");
    }

    [MenuItem("Shift Happens/Visual Design/Build LinkedOut")]
    public static void BuildLinkedOut()
    {
        EditPrefab("ShareLessContent.prefab", root =>
        {
            Image rootImage = AddOrGet<Image>(root);
            rootImage.color = ShiftOSVisualTokens.DesktopInk;

            Transform inbox = Find(root.transform, "Inbox");
            RectTransform inboxRect = (RectTransform)inbox;
            inboxRect.anchorMin = new Vector2(0f, 0f);
            inboxRect.anchorMax = new Vector2(0.36f, 1f);
            inboxRect.offsetMin = new Vector2(12f, 12f);
            inboxRect.offsetMax = new Vector2(-6f, -64f);
            inbox.name = "PersonnelIndex";
            Require<Image>(inbox.gameObject).color = ShiftOSVisualTokens.PanelSlate;
            AddOrUpdateRailHeader(inbox, "DIRECTORY", "ACTIVE PERSONNEL", ShiftOSVisualTokens.LinkedOut);

            Transform reader = FindComponentInChildren<DocumentReaderView>(root).transform;
            RectTransform readerRect = (RectTransform)reader;
            readerRect.anchorMin = new Vector2(0.36f, 0f);
            readerRect.anchorMax = new Vector2(1f, 1f);
            readerRect.offsetMin = new Vector2(6f, 12f);
            readerRect.offsetMax = new Vector2(-12f, -64f);
            Require<Image>(reader.gameObject).color = new Color(0.94f, 0.97f, 0.98f, 1f);

            AddOrUpdateAppBanner(root.transform, "LinkedOutBanner", "LINKEDOUT", "PERSONNEL INTELLIGENCE  /  INTERNAL USE", ShiftOSVisualTokens.LinkedOut);
            AddOrUpdateVerticalDivider(root.transform, "DossierDivider", 0.36f, ShiftOSVisualTokens.LinkedOut);

            TMP_Text title = Find(reader, "TitleLabel").GetComponent<TMP_Text>();
            title.text = "EMPLOYEE DOSSIER";
            title.color = new Color(0.08f, 0.16f, 0.25f, 1f);
            title.fontSize = 24f;
            title.fontStyle = FontStyles.Bold;

            TMP_Text body = Find(reader, "BodyLabel").GetComponent<TMP_Text>();
            body.color = new Color(0.16f, 0.23f, 0.31f, 1f);
            body.fontSize = ShiftOSVisualTokens.BodySize;
            body.lineSpacing = 10f;
        });

        EditPrefab("DocumentListRow.prefab", root =>
        {
            Image image = Require<Image>(root);
            image.color = ShiftOSVisualTokens.RaisedPanel;
            SetLayout(root, 70f, 1f);
            StyleButton(Require<Button>(root), ShiftOSVisualTokens.RaisedPanel, false);

            TMP_Text title = Find(root.transform, "TitleLabel").GetComponent<TMP_Text>();
            StyleText(title, ShiftOSVisualTokens.TextPrimary, 15f, FontStyles.Bold);
            title.margin = new Vector4(18f, 0f, 10f, 0f);

            TMP_Text status = Find(root.transform, "StatusLabel").GetComponent<TMP_Text>();
            StyleText(status, ShiftOSVisualTokens.LinkedOut, ShiftOSVisualTokens.MetadataSize, FontStyles.UpperCase);
            status.margin = new Vector4(18f, 0f, 10f, 0f);

            AddOrUpdateAccent(root.transform, "PersonnelAccent", ShiftOSVisualTokens.LinkedOut);
        });

        AssetDatabase.SaveAssets();
        Debug.Log("LinkedOut dossier visual pass built without changing data or interactions.");
    }

    public static void BuildAllForBatchMode()
    {
        BuildSharedFoundation();
        BuildTickTock();
        BuildLinkedOut();
    }

    private static void StyleBaseWindow()
    {
        EditPrefab("BaseWindow.prefab", root =>
        {
            Require<Image>(root).color = ShiftOSVisualTokens.DesktopInk;
            Transform titleBar = Find(root.transform, "TitleBar");
            Require<Image>(titleBar.gameObject).color = ShiftOSVisualTokens.PanelSlate;

            TMP_Text title = Find(titleBar, "TitleLabel").GetComponent<TMP_Text>();
            StyleText(title, ShiftOSVisualTokens.TextPrimary, ShiftOSVisualTokens.AppTitleSize, FontStyles.Bold);
            title.characterSpacing = 2f;

            Transform contentRoot = Find(root.transform, "ContentRoot");
            Require<Image>(contentRoot.gameObject).color = ShiftOSVisualTokens.DesktopInk;

            foreach (string name in new[] { "MinimizeButton", "MaximizeButton", "CloseButton" })
            {
                Button button = Find(root.transform, name).GetComponent<Button>();
                StyleButton(button, name == "CloseButton" ? ShiftOSVisualTokens.Error : ShiftOSVisualTokens.RaisedPanel, false);
            }

            AddOrUpdateAccent(titleBar, "SystemAccent", ShiftOSVisualTokens.Focus);
        });
    }

    private static void StyleTaskbarButton()
    {
        EditPrefab("TaskBarButton.prefab", root =>
        {
            Require<Image>(root).color = ShiftOSVisualTokens.PanelSlate;
            StyleButton(Require<Button>(root), ShiftOSVisualTokens.PanelSlate, false);
            TMP_Text label = Find(root.transform, "Label").GetComponent<TMP_Text>();
            StyleText(label, ShiftOSVisualTokens.TextPrimary, 12f, FontStyles.Normal);
            Transform indicator = Find(root.transform, "ActiveIndicator");
            Require<Image>(indicator.gameObject).color = ShiftOSVisualTokens.Focus;
        });
    }

    private static void StyleDocumentRow()
    {
        EditPrefab("DocumentListRow.prefab", root =>
        {
            Require<Image>(root).color = ShiftOSVisualTokens.RaisedPanel;
            StyleButton(Require<Button>(root), ShiftOSVisualTokens.RaisedPanel, false);
            StyleText(Find(root.transform, "TitleLabel").GetComponent<TMP_Text>(), ShiftOSVisualTokens.TextPrimary, 15f, FontStyles.Bold);
            StyleText(Find(root.transform, "StatusLabel").GetComponent<TMP_Text>(), ShiftOSVisualTokens.TextMuted, ShiftOSVisualTokens.MetadataSize, FontStyles.UpperCase);
        });
    }

    private static void StyleDocumentReader()
    {
        EditPrefab("DocumentReader.prefab", root =>
        {
            Require<Image>(root).color = new Color(0.94f, 0.96f, 0.97f, 1f);
            TMP_Text title = Find(root.transform, "TitleLabel").GetComponent<TMP_Text>();
            StyleText(title, new Color(0.08f, 0.13f, 0.2f, 1f), 22f, FontStyles.Bold);
            TMP_Text body = Find(root.transform, "BodyLabel").GetComponent<TMP_Text>();
            StyleText(body, new Color(0.15f, 0.2f, 0.27f, 1f), ShiftOSVisualTokens.BodySize, FontStyles.Normal);
            body.lineSpacing = 7f;
        });
    }

    private static void AssignIcons()
    {
        (string asset, string icon, Color color, int motif)[] assignments =
        {
            ("TickTock.asset", "ticktock", ShiftOSVisualTokens.TickTock, 0),
            ("ShareLess.asset", "linkedout", ShiftOSVisualTokens.LinkedOut, 1),
            ("InWatch.asset", "inwatch", ShiftOSVisualTokens.InWatch, 2),
            ("Swarms.asset", "swarms", ShiftOSVisualTokens.Swarms, 3),
            ("Excess.asset", "excess", ShiftOSVisualTokens.Excess, 4),
            ("Deleg8.asset", "deleg8", ShiftOSVisualTokens.Deleg8, 5),
            ("BlamePoint.asset", "blamepoint", ShiftOSVisualTokens.BlamePoint, 6)
        };

        foreach ((string asset, string icon, Color color, int motif) in assignments)
        {
            AppDefinition definition = AssetDatabase.LoadAssetAtPath<AppDefinition>("Assets/Scripts/ShiftOSSystem/ShiftOSObjects/Applications/" + asset);
            Sprite sprite = GetOrCreateIcon(icon, color, motif);
            if (definition == null || sprite == null)
                throw new InvalidOperationException($"Missing ShiftOS icon assignment source: {asset} / {icon}");

            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("icon").objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }
    }

    private static Sprite GetOrCreateIcon(string name, Color color, int motif)
    {
        const string generatedRoot = IconRoot + "Generated/";
        string path = generatedRoot + name + ".asset";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null)
            return existing;

        if (!AssetDatabase.IsValidFolder(generatedRoot.TrimEnd('/')))
        {
            if (!AssetDatabase.IsValidFolder(IconRoot.TrimEnd('/')))
                throw new InvalidOperationException("The ShiftOS icon source folder is missing.");
            AssetDatabase.CreateFolder(IconRoot.TrimEnd('/'), "Generated");
        }

        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
        {
            name = name + "Texture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] pixels = new Color[64 * 64];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.clear;
        texture.SetPixels(pixels);

        DrawMotif(texture, color, motif);
        texture.Apply(false, false);
        AssetDatabase.CreateAsset(texture, path);

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect);
        sprite.name = name;
        AssetDatabase.AddObjectToAsset(sprite, texture);
        EditorUtility.SetDirty(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        return Array.Find(AssetDatabase.LoadAllAssetsAtPath(path), item => item is Sprite) as Sprite;
    }

    private static void DrawMotif(Texture2D texture, Color color, int motif)
    {
        switch (motif)
        {
            case 0:
                Circle(texture, 32, 32, 23, color, 5);
                Line(texture, 32, 32, 32, 18, color, 5);
                Line(texture, 32, 32, 44, 39, color, 5);
                Line(texture, 23, 58, 41, 58, color, 5);
                break;
            case 1:
                Rect(texture, 7, 10, 57, 53, color, 5);
                Circle(texture, 23, 34, 8, color, 4);
                Line(texture, 11, 17, 35, 17, color, 4);
                Line(texture, 40, 40, 51, 40, color, 4);
                Line(texture, 40, 30, 51, 30, color, 4);
                break;
            case 2:
                Rect(texture, 7, 11, 57, 52, color, 5);
                Line(texture, 9, 49, 32, 31, color, 5);
                Line(texture, 55, 49, 32, 31, color, 5);
                Circle(texture, 53, 52, 5, color, 5);
                break;
            case 3:
                Line(texture, 9, 39, 18, 53, color, 5);
                Line(texture, 18, 53, 18, 42, color, 5);
                Line(texture, 18, 42, 46, 42, color, 5);
                Line(texture, 46, 42, 55, 28, color, 5);
                Line(texture, 55, 28, 46, 13, color, 5);
                Line(texture, 46, 13, 18, 13, color, 5);
                Line(texture, 18, 13, 9, 28, color, 5);
                Line(texture, 9, 28, 9, 39, color, 5);
                Line(texture, 20, 30, 43, 30, color, 4);
                break;
            case 4:
                Rect(texture, 7, 7, 57, 57, color, 5);
                Line(texture, 7, 24, 57, 24, color, 4);
                Line(texture, 7, 41, 57, 41, color, 4);
                Line(texture, 24, 7, 24, 57, color, 4);
                Line(texture, 41, 7, 41, 57, color, 4);
                break;
            case 5:
                Circle(texture, 27, 43, 8, color, 5);
                Circle(texture, 27, 22, 10, color, 5);
                Line(texture, 39, 32, 56, 32, color, 5);
                Line(texture, 56, 32, 48, 24, color, 5);
                Line(texture, 56, 32, 48, 40, color, 5);
                break;
            default:
                Circle(texture, 32, 32, 24, color, 5);
                Circle(texture, 32, 32, 12, color, 5);
                Line(texture, 32, 5, 32, 59, color, 4);
                Line(texture, 5, 32, 59, 32, color, 4);
                break;
        }
    }

    private static void Line(Texture2D texture, int x0, int y0, int x1, int y1, Color color, int width)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int error = dx - dy;
        while (true)
        {
            Disc(texture, x0, y0, width / 2, color);
            if (x0 == x1 && y0 == y1)
                break;
            int twice = error * 2;
            if (twice > -dy) { error -= dy; x0 += sx; }
            if (twice < dx) { error += dx; y0 += sy; }
        }
    }

    private static void Circle(Texture2D texture, int cx, int cy, int radius, Color color, int width)
    {
        for (int angle = 0; angle < 360; angle += 2)
        {
            float radians = angle * Mathf.Deg2Rad;
            Disc(texture, Mathf.RoundToInt(cx + Mathf.Cos(radians) * radius), Mathf.RoundToInt(cy + Mathf.Sin(radians) * radius), width / 2, color);
        }
    }

    private static void Rect(Texture2D texture, int left, int bottom, int right, int top, Color color, int width)
    {
        Line(texture, left, bottom, right, bottom, color, width);
        Line(texture, right, bottom, right, top, color, width);
        Line(texture, right, top, left, top, color, width);
        Line(texture, left, top, left, bottom, color, width);
    }

    private static void Disc(Texture2D texture, int cx, int cy, int radius, Color color)
    {
        for (int y = -radius; y <= radius; y++)
        for (int x = -radius; x <= radius; x++)
        {
            if (x * x + y * y > radius * radius)
                continue;
            int px = cx + x;
            int py = cy + y;
            if (px >= 0 && px < texture.width && py >= 0 && py < texture.height)
                texture.SetPixel(px, py, color);
        }
    }

    private static void StyleButton(Button button, Color baseColor, bool primary)
    {
        Image image = button.targetGraphic as Image ?? AddOrGet<Image>(button.gameObject);
        button.targetGraphic = image;
        image.color = primary ? baseColor : ShiftOSVisualTokens.RaisedPanel;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = primary ? new Color(1f, 0.88f, 0.88f, 1f) : new Color(1.12f, 1.12f, 1.12f, 1f),
            pressedColor = primary ? new Color(0.78f, 0.68f, 0.68f, 1f) : new Color(0.72f, 0.76f, 0.82f, 1f),
            selectedColor = ShiftOSVisualTokens.Focus,
            disabledColor = new Color(0.43f, 0.47f, 0.54f, 0.55f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f
        };

        foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
            StyleText(text, primary ? ShiftOSVisualTokens.DesktopInk : ShiftOSVisualTokens.TextPrimary, 13f, FontStyles.Bold);
    }

    private static void StyleInput(TMP_InputField input, string placeholder, Color accent)
    {
        Image image = input.targetGraphic as Image ?? AddOrGet<Image>(input.gameObject);
        image.color = ShiftOSVisualTokens.Field;
        input.targetGraphic = image;
        input.textComponent.color = ShiftOSVisualTokens.TextPrimary;
        input.textComponent.fontSize = 22f;
        input.textComponent.alignment = TextAlignmentOptions.Center;
        if (input.placeholder is TMP_Text placeholderText)
        {
            placeholderText.text = placeholder;
            placeholderText.color = ShiftOSVisualTokens.WithAlpha(accent, 0.35f);
            placeholderText.fontSize = 22f;
            placeholderText.alignment = TextAlignmentOptions.Center;
        }

        input.selectionColor = ShiftOSVisualTokens.WithAlpha(accent, 0.45f);
        input.caretColor = accent;
        SetLayout(input.gameObject, 44f, 0f);
        LayoutElement element = input.GetComponent<LayoutElement>();
        element.preferredWidth = 84f;
    }

    private static void AddOrUpdateHeader(Transform parent, string name, string title, string subtitle, Color accent)
    {
        Transform existing = parent.Find(name);
        GameObject header = existing != null ? existing.gameObject : CreatePanel(name, parent, ShiftOSVisualTokens.PanelSlate);
        header.transform.SetSiblingIndex(0);
        SetLayout(header, 54f, 0f);
        AddOrUpdateText(header.transform, "Title", title, accent, 15f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(12f, -7f), new Vector2(-12f, 22f));
        AddOrUpdateText(header.transform, "Subtitle", subtitle, ShiftOSVisualTokens.TextMuted, 9f, FontStyles.UpperCase, TextAlignmentOptions.Left, new Vector2(12f, 4f), new Vector2(-12f, 22f));
        AddOrUpdateAccent(header.transform, "Accent", accent);
    }

    private static void AddOrUpdateFooter(Transform parent, string name, string text, Color accent)
    {
        Transform existing = parent.Find(name);
        GameObject footer = existing != null ? existing.gameObject : CreatePanel(name, parent, ShiftOSVisualTokens.PanelSlate);
        footer.transform.SetAsLastSibling();
        SetLayout(footer, 24f, 0f);
        AddOrUpdateText(footer.transform, "Label", text, ShiftOSVisualTokens.TextMuted, 9f, FontStyles.UpperCase, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero);
        AddOrUpdateAccent(footer.transform, "Accent", accent);
    }

    private static void AddOrUpdateRailHeader(Transform parent, string title, string subtitle, Color accent)
    {
        Transform viewport = Find(parent, "Viewport");
        RectTransform viewportRect = (RectTransform)viewport;
        viewportRect.offsetMax = new Vector2(0f, -48f);
        AddOrUpdateText(parent, "RailTitle", title, ShiftOSVisualTokens.TextPrimary, 14f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(14f, -7f), new Vector2(-14f, 23f));
        AddOrUpdateText(parent, "RailSubtitle", subtitle, accent, 9f, FontStyles.UpperCase, TextAlignmentOptions.Left, new Vector2(14f, -27f), new Vector2(-14f, 17f));
    }

    private static void AddOrUpdateAppBanner(Transform parent, string name, string title, string subtitle, Color accent)
    {
        Transform existing = parent.Find(name);
        GameObject banner = existing != null ? existing.gameObject : CreatePanel(name, parent, ShiftOSVisualTokens.PanelSlate);
        RectTransform rect = (RectTransform)banner.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(12f, -58f);
        rect.offsetMax = new Vector2(-12f, -8f);
        AddOrUpdateText(banner.transform, "Title", title, accent, 17f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(14f, -6f), new Vector2(-14f, 24f));
        AddOrUpdateText(banner.transform, "Subtitle", subtitle, ShiftOSVisualTokens.TextMuted, 9f, FontStyles.UpperCase, TextAlignmentOptions.Left, new Vector2(14f, -26f), new Vector2(-14f, 16f));
        AddOrUpdateAccent(banner.transform, "Accent", accent);
    }

    private static void AddOrUpdateVerticalDivider(Transform parent, string name, float anchor, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject divider = existing != null ? existing.gameObject : CreatePanel(name, parent, ShiftOSVisualTokens.Border);
        Require<Image>(divider).color = ShiftOSVisualTokens.WithAlpha(color, 0.55f);
        RectTransform rect = (RectTransform)divider.transform;
        rect.anchorMin = new Vector2(anchor, 0f);
        rect.anchorMax = new Vector2(anchor, 1f);
        rect.sizeDelta = new Vector2(2f, -84f);
        rect.anchoredPosition = new Vector2(0f, -26f);
    }

    private static void AddOrUpdateAccent(Transform parent, string name, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject accent = existing != null ? existing.gameObject : CreatePanel(name, parent, color);
        Require<Image>(accent).color = color;
        RectTransform rect = (RectTransform)accent.transform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(3f, 0f);
        LayoutElement layout = AddOrGet<LayoutElement>(accent);
        layout.ignoreLayout = true;
    }

    private static void AddOrUpdateText(Transform parent, string name, string value, Color color, float size, FontStyles style, TextAlignmentOptions alignment, Vector2 offsetMax, Vector2 sizeDelta)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        if (existing == null)
            go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = offsetMax;
        rect.sizeDelta = sizeDelta;
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.text = value;
        text.alignment = alignment;
        StyleText(text, color, size, style);
    }

    private static void StyleText(TMP_Text text, Color color, float size, FontStyles style)
    {
        text.color = color;
        text.fontSize = size;
        text.fontStyle = style;
        text.raycastTarget = false;
    }

    private static void SetLayout(GameObject go, float preferredHeight, float flexibleHeight)
    {
        LayoutElement element = AddOrGet<LayoutElement>(go);
        element.minHeight = preferredHeight;
        element.preferredHeight = preferredHeight;
        element.flexibleHeight = flexibleHeight;
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static void EditPrefab(string fileName, Action<GameObject> edit)
    {
        string path = PrefabRoot + fileName;
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform Find(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name)
                return child;
        throw new InvalidOperationException($"Could not find {name} under {root.name}.");
    }

    private static T FindComponentInChildren<T>(GameObject root) where T : Component
    {
        T component = root.GetComponentInChildren<T>(true);
        return component != null ? component : throw new InvalidOperationException($"Could not find {typeof(T).Name} under {root.name}.");
    }

    private static T Require<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : throw new InvalidOperationException($"{go.name} requires {typeof(T).Name}.");
    }

    private static T AddOrGet<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }
}
