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
    internal static class InventoryUi
    {
        private enum Page { Equipment, Appearance, QuickSlots, Crafting }

        private static InventoryGui _gui;
        private static RectTransform _root;
        private static RectTransform _body;
        private static RectTransform _list;
        private static Image _background;
        private static TextMeshProUGUI _status;
        private static readonly List<(Button button, TextMeshProUGUI label, Page page)> Tabs =
            new List<(Button, TextMeshProUGUI, Page)>();
        private static Page _page;
        private static bool _open;
        private static bool _needsBuild;
        private static float _nextSignatureCheck;
        private static int _lastSignature;
        private static string _statusText = "";
        private static Color _statusColor = ValheimUi.Muted;
        private static string _selectedReal;
        private static string _selectedAppearance;
        private static bool _ownsPreview;
        private static bool _nativeHidden;
        private static readonly List<(CanvasGroup group, float alpha, bool blocks, bool added)> _nativeGroups =
            new List<(CanvasGroup, float, bool, bool)>();

        internal static void Open(InventoryGui gui)
        {
            _open = true;
            if (!EnsureCreated(gui)) return;
            _page = Page.Equipment;
            _statusText = "A grade original do inventário foi preservada.";
            _statusColor = ValheimUi.Muted;
            _root.gameObject.SetActive(true);
            _needsBuild = true;
            Refresh(true);
            SelectForNavigation(Tabs.FirstOrDefault(tab => tab.page == _page).button);
        }

        internal static void Close()
        {
            _open = false;
            HideNative(false);
            if (_root != null) _root.gameObject.SetActive(false);
            ClearOwnedPreview();
            _selectedAppearance = null;
            if (EventSystem.current != null && _root != null &&
                EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(_root))
                EventSystem.current.SetSelectedGameObject(null);
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

            if (Time.unscaledTime < _nextSignatureCheck) return;
            _nextSignatureCheck = Time.unscaledTime + 0.35f;
            Refresh(false);
        }

        private static bool EnsureCreated(InventoryGui gui)
        {
            if (gui == null || gui.m_crafting == null || !ValheimUi.EnsureAssets()) return false;
            if (_gui == gui && _root != null) return true;

            HideNative(false);
            ClearOwnedPreview();
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
            _gui = gui;
            _root = ValheimUi.Rect("DeadheimInventoryPanel", gui.m_crafting);
            ValheimUi.Stretch(_root);
            _background = _root.gameObject.AddComponent<Image>();
            _background.sprite = ValheimUi.PanelSprite;
            _background.type = Image.Type.Sliced;
            _background.color = new Color(1f, 1f, 1f, 0.99f);

            BuildTabs();
            var bodyFrame = ValheimUi.Rect("ContentFrame", _root);
            ValheimUi.Anchor(bodyFrame, new Vector2(0.035f, 0.105f), new Vector2(0.965f, 0.885f),
                Vector2.zero, Vector2.zero);
            var frameImage = bodyFrame.gameObject.AddComponent<Image>();
            frameImage.sprite = ValheimUi.FieldSprite;
            frameImage.type = Image.Type.Sliced;
            frameImage.color = new Color(0.46f, 0.39f, 0.29f, 0.75f);

            _body = ValheimUi.Rect("Content", bodyFrame);
            ValheimUi.Stretch(_body, 10f, 8f, 10f, 8f);
            _list = ValheimUi.ScrollList(_body, 5f);

            var statusRect = ValheimUi.Rect("Status", _root);
            ValheimUi.Anchor(statusRect, new Vector2(0.045f, 0.02f), new Vector2(0.95f, 0.085f),
                Vector2.zero, Vector2.zero);
            _status = ValheimUi.Label(statusRect, "", 14, ValheimUi.Muted,
                TextAlignmentOptions.Center);
            ValheimUi.Stretch((RectTransform)_status.transform, 4f, 0f, 4f, 0f);
            _root.gameObject.SetActive(_open);
            return true;
        }

        private static void BuildTabs()
        {
            Tabs.Clear();
            AddTab("Equipamento", Page.Equipment, 0);
            AddTab("Aparência", Page.Appearance, 1);
            AddTab("Atalhos", Page.QuickSlots, 2);
            AddTab("Artesanato", Page.Crafting, 3);
        }

        private static void AddTab(string title, Page page, int index)
        {
            Button button = ValheimUi.Button(_root, title, 14);
            var rect = (RectTransform)button.transform;
            float left = 0.035f + index * 0.235f;
            ValheimUi.Anchor(rect, new Vector2(left, 0.90f), new Vector2(left + 0.225f, 0.985f),
                Vector2.zero, Vector2.zero);
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            button.onClick.AddListener(() => Select(page));
            Tabs.Add((button, label, page));
        }

        private static void Select(Page page)
        {
            if (_page == Page.Appearance && page != Page.Appearance) ClearOwnedPreview();
            _page = page;
            _statusText = page == Page.Crafting ? "Artesanato original do Valheim." : "";
            _statusColor = ValheimUi.Muted;
            _needsBuild = true;
            Refresh(true);
            SelectForNavigation(page == Page.Crafting ? null : Tabs.FirstOrDefault(tab => tab.page == page).button);
        }

        private static void Refresh(bool force)
        {
            if (_root == null) return;
            UpdateTabs();

            bool crafting = _page == Page.Crafting;
            HideNative(!crafting);
            _background.enabled = !crafting;
            _body.parent.gameObject.SetActive(!crafting);
            _status.transform.parent.gameObject.SetActive(!crafting);
            if (crafting)
            {
                _lastSignature = 0;
                _needsBuild = true;
                return;
            }

            int signature = Signature();
            if (!force && !_needsBuild && signature == _lastSignature)
            {
                SetStatus(_statusText, _statusColor);
                return;
            }
            _needsBuild = false;
            _lastSignature = signature;
            ClearChildren(_list);
            switch (_page)
            {
                case Page.Equipment: BuildEquipment(); break;
                case Page.Appearance: BuildAppearance(); break;
                case Page.QuickSlots: BuildQuickSlots(); break;
            }
            SetStatus(_statusText, _statusColor);
        }

        private static void UpdateTabs()
        {
            foreach (var tab in Tabs)
            {
                tab.label.color = tab.page == _page ? new Color(1f, 0.88f, 0.38f, 1f) : ValheimUi.Orange;
                tab.button.image.color = tab.page == _page
                    ? new Color(1f, 0.90f, 0.68f, 1f)
                    : Color.white;
            }
        }

        private static void SelectForNavigation(Selectable selectable)
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(selectable != null ? selectable.gameObject : null);
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
                        if (item == null) continue;
                        int h = item.m_dropPrefab != null ? item.m_dropPrefab.name.GetHashCode() : 0;
                        h = h * 31 + item.m_stack;
                        h = h * 31 + item.m_quality;
                        h = h * 31 + (int)item.m_durability;
                        h = h * 31 + item.m_gridPos.x;
                        h = h * 31 + item.m_gridPos.y;
                        h = h * 31 + (item.m_equipped ? 1 : 0);
                        items += h * 16777619 + 1;
                    }
                }
                int result = (int)_page;
                result = result * 31 + (inventory == null ? 1 : 2);
                result = result * 31 + items;
                result = result * 31 + (TransmogBridge.Available ? TransmogBridge.Revision() : -1);
                result = result * 31 + (_selectedReal != null ? _selectedReal.GetHashCode() : 0);
                result = result * 31 + (_selectedAppearance != null ? _selectedAppearance.GetHashCode() : 0);
                return result;
            }
        }

        private static void BuildEquipment()
        {
            AddTitle("Seu equipamento", "A prévia 3D do Valheim continua visível ao lado. Esta tela só lê seus itens.");
            Inventory inventory = Player.m_localPlayer != null ? Player.m_localPlayer.GetInventory() : null;
            List<ItemDrop.ItemData> equipped = inventory?.GetAllItems()
                .Where(item => item != null && item.m_equipped && item.m_shared != null).ToList()
                ?? new List<ItemDrop.ItemData>();

            AddSection("ARMADURA");
            AddEquipmentSlot("Cabeça", ItemDrop.ItemData.ItemType.Helmet, equipped);
            AddEquipmentSlot("Peito", ItemDrop.ItemData.ItemType.Chest, equipped);
            AddEquipmentSlot("Pernas", ItemDrop.ItemData.ItemType.Legs, equipped);
            AddEquipmentSlot("Capa", ItemDrop.ItemData.ItemType.Shoulder, equipped);

            AddSection("ARMAS E UTILITÁRIOS");
            List<ItemDrop.ItemData> other = equipped.Where(item => !IsArmor(item.m_shared.m_itemType)).ToList();
            if (other.Count == 0) AddMessage("Nenhuma arma ou ferramenta equipada.");
            foreach (ItemDrop.ItemData item in other) AddItemRow("Equipado", item);
            if (equipped.Count == 0) AddMessage("Nenhum item equipado no momento.");
        }

        private static void AddEquipmentSlot(string label, ItemDrop.ItemData.ItemType type,
            List<ItemDrop.ItemData> equipped)
        {
            ItemDrop.ItemData item = equipped.FirstOrDefault(candidate => candidate.m_shared.m_itemType == type);
            AddItemRow(label, item);
        }

        private static bool IsArmor(ItemDrop.ItemData.ItemType type) =>
            type == ItemDrop.ItemData.ItemType.Helmet || type == ItemDrop.ItemData.ItemType.Chest ||
            type == ItemDrop.ItemData.ItemType.Legs || type == ItemDrop.ItemData.ItemType.Shoulder;

        private static void AddItemRow(string slot, ItemDrop.ItemData item)
        {
            var row = ValheimUi.Rect("EquipmentRow", _list);
            var bg = row.gameObject.AddComponent<Image>();
            bg.sprite = ValheimUi.FieldSprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.38f, 0.31f, 0.23f, 0.78f);
            var horizontal = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 10f;
            horizontal.padding = new RectOffset(9, 9, 5, 5);
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = true;
            ValheimUi.FixedHeight(row.gameObject, 64f);

            var iconRect = ValheimUi.Rect("ItemIcon", row);
            ValheimUi.FixedWidth(iconRect.gameObject, 48f);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            if (item != null && item.m_dropPrefab != null)
            {
                icon.sprite = ValheimUi.ItemIcon(item.m_dropPrefab.name);
                icon.enabled = icon.sprite != null;
            }
            else
            {
                icon.color = new Color(0.1f, 0.08f, 0.06f, 0.7f);
            }

            var texts = ValheimUi.Rect("ItemText", row);
            var vertical = texts.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = 1f;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandHeight = false;
            vertical.childForceExpandWidth = true;
            var flexible = texts.gameObject.AddComponent<LayoutElement>();
            flexible.flexibleWidth = 1f;
            string name = item != null ? ItemName(item) : "Vazio";
            var title = ValheimUi.Label(texts, slot + "  ·  " + name, 16,
                item != null ? ValheimUi.Beige : ValheimUi.Muted);
            ValheimUi.FixedHeight(title.gameObject, 25f);
            string detail = item == null ? "Nenhum item neste espaço" : ItemDetail(item);
            var subtitle = ValheimUi.Label(texts, detail, 12, ValheimUi.Muted);
            ValheimUi.FixedHeight(subtitle.gameObject, 19f);
            AddItemTooltip(row.gameObject, item);
        }

        private static void AddItemTooltip(GameObject target, ItemDrop.ItemData item)
        {
            if (item == null || Plugin.ShowTooltips == null || !Plugin.ShowTooltips.Value) return;
            try { ValheimUi.AddTooltip(target, ItemName(item), TooltipOf(item)); }
            catch (Exception exception) { Plugin.Log.LogWarning("Tooltip do item: " + exception.Message); }
        }

        private static string TooltipOf(ItemDrop.ItemData item) => item.GetTooltip();

        private static string ItemDetail(ItemDrop.ItemData item)
        {
            string quality = item.m_quality > 1 ? "Qualidade " + item.m_quality : "";
            string stack = item.m_stack > 1 ? " · x" + item.m_stack : "";
            string durability = "";
            if (item.m_durability > 0f)
            {
                durability = " · Durabilidade " + item.m_durability.ToString("0");
                float max = item.GetMaxDurability();
                int threshold = Plugin.LowDurabilityPercent != null ? Plugin.LowDurabilityPercent.Value : 0;
                if (item.m_shared.m_useDurability && max > 0f && item.m_durability / max * 100f < threshold)
                    durability = " · <color=#FF6A4D>Durabilidade " + item.m_durability.ToString("0") + "</color>";
            }
            return (quality + stack + durability).Trim(' ', '·');
        }

        private static void BuildQuickSlots()
        {
            AddTitle("Atalhos do jogo", "Exibição dos itens na primeira linha do inventário. Não cria espaços nem move itens.");
            Inventory inventory = Player.m_localPlayer != null ? Player.m_localPlayer.GetInventory() : null;
            if (inventory == null)
            {
                AddMessage("O inventário do personagem ainda não está disponível.");
                return;
            }

            var row = ValheimUi.Rect("Hotbar", _list);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 5f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ValheimUi.FixedHeight(row.gameObject, 94f);

            int count = Math.Min(8, inventory.GetWidth());
            for (int index = 0; index < count; index++)
                AddHotkeyCell(row, index, inventory.GetItemAt(index, 0));

            AddSection("COMO USAR");
            AddMessage("Os números 1–8 continuam sendo os atalhos nativos do Valheim. Arraste os itens na grade original para reorganizar a primeira linha.");
        }

        private static void AddHotkeyCell(Transform parent, int index, ItemDrop.ItemData item)
        {
            var cell = ValheimUi.Rect("Hotkey " + (index + 1), parent);
            var background = cell.gameObject.AddComponent<Image>();
            background.sprite = ValheimUi.FieldSprite;
            background.type = Image.Type.Sliced;
            background.color = new Color(0.30f, 0.25f, 0.19f, 0.9f);
            ValheimUi.FixedWidth(cell.gameObject, 56f);

            var key = ValheimUi.Label(cell, (index + 1).ToString(), 12, ValheimUi.Orange,
                TextAlignmentOptions.TopLeft);
            ValheimUi.Anchor((RectTransform)key.transform, new Vector2(0f, 0.75f), new Vector2(0.25f, 1f),
                new Vector2(5f, 0f), Vector2.zero);
            if (item != null && item.m_dropPrefab != null)
            {
                var iconRect = ValheimUi.Rect("Icon", cell);
                ValheimUi.Anchor(iconRect, new Vector2(0.18f, 0.05f), new Vector2(0.82f, 0.78f),
                    Vector2.zero, Vector2.zero);
                var icon = iconRect.gameObject.AddComponent<Image>();
                icon.sprite = ValheimUi.ItemIcon(item.m_dropPrefab.name);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                AddItemTooltip(cell.gameObject, item);
            }
        }

        private static void BuildAppearance()
        {
            AddTitle("Aparência", "Prévia local. A aparência real só muda depois da confirmação pelas regras do Deadheim.");
            if (!TransmogBridge.Available)
            {
                AddMessage("A integração de aparência do Deadheim não está disponível. Confirme que o mod principal está instalado e atualizado.");
                return;
            }
            if (!TransmogBridge.Enabled())
            {
                ClearOwnedPreview();
                AddMessage("O servidor desativou a troca de aparência.");
                return;
            }

            Inventory inventory = Player.m_localPlayer != null ? Player.m_localPlayer.GetInventory() : null;
            List<ItemDrop.ItemData> equipped = inventory?.GetAllItems()
                .Where(item => item != null && item.m_equipped && item.m_shared != null &&
                               !string.IsNullOrEmpty(TransmogBridge.SlotOf(Prefab(item))))
                .OrderBy(item => TransmogBridge.SlotOf(Prefab(item)), StringComparer.Ordinal)
                .ToList() ?? new List<ItemDrop.ItemData>();
            if (equipped.Count == 0)
            {
                AddMessage("Vista uma peça de armadura elegível. O servidor libera aparências ao detectar peças equipadas.");
                return;
            }

            if (string.IsNullOrEmpty(_selectedReal) || equipped.All(item => Prefab(item) != _selectedReal))
            {
                ClearOwnedPreview();
                _selectedReal = Prefab(equipped[0]);
                _selectedAppearance = null;
            }

            AddSection("PEÇA EQUIPADA");
            foreach (ItemDrop.ItemData item in equipped)
            {
                string prefab = Prefab(item);
                string title = ItemName(item) + (prefab == _selectedReal ? "  ·  selecionada" : "");
                Button choice = ValheimUi.Button(_list, title, 14);
                choice.onClick.AddListener(() =>
                {
                    if (_selectedReal != prefab) ClearOwnedPreview();
                    _selectedReal = prefab;
                    _selectedAppearance = null;
                    _statusText = "Peça selecionada: " + ItemName(item);
                    _statusColor = ValheimUi.Muted;
                    _needsBuild = true;
                    Refresh(true);
                });
            }

            ItemDrop.ItemData selected = equipped.FirstOrDefault(item => Prefab(item) == _selectedReal);
            if (selected == null) return;
            var unlocked = new HashSet<string>(TransmogBridge.Unlocked(), StringComparer.Ordinal);
            var chosenLooks = TransmogBridge.Chosen();
            List<string> choices = unlocked
                .Where(prefab => TransmogBridge.SlotOf(prefab) == TransmogBridge.SlotOf(_selectedReal))
                .Where(prefab => TransmogBridge.WhyIncompatible(_selectedReal, prefab) == null)
                .OrderBy(prefab => DisplayName(prefab), StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            bool hasCurrentLook = chosenLooks.ContainsKey(_selectedReal);

            AddSection("APARÊNCIAS DESBLOQUEADAS");
            if (choices.Count == 0)
                AddMessage("Ainda não há aparências compatíveis desbloqueadas para esta peça.");
            foreach (string prefab in choices)
            {
                string label = DisplayName(prefab);
                if (prefab == _selectedAppearance) label += "  ·  prévia";
                if (chosenLooks.TryGetValue(_selectedReal, out string chosen) && chosen == prefab)
                    label += "  ·  atual";
                Button choice = ValheimUi.Button(_list, label, 14);
                choice.onClick.AddListener(() =>
                {
                    _selectedAppearance = prefab;
                    TransmogBridge.Preview(_selectedReal + ">" + prefab);
                    _ownsPreview = true;
                    _statusText = "Prévia aplicada somente ao seu personagem.";
                    _statusColor = ValheimUi.Green;
                    _needsBuild = true;
                    Refresh(true);
                });
            }

            string restriction = TransmogBridge.ChangeRestriction();
            var actions = ValheimUi.Rect("AppearanceActions", _list);
            var actionLayout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 8f;
            actionLayout.childControlWidth = true;
            actionLayout.childControlHeight = true;
            actionLayout.childForceExpandWidth = true;
            actionLayout.childForceExpandHeight = false;
            ValheimUi.FixedHeight(actions.gameObject, 48f);

            Button apply = ValheimUi.Button(actions, "Aplicar aparência", 14);
            apply.interactable = _selectedAppearance != null && restriction == null;
            apply.onClick.AddListener(() =>
            {
                string result = TransmogBridge.Apply(_selectedReal, _selectedAppearance);
                _statusText = result;
                _statusColor = string.IsNullOrEmpty(result) ? ValheimUi.Green : ValheimUi.Orange;
                if (string.IsNullOrEmpty(result))
                {
                    _statusText = "Pedido enviado; o servidor validará a troca.";
                    ClearOwnedPreview();
                }
                _needsBuild = true;
                Refresh(true);
            });

            Button revert = ValheimUi.Button(actions, "Voltar ao original", 14);
            revert.interactable = hasCurrentLook;
            revert.onClick.AddListener(() =>
            {
                string result = TransmogBridge.Revert(_selectedReal);
                ClearOwnedPreview();
                _selectedAppearance = null;
                _statusText = string.IsNullOrEmpty(result)
                    ? "Pedido para voltar à aparência original enviado."
                    : result;
                _statusColor = string.IsNullOrEmpty(result) ? ValheimUi.Green : ValheimUi.Orange;
                _needsBuild = true;
                Refresh(true);
            });

            if (restriction != null)
                AddMessage(restriction + " A prévia continua disponível aqui.");
            else if (TransmogBridge.IsSuppressedHere())
                AddMessage("Nesta área de arena ou raid, os outros jogadores verão a aparência original.");
        }

        private static string Prefab(ItemDrop.ItemData item) => item?.m_dropPrefab != null ? item.m_dropPrefab.name : "";

        private static string ItemName(ItemDrop.ItemData item)
        {
            string raw = item?.m_shared?.m_name;
            if (string.IsNullOrEmpty(raw)) return Prefab(item);
            return Localization.instance != null ? Localization.instance.Localize(raw) : raw;
        }

        private static string DisplayName(string prefab)
        {
            if (ObjectDB.instance != null)
            {
                GameObject go = ObjectDB.instance.GetItemPrefab(prefab);
                ItemDrop drop = go != null ? go.GetComponent<ItemDrop>() : null;
                string name = drop?.m_itemData?.m_shared?.m_name;
                if (!string.IsNullOrEmpty(name) && Localization.instance != null)
                    return Localization.instance.Localize(name);
            }
            return prefab;
        }

        private static void AddTitle(string title, string description)
        {
            var titleText = ValheimUi.Label(_list, title, 25, ValheimUi.Orange, display: true);
            ValheimUi.FixedHeight(titleText.gameObject, 38f);
            var desc = ValheimUi.Label(_list, description, 13, ValheimUi.Muted);
            ValheimUi.FixedHeight(desc.gameObject, 34f);
        }

        private static void AddSection(string title)
        {
            var label = ValheimUi.Label(_list, title, 13, ValheimUi.Orange);
            ValheimUi.FixedHeight(label.gameObject, 24f);
        }

        private static void AddMessage(string message)
        {
            var box = ValheimUi.Rect("Message", _list);
            var image = box.gameObject.AddComponent<Image>();
            image.sprite = ValheimUi.FieldSprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.28f, 0.23f, 0.17f, 0.68f);
            var label = ValheimUi.Label(box, message, 14, ValheimUi.Beige,
                TextAlignmentOptions.Center);
            ValheimUi.Stretch((RectTransform)label.transform, 12f, 7f, 12f, 7f);
            ValheimUi.FixedHeight(box.gameObject, 58f);
        }

        private static void SetStatus(string text, Color color)
        {
            _statusText = text ?? "";
            _statusColor = color;
            if (_status != null)
            {
                _status.text = _statusText;
                _status.color = _statusColor;
            }
        }

        private static void ClearOwnedPreview()
        {
            if (!_ownsPreview) return;
            if (TransmogBridge.Available) TransmogBridge.ClearPreview();
            _ownsPreview = false;
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

        // Esconde (alpha 0, sem raycast) os filhos nativos do painel de crafting enquanto o painel
        // do mod está aberto. CanvasGroup evita brigar com os SetActive que o jogo faz neles.
        private static void HideNative(bool hide)
        {
            if (hide == _nativeHidden) return;
            if (hide)
            {
                if (_gui == null || _gui.m_crafting == null) return;
                _nativeGroups.Clear();
                Transform crafting = _gui.m_crafting;
                for (int index = 0; index < crafting.childCount; index++)
                {
                    Transform child = crafting.GetChild(index);
                    if (_root != null && child == _root) continue;
                    CanvasGroup group = child.GetComponent<CanvasGroup>();
                    bool added = group == null;
                    if (added) group = child.gameObject.AddComponent<CanvasGroup>();
                    _nativeGroups.Add((group, group.alpha, group.blocksRaycasts, added));
                    group.alpha = 0f;
                    group.blocksRaycasts = false;
                }
                _nativeHidden = true;
                return;
            }

            foreach (var entry in _nativeGroups)
            {
                if (entry.group == null) continue;
                if (entry.added) UnityEngine.Object.Destroy(entry.group);
                else
                {
                    entry.group.alpha = entry.alpha;
                    entry.group.blocksRaycasts = entry.blocks;
                }
            }
            _nativeGroups.Clear();
            _nativeHidden = false;
        }

        // Chamado ao sair do mundo: solta tudo que pertence à cena que vai ser destruída.
        internal static void Reset()
        {
            Close();
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
            _root = null;
            _gui = null;
            _selectedReal = null;
            _selectedAppearance = null;
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
