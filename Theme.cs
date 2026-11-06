using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deadheim.InventoryUI
{
    // Paleta e materiais da ficha. Painel e fundo dos slots vêm do próprio inventário do jogo
    // (mesma madeira escura); o resto é uma paleta curta: bronze envelhecido, ouro apagado,
    // recessos quase pretos e texto em pergaminho, como as telas nativas do Valheim.
    internal static class Theme
    {
        internal static readonly Color Title = new Color(0.98f, 0.70f, 0.32f, 1f);
        internal static readonly Color Gold = new Color(0.86f, 0.64f, 0.34f, 0.75f);
        internal static readonly Color BronzeRim = new Color(0.56f, 0.43f, 0.26f, 0.95f);
        internal static readonly Color BronzeRimDim = new Color(0.30f, 0.24f, 0.17f, 0.90f);
        internal static readonly Color Recess = new Color(0.035f, 0.028f, 0.022f, 0.82f);
        internal static readonly Color PreviewBackground = new Color(0.055f, 0.045f, 0.036f, 1f);
        internal static readonly Color Label = new Color(0.80f, 0.70f, 0.54f, 1f);
        internal static readonly Color Value = new Color(0.96f, 0.92f, 0.84f, 1f);
        internal static readonly Color Penalty = new Color(1f, 0.52f, 0.36f, 1f);
        internal static readonly Color Health = new Color(0.95f, 0.45f, 0.40f, 1f);
        internal static readonly Color Stamina = new Color(0.98f, 0.82f, 0.40f, 1f);
        internal static readonly Color Eitr = new Color(0.62f, 0.66f, 1f, 1f);
        internal static readonly Color EmptyCaption = new Color(0.62f, 0.56f, 0.46f, 0.55f);

        private static Image _nativePanel;
        private static Sprite _fadeLine;
        private static Sprite _vignette;
        private static Sprite _outline;

        // Copia o visual do painel da grade do inventário (sprite, recorte e tom).
        internal static void ApplyPanel(Image target, InventoryGui gui)
        {
            _nativePanel = gui.m_player != null ? gui.m_player.GetComponent<Image>() : null;
            if (_nativePanel == null && gui.m_player != null)
                foreach (Image candidate in gui.m_player.GetComponentsInChildren<Image>(true))
                {
                    string name = candidate.name.ToLowerInvariant();
                    if (name.Contains("bkg") || name.Contains("background")) { _nativePanel = candidate; break; }
                }

            // A moldura do painel da grade é assimétrica (lateral esquerda grossa, folga no topo e à
            // direita): num painel menor o nome ficava sobre a borda. Usa a madeira dos painéis do
            // jogo (quatro bordas iguais), mas com o material e a cor da grade: o material é o que
            // acompanha a luz do dia, então o tom fica igual ao do inventário a qualquer hora.
            if (ValheimUi.PanelSprite != null)
            {
                target.sprite = ValheimUi.PanelSprite;
                target.type = Image.Type.Sliced;
                if (_nativePanel != null)
                {
                    target.material = _nativePanel.material;
                    target.color = _nativePanel.color;
                }
                else
                    target.color = new Color(0.55f, 0.50f, 0.47f, 0.98f);
            }
            else if (_nativePanel != null && _nativePanel.sprite != null)
            {
                target.sprite = _nativePanel.sprite;
                target.type = _nativePanel.type;
                target.material = _nativePanel.material;
                target.pixelsPerUnitMultiplier = _nativePanel.pixelsPerUnitMultiplier;
                target.color = _nativePanel.color;
            }
        }

        // Linha horizontal que some nas pontas.
        internal static Sprite FadeLine()
        {
            if (_fadeLine != null) return _fadeLine;
            const int width = 128;
            var texture = NewTexture("DeadheimFadeLine", width, 1);
            var pixels = new Color32[width];
            for (int x = 0; x < width; x++)
            {
                float t = Mathf.Abs((x + 0.5f) / width * 2f - 1f);
                pixels[x] = new Color32(255, 255, 255, (byte)(255f * (1f - Mathf.SmoothStep(0.35f, 1f, t))));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return _fadeLine = Sprite.Create(texture, new Rect(0, 0, width, 1), new Vector2(0.5f, 0.5f));
        }

        // Escurece as bordas da prévia e deixa o centro limpo, com um leve calor no meio.
        internal static Sprite Vignette()
        {
            if (_vignette != null) return _vignette;
            const int size = 128;
            var texture = NewTexture("DeadheimVignette", size, size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = ((y + 0.5f) / size * 2f - 1f) * 0.9f + 0.08f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.SmoothStep(0.55f, 1.35f, distance) * 0.92f;
                pixels[y * size + x] = new Color32(10, 7, 5, (byte)(alpha * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return _vignette = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        // Contorno de 1 px (fatiado) para aros finos.
        internal static Sprite Outline()
        {
            if (_outline != null) return _outline;
            const int size = 8;
            var texture = NewTexture("DeadheimOutline", size, size);
            texture.filterMode = FilterMode.Point;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(edge ? 255 : 0));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return _outline = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(2f, 2f, 2f, 2f));
        }

        private static Texture2D NewTexture(string name, int width, int height) =>
            new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
    }

    // Arrastar a barra do título move a ficha; a posição fica salva na config.
    // Clique duplo volta para a posição padrão (logo abaixo da grade).
    internal sealed class PanelDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        internal static readonly Vector2 BasePosition = new Vector2(0f, -6f);
        internal RectTransform Target;

        internal static Vector2 SavedOffset => Plugin.PanelOffset != null ? Plugin.PanelOffset.Value : Vector2.zero;

        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            if (Target == null || !(Target.parent is RectTransform parent)) return;
            Camera camera = eventData.pressEventCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, camera, out Vector2 now)) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position - eventData.delta, camera, out Vector2 before)) return;
            Target.anchoredPosition += now - before;
            Clamp(Target);
        }

        public void OnEndDrag(PointerEventData eventData) => Save();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || eventData.clickCount < 2 || Target == null) return;
            Target.anchoredPosition = BasePosition;
            Save();
        }

        private void Save()
        {
            if (Target == null || Plugin.PanelOffset == null) return;
            Plugin.PanelOffset.Value = Target.anchoredPosition - BasePosition;
        }

        // Mantém o painel inteiro dentro da tela (resolução nova, painel arrastado para fora).
        internal static void Clamp(RectTransform target)
        {
            if (target == null) return;
            Canvas canvas = target.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var area = (RectTransform)canvas.rootCanvas.transform;
            var panel = new Vector3[4];
            var screen = new Vector3[4];
            target.GetWorldCorners(panel);
            area.GetWorldCorners(screen);
            float dx = Mathf.Max(0f, screen[0].x - panel[0].x) - Mathf.Max(0f, panel[2].x - screen[2].x);
            float dy = Mathf.Max(0f, screen[0].y - panel[0].y) - Mathf.Max(0f, panel[2].y - screen[2].y);
            if (Mathf.Abs(dx) > 0.01f || Mathf.Abs(dy) > 0.01f) target.position += new Vector3(dx, dy, 0f);
        }
    }
}
