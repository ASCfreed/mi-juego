using UnityEngine;
using UnityEngine.UI;

namespace ParticulasRA
{
    /// <summary>Tutorial de 3 tarjetas que solo aparece la primera vez (PlayerPrefs).</summary>
    public class TutorialController : MonoBehaviour
    {
        [SerializeField] UIController ui;

        const string PrefKey = "pra_tutorial_visto";

        static readonly string[] Titles =
        {
            "Coloca la cámara de burbujas",
            "Lanza partículas",
            "Prueba los experimentos",
        };

        static readonly string[] Bodies =
        {
            "Apunta el móvil al suelo y muévelo despacio. Cuando veas el anillo, toca la pantalla para colocar la cámara. " +
            "Pellizca para cambiar su tamaño y gira con dos dedos para rotarla.",
            "Elige una partícula en el panel y arrastra el dedo: la dirección del arrastre marca hacia dónde sale y su longitud, " +
            "la velocidad (en el fotón, la energía). Camina alrededor para ver las espirales en 3D.",
            "Rayos cósmicos, aniquilación p̄ + p, producción de pares y e⁻ + e⁺. En «Ajustes de la cámara» puedes cambiar " +
            "el campo magnético, la velocidad, el medio y más.",
        };

        GameObject overlay;
        Text title, body, page, nextText;
        int index;

        void Start()
        {
            if (ui == null || ui.Root == null) return;
            if (PlayerPrefs.GetInt(PrefKey, 0) == 1) return;
            Build();
            Show(0);
        }

        void Build()
        {
            var root = UIFactory.Rect("Tutorial", ui.Root);
            UIFactory.Stretch(root);
            UIFactory.Img(root, new Color(0f, 0f, 0f, 0.72f)); // bloquea toques mientras está visible
            overlay = root.gameObject;

            var card = UIFactory.Rect("Tarjeta", root);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(920, 760);
            var bg = UIFactory.Img(card, UIFactory.PanelCol);
            UIFactory.Border(bg, UIFactory.LineCol);

            var col = UIFactory.VCol(card, 24);
            UIFactory.Stretch(col, 48, 48, 40, 48);

            page = UIFactory.Label(col, "", 24, UIFactory.Acc);
            title = UIFactory.Label(col, "", 44, UIFactory.Txt);
            title.fontStyle = FontStyle.Bold;
            body = UIFactory.Label(col, "", 32, UIFactory.Mut, TextAnchor.UpperLeft);
            UIFactory.Layout(body.gameObject, prefH: 380);

            var row = UIFactory.HRow(col, 20, 104);
            UIFactory.Btn(row, "Saltar", Finish, out _, UIFactory.ChipCol, 32, 100);
            var next = UIFactory.Btn(row, "Siguiente", Next, out nextText, UIFactory.Acc, 32, 100);
            nextText.color = UIFactory.PanelCol;
            next.transform.SetAsLastSibling();
        }

        void Show(int i)
        {
            index = i;
            page.text = $"{i + 1} / {Titles.Length}";
            title.text = Titles[i];
            body.text = Bodies[i];
            nextText.text = i == Titles.Length - 1 ? "¡Empezar!" : "Siguiente";
        }

        void Next()
        {
            if (index < Titles.Length - 1) Show(index + 1);
            else Finish();
        }

        void Finish()
        {
            PlayerPrefs.SetInt(PrefKey, 1);
            PlayerPrefs.Save();
            if (overlay != null) Destroy(overlay);
        }
    }
}
