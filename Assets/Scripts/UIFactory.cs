using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ParticulasRA
{
    /// <summary>Utilidades para construir la interfaz uGUI por código (texto legacy con fuente del sistema).</summary>
    public static class UIFactory
    {
        public static readonly Color PanelCol = ParticleDefs.Hex("#0D121E");
        public static readonly Color LineCol = ParticleDefs.Hex("#1C2537");
        public static readonly Color Txt = ParticleDefs.Hex("#E7ECF6");
        public static readonly Color Mut = ParticleDefs.Hex("#8B97AE");
        public static readonly Color Acc = ParticleDefs.Hex("#4FD1FF");
        public static readonly Color BtnCol = ParticleDefs.Hex("#121A2A");
        public static readonly Color ChipCol = ParticleDefs.Hex("#090E18");
        public static readonly Color ChipOn = ParticleDefs.Hex("#16223A");
        public static readonly Color NoteCol = ParticleDefs.Hex("#5F6B82");

        static Font font;

        /// <summary>
        /// Fuente dinámica del sistema (Roboto / Noto en Android) para cubrir μ, π, γ, ν, ⁺, ⁻, ₑ, etc.
        /// Si no está disponible, usa la fuente integrada de Unity.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (font != null) return font;
                try
                {
                    font = Font.CreateDynamicFontFromOSFont(
                        new[] { "Roboto", "Noto Sans", "NotoSans-Regular", "Droid Sans", "Segoe UI", "Arial", "Helvetica" }, 32);
                }
                catch { font = null; }
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float left = 0, float right = 0, float bottom = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static Image Img(RectTransform rt, Color c, bool raycast = true, Sprite sprite = null)
        {
            var im = rt.gameObject.AddComponent<Image>();
            im.color = c;
            im.raycastTarget = raycast;
            if (sprite != null) im.sprite = sprite;
            return im;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rt = Rect("Texto", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.1f;
            return t;
        }

        public static LayoutElement Layout(GameObject go, float prefW = -1, float flexW = -1, float prefH = -1, float minH = -1)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (prefW >= 0) le.preferredWidth = prefW;
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (prefH >= 0) le.preferredHeight = prefH;
            if (minH >= 0) le.minHeight = minH;
            return le;
        }

        public static Outline Border(Graphic g, Color c)
        {
            var o = g.gameObject.AddComponent<Outline>();
            o.effectColor = c;
            o.effectDistance = new Vector2(2f, -2f);
            return o;
        }

        public static Button Btn(Transform parent, string label, UnityAction onClick, out Text text,
                                 Color? bg = null, int size = 32, float height = 96)
        {
            var rt = Rect("Boton", parent);
            var im = Img(rt, bg ?? BtnCol);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = im;
            var cb = b.colors;
            cb.highlightedColor = new Color(1f, 1f, 1f, 1f);
            cb.pressedColor = new Color(0.75f, 0.82f, 1f, 1f);
            cb.selectedColor = Color.white;
            b.colors = cb;
            if (onClick != null) b.onClick.AddListener(onClick);
            text = Label(rt, label, size, Txt, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 12, 12, 0, 0);
            Layout(rt.gameObject, flexW: 1, prefH: height, minH: height);
            return b;
        }

        public static RectTransform HRow(Transform parent, float spacing = 12f, float height = -1f, bool expandWidth = true)
        {
            var rt = Rect("Fila", parent);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = expandWidth;
            h.childForceExpandHeight = false;
            if (height > 0) Layout(rt.gameObject, prefH: height, minH: height);
            return rt;
        }

        public static RectTransform VCol(Transform parent, float spacing = 12f)
        {
            var rt = Rect("Columna", parent);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return rt;
        }

        public static RectTransform Grid(Transform parent, Vector2 cell, int columns, float spacing = 12f)
        {
            var rt = Rect("Rejilla", parent);
            var g = rt.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = cell;
            g.spacing = new Vector2(spacing, spacing);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = columns;
            g.childAlignment = TextAnchor.UpperLeft;
            return rt;
        }

        public static Slider Slider(Transform parent, float min, float max, float value, UnityAction<float> onChange)
        {
            var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.transform.SetParent(parent, false);
            var s = go.GetComponent<Slider>();
            s.minValue = min;
            s.maxValue = max;
            s.wholeNumbers = true;
            s.SetValueWithoutNotify(value);
            s.onValueChanged.AddListener(onChange);
            foreach (var im in go.GetComponentsInChildren<Image>(true))
            {
                switch (im.name)
                {
                    case "Background": im.color = LineCol; break;
                    case "Fill": im.color = Acc; break;
                    case "Handle": im.color = Txt; im.sprite = ProceduralAssets.CircleSprite; im.preserveAspect = true; break;
                }
            }
            if (s.handleRect != null) s.handleRect.sizeDelta = new Vector2(48f, 0f);
            Layout(go, flexW: 1, prefH: 64, minH: 64);
            return s;
        }
    }
}
