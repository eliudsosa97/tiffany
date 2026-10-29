using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TiffanyGame.Tests
{
    public class TylerAnimatorTests
    {
        const string TempFolder = "Assets/TylerAnimatorTestTemp";
        string ControllerPath => TempFolder + "/" + Path.GetFileName(ModelLibrary.LoadSpec("tyler").controllerPath);

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(TempFolder);

        [Test]
        public void StatesResolveTheRuntimeHashesWithoutTransitions()
        {
            var controller = TylerAnimatorBuilder.Build(ControllerPath);
            Assert.AreEqual(1, controller.layers.Length);
            Assert.AreEqual(1, controller.parameters.Length);
            Assert.AreEqual(TylerAnimation.SpeedParameter, controller.parameters[0].name);
            Assert.AreEqual(AnimatorControllerParameterType.Float, controller.parameters[0].type);
            var machine = controller.layers[0].stateMachine;
            var names = new[] { TylerAnimation.Idle, TylerAnimation.Walk, TylerAnimation.Sit,
                TylerAnimation.Whine, TylerAnimation.Growl, TylerAnimation.Bark };
            CollectionAssert.AreEquivalent(names, Array.ConvertAll(machine.states, s => s.state.name));
            Assert.AreEqual(TylerAnimation.Idle, machine.defaultState.name);
            Assert.IsEmpty(machine.stateMachines);
            Assert.IsEmpty(machine.anyStateTransitions);
            Assert.IsEmpty(machine.entryTransitions);
            foreach (string name in names)
            {
                var state = Array.Find(machine.states, s => s.state.nameHash == Animator.StringToHash(name)).state;
                Assert.IsNotNull(state, name);
                Assert.IsEmpty(state.transitions, name);
                Assert.IsNotNull(state.motion, name);
                if (name == TylerAnimation.Walk) continue;
                Assert.AreEqual(name, state.motion.name);
                Assert.AreEqual(ModelLibrary.LoadSpec("tyler").modelPath, AssetDatabase.GetAssetPath(state.motion));
                Assert.IsFalse(state.speedParameterActive);
            }
        }

        [Test]
        public void LocomotionUsesSourceSpeedsAndNormalizesEveryCycle()
        {
            var state = Walk(TylerAnimatorBuilder.Build(ControllerPath));
            var tree = state.motion as BlendTree;
            Assert.IsNotNull(tree);
            Assert.AreEqual(BlendTreeType.Simple1D, tree.blendType);
            Assert.AreEqual(TylerAnimation.SpeedParameter, tree.blendParameter);
            Assert.IsFalse(tree.useAutomaticThresholds);
            var children = tree.children;
            Assert.AreEqual(4, children.Length);
            var spec = ModelLibrary.LoadSpec("tyler");
            var idle = children[0];
            Assert.IsInstanceOf<AnimationClip>(idle.motion);
            Assert.AreEqual(TylerAnimation.Idle, idle.motion.name);
            Assert.AreEqual(spec.modelPath, AssetDatabase.GetAssetPath(idle.motion));
            Assert.AreEqual(0f, idle.threshold);
            Assert.AreEqual(1f, idle.timeScale);
            var names = new[] { null, "Walk", "Trot", "Run" };
            for (int i = 1; i < children.Length; i++)
            {
                var entry = Array.Find(spec.clips, c => c.name == names[i]);
                var child = children[i];
                Assert.AreEqual(entry.speed, child.threshold);
                Assert.Greater(child.timeScale, 0f);
                Assert.AreEqual(child.threshold / entry.speed, child.timeScale);
                Assert.Greater(child.threshold, children[i - 1].threshold);
                var cycle = child.motion as BlendTree;
                Assert.IsNotNull(cycle);
                Assert.AreEqual(names[i], cycle.name);
                Assert.AreEqual(1, cycle.children.Length);
                var leaf = cycle.children[0];
                Assert.IsInstanceOf<AnimationClip>(leaf.motion);
                Assert.AreEqual(entry.name, leaf.motion.name);
                Assert.AreEqual(spec.modelPath, AssetDatabase.GetAssetPath(leaf.motion));
                Assert.AreEqual(1f, child.timeScale * leaf.timeScale, 1e-5f);
            }
        }

        [Test]
        public void WalkStatePlaysAtFixedSpeedWithoutParameter()
        {
            var state = Walk(TylerAnimatorBuilder.Build(ControllerPath));
            Assert.IsFalse(state.speedParameterActive);
            Assert.AreEqual(1f, state.speed);
        }

        [TestCase("Idle")]
        [TestCase("Walk")]
        [TestCase("Trot")]
        [TestCase("Run")]
        public void PlaybackAtEachThresholdMatchesTheNaturalClip(string clip)
        {
            var spec = ModelLibrary.LoadSpec("tyler");
            var entry = Array.Find(spec.clips, c => c.name == clip);
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(spec.modelPath));
            try
            {
                var animator = Animate(root);
                animator.SetFloat(TylerAnimation.SpeedParameter, entry.speed);
                animator.Play(Animator.StringToHash(TylerAnimation.Walk), 0, 0f);
                animator.Update(0f);
                float start = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                animator.Update(0.05f);
                float elapsed = animator.GetCurrentAnimatorStateInfo(0).normalizedTime - start;
                Assert.AreEqual(0.05f / (entry.frames / (float)spec.fps), elapsed, 0.002f);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void StoppingCompletesTheCrossFadeToIdle()
        {
            var spec = ModelLibrary.LoadSpec("tyler");
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(spec.modelPath));
            try
            {
                var animator = Animate(root);
                animator.SetFloat(TylerAnimation.SpeedParameter, Array.Find(spec.clips, c => c.name == "Walk").speed);
                animator.Play(Animator.StringToHash(TylerAnimation.Walk), 0, 0f);
                animator.Update(0.1f);
                animator.SetFloat(TylerAnimation.SpeedParameter, 0f);
                animator.CrossFade(Animator.StringToHash(TylerAnimation.Idle), 0.15f, 0);
                for (int i = 0; i < 20; i++) animator.Update(0.1f);
                Assert.IsFalse(animator.IsInTransition(0));
                Assert.AreEqual(Animator.StringToHash(TylerAnimation.Idle), animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
            }
            finally { Object.DestroyImmediate(root); }
        }

        Animator Animate(GameObject root)
        {
            var animator = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
            animator.runtimeAnimatorController = TylerAnimatorBuilder.Build(ControllerPath);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            return animator;
        }

        [Test]
        public void RebuildingPreservesBytesGuidAndSubassetsAfterReload()
        {
            TylerAnimatorBuilder.Build(ControllerPath);
            byte[] bytes = File.ReadAllBytes(ControllerPath);
            string guid = AssetDatabase.AssetPathToGUID(ControllerPath);
            var ids = SubassetIds();
            int count = AssetDatabase.LoadAllAssetsAtPath(ControllerPath).Length;
            TylerAnimatorBuilder.Build(ControllerPath);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(ControllerPath));
            AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceUpdate);
            TylerAnimatorBuilder.Build(ControllerPath);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(ControllerPath));
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(ControllerPath));
            Assert.AreEqual(count, AssetDatabase.LoadAllAssetsAtPath(ControllerPath).Length);
            CollectionAssert.AreEquivalent(ids, SubassetIds());
        }

        [Test]
        public void RebuildingRepairsAChangedGraphWithoutLeavingSubassets()
        {
            var controller = TylerAnimatorBuilder.Build(ControllerPath);
            var ids = SubassetIds();
            int count = AssetDatabase.LoadAllAssetsAtPath(ControllerPath).Length;
            var tree = (BlendTree)Walk(controller).motion;
            var children = tree.children;
            children[1].threshold = -1f;
            tree.children = children;
            var machine = controller.layers[0].stateMachine;
            machine.AddAnyStateTransition(machine.AddState("Obsolete"));
            AssetDatabase.SaveAssets();
            controller = TylerAnimatorBuilder.Build(ControllerPath);
            Assert.Greater(((BlendTree)Walk(controller).motion).children[1].threshold, 0f);
            Assert.AreEqual(6, controller.layers[0].stateMachine.states.Length);
            Assert.IsEmpty(controller.layers[0].stateMachine.anyStateTransitions);
            Assert.AreEqual(count, AssetDatabase.LoadAllAssetsAtPath(ControllerPath).Length);
            CollectionAssert.AreEquivalent(ids, SubassetIds());
        }

        Dictionary<string, long> SubassetIds()
        {
            var ids = new Dictionary<string, long>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
            {
                Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _, out long id));
                ids.Add(asset.GetType().Name + ":" + asset.name, id);
            }
            return ids;
        }

        static AnimatorState Walk(AnimatorController controller) =>
            Array.Find(controller.layers[0].stateMachine.states, s => s.state.name == TylerAnimation.Walk).state;
    }
}
