using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ItemType = ItemDrop.ItemData.ItemType;

namespace Deadheim.InventoryUI
{
    // Ficha do personagem no estilo da do WoW, logo abaixo da grade do inventário (lado esquerdo):
    // nome no topo, slots nas laterais (3 de cada lado), armas embaixo, personagem 3D no centro
    // (clone isolado, ver CharacterPreview) e atributos sob ele. Somente exibição.
    internal static class InventoryUi
    {
        private const float PanelWidth = 486f;
        private const float PanelHeight = 576f;
        private const float Pad = 30f;
        private const float SlotSize = 60f;
        private const float Gap = 8f;
        private const float TitleHeight = 54f;
        private const float StatsHeight = 78f;
        private const float BottomRowTop = Pad + SlotSize + 10f;

        private static InventoryGui _gui;
        private static RectTransform _root;
        private static TextMeshProUGUI _title;
        private static RectTransform _leftSlots;
        private static RectTransform _rightSlots;
        private static RectTransform _bottomSlots;
        private static RawImage _preview;
        private static readonly List<(Func<Player, string> read, TextMeshProUGUI value, Func<Player, Color> colorize)> Attributes =
            new List<(Func<Player, string>, TextMeshProUGUI, Func<Player, Color>)>();
        private static bool _open;
        private static bool _needsBuild;
        private static float _nextSignatureCheck;
        private static int _lastSignature;

        internal static bool IsOpen => _open;

        internal static void Open(InventoryGui gui)
        {
            _open = true;
            if (!EnsureCreated(gui)) return;
            _root.gameObject.SetActive(true);
            PanelDrag.Clamp(_root);
            CharacterPreview.SetActive(true);
            _needsBuild = true;
            Refresh(true);
        }

        internal static void Close()
        {
            _open = false;
            ClosePicker();
            CharacterPreview.SetActive(false);
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

            UpdateTitle();
            UpdateAttributes();
            CharacterPreview.Tick();
            if (_preview != null && _preview.texture != CharacterPreview.Texture) _preview.texture = CharacterPreview.Texture;
            if (Time.unscaledTime < _nextSignatureCheck) return;
            _nextSignatureCheck = Time.unscaledTime + 0.35f;
            Refresh(false);
        }

        private static bool EnsureCreated(InventoryGui gui)
        {
            if (gui == null || gui.m_player == null || !ValheimUi.EnsureAssets()) return false;
            if (_gui == gui && _root != null) return true;

            ReleaseSelection();
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
            _gui = gui;

            // Filho do painel da grade do inventário, preso à borda de baixo dele (alinhado à esquerda).
            _root = ValheimUi.Rect("DeadheimInventoryPanel", gui.m_player);
            _root.anchorMin = _root.anchorMax = new Vector2(0f, 0f);
            _root.pivot = new Vector2(0f, 1f);
            _root.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            _root.anchoredPosition = PanelDrag.BasePosition + PanelDrag.SavedOffset;
            // Mesma madeira escura do painel da grade (lida do próprio jogo).
            var background = _root.gameObject.AddComponent<Image>();
            Theme.ApplyPanel(background, gui);

            // Barra do título: arrastar move o painel; clique duplo volta à posição padrão.
            var titleRect = ValheimUi.Rect("Title", _root);
            ValheimUi.Anchor(titleRect, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(Pad, -TitleHeight + 2f), new Vector2(-Pad, -14f));
            var handle = titleRect.gameObject.AddComponent<Image>();
            handle.color = new Color(0f, 0f, 0f, 0f);
            titleRect.gameObject.AddComponent<PanelDrag>().Target = _root;
            _title = ValheimUi.Label(titleRect, "", 26, Theme.Title, TextAlignmentOptions.Center, display: true);
            ValheimUi.Stretch((RectTransform)_title.transform, 0f, 4f, 0f, 0f);
            _title.textWrappingMode = TextWrappingModes.NoWrap;
            _title.characterSpacing = 4f;
            // Filete dourado sob o nome, apagando nas pontas.
            var divider = ValheimUi.Rect("Divider", _root);
            divider.anchorMin = divider.anchorMax = new Vector2(0.5f, 1f);
            divider.sizeDelta = new Vector2(PanelWidth * 0.62f, 2f);
            divider.anchoredPosition = new Vector2(0f, -TitleHeight + 2f);
            var line = divider.gameObject.AddComponent<Image>();
            line.sprite = Theme.FadeLine();
            line.color = Theme.Gold;
            line.raycastTarget = false;

            float side = Pad + SlotSize + Gap;
            _leftSlots = SlotColumn("LeftSlots", new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(Pad, BottomRowTop), new Vector2(Pad + SlotSize, -TitleHeight));
            _rightSlots = SlotColumn("RightSlots", new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-Pad - SlotSize, BottomRowTop), new Vector2(-Pad, -TitleHeight));

            BuildPreview(new Vector2(side, BottomRowTop + StatsHeight + Gap), new Vector2(-side, -TitleHeight));

            var stats = ValheimUi.Rect("Stats", _root);
            ValheimUi.Anchor(stats, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(side, BottomRowTop), new Vector2(-side, BottomRowTop + StatsHeight));
            BuildAttributes(stats);

            _bottomSlots = ValheimUi.Rect("BottomSlots", _root);
            ValheimUi.Anchor(_bottomSlots, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(Pad, Pad), new Vector2(-Pad, Pad + SlotSize));
            var row = _bottomSlots.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 14f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            _root.gameObject.SetActive(_open);
            return true;
        }

        private static RectTransform SlotColumn(string name, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            var column = ValheimUi.Rect(name, _root);
            ValheimUi.Anchor(column, min, max, offsetMin, offsetMax);
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 22f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return column;
        }

        private static void UpdateTitle()
        {
            Player player = Player.m_localPlayer;
            string name = player != null ? player.GetPlayerName() : "";
            if (_title != null && _title.text != name) _title.text = name;
        }

        private static void BuildPreview(Vector2 offsetMin, Vector2 offsetMax)
        {
            var frameRect = ValheimUi.Rect("CharacterFrame", _root);
            ValheimUi.Anchor(frameRect, Vector2.zero, Vector2.one, offsetMin, offsetMax);
            var frame = frameRect.gameObject.AddComponent<Image>();
            frame.sprite = ValheimUi.FieldSprite;
            frame.type = Image.Type.Sliced;
            frame.color = Theme.Recess;
            frame.raycastTarget = false;

            // A imagem ocupa a moldura toda (o fundo da câmera tem a mesma cor do recesso).
            var imageRect = ValheimUi.Rect("Character", frameRect);
            ValheimUi.Stretch(imageRect, 3f, 3f, 3f, 3f);
            _preview = imageRect.gameObject.AddComponent<RawImage>();
            _preview.texture = CharacterPreview.Texture;
            _preview.raycastTarget = true;
            imageRect.gameObject.AddComponent<PreviewDrag>();
            var fitter = imageRect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)CharacterPreview.TextureWidth / CharacterPreview.TextureHeight;
        }

        // ---------- Atributos (duas colunas sob o personagem, como no WoW) ----------

        private static void BuildAttributes(RectTransform host)
        {
            Attributes.Clear();
            var left = StatsBox(host, "StatsLeft", new Vector2(0f, 0f), new Vector2(0.5f, 1f), 0f, Gap / 2f);
            var right = StatsBox(host, "StatsRight", new Vector2(0.5f, 0f), new Vector2(1f, 1f), Gap / 2f, 0f);

            AddAttribute(left, "Vida", p => $"{p.GetHealth():0}/{p.GetMaxHealth():0}", Theme.Health);
            AddAttribute(left, "Stamina", p => $"{p.GetStamina():0}/{p.GetMaxStamina():0}", Theme.Stamina);
            AddAttribute(left, "Eitr", p => $"{p.GetEitr():0}/{p.GetMaxEitr():0}", Theme.Eitr);
            AddAttribute(right, "Armadura", p => p.GetBodyArmor().ToString("0"));
            // Abaixo de 100% a velocidade é penalidade: tom de alerta.
            AddAttribute(right, "Velocidade",
                p => (100f * (1f + p.GetEquipmentMovementModifier())).ToString("0") + "%",
                p => p.GetEquipmentMovementModifier() < -0.001f ? Theme.Penalty : Theme.Value);
            AddAttribute(right, "Peso", p =>
            {
                Inventory inventory = p.GetInventory();
                return inventory == null ? "-" : $"{inventory.GetTotalWeight():0}/{p.GetMaxCarryWeight():0}";
            }, p =>
            {
                Inventory inventory = p.GetInventory();
                return inventory != null && inventory.GetTotalWeight() > p.GetMaxCarryWeight() * 0.9f ? Theme.Penalty : Theme.Value;
            });
        }

        private static RectTransform StatsBox(RectTransform host, string name, Vector2 min, Vector2 max,
            float insetLeft, float insetRight)
        {
            var box = ValheimUi.Rect(name, host);
            ValheimUi.Anchor(box, min, max, new Vector2(insetLeft, 0f), new Vector2(-insetRight, 0f));
            var image = box.gameObject.AddComponent<Image>();
            image.sprite = ValheimUi.FieldSprite;
            image.type = Image.Type.Sliced;
            image.color = Theme.Recess;
            image.raycastTarget = false;
            var layout = box.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 6, 6);
            layout.spacing = 1f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return box;
        }

        private static void AddAttribute(RectTransform parent, string name, Func<Player, string> read, Color color) =>
            AddAttribute(parent, name, read, _ => color);

        private static void AddAttribute(RectTransform parent, string name, Func<Player, string> read,
            Func<Player, Color> colorize = null)
        {
            var row = ValheimUi.Rect("Attribute", parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var label = ValheimUi.Label(row, name, 13, Theme.Label, TextAlignmentOptions.MidlineLeft);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var value = ValheimUi.Label(row, "-", 13, Theme.Value, TextAlignmentOptions.MidlineRight);
            value.textWrappingMode = TextWrappingModes.NoWrap;
            value.fontStyle = FontStyles.Bold;
            value.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Attributes.Add((read, value, colorize));
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
                if (attribute.colorize == null) continue;
                Color color;
                try { color = attribute.colorize(player); }
                catch (Exception) { color = Theme.Value; }
                if (attribute.value.color != color) attribute.value.color = color;
            }
        }

        // ---------- Slots de equipamento ----------

        private static void Refresh(bool force)
        {
            if (_root == null) return;
            int signature = Signature();
            if (!force && !_needsBuild && signature == _lastSignature) return;
            _needsBuild = false;
            _lastSignature = signature;
            ClosePicker();
            ClearChildren(_leftSlots);
            ClearChildren(_rightSlots);
            ClearChildren(_bottomSlots);
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

        // Cada slot mostra o ícone de como a peça aparece (a aparência aplicada, se houver);
        // tooltip e durabilidade são da peça real.
        private static void BuildEquipment()
        {
            Player player = Player.m_localPlayer;
            Inventory inventory = player != null ? player.GetInventory() : null;
            List<ItemDrop.ItemData> equipped = inventory?.GetAllItems()
                .Where(item => item != null && item.m_equipped && item.m_shared != null).ToList()
                ?? new List<ItemDrop.ItemData>();
            Dictionary<string, string> looks = TransmogBridge.Available
                ? TransmogBridge.Chosen() : new Dictionary<string, string>();

            AddSlot(_leftSlots, "Cabeça", Find(equipped, ItemType.Helmet), looks, PickerSide.Right, ItemType.Helmet);
            AddSlot(_leftSlots, "Capa", Find(equipped, ItemType.Shoulder), looks, PickerSide.Right, ItemType.Shoulder);
            AddSlot(_leftSlots, "Peito", Find(equipped, ItemType.Chest), looks, PickerSide.Right, ItemType.Chest);
            AddSlot(_rightSlots, "Pernas", Find(equipped, ItemType.Legs), looks, PickerSide.Left, ItemType.Legs);
            AddSlot(_rightSlots, "Utilitário", Find(equipped, ItemType.Utility), looks, PickerSide.Left, ItemType.Utility);
            AddSlot(_rightSlots, "Berloque", Find(equipped, ItemType.Trinket), looks, PickerSide.Left, ItemType.Trinket);
            // Mãos como o Humanoid.EquipItem distribui: arco/escudo/arma de mão esquerda vão para a esquerda,
            // tocha pode ir para qualquer uma.
            AddSlot(_bottomSlots, "Mão direita", HumanoidItem(player, "m_rightItem"), looks, PickerSide.Up,
                ItemType.OneHandedWeapon, ItemType.TwoHandedWeapon, ItemType.Tool, ItemType.Torch);
            AddSlot(_bottomSlots, "Mão esquerda", HumanoidItem(player, "m_leftItem"), looks, PickerSide.Up,
                ItemType.Shield, ItemType.Bow, ItemType.TwoHandedWeaponLeft, ItemType.Torch);
            AddSlot(_bottomSlots, "Munição", HumanoidItem(player, "m_ammoItem"), looks, PickerSide.Up, ItemType.Ammo);
        }

        private enum PickerSide { Right, Left, Up }

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

        private static ItemDrop.ItemData Find(List<ItemDrop.ItemData> equipped, ItemType type) =>
            equipped.FirstOrDefault(candidate => candidate.m_shared.m_itemType == type);

        private static void AddSlot(Transform parent, string label, ItemDrop.ItemData item,
            Dictionary<string, string> looks, PickerSide side, params ItemType[] accepts)
        {
            InventoryElement element = NativeSlot(parent, label);
            RectTransform slot = element != null ? (RectTransform)element.transform : FallbackSlot(parent, label);
            ValheimUi.FixedHeight(slot.gameObject, SlotSize);
            ValheimUi.FixedWidth(slot.gameObject, SlotSize);

            if (item != null && item.m_shared != null)
            {
                Sprite sprite = null;
                string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : null;
                if (prefab != null && looks.TryGetValue(prefab, out string look) && !string.IsNullOrEmpty(look))
                    sprite = ValheimUi.ItemIcon(look);
                ShowItem(element, slot, item, sprite);
                // Clique na peça equipada: desequipa (mesmo caminho do clique direito no inventário).
                OnClick(element, slot, () => ToggleEquip(item));
            }
            else
            {
                // Slot vazio: nome apagado; o clique abre o que dá para equipar ali, vindo da bolsa.
                var caption = ValheimUi.Label(slot, label, 11, Theme.EmptyCaption, TextAlignmentOptions.Center);
                ValheimUi.Stretch((RectTransform)caption.transform, 3f, 3f, 3f, 3f);
                OnClick(element, slot, () => TogglePicker(slot, label, side, accepts));
            }
        }

        // Ícone, barra de durabilidade, qualidade e pilha iguais aos da grade do inventário.
        private static void ShowItem(InventoryElement element, RectTransform slot, ItemDrop.ItemData item, Sprite sprite)
        {
            if (sprite == null) sprite = item.GetIcon();
            Image icon = element != null ? element.m_icon : null;
            if (icon == null)
            {
                var iconRect = ValheimUi.Rect("Icon", slot);
                ValheimUi.Stretch(iconRect, 6f, 6f, 6f, 6f);
                icon = iconRect.gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }
            icon.sprite = sprite;
            icon.color = Color.white;
            icon.enabled = sprite != null;

            if (element != null)
            {
                bool worn = item.m_shared.m_useDurability && item.m_durability < item.GetMaxDurability();
                if (element.m_durability != null)
                {
                    element.m_durability.gameObject.SetActive(worn);
                    if (worn)
                    {
                        if (item.m_durability <= 0f)
                        {
                            element.m_durability.SetValue(1f);
                            element.m_durability.SetColor(Color.red);
                        }
                        else
                            element.m_durability.SetValue(item.GetDurabilityPercentage());
                    }
                }
                if (element.m_quality != null && item.m_shared.m_maxQuality > 1)
                {
                    element.m_quality.enabled = true;
                    element.m_quality.text = item.m_quality.ToString();
                }
                if (element.m_amount != null && item.m_shared.m_maxStackSize > 1)
                {
                    element.m_amount.enabled = true;
                    element.m_amount.text = $"{item.m_stack}/{item.m_shared.m_maxStackSize}";
                }
            }
            AddItemTooltip(element != null && element.m_tooltip != null ? element.m_tooltip.gameObject : slot.gameObject, item);
        }

        // Clique pelo UIInputHandler do próprio slot (é ele que recebe o clique na grade).
        private static void OnClick(InventoryElement element, RectTransform slot, Action action)
        {
            UIInputHandler input = element != null ? element.GetComponentInChildren<UIInputHandler>() : null;
            if (input != null)
            {
                input.m_onLeftClick = _ => action();
                input.m_onRightClick = _ => action();
                return;
            }
            var relay = slot.GetComponent<ClickRelay>() ?? slot.gameObject.AddComponent<ClickRelay>();
            relay.Action = action;
            var graphic = slot.GetComponent<Graphic>();
            if (graphic != null) graphic.raycastTarget = true;
        }

        // Equipar/desequipar pelo caminho do jogo (respeita tempo de equipar armadura e regras das mãos).
        private static void ToggleEquip(ItemDrop.ItemData item)
        {
            Player player = Player.m_localPlayer;
            if (player == null || item == null) return;
            ClosePicker();
            try { player.UseItem(null, item, true); }
            catch (Exception exception) { Plugin.Log.LogWarning("Equipar/desequipar: " + exception.Message); }
            _needsBuild = true;
            _nextSignatureCheck = 0f;
        }

        // ---------- Lista de itens da bolsa para um slot vazio ----------

        private const float PickerSlot = 56f;
        private const int PickerColumns = 5;
        private static RectTransform _picker;
        private static string _pickerFor;

        private static void TogglePicker(RectTransform slot, string label, PickerSide side, ItemType[] accepts)
        {
            bool same = _picker != null && _pickerFor == label;
            ClosePicker();
            if (same || _root == null) return;
            Player player = Player.m_localPlayer;
            Inventory inventory = player != null ? player.GetInventory() : null;
            if (inventory == null) return;

            List<ItemDrop.ItemData> options = inventory.GetAllItems()
                .Where(item => item != null && item.m_shared != null && !item.m_equipped && item.IsEquipable()
                    && accepts.Contains(item.m_shared.m_itemType))
                .OrderByDescending(item => item.m_quality)
                .ThenBy(item => ItemName(item))
                .ToList();

            // Fundo invisível bem maior que a tela: clicar fora fecha a lista.
            _pickerFor = label;
            _picker = ValheimUi.Rect("PickerBlocker", _root);
            ValheimUi.Stretch(_picker, -6000f, -6000f, -6000f, -6000f);
            var blocker = _picker.gameObject.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0f);
            _picker.gameObject.AddComponent<ClickRelay>().Action = ClosePicker;

            var panel = ValheimUi.Rect("Picker", _picker);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            var background = panel.gameObject.AddComponent<Image>();
            Theme.ApplyPanel(background, _gui);
            panel.gameObject.AddComponent<ClickRelay>();   // clique no próprio painel não fecha
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 14, 20);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var title = ValheimUi.Label(panel, label, 18, Theme.Title, TextAlignmentOptions.Center, display: true);
            title.textWrappingMode = TextWrappingModes.NoWrap;

            if (options.Count == 0)
            {
                var empty = ValheimUi.Label(panel, "Nada na bolsa para este slot", 13, Theme.Label, TextAlignmentOptions.Center);
                empty.textWrappingMode = TextWrappingModes.NoWrap;
            }
            else
            {
                var gridRect = ValheimUi.Rect("Options", panel);
                var grid = gridRect.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(PickerSlot, PickerSlot);
                grid.spacing = new Vector2(6f, 6f);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = Mathf.Min(PickerColumns, options.Count);
                grid.childAlignment = TextAnchor.UpperCenter;
                foreach (ItemDrop.ItemData option in options)
                {
                    InventoryElement element = NativeSlot(gridRect, ItemName(option));
                    RectTransform cell = element != null ? (RectTransform)element.transform : FallbackSlot(gridRect, ItemName(option));
                    ShowItem(element, cell, option, null);
                    ItemDrop.ItemData chosen = option;
                    OnClick(element, cell, () => ToggleEquip(chosen));
                }
            }

            // Encosta no slot: ao lado (colunas) ou acima (linha das mãos), sem sair da tela.
            var corners = new Vector3[4];
            slot.GetWorldCorners(corners);
            Vector3 anchor;
            Vector2 nudge;
            switch (side)
            {
                case PickerSide.Right:
                    panel.pivot = new Vector2(0f, 0.5f);
                    anchor = (corners[2] + corners[3]) * 0.5f;
                    nudge = new Vector2(8f, 0f);
                    break;
                case PickerSide.Left:
                    panel.pivot = new Vector2(1f, 0.5f);
                    anchor = (corners[0] + corners[1]) * 0.5f;
                    nudge = new Vector2(-8f, 0f);
                    break;
                default:
                    panel.pivot = new Vector2(0.5f, 0f);
                    anchor = (corners[1] + corners[2]) * 0.5f;
                    nudge = new Vector2(0f, 8f);
                    break;
            }
            Vector2 local = _picker.InverseTransformPoint(anchor);
            Vector2 center = _picker.rect.center;
            panel.anchoredPosition = local - center + nudge;
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            PanelDrag.Clamp(panel);
        }

        private static void ClosePicker()
        {
            _pickerFor = null;
            if (_picker == null) return;
            ReleaseSelection();
            UnityEngine.Object.Destroy(_picker.gameObject);
            _picker = null;
        }

        // Cópia do próprio slot da grade do inventário (fundo, realce do mouse, barra de durabilidade
        // e fonte de qualidade nativos), com tudo que é de grade desligado.
        private static InventoryElement NativeSlot(Transform parent, string label)
        {
            GameObject prefab = _gui != null && _gui.m_playerGrid != null ? _gui.m_playerGrid.m_elementPrefab : null;
            if (prefab == null) return null;
            GameObject go = UnityEngine.Object.Instantiate(prefab, parent);
            go.name = "Slot " + label;
            var element = go.GetComponent<InventoryElement>();
            if (element == null)
            {
                UnityEngine.Object.Destroy(go);
                return null;
            }
            try { element.Initialize(0, 0); }
            catch (Exception) { if (element.m_dropFocus != null) element.m_dropFocus.color = Color.clear; }
            if (element.m_button != null)
                element.m_button.navigation = new Navigation { mode = Navigation.Mode.None };
            // Sem arrastar: o painel só exibe.
            foreach (UIDragHandler drag in go.GetComponentsInChildren<UIDragHandler>(true))
                UnityEngine.Object.Destroy(drag);
            Transform binding = go.transform.Find("binding");
            if (binding != null) binding.gameObject.SetActive(false);
            if (element.m_selected != null) element.m_selected.SetActive(false);
            if (element.m_durability != null) element.m_durability.gameObject.SetActive(false);
            if (element.m_icon != null) element.m_icon.enabled = false;
            if (element.m_amount != null) element.m_amount.enabled = false;
            if (element.m_quality != null) element.m_quality.enabled = false;
            // Tudo aqui está equipado: o fundo azul de "equipado" só poluiria.
            if (element.m_equiped != null) element.m_equiped.enabled = false;
            if (element.m_queued != null) element.m_queued.enabled = false;
            if (element.m_noteleport != null) element.m_noteleport.enabled = false;
            if (element.m_food != null) element.m_food.enabled = false;
            if (element.m_tooltip != null)
            {
                element.m_tooltip.m_text = "";
                element.m_tooltip.m_topic = "";
            }
            return element;
        }

        private static RectTransform FallbackSlot(Transform parent, string label)
        {
            var slot = ValheimUi.Rect("Slot " + label, parent);
            var background = slot.gameObject.AddComponent<Image>();
            background.sprite = ValheimUi.FieldSprite;
            background.type = Image.Type.Sliced;
            background.color = Theme.Recess;
            return slot;
        }

        private static void AddItemTooltip(GameObject target, ItemDrop.ItemData item)
        {
            if (item == null || Plugin.ShowTooltips == null || !Plugin.ShowTooltips.Value) return;
            try { ValheimUi.AddLazyTooltip(target, ItemName(item), () => item.GetTooltip()); }
            catch (Exception exception) { Plugin.Log.LogWarning("Tooltip do item: " + exception.Message); }
        }

        private static string ItemName(ItemDrop.ItemData item)
        {
            string raw = item?.m_shared?.m_name;
            if (string.IsNullOrEmpty(raw)) return item?.m_dropPrefab != null ? item.m_dropPrefab.name : "";
            return Localization.instance != null ? Localization.instance.Localize(raw) : raw;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
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
            CharacterPreview.Reset();
            _root = null;
            _preview = null;
            _gui = null;
            Attributes.Clear();
        }
    }

    // Clique esquerdo/direito simples (fundo da lista e slots sem prefab nativo).
    internal sealed class ClickRelay : MonoBehaviour, IPointerClickHandler
    {
        internal Action Action;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Middle) return;
            Action?.Invoke();
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
    internal static class InventoryGuiShowPatch
    {
        private static void Postfix(InventoryGui __instance)
        {
            if (Plugin.AutoOpen.Value) InventoryUi.Open(__instance);
        }
    }

    /// <summary>Porta usada pelo mod Deadheim (Central Deadheim, icone Ficha): abre/fecha o painel por reflexao.</summary>
    public static class InventoryUiApi
    {
        /// <summary>Marcado = a ficha abre sempre com o inventario (salvo na config do jogador).</summary>
        public static bool Sempre => Plugin.AutoOpen != null && Plugin.AutoOpen.Value;

        public static void Alternar()
        {
            Plugin.AutoOpen.Value = !Plugin.AutoOpen.Value;
            if (!InventoryGui.IsVisible() || InventoryGui.instance == null) return;
            if (Plugin.AutoOpen.Value) InventoryUi.Open(InventoryGui.instance);
            else InventoryUi.Close();
        }
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
