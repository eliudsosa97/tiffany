using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TiffanyGame.EditorTools
{
    public static class WitchAnimator
    {
        public const string ModelPath = ModelLibrary.ModelFolder + "/witch.fbx";
        public const string ControllerPath = ModelLibrary.ModelFolder + "/Witch.controller";
        public const string SpeedParameter = "Speed";
        public static readonly string[] States = { "Idle", "Stalking", "WindUp", "Sprinting", "Recovering", "Stunned", "Caught", "Landing", "TakingOff", "Suspicious" };

        public static Dictionary<string, AnimationClip> Clips()
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                    clips[clip.name] = clip;
            foreach (string state in States)
                if (!clips.ContainsKey(state))
                    throw new InvalidOperationException($"{GameInfo.LogPrefix} Falta la toma {state} en {ModelPath}.");
            return clips;
        }

        static (Motion motion, float threshold, float timeScale)[] StalkingBlend(Dictionary<string, AnimationClip> clips)
        {
            var profile = new WitchProfile();
            var walk = clips["Stalking"];
            return new (Motion, float, float)[]
            {
                (clips["Idle"], 0f, 1f),
                (walk, profile.StalkSpeed, 1f),
                (walk, profile.FollowSpeed, profile.FollowSpeed / profile.StalkSpeed),
            };
        }

        static float SprintScale => 1f / new WitchProfile().SprintSpeed;

        public static RuntimeAnimatorController Controller()
        {
            var clips = Clips();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller != null && Matches(controller, clips)) return controller;
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            else
            {
                controller.layers = Array.Empty<AnimatorControllerLayer>();
                controller.parameters = Array.Empty<AnimatorControllerParameter>();
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
                    if (asset != controller) Object.DestroyImmediate(asset, true);
                controller.AddLayer("Base Layer");
            }
            controller.AddParameter(SpeedParameter, AnimatorControllerParameterType.Float);
            var machine = controller.layers[0].stateMachine;
            foreach (string name in States)
            {
                var state = machine.AddState(name);
                if (name == "Stalking")
                {
                    var tree = new BlendTree
                    {
                        name = name, blendType = BlendTreeType.Simple1D, blendParameter = SpeedParameter,
                        useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy,
                    };
                    AssetDatabase.AddObjectToAsset(tree, controller);
                    var blend = StalkingBlend(clips);
                    foreach (var (motion, threshold, _) in blend) tree.AddChild(motion, threshold);
                    var children = tree.children;
                    for (int i = 0; i < children.Length; i++) children[i].timeScale = blend[i].timeScale;
                    tree.children = children;
                    state.motion = tree;
                }
                else state.motion = clips[name];
                if (name == "Sprinting")
                {
                    // La toma corre a 9 m/s: Speed / 9 da la cadencia sin que patinen los apoyos.
                    state.speed = SprintScale;
                    state.speedParameterActive = true;
                    state.speedParameter = SpeedParameter;
                }
                if (name == "Idle") machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        static bool Matches(AnimatorController controller, Dictionary<string, AnimationClip> clips)
        {
            if (controller.layers.Length != 1 || controller.parameters.Length != 1) return false;
            var parameter = controller.parameters[0];
            if (parameter.name != SpeedParameter || parameter.type != AnimatorControllerParameterType.Float) return false;
            var machine = controller.layers[0].stateMachine;
            var states = machine.states;
            if (states.Length != States.Length || machine.anyStateTransitions.Length != 0 || machine.entryTransitions.Length != 0
                || machine.stateMachines.Length != 0 || machine.defaultState == null || machine.defaultState.name != "Idle") return false;
            for (int i = 0; i < States.Length; i++)
            {
                var state = states[i].state;
                if (state.name != States[i] || state.transitions.Length != 0) return false;
                bool sprint = state.name == "Sprinting";
                if (state.speedParameterActive != sprint || (sprint && (state.speedParameter != SpeedParameter || !Mathf.Approximately(state.speed, SprintScale)))
                    || (!sprint && state.speed != 1f)) return false;
                if (state.name != "Stalking")
                {
                    if (state.motion != clips[state.name]) return false;
                    continue;
                }
                if (!(state.motion is BlendTree tree) || tree.blendType != BlendTreeType.Simple1D || tree.blendParameter != SpeedParameter
                    || tree.useAutomaticThresholds) return false;
                var blend = StalkingBlend(clips);
                var children = tree.children;
                if (children.Length != blend.Length) return false;
                for (int j = 0; j < children.Length; j++)
                    if (children[j].motion != blend[j].motion || children[j].threshold != blend[j].threshold
                        || !Mathf.Approximately(children[j].timeScale, blend[j].timeScale)) return false;
            }
            return true;
        }
    }
}
