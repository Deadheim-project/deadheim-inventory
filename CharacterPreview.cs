using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace Deadheim.InventoryUI
{
    // Prévia 3D da ficha: um clone só visual do jogador (mesma técnica da tela de seleção de
    // personagem do Valheim: prefab instanciado com ZNetView.m_forceDisableInit), longe do mundo,
    // com câmera e luzes próprias. O clone copia do VisEquipment real os hashes já desenhados
    // (inclui a aparência aplicada pelo Deadheim), então mostra exatamente o que os outros veem.
    // Nada aqui altera o jogador real nem entra na rede.
    internal static class CharacterPreview
    {
        private static readonly Vector3 Origin = new Vector3(0f, 5000f, 0f);
        private const float CameraDistance = 4.1f;
        private const float LookHeight = 0.95f;
        internal const int TextureWidth = 512;
        internal const int TextureHeight = 640;

        private static GameObject _holder;
        private static GameObject _clone;
        private static VisEquipment _cloneVis;
        private static Animator[] _animators = new Animator[0];
        private static LODGroup[] _lodGroups = new LODGroup[0];
        private static Camera _camera;
        private static Light[] _lights = new Light[0];
        private static RenderTexture _renderTexture;
        private static RenderTexture _texture;
        private static float _nextRender;
        private static float _nextSync;
        private static bool _failed;
        internal static float Yaw = 15f;

        internal static Texture Texture
        {
            get
            {
                EnsureTextures();
                return _texture;
            }
        }

        internal static void SetActive(bool active)
        {
            if (!active)
            {
                if (_holder != null) _holder.SetActive(false);
                return;
            }
            _nextSync = 0f;
            _nextRender = 0f;
        }

        internal static void Tick()
        {
            Player player = Player.m_localPlayer;
            if (player == null || _failed) return;
            if (!EnsureClone(player)) return;
            if (!_holder.activeSelf)
            {
                _holder.SetActive(true);
                SetupAnimators();
            }

            float now = Time.unscaledTime;
            if (now >= _nextSync)
            {
                _nextSync = now + 0.35f;
                Sync(player);
            }
            if (now < _nextRender) return;
            _nextRender = now + 0.033f;
            Render();
        }

        // ---------- Clone ----------

        private static bool EnsureClone(Player player)
        {
            if (_clone != null && _cloneVis != null) return true;
            Destroy();
            GameObject prefab = Game.instance != null ? Game.instance.m_playerPrefab : null;
            if (prefab == null && ZNetScene.instance != null) prefab = ZNetScene.instance.GetPrefab("Player");
            if (prefab == null) return false;

            try
            {
                // Pai inativo: nenhum Awake roda até tirarmos tudo que não é visual.
                _holder = new GameObject("DeadheimPreviewHolder");
                _holder.SetActive(false);
                _holder.transform.position = Origin;
                _clone = UnityEngine.Object.Instantiate(prefab, Origin, Quaternion.identity, _holder.transform);
                _clone.name = "DeadheimPreviewCharacter";
                StripToVisual(_clone);
                _cloneVis = _clone.GetComponent<VisEquipment>();
                if (_cloneVis == null) throw new Exception("prefab do jogador sem VisEquipment");
                _cloneVis.m_playerComponent = null;

                bool previous = ZNetView.m_forceDisableInit;
                ZNetView.m_forceDisableInit = true;
                try { _holder.SetActive(true); }
                finally { ZNetView.m_forceDisableInit = previous; }

                SetupAnimators();
                EnsureCamera();
                Sync(player);
                return true;
            }
            catch (Exception exception)
            {
                _failed = true;
                Plugin.Log.LogError("Prévia do personagem desativada: " + exception);
                Destroy();
                return false;
            }
        }

        private static readonly HashSet<Type> KeepTypes = new HashSet<Type> { typeof(VisEquipment), typeof(ZNetView) };

        // Remove a lógica do personagem (Player, físicas, rede, som, eventos de animação) e deixa
        // malha, ossos, Animator, LOD, Cloth e o VisEquipment. Algumas classes dependem de outras
        // (RequireComponent), então tenta em algumas passadas, das mais "novas" para as mais antigas.
        private static void StripToVisual(GameObject root)
        {
            for (int pass = 0; pass < 6; pass++)
            {
                bool removedAny = false;
                bool remaining = false;
                MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
                for (int index = behaviours.Length - 1; index >= 0; index--)
                {
                    MonoBehaviour behaviour = behaviours[index];
                    if (behaviour == null || KeepTypes.Contains(behaviour.GetType())) continue;
                    if (BlockedByDependency(behaviour)) { remaining = true; continue; }
                    UnityEngine.Object.DestroyImmediate(behaviour);
                    removedAny = true;
                }
                if (!remaining || !removedAny) break;
            }
            foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
                if (!BlockedByDependency(body)) UnityEngine.Object.DestroyImmediate(body);
            foreach (AudioSource audio in root.GetComponentsInChildren<AudioSource>(true))
                audio.enabled = false;
        }

        // Outro componente ainda vivo no mesmo objeto exige este tipo?
        private static bool BlockedByDependency(Component component)
        {
            Type type = component.GetType();
            foreach (MonoBehaviour other in component.GetComponents<MonoBehaviour>())
            {
                if (other == null || other == component) continue;
                foreach (RequireComponent require in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                {
                    if (Requires(require.m_Type0, type) || Requires(require.m_Type1, type) || Requires(require.m_Type2, type))
                    {
                        // Se quem exige também vai sair, só precisa sair antes.
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool Requires(Type required, Type candidate) =>
            required != null && required.IsAssignableFrom(candidate);

        private static void SetupAnimators()
        {
            if (_clone == null) return;
            _animators = _clone.GetComponentsInChildren<Animator>(true);
            foreach (Animator animator in _animators)
            {
                if (animator == null) continue;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
                // Os receptores dos eventos de animação (passos, sons) foram removidos do clone.
                animator.fireEvents = false;
                // Sem o Character para alimentar o Animator, "onGround" ficaria falso (pose de queda).
                foreach (AnimatorControllerParameter parameter in animator.parameters)
                    if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == "onGround")
                        animator.SetBool(parameter.nameHash, true);
            }
        }

        // ---------- Cópia do visual do jogador real ----------

        private static readonly Dictionary<string, FieldInfo> VisFields = new Dictionary<string, FieldInfo>();

        private static T Read<T>(VisEquipment vis, string name, T fallback)
        {
            if (!VisFields.TryGetValue(name, out FieldInfo field))
                VisFields[name] = field = AccessTools.Field(typeof(VisEquipment), name);
            if (field == null) return fallback;
            object value = field.GetValue(vis);
            return value is T typed ? typed : fallback;
        }

        private static void Sync(Player player)
        {
            if (_cloneVis == null || player == null) return;
            VisEquipment real = player.GetComponent<VisEquipment>();
            if (real == null) return;
            try
            {
                ZNetView view = player.GetComponent<ZNetView>();
                ZDO zdo = view != null ? view.GetZDO() : null;

                _cloneVis.SetModel(Read(real, "m_currentModelIndex", 0));
                _cloneVis.SetSkinColor(zdo != null ? zdo.GetVec3(ZDOVars.s_skinColor, Vector3.one) : Read(real, "m_skinColor", Vector3.one));
                _cloneVis.SetHairColor(zdo != null ? zdo.GetVec3(ZDOVars.s_hairColor, Vector3.one) : Read(real, "m_hairColor", Vector3.one));
                // Cabelo/barba originais (o clone aplica de novo a regra do capacete).
                _cloneVis.SetHairItem(zdo != null ? zdo.GetInt(ZDOVars.s_hairItem) : Read(real, "m_hairItem", 0));
                _cloneVis.SetBeardItem(zdo != null ? zdo.GetInt(ZDOVars.s_beardItem) : Read(real, "m_beardItem", 0));

                // Hashes já desenhados no jogador real: incluem a aparência aplicada.
                _cloneVis.SetHelmetItem(Read(real, "m_currentHelmetItemHash", 0));
                _cloneVis.SetChestItem(Read(real, "m_currentChestItemHash", 0));
                _cloneVis.SetLegItem(Read(real, "m_currentLegItemHash", 0));
                _cloneVis.SetShoulderItem(Read(real, "m_currentShoulderItemHash", 0),
                    Read(real, "m_currentShoulderItemVariant", 0), Read(real, "m_currentShoulderItemQuality", 0));
                _cloneVis.SetUtilityItem(Read(real, "m_currentUtilityItemHash", 0));
                _cloneVis.SetTrinketItem(PrefabName(Read(real, "m_currentTrinketItemHash", 0)));
                _cloneVis.SetRightItem(Read(real, "m_currentRightItemHash", 0), Read(real, "m_currentRightItemQuality", 0));
                _cloneVis.SetLeftItem(Read(real, "m_currentLeftItemHash", 0),
                    Read(real, "m_currentLeftItemVariant", 0), Read(real, "m_currentLeftItemQuality", 0));
                _cloneVis.SetRightBackItem(Read(real, "m_currentRightBackItemHash", 0), Read(real, "m_currentRightBackItemQuality", 0));
                _cloneVis.SetLeftBackItem(Read(real, "m_currentLeftBackItemHash", 0),
                    Read(real, "m_currentLeftBackItemVariant", 0), Read(real, "m_currentLeftBackItemQuality", 0));
            }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning("Prévia do personagem (equipamento): " + exception.Message);
            }
            // Peças novas trazem LODGroups próprios.
            _lodGroups = _clone.GetComponentsInChildren<LODGroup>(true);
        }

        private static string PrefabName(int hash)
        {
            if (hash == 0 || ObjectDB.instance == null) return "";
            GameObject prefab = ObjectDB.instance.GetItemPrefab(hash);
            return prefab != null ? prefab.name : "";
        }

        // ---------- Câmera, luzes e render ----------

        private static void EnsureTextures()
        {
            if (_texture != null) return;
            // Renderiza em ARGB32 (deferred, como a câmera do jogo; em forward os shaders do Valheim
            // não desenham as armaduras). O alfa que o deferred deixa é lixo, então o painel mostra
            // uma cópia num formato sem alfa: sempre opaca.
            _renderTexture = new RenderTexture(TextureWidth, TextureHeight, 24, RenderTextureFormat.ARGB32)
                { name = "DeadheimCharacterRender", antiAliasing = 1 };
            _renderTexture.Create();
            RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float)
                ? RenderTextureFormat.RGB111110Float : RenderTextureFormat.RGB565;
            _texture = new RenderTexture(TextureWidth, TextureHeight, 0, format) { name = "DeadheimCharacterPreview" };
            _texture.Create();
        }

        private static void EnsureCamera()
        {
            EnsureTextures();
            if (_camera != null) return;
            var cameraObject = new GameObject("DeadheimPreviewCamera");
            cameraObject.transform.SetParent(_holder.transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Theme.PreviewBackground;
            _camera.cullingMask = ~0;
            _camera.useOcclusionCulling = false;
            _camera.fieldOfView = 30f;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 12f;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _camera.targetTexture = _renderTexture;
            _camera.enabled = false;
            cameraObject.transform.position = Origin + new Vector3(0f, LookHeight + 0.05f, CameraDistance);
            cameraObject.transform.rotation = Quaternion.LookRotation(
                Origin + Vector3.up * LookHeight - cameraObject.transform.position, Vector3.up);

            // Luz principal pela frente/esquerda, preenchimento pela direita e contraluz.
            _lights = new[]
            {
                CreateLight("Key", new Vector3(0.45f, -0.55f, -1f), 1.15f, new Color(1f, 0.95f, 0.86f)),
                CreateLight("Fill", new Vector3(-0.7f, -0.15f, -1f), 0.45f, new Color(0.80f, 0.86f, 1f)),
                CreateLight("Rim", new Vector3(0.1f, -0.35f, 1f), 0.65f, new Color(1f, 0.85f, 0.65f))
            };
        }

        private static Light CreateLight(string name, Vector3 direction, float intensity, Color color)
        {
            var lightObject = new GameObject("DeadheimPreviewLight" + name);
            lightObject.transform.SetParent(_holder.transform, false);
            lightObject.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.None;
            light.enabled = false;
            return light;
        }

        private static void Render()
        {
            if (_camera == null || _clone == null) return;
            _clone.transform.SetPositionAndRotation(Origin, Quaternion.Euler(0f, Yaw, 0f));

            // Isola a renderização: sem sol, neblina nem ambiente do bioma/hora do dia.
            Light sun = RenderSettings.sun;
            bool sunEnabled = sun != null && sun.enabled;
            bool fog = RenderSettings.fog;
            Color sky = RenderSettings.ambientSkyColor;
            Color equator = RenderSettings.ambientEquatorColor;
            Color ground = RenderSettings.ambientGroundColor;
            float ambientIntensity = RenderSettings.ambientIntensity;
            SphericalHarmonicsL2 probe = RenderSettings.ambientProbe;
            try
            {
                if (sunEnabled) sun.enabled = false;
                RenderSettings.fog = false;
                var ambient = new Color(0.40f, 0.38f, 0.36f);
                RenderSettings.ambientSkyColor = ambient;
                RenderSettings.ambientEquatorColor = ambient;
                RenderSettings.ambientGroundColor = ambient * 0.7f;
                RenderSettings.ambientIntensity = 1f;
                var flat = new SphericalHarmonicsL2();
                flat.AddAmbientLight(ambient);
                RenderSettings.ambientProbe = flat;
                foreach (Light light in _lights) if (light != null) light.enabled = true;
                foreach (LODGroup group in _lodGroups) if (group != null) group.ForceLOD(0);

                _camera.Render();
            }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning("Prévia do personagem (render): " + exception.Message);
            }
            finally
            {
                foreach (Light light in _lights) if (light != null) light.enabled = false;
                if (sunEnabled) sun.enabled = true;
                RenderSettings.fog = fog;
                RenderSettings.ambientSkyColor = sky;
                RenderSettings.ambientEquatorColor = equator;
                RenderSettings.ambientGroundColor = ground;
                RenderSettings.ambientIntensity = ambientIntensity;
                RenderSettings.ambientProbe = probe;
            }
            Graphics.Blit(_renderTexture, _texture);
        }

        internal static void Destroy()
        {
            if (_holder != null) UnityEngine.Object.Destroy(_holder);
            _holder = null;
            _clone = null;
            _cloneVis = null;
            _camera = null;
            _lights = new Light[0];
            _animators = new Animator[0];
            _lodGroups = new LODGroup[0];
        }

        // Saída do mundo: libera também as texturas e permite tentar de novo no próximo mundo.
        internal static void Reset()
        {
            Destroy();
            _failed = false;
            foreach (RenderTexture texture in new[] { _texture, _renderTexture })
            {
                if (texture == null) continue;
                texture.Release();
                UnityEngine.Object.Destroy(texture);
            }
            _texture = null;
            _renderTexture = null;
        }
    }

    // Arrastar a prévia gira o personagem (como na ficha do WoW).
    internal sealed class PreviewDrag : MonoBehaviour, IDragHandler
    {
        public void OnDrag(PointerEventData eventData) =>
            CharacterPreview.Yaw = Mathf.Repeat(CharacterPreview.Yaw - eventData.delta.x * 0.5f, 360f);
    }
}
