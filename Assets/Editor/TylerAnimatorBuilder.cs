using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TiffanyGame.EditorTools
{
    public static class TylerAnimatorBuilder
    {
        static readonly string[] States =
        {
            TylerAnimation.Idle, TylerAnimation.Walk, TylerAnimation.Sit,
            TylerAnimation.Whine, TylerAnimation.Growl, TylerAnimation.Bark
        };

        [MenuItem("Tiffany/Art/Build Tyler Animator")]
        public static void BuildFromCommandLine()
        {
            Debug.Log($"{GameInfo.LogPrefix} Animator de Tyler: {AssetDatabase.GetAssetPath(Build())}");
        }

        public static AnimatorController Build(string outputPath = null)
        {
            var spec = ModelLibrary.LoadSpec("tyler");
            string path = outputPath ?? spec.controllerPath;
            if (string.IsNullOrEmpty(spec.modelPath) || string.IsNullOrEmpty(path))
                throw new InvalidDataException($"{GameInfo.LogPrefix} Tyler necesita modelPath y controllerPath.");
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(spec.modelPath))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                    clips.Add(clip.name, clip);
            foreach (var entry in spec.clips)
                if (!clips.ContainsKey(entry.name))
                    throw new InvalidDataException($"{GameInfo.LogPrefix} Falta el clip '{entry.name}' en {spec.modelPath}.");
            var gaits = Array.FindAll(spec.clips, c => c.speed > 0f);
            Array.Sort(gaits, (a, b) => a.speed.CompareTo(b.speed));
            if (gaits.Length != 3 || Array.Exists(gaits, c => float.IsInfinity(c.speed)) ||
                gaits[0].speed >= gaits[1].speed || gaits[1].speed >= gaits[2].speed)
                throw new InvalidDataException($"{GameInfo.LogPrefix} Tyler necesita tres velocidades distintas y positivas.");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                GeneratedMaterials.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                controller = new AnimatorController { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(controller, path);
            }
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            var owned = new HashSet<Object> { controller };
            var parameters = controller.parameters;
            if (parameters.Length != 1 || parameters[0].name != TylerAnimation.SpeedParameter ||
                parameters[0].type != AnimatorControllerParameterType.Float || parameters[0].defaultFloat != 0f)
            {
                controller.parameters = new[]
                {
                    new AnimatorControllerParameter { name = TylerAnimation.SpeedParameter, type = AnimatorControllerParameterType.Float }
                };
                EditorUtility.SetDirty(controller);
            }
            var layers = controller.layers;
            var machine = Reuse(layers.Length > 0 ? layers[0].stateMachine : null, "Base Layer", controller, assets, owned);
            if (layers.Length != 1 || layers[0].stateMachine != machine || layers[0].name != machine.name || layers[0].defaultWeight != 1f)
            {
                controller.layers = new[] { new AnimatorControllerLayer { name = machine.name, stateMachine = machine, defaultWeight = 1f } };
                EditorUtility.SetDirty(controller);
            }
            SetArray(machine, machine.stateMachines, Array.Empty<ChildAnimatorStateMachine>(), v => machine.stateMachines = v);
            SetArray(machine, machine.anyStateTransitions, Array.Empty<AnimatorStateTransition>(), v => machine.anyStateTransitions = v);
            SetArray(machine, machine.entryTransitions, Array.Empty<AnimatorTransition>(), v => machine.entryTransitions = v);
            SetArray(machine, machine.behaviours, Array.Empty<StateMachineBehaviour>(), v => machine.behaviours = v);
            var states = new ChildAnimatorState[States.Length];
            var previous = machine.states;
            for (int i = 0; i < States.Length; i++)
            {
                var child = Array.Find(previous, s => s.state != null && s.state.name == States[i]);
                var state = Reuse(child.state, States[i], controller, assets, owned);
                states[i] = new ChildAnimatorState { state = state, position = child.state != null ? child.position : new Vector3(300f, 60f * i) };
                Set(state, state.speed, 1f, v => state.speed = v);
                Set(state, state.writeDefaultValues, true, v => state.writeDefaultValues = v);
                Set(state, state.cycleOffset, 0f, v => state.cycleOffset = v);
                Set(state, state.cycleOffsetParameterActive, false, v => state.cycleOffsetParameterActive = v);
                Set(state, state.timeParameterActive, false, v => state.timeParameterActive = v);
                Set(state, state.mirror, false, v => state.mirror = v);
                Set(state, state.mirrorParameterActive, false, v => state.mirrorParameterActive = v);
                SetArray(state, state.transitions, Array.Empty<AnimatorStateTransition>(), v => state.transitions = v);
                SetArray(state, state.behaviours, Array.Empty<StateMachineBehaviour>(), v => state.behaviours = v);
                Set(state, state.speedParameterActive, false, v => state.speedParameterActive = v);
                Set(state, state.speedParameter, string.Empty, v => state.speedParameter = v);
                if (state.name == TylerAnimation.Walk)
                {
                    var tree = Reuse(state.motion as BlendTree, "Locomotion", controller, assets, owned);
                    ConfigureTree(tree);
                    var children = new ChildMotion[gaits.Length + 1];
                    // Idle en 0 deja que Walk siga avanzando con Speed en 0 y el CrossFade termine.
                    children[0] = new ChildMotion { motion = clips[TylerAnimation.Idle], threshold = 0f, timeScale = 1f };
                    var oldChildren = tree.children;
                    for (int j = 0; j < gaits.Length; j++)
                    {
                        float threshold = gaits[j].speed;
                        var old = Array.Find(oldChildren, c => c.motion != null && c.motion.name == gaits[j].name);
                        var cycle = Reuse(old.motion as BlendTree, gaits[j].name, controller, assets, owned);
                        ConfigureTree(cycle);
                        SetChildren(cycle, new[] { new ChildMotion { motion = clips[gaits[j].name], timeScale = 1f } });
                        children[j + 1] = new ChildMotion { motion = cycle, threshold = threshold, timeScale = threshold / gaits[j].speed };
                    }
                    SetChildren(tree, children);
                    Set<Motion>(state, state.motion, tree, v => state.motion = v);
                }
                else Set<Motion>(state, state.motion, clips[state.name], v => state.motion = v);
            }
            SetArray(machine, machine.states, states, v => machine.states = v);
            Set(machine, machine.defaultState, states[0].state, v => machine.defaultState = v);
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

        static void ConfigureTree(BlendTree tree)
        {
            Set(tree, tree.blendType, BlendTreeType.Simple1D, v => tree.blendType = v);
            Set(tree, tree.blendParameter, TylerAnimation.SpeedParameter, v => tree.blendParameter = v);
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
