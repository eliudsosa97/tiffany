using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TiffanyGame.EditorTools
{
    public static class ModelAnimatorBuilder
    {
        [MenuItem("Tiffany/Art/Build Model Animators")]
        public static void BuildFromCommandLine()
        {
            foreach (string id in new[] { "tiffany-provisional" })
                Debug.Log($"{GameInfo.LogPrefix} Animator de {id}: {AssetDatabase.GetAssetPath(Build(id))}");
        }

        public static AnimatorController Build(string id, string outputPath = null)
        {
            var spec = ModelLibrary.LoadSpec(id);
            string path = outputPath ?? spec.controllerPath;
            Validate(spec, path);
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(spec.modelPath))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                    clips.Add(clip.name, clip);
            foreach (var entry in spec.clips)
                if (!clips.ContainsKey(entry.name))
                    throw Invalid($"Falta el clip '{entry.name}' en {spec.modelPath}.");

            var layers = spec.controller.layers;
            var masks = new AvatarMask[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                var mask = layers[i].mask;
                if (string.IsNullOrEmpty(mask?.path)) continue;
                string maskPath = outputPath == null ? mask.path :
                    Path.Combine(Path.GetDirectoryName(path), Path.GetFileName(mask.path)).Replace('\\', '/');
                masks[i] = SyncMask(mask, maskPath, spec.modelPath);
            }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                GeneratedMaterials.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                controller = new AnimatorController { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(controller, path);
            }
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            var owned = new HashSet<Object> { controller };
            string speed = spec.controller.speedParameter;
            var parameters = controller.parameters;
            if (parameters.Length != 1 || parameters[0].name != speed ||
                parameters[0].type != AnimatorControllerParameterType.Float || parameters[0].defaultFloat != 0f)
            {
                controller.parameters = new[]
                {
                    new AnimatorControllerParameter { name = speed, type = AnimatorControllerParameterType.Float }
                };
                EditorUtility.SetDirty(controller);
            }
            var previousLayers = controller.layers;
            var nextLayers = new AnimatorControllerLayer[layers.Length];
            bool layersChanged = previousLayers.Length != layers.Length;
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                var previousLayer = i < previousLayers.Length ? previousLayers[i] : null;
                var machine = Reuse(previousLayer?.stateMachine, layer.name, controller, assets, owned);
                nextLayers[i] = new AnimatorControllerLayer
                {
                    name = layer.name, stateMachine = machine, defaultWeight = layer.weight,
                    blendingMode = AnimatorLayerBlendingMode.Override, avatarMask = masks[i]
                };
                layersChanged |= !LayerMatches(previousLayer, nextLayers[i]);
                SetArray(machine, machine.stateMachines, Array.Empty<ChildAnimatorStateMachine>(), v => machine.stateMachines = v);
                SetArray(machine, machine.anyStateTransitions, Array.Empty<AnimatorStateTransition>(), v => machine.anyStateTransitions = v);
                SetArray(machine, machine.entryTransitions, Array.Empty<AnimatorTransition>(), v => machine.entryTransitions = v);
                SetArray(machine, machine.behaviours, Array.Empty<StateMachineBehaviour>(), v => machine.behaviours = v);
                var previous = machine.states;
                var states = new ChildAnimatorState[layer.states.Length];
                for (int j = 0; j < layer.states.Length; j++)
                {
                    var entry = layer.states[j];
                    var oldState = Array.Find(previous, s => s.state != null && s.state.name == entry.name);
                    var state = Reuse(oldState.state, entry.name, controller, assets, owned);
                    states[j] = new ChildAnimatorState { state = state, position = oldState.state != null ? oldState.position : new Vector3(300f, 60f * j) };
                    Set(state, state.speed, 1f, v => state.speed = v);
                    Set(state, state.writeDefaultValues, layer.writeDefaultValues, v => state.writeDefaultValues = v);
                    Set(state, state.cycleOffset, 0f, v => state.cycleOffset = v);
                    Set(state, state.cycleOffsetParameterActive, false, v => state.cycleOffsetParameterActive = v);
                    Set(state, state.timeParameterActive, false, v => state.timeParameterActive = v);
                    Set(state, state.mirror, false, v => state.mirror = v);
                    Set(state, state.mirrorParameterActive, false, v => state.mirrorParameterActive = v);
                    SetArray(state, state.transitions, Array.Empty<AnimatorStateTransition>(), v => state.transitions = v);
                    SetArray(state, state.behaviours, Array.Empty<StateMachineBehaviour>(), v => state.behaviours = v);
                    bool blend = entry.blendTree?.children != null;
                    Set(state, state.speedParameterActive, blend && entry.blendTree.normalizeSpeed, v => state.speedParameterActive = v);
                    if (blend)
                    {
                        var tree = Reuse(state.motion as BlendTree, entry.blendTree.name, controller, assets, owned);
                        ConfigureTree(tree, speed);
                        var children = SortedChildren(entry.blendTree);
                        var oldChildren = tree.children;
                        var motions = new ChildMotion[children.Length];
                        for (int k = 0; k < children.Length; k++)
                        {
                            var child = children[k];
                            float natural = NaturalSpeed(spec, child.clip);
                            Motion motion = clips[child.clip];
                            if (entry.blendTree.normalizeSpeed)
                            {
                                var old = Array.Find(oldChildren, c => c.motion != null && c.motion.name == child.clip);
                                var cycle = Reuse(old.motion as BlendTree, child.clip, controller, assets, owned);
                                ConfigureTree(cycle, speed);
                                // Speed está en m/s y cada rama se normaliza a 1 m/s.
                                SetChildren(cycle, new[] { new ChildMotion { motion = motion, timeScale = 1f / child.threshold } });
                                motion = cycle;
                            }
                            motions[k] = new ChildMotion { motion = motion, threshold = child.threshold, timeScale = child.threshold / natural };
                        }
                        SetChildren(tree, motions);
                        Set<Motion>(state, state.motion, tree, v => state.motion = v);
                        Set(state, state.speedParameter, speed, v => state.speedParameter = v);
                    }
                    else Set<Motion>(state, state.motion, string.IsNullOrEmpty(entry.clip) ? null : clips[entry.clip], v => state.motion = v);
                    if (entry.name == layer.defaultState) Set(machine, machine.defaultState, state, v => machine.defaultState = v);
                }
                SetArray(machine, machine.states, states, v => machine.states = v);
            }
            if (layersChanged)
            {
                controller.layers = nextLayers;
                EditorUtility.SetDirty(controller);
            }
            foreach (var asset in assets)
                if (!owned.Contains(asset))
                {
                    Object.DestroyImmediate(asset, true);
                    EditorUtility.SetDirty(controller);
                }
            foreach (var asset in owned)
                if (EditorUtility.IsDirty(asset)) AssetDatabase.SaveAssetIfDirty(asset);
            return controller;
        }

        static InvalidDataException Invalid(string message) => new InvalidDataException($"{GameInfo.LogPrefix} {message}");
        static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
        static float NaturalSpeed(ModelLibrary.ModelSpec spec, string clip) => Array.Find(spec.clips, c => c.name == clip).speed;

        static ModelLibrary.BlendChildSpec[] SortedChildren(ModelLibrary.BlendTreeSpec tree)
        {
            var children = (ModelLibrary.BlendChildSpec[])tree.children.Clone();
            Array.Sort(children, (a, b) => a.threshold.CompareTo(b.threshold));
            return children;
        }

        static void Validate(ModelLibrary.ModelSpec spec, string path)
        {
            if (string.IsNullOrEmpty(spec.modelPath) || string.IsNullOrEmpty(path) || spec.clips == null ||
                string.IsNullOrEmpty(spec.controller?.speedParameter) || spec.controller.layers == null || spec.controller.layers.Length == 0)
                throw Invalid("El modelo necesita clips, modelPath, controllerPath y una definición del controller.");
            var names = new HashSet<string>();
            foreach (var clip in spec.clips)
                if (string.IsNullOrEmpty(clip.name) || !names.Add(clip.name)) throw Invalid("Los clips necesitan nombres únicos.");
            var layerNames = new HashSet<string>();
            foreach (var layer in spec.controller.layers)
            {
                if (string.IsNullOrEmpty(layer.name) || !layerNames.Add(layer.name) || layer.states == null ||
                    !(layer.weight >= 0f && layer.weight <= 1f)) throw Invalid("Capa inválida en el controller.");
                var stateNames = new HashSet<string>();
                foreach (var state in layer.states)
                {
                    if (string.IsNullOrEmpty(state.name) || !stateNames.Add(state.name)) throw Invalid("Los estados necesitan nombres únicos por capa.");
                    bool hasClip = !string.IsNullOrEmpty(state.clip);
                    if (hasClip && !names.Contains(state.clip)) throw Invalid($"Clip desconocido: {state.clip}.");
                    if (state.blendTree?.children == null) continue;
                    if (hasClip || string.IsNullOrEmpty(state.blendTree.name) || state.blendTree.children.Length < 2)
                        throw Invalid($"BlendTree inválido: {state.name}.");
                    var thresholds = new HashSet<float>();
                    foreach (var child in state.blendTree.children)
                        if (!names.Contains(child.clip) || !Positive(NaturalSpeed(spec, child.clip)) ||
                            !Positive(child.threshold) || !thresholds.Add(child.threshold))
                            throw Invalid($"{state.name} necesita velocidades y umbrales distintos y positivos.");
                }
                if (!stateNames.Contains(layer.defaultState)) throw Invalid($"No existe el estado inicial de {layer.name}.");
            }
        }

        static AvatarMask SyncMask(ModelLibrary.MaskSpec spec, string path, string modelPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null || spec.bones == null || spec.bones.Length == 0) throw Invalid("La máscara necesita un modelo y huesos.");
            var enabled = new HashSet<string>(spec.bones);
            var found = new HashSet<string>();
            var transforms = model.GetComponentsInChildren<Transform>(true);
            var paths = new string[transforms.Length];
            var active = new bool[transforms.Length];
            for (int i = 0; i < transforms.Length; i++)
            {
                paths[i] = AnimationUtility.CalculateTransformPath(transforms[i], model.transform);
                active[i] = enabled.Contains(transforms[i].name);
                if (active[i] && !found.Add(transforms[i].name)) throw Invalid($"Hueso ambiguo en la máscara: {transforms[i].name}.");
            }
            if (!found.SetEquals(enabled)) throw Invalid($"La máscara {path} contiene huesos ausentes en {modelPath}.");
            Array.Sort(paths, active, StringComparer.Ordinal);
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (mask != null && MaskMatches(mask, paths, active)) return mask;
            if (mask == null)
            {
                GeneratedMaterials.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                mask = new AvatarMask { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(mask, path);
            }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            mask.transformCount = paths.Length;
            for (int i = 0; i < paths.Length; i++)
            {
                mask.SetTransformPath(i, paths[i]);
                mask.SetTransformActive(i, active[i]);
            }
            EditorUtility.SetDirty(mask);
            AssetDatabase.SaveAssetIfDirty(mask);
            return mask;
        }

        static bool MaskMatches(AvatarMask mask, string[] paths, bool[] active)
        {
            if (mask.transformCount != paths.Length) return false;
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                if (mask.GetHumanoidBodyPartActive((AvatarMaskBodyPart)i)) return false;
            for (int i = 0; i < paths.Length; i++)
                if (mask.GetTransformPath(i) != paths[i] || mask.GetTransformActive(i) != active[i]) return false;
            return true;
        }

        static T Reuse<T>(T candidate, string name, AnimatorController controller, Object[] assets, HashSet<Object> owned) where T : Object, new()
        {
            if (candidate == null || owned.Contains(candidate) || AssetDatabase.GetAssetPath(candidate) != AssetDatabase.GetAssetPath(controller))
                candidate = Array.Find(assets, a => a is T && a.name == name && !owned.Contains(a)) as T;
            if (candidate == null)
            {
                candidate = new T { name = name, hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(candidate, controller);
            }
            owned.Add(candidate);
            Set(candidate, candidate.name, name, v => candidate.name = v);
            return candidate;
        }

        static void Set<T>(Object target, T current, T expected, Action<T> apply)
        {
            if (EqualityComparer<T>.Default.Equals(current, expected)) return;
            apply(expected);
            EditorUtility.SetDirty(target);
        }

        static void SetArray<T>(Object target, T[] current, T[] expected, Action<T[]> apply)
        {
            if (current.Length == expected.Length)
            {
                int i = 0;
                while (i < current.Length && EqualityComparer<T>.Default.Equals(current[i], expected[i])) i++;
                if (i == current.Length) return;
            }
            apply(expected);
            EditorUtility.SetDirty(target);
        }

        static bool LayerMatches(AnimatorControllerLayer current, AnimatorControllerLayer expected) => current != null &&
            current.name == expected.name && current.stateMachine == expected.stateMachine && current.defaultWeight == expected.defaultWeight &&
            current.avatarMask == expected.avatarMask && current.blendingMode == expected.blendingMode && current.iKPass == expected.iKPass &&
            current.syncedLayerIndex == expected.syncedLayerIndex && current.syncedLayerAffectsTiming == expected.syncedLayerAffectsTiming;

        static void ConfigureTree(BlendTree tree, string speed)
        {
            Set(tree, tree.blendType, BlendTreeType.Simple1D, v => tree.blendType = v);
            Set(tree, tree.blendParameter, speed, v => tree.blendParameter = v);
            Set(tree, tree.useAutomaticThresholds, false, v => tree.useAutomaticThresholds = v);
        }

        static void SetChildren(BlendTree tree, ChildMotion[] expected)
        {
            var current = tree.children;
            if (current.Length == expected.Length)
            {
                for (int i = 0; i < current.Length; i++)
                {
                    var child = current[i];
                    child.motion = expected[i].motion;
                    child.threshold = expected[i].threshold;
                    child.timeScale = expected[i].timeScale;
                    child.cycleOffset = expected[i].cycleOffset;
                    child.mirror = expected[i].mirror;
                    expected[i] = child;
                }
            }
            SetArray(tree, current, expected, v => tree.children = v);
        }
    }
}
