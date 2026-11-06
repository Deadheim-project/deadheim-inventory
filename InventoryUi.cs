using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deadheim.InventoryUI
{
    // Painel único (sem abas) à esquerda do painel de crafting:
    // coluna esquerda = slots de equipamento; centro = prévia 3D; coluna direita = Atributos.
    // Somente exibição: não há botões nem nada que altere equipamento ou aparência.
    internal static class InventoryUi
    {
        private const float PanelWidth = 600f;

        private static InventoryGui _gui;
        private static RectTransform _root;
        private static RectTransform _list;
        private static RectTransform _attributes;
        private static readonly List<(string name, Func<Player, string> read, TextMeshProUGUI value)> Attributes =
            new List<(string, Func<Player, string>, TextMeshProUGUI)>();
        private static bool _open;
        private static bool _needsBuild;
        private static float _nextSignatureCheck;
        private static int _lastSignature;
        private static Camera _camera;
        private static Light _light;
        private static RenderTexture _texture;
        private static int _previewMask;
        private static float _nextMaskUpdate;
        private static float _nextRender;

        internal static void Open(InventoryGui gui)
        {
            _open = true;
            if (!EnsureCreated(gui)) return;
            _root.gameObject.SetActive(true);
            SetPreviewActive(true);
            _needsBuild = true;
            Refresh(true);
        }

        internal static void Close()
        {
            _open = false;
            SetPreviewActive(false);
            if (_root != null) _root.gameObject.SetActive(false);
            ReleaseSelection();
        }

        // Nunca deixa um objeto do painel como "selecionado": o EventSystem seguiria
        // consumindo a navegação (e o jogo trataria como UI com foco) depois de fechar.
        private static void ReleaseSelection()
        {
            EventSystem system = EventSystem.current;
            if (system == null) return;
            GameObject selected = system.currentSelectedGameObject;
            if (selected == null) return;
            if (_root == null || selected.transform.IsChildOf(_root))
                system.SetSelectedGameObject(null);
        }

        internal static void Tick(InventoryGui gui)
        {
            if (!_open) return;
            if (!EnsureCreated(gui)) return;
            if (!InventoryGui.IsVisible())
            {
                Close();
                return;
            }

            float scale = Plugin.UiScale != null ? Plugin.UiScale.Value : 1f;
            if (Math.Abs(_root.localScale.x - scale) > 0.001f)
                _root.localScale = new Vector3(scale, scale, 1f);

            UpdateAttributes();
            UpdatePreview();
            if (Time.unscaledTime < _nextSignatureCheck) return;
            _nextSignatureCheck = Time.unscaledTime + 0.35f;
            Refresh(false);
        }

        private static bool EnsureCreated(InventoryGui gui)
        {
            if (gui == null || gui.m_crafting == null || !ValheimUi.EnsureAssets()) return false;
            if (_gui == gui && _root != null) return true;

            ReleaseSelection();
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
            _gui = gui;

            // Filho do crafting, ancorado fora da borda esquerda dele (pivô na direita): ocupa o espaço
            // livre entre a grade do inventário (canto superior esquerdo) e o crafting.
            _root = ValheimUi.Rect("DeadheimInventoryPanel", gui.m_crafting);
            _root.anchorMin = new Vector2(0f, 0f);
            _root.anchorMax = new Vector2(0f, 1f);
            _root.pivot = new Vector2(1f, 0.5f);
            _root.sizeDelta = new Vector2(PanelWidth, 0f);
            _root.anchoredPosition = Vector2.zero;
            var background = _root.gameObject.AddComponent<Image>();
            background.sprite = ValheimUi.PanelSprite;
            background.type = Image.Type.Sliced;
            background.color = new Color(1f, 1f, 1f, 0.99f);

            var left = ValheimUi.Rect("Equipment", _root);
            ValheimUi.Anchor(left, new Vector2(0.025f, 0.05f), new Vector2(0.41f, 0.96f), Vector2.zero, Vector2.zero);
            _list = ValheimUi.ScrollList(left, 5f);

            BuildPreview();

            var right = ValheimUi.Rect("AttributesFrame", _root);
            ValheimUi.Anchor(right, new Vector2(0.64f, 0.05f), new Vector2(0.975f, 0.96f), Vector2.zero, Vector2.zero);
            var frame = right.gameObject.AddComponent<Image>();
            frame.sprite = ValheimUi.FieldSprite;
            frame.type = Image.Type.Sliced;
            frame.color = new Color(0.46f, 0.39f, 0.29f, 0.75f);
            var attributesHost = ValheimUi.Rect("Content", right);
            ValheimUi.Stretch(attributesHost, 10f, 8f, 10f, 8f);
            _attributes = ValheimUi.ScrollList(attributesHost, 2f);
            BuildAttributes();
            _root.gameObject.SetActive(_open);
            return true;
        }

        // ---------- Prévia 3D do personagem ----------
        // Câmera própria renderiza só o jogador local como os outros o veem (o modelo já carrega
        // a aparência aplicada pelo Deadheim) numa RenderTexture; mostrada num RawImage no painel.

        private static void BuildPreview()
        {
            var frameRect = ValheimUi.Rect("CharacterFrame", _root);
            ValheimUi.Anchor(frameRect, new Vector2(0.42f, 0.05f), new Vector2(0.63f, 0.96f), Vector2.zero, Vector2.zero);
            var frame = frameRect.gameObject.AddComponent<Image>();
            frame.sprite = ValheimUi.FieldSprite;
            frame.type = Image.Type.Sliced;
            frame.color = new Color(0.20f, 0.16f, 0.12f, 0.85f);

            var imageRect = ValheimUi.Rect("Character", frameRect);
            ValheimUi.Stretch(imageRect, 4f, 4f, 4f, 4f);
            var raw = imageRect.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            EnsurePreviewObjects();
            raw.texture = _texture;
            var fitter = imageRect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)_texture.width / _texture.height;
        }

        private static void EnsurePreviewObjects()
        {
            if (_texture == null)
            {
                _texture = new RenderTexture(384, 768, 24, RenderTextureFormat.ARGB32) { name = "DeadheimCharacterPreview" };
                _texture.Create();
            }
            if (_camera != null) return;
            _previewMask = LayerMask.GetMask("character", "character_net", "character_ghost", "character_noenv", "item");
            var cameraObject = new GameObject("DeadheimPreviewCamera");
            UnityEngine.Object.DontDestroyOnLoad(cameraObject);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.cullingMask = _previewMask;
            _camera.fieldOfView = 30f;
            _camera.nearClipPlane = 0.5f;
            _camera.farClipPlane = 6.5f;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _camera.targetTexture = _texture;
            _camera.enabled = false;

            var lightObject = new GameObject("DeadheimPreviewLight");
            lightObject.transform.SetParent(cameraObject.transform, false);
            _light = lightObject.AddComponent<Light>();
            _light.type = LightType.Directional;
            _light.intensity = 1.1f;
            _light.shadows = LightShadows.None;
            _light.cullingMask = _previewMask;
            _light.enabled = false;
        }

        private static void SetPreviewActive(bool active)
        {
            if (_camera != null) _camera.enabled = false;
            if (_light != null) _light.enabled = active;
            _nextMaskUpdate = 0f;
        }

        private static void UpdatePreview()
        {
            Player player = Player.m_localPlayer;
            if (_camera == null || player == null) return;
            Vector3 forward = player.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 target = player.transform.position + Vector3.up * 0.95f;
            Transform camera = _camera.transform;
            Vector3 offset = Quaternion.Euler(0f, 22f, 0f) * forward;
            camera.position = target + offset * 3.6f + Vector3.up * 0.15f;
            camera.rotation = Quaternion.LookRotation(target - camera.position, Vector3.up);
            // Luz de frente e levemente de cima, independente da hora do dia.
            _light.transform.rotation = Quaternion.LookRotation(-offset + Vector3.down * 0.35f, Vector3.up);

            // Inclui as camadas dos renderers do jogador (peças que não estavam na máscara fixa sumiam).
            float now = Time.unscaledTime;
            if (now >= _nextMaskUpdate)
            {
                _nextMaskUpdate = now + 1f;
                int mask = _previewMask;
                foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(false))
                    if (renderer.gameObject.layer != 0) mask |= 1 << renderer.gameObject.layer;
                _camera.cullingMask = mask;
                _light.cullingMask = mask;
            }
            // Câmera desligada e renderizada a ~25 fps: o painel não precisa de mais.
            if (now >= _nextRender)
            {
                _nextRender = now + 0.04f;
                _camera.Render();
            }
        }

        private static void DestroyPreview()
        {
            if (_camera != null) UnityEngine.Object.Destroy(_camera.gameObject);
            _camera = null;
            _light = null;
            if (_texture != null)
            {
                _texture.Release();
                UnityEngine.Object.Destroy(_texture);
            }
            _texture = null;
        }

        // ---------- Atributos ----------

        private static void BuildAttributes()
        {
            Attributes.Clear();
            var heading = ValheimUi.Label(_attributes, "Atributos", 22, ValheimUi.Orange, display: true);
            ValheimUi.FixedHeight(heading.gameObject, 34f);

            AddAttribute("Vida", p => $"{p.GetHealth():0} / {p.GetMaxHealth():0}");
            AddAttribute("Stamina", p => $"{p.GetStamina():0} / {p.GetMaxStamina():0}");
            AddAttribute("Eitr", p => $"{p.GetEitr():0} / {p.GetMaxEitr():0}");
            AddAttribute("Armadura", p => p.GetBodyArmor().ToString("0"));
            AddAttribute("Velocidade",
                p => (100f * (1f + p.GetEquipmentMovementModifier())).ToString("0") + "%");
            AddAttribute("Peso", p =>
            {
                Inventory inventory = p.GetInventory();
                return inventory == null ? "-" : $"{inventory.GetTotalWeight():0} / {p.GetMaxCarryWeight():0}";
            });
        }

        private static void AddAttribute(string name, Func<Player, string> read)
        {
            var row = ValheimUi.Rect("Attribute", _attributes);
            ValheimUi.FixedHeight(row.gameObject, 30f);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(2, 2, 0, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var label = ValheimUi.Label(row, name, 13, ValheimUi.Beige, TextAlignmentOptions.MidlineLeft);
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var value = ValheimUi.Label(row, "-", 13, ValheimUi.Beige, TextAlignmentOptions.MidlineRight);
            ValheimUi.FixedWidth(value.gameObject, 78f);
            Attributes.Add((name, read, value));
        }

        private static void UpdateAttributes()
        {
            Player player = Player.m_localPlayer;
            if (player == null) return;
            foreach (var attribute in Attributes)
            {
                if (attribute.value == null) continue;
                string text;
                try { text = attribute.read(player); }
                catch (Exception) { text = "-"; }
                if (attribute.value.text != text) attribute.value.text = text;
            }
        }

        // ---------- Equipamento + aparência ----------

        private static void Refresh(bool force)
        {
            if (_root == null) return;
            int signature = Signature();
            if (!force && !_needsBuild && signature == _lastSignature) return;
            _needsBuild = false;
            _lastSignature = signature;
            ClearChildren(_list);
            BuildEquipment();
        }

        // Hash sem alocação (sem strings/LINQ): roda a cada 0,35 s com a UI aberta.
        // A soma dos hashes por item não depende da ordem da lista.
        private static int Signature()
        {
            var player = Player.m_localPlayer;
            Inventory inventory = player != null ? player.GetInventory() : null;
            unchecked
            {
                int items = 0;
                if (inventory != null)
                {
                    List<ItemDrop.ItemData> all = inventory.GetAllItems();
                    for (int index = 0; index < all.Count; index++)
                    {
                        ItemDrop.ItemData item = all[index];
                        // Só o equipado importa: craftar/mover itens não deve reconstruir o painel.
                        if (item == null || !item.m_equipped) continue;
                        int h = item.m_dropPrefab != null ? item.m_dropPrefab.name.GetHashCode() : 0;
                        h = h * 31 + item.m_quality;
                        h = h * 31 + (int)item.m_durability;
                        items += h * 16777619 + 1;
                    }
                }
                int result = inventory == null ? 1 : 2;
                result = result * 31 + items;
                result = result * 31 + (TransmogBridge.Available ? TransmogBridge.Revision() : -1);
                return result;
            }
        }

        // Só exibição, como a ficha de personagem do WoW: cada slot mostra o ícone de como a peça
        // aparece (a aparência aplicada, se houver); tooltip e durabilidade são da peça real.
        private static void BuildEquipment()
        {
            var title = ValheimUi.Label(_list, "Equipamento", 22, ValheimUi.Orange, display: true);
            ValheimUi.FixedHeight(title.gameObject, 34f);

            Inventory inventory = Player.m_localPlayer != null ? Player.m_localPlayer.GetInventory() : null;
            List<ItemDrop.ItemData> equipped = inventory?.GetAllItems()
                .Where(item => item != null && item.m_equipped && item.m_shared != null).ToList()
                ?? new List<ItemDrop.ItemData>();

            var cells = new List<(string label, ItemDrop.ItemData item)>
            {
                ("Cabeça", Find(equipped, ItemDrop.ItemData.ItemType.Helmet)),
                ("Peito", Find(equipped, ItemDrop.ItemData.ItemType.Chest)),
                ("Pernas", Find(equipped, ItemDrop.ItemData.ItemType.Legs)),
                ("Capa", Find(equipped, ItemDrop.ItemData.ItemType.Shoulder)),
                ("Utilitário", Find(equipped, ItemDrop.ItemData.ItemType.Utility)),
                ("Berloque", Find(equipped, ItemDrop.ItemData.ItemType.Trinket)),
            };
            Player player = Player.m_localPlayer;
            cells.Add(("Mão direita", HumanoidItem(player, "m_rightItem")));
            cells.Add(("Mão esquerda", HumanoidItem(player, "m_leftItem")));
            cells.Add(("Munição", HumanoidItem(player, "m_ammoItem")));

            const int columns = 3;
            const float cell = 56f;
            const float gap = 6f;
            int rows = (cells.Count + columns - 1) / columns;
            var grid = ValheimUi.Rect("Slots", _list);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(cell, cell);
            layout.spacing = new Vector2(gap, gap);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;
            ValheimUi.FixedHeight(grid.gameObject, rows * (cell + gap));
            Dictionary<string, string> looks = TransmogBridge.Available
                ? TransmogBridge.Chosen() : new Dictionary<string, string>();
            foreach (var entry in cells) AddSlot(grid, entry.label, entry.item, looks);
        }

        // Campos protegidos do Humanoid: o jogo roda o assembly original, então só por reflexão.
        private static readonly Dictionary<string, System.Reflection.FieldInfo> HumanoidFields =
            new Dictionary<string, System.Reflection.FieldInfo>();

        private static ItemDrop.ItemData HumanoidItem(Player player, string field)
        {
            if (player == null) return null;
            if (!HumanoidFields.TryGetValue(field, out var info))
                HumanoidFields[field] = info = AccessTools.Field(typeof(Humanoid), field);
            return info?.GetValue(player) as ItemDrop.ItemData;
        }

        private static ItemDrop.ItemData Find(List<ItemDrop.ItemData> equipped, ItemDrop.ItemData.ItemType type) =>
            equipped.FirstOrDefault(candidate => candidate.m_shared.m_itemType == type);

        private static void AddSlot(Transform parent, string label, ItemDrop.ItemData item,
            Dictionary<string, string> looks)
        {
            var slot = ValheimUi.Rect("Slot " + label, parent);
            var background = slot.gameObject.AddComponent<Image>();
            background.sprite = ValheimUi.FieldSprite;
            background.type = Image.Type.Sliced;
            background.color = new Color(0.30f, 0.25f, 0.19f, 0.9f);

            if (item != null && item.m_dropPrefab != null)
            {
                string prefab = item.m_dropPrefab.name;
                var iconRect = ValheimUi.Rect("Icon", slot);
                ValheimUi.Anchor(iconRect, new Vector2(0.12f, 0.08f), new Vector2(0.88f, 0.80f), Vector2.zero, Vector2.zero);
                var icon = iconRect.gameObject.AddComponent<Image>();
                Sprite sprite = null;
                if (looks.TryGetValue(prefab, out string look) && !string.IsNullOrEmpty(look))
                    sprite = ValheimUi.ItemIcon(look);
                icon.sprite = sprite != null ? sprite : ValheimUi.ItemIcon(prefab);
                icon.enabled = icon.sprite != null;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                AddItemTooltip(slot.gameObject, item);
            }

            var caption = ValheimUi.Label(slot, label, 11, ValheimUi.Orange, TextAlignmentOptions.TopLeft);
            ValheimUi.Anchor((RectTransform)caption.transform, new Vector2(0f, 0.72f), new Vector2(1f, 1f),
                new Vector2(4f, 0f), Vector2.zero);
            caption.overflowMode = TextOverflowModes.Overflow;
            caption.textWrappingMode = TextWrappingModes.NoWrap;

            if (item != null && item.m_durability > 0f && item.m_shared.m_useDurability)
            {
                float max = item.GetMaxDurability();
                int threshold = Plugin.LowDurabilityPercent != null ? Plugin.LowDurabilityPercent.Value : 0;
                bool low = max > 0f && item.m_durability / max * 100f < threshold;
                var durability = ValheimUi.Label(slot, item.m_durability.ToString("0"), 11,
                    low ? new Color(1f, 0.42f, 0.30f, 1f) : ValheimUi.Muted, TextAlignmentOptions.BottomRight);
                ValheimUi.Stretch((RectTransform)durability.transform, 0f, 2f, 4f, 0f);
            }
        }

        private static void AddItemTooltip(GameObject target, ItemDrop.ItemData item)
        {
            if (item == null || Plugin.ShowTooltips == null || !Plugin.ShowTooltips.Value) return;
            try { ValheimUi.AddLazyTooltip(target, ItemName(item), () => item.GetTooltip()); }
            catch (Exception exception) { Plugin.Log.LogWarning("Tooltip do item: " + exception.Message); }
        }

        private static string Prefab(ItemDrop.ItemData item) => item?.m_dropPrefab != null ? item.m_dropPrefab.name : "";

        private static string ItemName(ItemDrop.ItemData item)
        {
            string raw = item?.m_shared?.m_name;
            if (string.IsNullOrEmpty(raw)) return Prefab(item);
            return Localization.instance != null ? Localization.instance.Localize(raw) : raw;
        }

        private static void ClearChildren(Transform parent)
        {
            // Destroy só age no fim do frame; desativar antes tira os filhos antigos do layout agora.
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                GameObject child = parent.GetChild(index).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }

        // Chamado ao sair do mundo: solta tudo que pertence à cena que vai ser destruída.
        internal static void Reset()
        {
            Close();
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
            DestroyPreview();
            _root = null;
            _gui = null;
            Attributes.Clear();
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
    internal static class InventoryGuiShowPatch
    {
        private static void Postfix(InventoryGui __instance) => InventoryUi.Open(__instance);
    }

    [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
    internal static class GameLogoutPatch
    {
        private static void Prefix() => InventoryUi.Reset();
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
    internal static class InventoryGuiHidePatch
    {
        private static void Postfix() => InventoryUi.Close();
    }

    [HarmonyPatch(typeof(InventoryGui), "Update")]
    internal static class InventoryGuiUpdatePatch
    {
        private static void Postfix(InventoryGui __instance) => InventoryUi.Tick(__instance);
    }
}
