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
    public class TiffanyProvisionalTests
    {
        const string Id = "tiffany-provisional";
        const string TempFolder = "Assets/TiffanyProvisionalTestTemp";
        const string ControllerPath = TempFolder + "/TiffanyProvisional.controller";
        const string MaskPath = TempFolder + "/TiffanyArms.mask";
        static readonly string[] ArmBones = { "Shoulder.L", "UpperArm.L", "LowerArm.L", "Hand.L",
            "Shoulder.R", "UpperArm.R", "LowerArm.R", "Hand.R" };
        static readonly string[] ClipNames = { "Idle", "Slow", "Walk", "Run", "TurnLeft", "TurnRight", "Hide",
            "CarryLight", "Flash", "Caught", "Vanish" };
        static readonly string[][] StateNames =
        {
            new[] { TiffanyAnimation.Idle, TiffanyAnimation.Walk, TiffanyAnimation.WalkBack, TiffanyAnimation.TurnLeft, TiffanyAnimation.TurnRight,
                TiffanyAnimation.Hide, TiffanyAnimation.Caught, TiffanyAnimation.Vanish },
            new[] { TiffanyAnimation.Empty, TiffanyAnimation.CarryLight, TiffanyAnimation.Flash }
        };
        ModelLibrary.ModelSpec spec;
        GameObject parent;

        [SetUp]
        public void SetUp()
        {
            spec = ModelLibrary.LoadSpec(Id);
            ModelLibrary.materialFolder = TempFolder + "/Materials";
            parent = new GameObject("TiffanyProvisionalTests");
            parent.transform.SetPositionAndRotation(new Vector3(-2f, 3f, 4f), Quaternion.Euler(12f, 38f, -7f));
            parent.transform.localScale = new Vector3(2f, 3f, 0.5f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(parent);
            AssetDatabase.DeleteAsset(TempFolder);
            ModelLibrary.materialFolder = ModelLibrary.MaterialFolder;
        }

        GameObject Instance() => ModelLibrary.Instantiate(Id, parent.transform);
        AnimatorController Controller() => ModelAnimatorBuilder.Build(Id, ControllerPath);

        Dictionary<string, AnimationClip> Clips()
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(spec.modelPath))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal)) clips.Add(clip.name, clip);
            return clips;
        }

        static Transform Bone(GameObject root, string name)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.name == name) return transform;
            Assert.Fail($"Falta el hueso {name}.");
            return null;
        }

        static Vector3 Local(GameObject root, string name) => root.transform.InverseTransformPoint(Bone(root, name).position);
        static Vector3 Point(float[] xyz) => new Vector3(xyz[0], xyz[1], xyz[2]);
        static AnimatorState State(AnimatorController controller, int layer, string name) =>
            Array.Find(controller.layers[layer].stateMachine.states, s => s.state.name == name).state;

        void SampleBindPose(GameObject root)
        {
            var skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
            var bones = skin.bones;
            var bindposes = skin.sharedMesh.bindposes;
            var matrices = new Dictionary<Transform, Matrix4x4>();
            for (int i = 0; i < bones.Length; i++) matrices.Add(bones[i], skin.transform.localToWorldMatrix * bindposes[i].inverse);
            foreach (var bone in bones)
            {
                if (bone.name == spec.rootMotionBone) continue;
                var parentMatrix = matrices.TryGetValue(bone.parent, out var matrix) ? matrix : bone.parent.localToWorldMatrix;
                var local = parentMatrix.inverse * matrices[bone];
                bone.localPosition = local.GetColumn(3);
                bone.localRotation = local.rotation;
                bone.localScale = local.lossyScale;
            }
        }

        [Test]
        public void MannequinHasTheRequestedScalePivotAndBudget()
        {
            var root = Instance();
            SampleBindPose(root);
            ModelLibraryTests.AssertNeutral(root.transform);
            Assert.AreSame(parent.transform, root.transform.parent);
            Assert.IsEmpty(root.GetComponentsInChildren<MeshFilter>(true));
            var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.AreEqual(1, skins.Length);
            var mesh = skins[0].sharedMesh;
            Assert.That(ModelLibraryTests.TriangleCount(mesh), Is.InRange(1, 4000));
            Assert.That(skins[0].bones.Length, Is.InRange(1, 40));
            Assert.AreEqual(skins[0].bones.Length, mesh.bindposes.Length);
            Assert.IsNotNull(skins[0].rootBone);
            Assert.That(mesh.bounds.size.y, Is.InRange(1.15f, 1.25f));
            Assert.AreEqual(0f, mesh.bounds.min.y, 0.005f);
            Assert.Less(new Vector2(mesh.bounds.center.x, mesh.bounds.center.z).magnitude, 0.02f);
            Assert.Less(Local(root, "UpperArm.L").x, 0f);
            Assert.Greater(Local(root, "UpperArm.R").x, 0f);
            Assert.AreEqual(Local(root, "UpperArm.L").y, Local(root, "Hand.L").y, 0.002f);
            Assert.AreEqual(Local(root, "UpperArm.R").y, Local(root, "Hand.R").y, 0.002f);
            var clothes = ModelLibraryTests.SubMeshBounds(skins[0], "TiffanyClothes");
            Assert.Greater(clothes.max.z, -clothes.min.z, "Los pies apuntan hacia +Z.");
        }

        [Test]
        public void SkeletonNamesParentsAndRestHeadsMatchTheJson()
        {
            var root = Instance();
            SampleBindPose(root);
            var bones = Bone(root, spec.rootMotionBone).GetComponentsInChildren<Transform>(true);
            Assert.LessOrEqual(bones.Length, 40);
            CollectionAssert.AreEquivalent(Array.ConvertAll(spec.skeleton, b => b.name), Array.ConvertAll(bones, b => b.name));
            Assert.AreEqual(Vector3.zero, Local(root, spec.rootMotionBone));
            foreach (var entry in spec.skeleton)
            {
                var bone = Bone(root, entry.name);
                if (!string.IsNullOrEmpty(entry.parent)) Assert.AreEqual(entry.parent, bone.parent.name, entry.name);
                Assert.Less(Vector3.Distance(Point(entry.head), Local(root, entry.name)), 0.002f, entry.name);
                Assert.Greater(Vector3.Distance(Point(entry.head), Point(entry.tail)), 0f, entry.name);
            }
        }

        [TestCase("L")]
        [TestCase("R")]
        public void BraidsHangFromTheHeadInAChainOfThree(string side)
        {
            var root = Instance();
            string parentName = "Head";
            for (int i = 1; i <= 3; i++)
            {
                string name = $"Braid.{side}.{i}";
                Assert.AreEqual(parentName, Bone(root, name).parent.name, name);
                Assert.AreEqual(parentName, Array.Find(spec.skeleton, b => b.name == name)?.parent, name);
                parentName = name;
            }
        }

        [Test]
        public void SkirtBonesHangFromTheHipsAndTheLongHairIsGone()
        {
            var root = Instance();
            foreach (string name in new[] { "Skirt.F", "Skirt.B", "Skirt.L", "Skirt.R" })
                Assert.AreEqual("Hips", Bone(root, name).parent.name, name);
            var names = Array.ConvertAll(root.GetComponentsInChildren<Transform>(true), t => t.name);
            foreach (string name in new[] { "HairBack1", "HairBack2" })
            {
                CollectionAssert.DoesNotContain(names, name);
                Assert.IsNull(Array.Find(spec.skeleton, b => b.name == name), name);
            }
        }

        [Test]
        public void BothHandsCarryTheSoulGlow()
        {
            var skin = Instance().GetComponentInChildren<SkinnedMeshRenderer>();
            int glow = Array.FindIndex(skin.sharedMaterials, m => m.name == "TiffanyGlow");
            Assert.GreaterOrEqual(glow, 0);
            var weights = skin.sharedMesh.boneWeights;
            var hands = new HashSet<string>();
            foreach (int vertex in skin.sharedMesh.GetTriangles(glow))
            {
                string bone = skin.bones[weights[vertex].boneIndex0].name;
                if (bone == "Hand.L" || bone == "Hand.R") hands.Add(bone);
            }
            CollectionAssert.AreEquivalent(new[] { "Hand.L", "Hand.R" }, hands);
        }

        [Test]
        public void UsesTheFourSyncedOpaqueMaterials()
        {
            var root = Instance();
            ModelLibraryTests.AssertSyncedMaterials(root, Id, 1, "TiffanyGame/Spirit");
            var materials = root.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials;
            CollectionAssert.AreEquivalent(new[] { "TiffanySkin", "TiffanyHair", "TiffanyClothes", "TiffanyGlow" },
                Array.ConvertAll(materials, m => m.name));
        }

        [Test]
        public void EveryMaterialUsesTheSameSpiritRimAsTyler()
        {
            var materials = Instance().GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials;
            var tyler = ModelLibrary.LoadSpec("tyler").materials[0];
            foreach (var entry in spec.materials)
            {
                Assert.IsTrue(entry.spirit, entry.name);
                Assert.AreEqual(tyler.rimColor, entry.rimColor, entry.name);
                Assert.AreEqual(tyler.rimPower, entry.rimPower, entry.name);
                Assert.AreEqual(tyler.rimIntensity, entry.rimIntensity, entry.name);
                var material = Array.Find(materials, m => m.name == entry.name);
                Assert.IsNotNull(material, entry.name);
                Assert.AreEqual("TiffanyGame/Spirit", material.shader.name, entry.name);
                Assert.IsTrue(ColorUtility.TryParseHtmlString(entry.rimColor, out var color));
                for (int c = 0; c < 4; c++) Assert.AreEqual(color[c], material.GetColor("_RimColor")[c], 1e-4f, entry.name);
                Assert.AreEqual(entry.rimPower, material.GetFloat("_RimPower"), 1e-4f, entry.name);
                Assert.AreEqual(entry.rimIntensity, material.GetFloat("_RimIntensity"), 1e-4f, entry.name);
            }
        }

        [Test]
        public void ClothesUseBoneWhiteFromJson()
        {
            var entry = Array.Find(spec.materials, m => m.name == "TiffanyClothes");
            Assert.AreEqual("#ECE8E0", entry.color);
            Assert.IsTrue(ColorUtility.TryParseHtmlString(entry.color, out var color));
            var materials = Instance().GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials;
            var clothes = Array.Find(materials, m => m.name == entry.name);
            Assert.IsNotNull(clothes);
            for (int c = 0; c < 4; c++) Assert.AreEqual(color[c], clothes.GetColor("_BaseColor")[c], 1e-4f);
        }

        [TestCase("Idle")]
        [TestCase("Slow")]
        [TestCase("Walk")]
        [TestCase("Run")]
        public void LocomotionKeepsElbowsByTheBody(string name)
        {
            var root = Instance();
            var clip = Clips()[name];
            for (int frame = 0; frame < 24; frame++)
            {
                clip.SampleAnimation(root, clip.length * frame / 24f);
                foreach (string side in new[] { "L", "R" })
                {
                    var shoulder = Local(root, "UpperArm." + side);
                    var elbow = Local(root, "LowerArm." + side);
                    Assert.Less(Mathf.Abs(elbow.x), Mathf.Abs(shoulder.x) + 0.05f, name + " " + side);
                    Assert.Less(elbow.y, shoulder.y - 0.1f, name + " " + side);
                }
            }
        }

        [Test]
        public void ClipsUseSourceDurationsLoopsAndPlayerSpeeds()
        {
            Assert.IsTrue(spec.preserveAnimationKeys);
            var importer = (ModelImporter)AssetImporter.GetAtPath(spec.modelPath);
            Assert.AreEqual(ModelImporterAnimationCompression.Optimal, importer.animationCompression);
            var clips = Clips();
            CollectionAssert.AreEquivalent(ClipNames, clips.Keys);
            Assert.AreEqual(spec.clips.Length, clips.Count);
            foreach (string once in new[] { "Caught", "Flash" })
                Assert.IsFalse(AnimationUtility.GetAnimationClipSettings(clips[once]).loopTime, once);
            Assert.That(clips["Idle"].length, Is.InRange(6f, 8f));
            Assert.That(clips["Flash"].length, Is.InRange(0.6f, 0.8f));
            Assert.That(clips["Caught"].length, Is.InRange(1.2f, 1.6f));
            Assert.AreEqual(3f, clips["Vanish"].length, 0.5f / spec.fps);
            Assert.AreEqual(PlayerMotion.SlowSpeed, Array.Find(spec.clips, c => c.name == "Slow").speed);
            Assert.AreEqual(PlayerMotion.WalkSpeed, Array.Find(spec.clips, c => c.name == "Walk").speed);
            Assert.AreEqual(PlayerMotion.RunSpeed, Array.Find(spec.clips, c => c.name == "Run").speed);
            foreach (var entry in spec.clips)
            {
                var clip = clips[entry.name];
                Assert.AreEqual(entry.frames / (float)spec.fps, clip.length, 0.5f / spec.fps, entry.name);
                Assert.AreEqual(entry.loop, AnimationUtility.GetAnimationClipSettings(clip).loopTime, entry.name);
                Assert.IsFalse(clip.legacy, entry.name);
            }
        }

        [Test]
        public void GenericClipsNeverMoveOrRotateTheRoot()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(spec.modelPath);
            Assert.AreEqual(ModelImporterAnimationType.Generic, importer.animationType);
            Assert.IsFalse(importer.optimizeGameObjects);
            Assert.AreEqual(spec.rootMotionBone, importer.motionNodeName);
            foreach (var entry in importer.clipAnimations)
            {
                Assert.IsTrue(entry.lockRootPositionXZ, entry.name);
                Assert.IsTrue(entry.lockRootHeightY, entry.name);
                Assert.IsTrue(entry.lockRootRotation, entry.name);
            }
            var root = Instance();
            foreach (var clip in Clips().Values)
            {
                clip.SampleAnimation(root, 0f);
                Vector3 position = Local(root, spec.rootMotionBone);
                Quaternion rotation = Bone(root, spec.rootMotionBone).localRotation;
                for (int i = 1; i <= 16; i++)
                {
                    clip.SampleAnimation(root, clip.length * i / 16f);
                    Assert.Less(Vector3.Distance(position, Local(root, spec.rootMotionBone)), 1e-4f, clip.name);
                    Assert.Less(Quaternion.Angle(rotation, Bone(root, spec.rootMotionBone).localRotation), 0.01f, clip.name);
                    ModelLibraryTests.AssertNeutral(root.transform);
                }
            }
        }

        [Test]
        public void LoopsCloseWithoutAJolt()
        {
            var root = Instance();
            var bones = Bone(root, spec.rootMotionBone).GetComponentsInChildren<Transform>(true);
            var clips = Clips();
            foreach (var entry in spec.clips)
            {
                if (!entry.loop) continue;
                var clip = clips[entry.name];
                clip.SampleAnimation(root, 0f);
                var positions = Array.ConvertAll(bones, b => b.localPosition);
                var rotations = Array.ConvertAll(bones, b => b.localRotation);
                clip.SampleAnimation(root, clip.length - 1e-4f);
                for (int i = 0; i < bones.Length; i++)
                {
                    Assert.Less(Vector3.Distance(positions[i], bones[i].localPosition), 0.002f, entry.name + " " + bones[i].name);
                    Assert.Less(Quaternion.Angle(rotations[i], bones[i].localRotation), 1f, entry.name + " " + bones[i].name);
                }
            }
        }

        [TestCase("Slow", "L", 0.08f)]
        [TestCase("Slow", "R", 0.58f)]
        [TestCase("Walk", "L", 0.08f)]
        [TestCase("Walk", "R", 0.58f)]
        [TestCase("Run", "L", 0.08f)]
        [TestCase("Run", "R", 0.58f)]
        public void PlantedFootMovesBackAtTheNaturalSpeed(string name, string side, float start)
        {
            var root = Instance();
            var clip = Clips()[name];
            float speed = Array.Find(spec.clips, c => c.name == name).speed;
            clip.SampleAnimation(root, clip.length * start);
            Vector3 a = Local(root, "Foot." + side);
            clip.SampleAnimation(root, clip.length * (start + 0.14f));
            Vector3 b = Local(root, "Foot." + side);
            Assert.AreEqual(-speed * clip.length * 0.14f, b.z - a.z, 0.005f);
            Assert.AreEqual(a.y, b.y, 0.003f);
            Assert.AreEqual(0.05f, b.y, 0.004f);
        }

        [Test]
        public void HideStaysLowAndCarryLightOnlyMovesTheArms()
        {
            var root = Instance();
            var clips = Clips();
            clips["Idle"].SampleAnimation(root, 0f);
            float standing = Local(root, "Hips").y;
            for (int i = 0; i < 8; i++)
            {
                clips["Hide"].SampleAnimation(root, clips["Hide"].length * i / 8f);
                Assert.Less(Local(root, "Hips").y, standing - 0.25f);
                Assert.Less(Local(root, "Head").y, 0.75f);
            }
            var rest = Instance();
            SampleBindPose(rest);
            var carry = Instance();
            var outside = Array.FindAll(spec.skeleton, b => Array.IndexOf(ArmBones, b.name) < 0);
            for (int i = 0; i < 8; i++)
            {
                clips["CarryLight"].SampleAnimation(carry, clips["CarryLight"].length * i / 8f);
                foreach (var entry in outside)
                {
                    var actual = Bone(carry, entry.name);
                    var expected = Bone(rest, entry.name);
                    Assert.Less(Vector3.Distance(expected.localPosition, actual.localPosition), 0.002f, entry.name);
                    Assert.Less(Quaternion.Angle(expected.localRotation, actual.localRotation), 0.5f, entry.name);
                }
                Vector3 left = Local(carry, "Hand.L"), right = Local(carry, "Hand.R"), chest = Local(carry, "Chest");
                Assert.Less(Vector3.Distance(left, right), 0.09f);
                Assert.Greater(left.z, chest.z + 0.15f);
                Assert.That(left.y - chest.y, Is.InRange(-0.05f, 0.06f));
            }
        }

        [Test]
        public void ControllerHasNamedStatesAndNoTransitions()
        {
            var controller = Controller();
            Assert.AreEqual(2, controller.parameters.Length);
            Assert.AreEqual("Speed", controller.parameters[0].name);
            Assert.AreEqual(AnimatorControllerParameterType.Float, controller.parameters[0].type);
            Assert.AreEqual(0f, controller.parameters[0].defaultFloat);
            Assert.AreEqual(TiffanyAnimation.TurnRateParameter, controller.parameters[1].name);
            Assert.AreEqual(AnimatorControllerParameterType.Float, controller.parameters[1].type);
            Assert.AreEqual(1f, controller.parameters[1].defaultFloat);
            Assert.AreEqual(2, controller.layers.Length);
            var names = StateNames;
            for (int i = 0; i < names.Length; i++)
            {
                var layer = controller.layers[i];
                Assert.AreEqual(i == 0 ? "Base Layer" : "Arms", layer.name);
                Assert.AreEqual(AnimatorLayerBlendingMode.Override, layer.blendingMode);
                Assert.AreEqual(1f, layer.defaultWeight);
                var machine = layer.stateMachine;
                CollectionAssert.AreEquivalent(names[i], Array.ConvertAll(machine.states, s => s.state.name));
                Assert.AreEqual(names[i][0], machine.defaultState.name);
                Assert.IsEmpty(machine.anyStateTransitions);
                Assert.IsEmpty(machine.entryTransitions);
                Assert.IsEmpty(machine.stateMachines);
                foreach (var state in machine.states) Assert.IsEmpty(state.state.transitions);
            }
            Assert.IsNull(State(controller, 1, "Empty").motion);
            Assert.IsTrue(State(controller, 1, "Empty").writeDefaultValues);
            Assert.IsTrue(State(controller, 1, "CarryLight").writeDefaultValues);
            var clips = Clips();
            for (int i = 0; i < names.Length; i++)
                foreach (string name in names[i])
                    if (name != TiffanyAnimation.Walk && name != TiffanyAnimation.WalkBack && name != TiffanyAnimation.Empty)
                        Assert.AreEqual(clips[name], State(controller, i, name).motion, name);
            Assert.IsNull(controller.layers[0].avatarMask);
            AssertMask(controller.layers[1].avatarMask);
        }

        void AssertMask(AvatarMask mask)
        {
            Assert.IsNotNull(mask);
            Assert.AreEqual(MaskPath, AssetDatabase.GetAssetPath(mask));
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(spec.modelPath);
            var enabled = new HashSet<string>();
            for (int i = 0; i < mask.transformCount; i++)
            {
                string path = mask.GetTransformPath(i);
                if (path.Length > 0) Assert.IsNotNull(root.transform.Find(path), path);
                if (mask.GetTransformActive(i)) enabled.Add(path);
            }
            CollectionAssert.AreEquivalent(Array.ConvertAll(ArmBones, b => AnimationUtility.CalculateTransformPath(Bone(root, b), root.transform)), enabled);
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) Assert.IsFalse(mask.GetHumanoidBodyPartActive((AvatarMaskBodyPart)i));
        }

        [TestCase(TiffanyAnimation.Walk, 1f, "Locomotion")]
        [TestCase(TiffanyAnimation.WalkBack, -1f, "Locomotion")]
        public void BlendTreeHasThreeOrderedCyclesAtTheirSourceSpeeds(string name, float speed, string treeName)
        {
            var state = State(Controller(), 0, name);
            Assert.IsNotNull(state);
            Assert.AreEqual(speed, state.speed);
            Assert.IsTrue(state.speedParameterActive);
            Assert.AreEqual("Speed", state.speedParameter);
            var tree = state.motion as BlendTree;
            Assert.IsNotNull(tree);
            Assert.AreEqual(treeName, tree.name);
            Assert.AreEqual(BlendTreeType.Simple1D, tree.blendType);
            Assert.AreEqual("Speed", tree.blendParameter);
            Assert.IsFalse(tree.useAutomaticThresholds);
            Assert.AreEqual(3, tree.children.Length);
            var names = new[] { "Slow", "Walk", "Run" };
            for (int i = 0; i < names.Length; i++)
            {
                var child = tree.children[i];
                float natural = Array.Find(spec.clips, c => c.name == names[i]).speed;
                Assert.AreEqual(natural, child.threshold);
                Assert.AreEqual(child.threshold / natural, child.timeScale);
                if (i > 0) Assert.Greater(child.threshold, tree.children[i - 1].threshold);
                var cycle = child.motion as BlendTree;
                Assert.IsNotNull(cycle);
                Assert.AreEqual(names[i], cycle.name);
                Assert.AreEqual(1, cycle.children.Length);
                Assert.AreEqual(Clips()[names[i]], cycle.children[0].motion);
                Assert.AreEqual(1f, child.threshold * child.timeScale * cycle.children[0].timeScale, 1e-5f);
            }
        }

        Animator Animate(GameObject root, AnimatorController controller)
        {
            var animator = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            return animator;
        }

        [TestCase("Slow", 0f)]
        [TestCase("Slow", 0.5f)]
        [TestCase("Slow", 1f)]
        [TestCase("Walk", 1f)]
        [TestCase("Run", 1f)]
        public void PlaybackFollowsSpeedInMetersPerSecond(string gait, float fraction)
        {
            var entry = Array.Find(spec.clips, c => c.name == gait);
            var animator = Animate(Instance(), Controller());
            animator.SetFloat("Speed", entry.speed * fraction);
            animator.Play("Base Layer.Walk", 0, 0f);
            animator.Update(0f);
            float start = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            animator.Update(0.05f);
            float elapsed = animator.GetCurrentAnimatorStateInfo(0).normalizedTime - start;
            Assert.AreEqual(0.05f * fraction / (entry.frames / (float)spec.fps), elapsed, 0.002f);
        }

        [TestCase(TiffanyAnimation.TurnLeft, -180f)]
        [TestCase(TiffanyAnimation.TurnRight, 45f)]
        public void TurningScalesOnlyTheTurnClipAndLeavesArmsAtNaturalSpeed(string name, float angularSpeed)
        {
            var controller = Controller();
            var entry = Array.Find(spec.clips, clip => clip.name == name);
            Assert.AreEqual(90f, entry.turnSpeed);
            var state = State(controller, 0, name);
            Assert.IsTrue(state.speedParameterActive);
            Assert.AreEqual(TiffanyAnimation.TurnRateParameter, state.speedParameter);
            Assert.IsFalse(State(controller, 1, TiffanyAnimation.CarryLight).speedParameterActive);
            var animator = Animate(Instance(), controller);
            float rate = TiffanyAnimation.TurnRate(angularSpeed, entry.turnSpeed);
            animator.SetFloat(TiffanyAnimation.TurnRateParameter, rate);
            animator.Play("Base Layer." + name, 0, 0f);
            animator.Play("Arms.CarryLight", 1, 0f);
            animator.Update(0f);
            animator.Update(0.1f);
            Assert.AreEqual(0.1f * rate / (entry.frames / (float)spec.fps),
                animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 0.002f);
            Assert.AreEqual(0.1f / Clips()[TiffanyAnimation.CarryLight].length,
                animator.GetCurrentAnimatorStateInfo(1).normalizedTime, 0.002f);
        }

        [Test]
        public void EmptyPassesThroughAndCarryLightPreservesTheBasePose()
        {
            var controller = Controller();
            var reference = Instance();
            var layered = Instance();
            var baseAnimator = Animate(reference, controller);
            var armsAnimator = Animate(layered, controller);
            baseAnimator.SetLayerWeight(1, 0f);
            foreach (var animator in new[] { baseAnimator, armsAnimator })
            {
                animator.SetFloat("Speed", PlayerMotion.WalkSpeed);
                animator.Play("Base Layer.Walk", 0, 0f);
                animator.Update(0f);
            }
            foreach (string state in new[] { "Empty", "CarryLight", "Empty" })
            {
                armsAnimator.Play("Arms." + state, 1, 0f);
                baseAnimator.Update(0.1f);
                armsAnimator.Update(0.1f);
                float armDifference = 0f;
                foreach (var entry in spec.skeleton)
                {
                    var a = Bone(reference, entry.name);
                    var b = Bone(layered, entry.name);
                    bool arm = Array.IndexOf(ArmBones, entry.name) >= 0;
                    if (arm) armDifference += Quaternion.Angle(a.localRotation, b.localRotation);
                    if (state == "CarryLight" && arm) continue;
                    Assert.Less(Vector3.Distance(a.localPosition, b.localPosition), 0.002f, state + " " + entry.name);
                    Assert.Less(Quaternion.Angle(a.localRotation, b.localRotation), 0.5f, state + " " + entry.name);
                }
                if (state == "CarryLight") Assert.Greater(armDifference, 10f);
            }
        }

        [Test]
        public void RebuildingPreservesControllerAndMaskBytesGuidsAndSubassets()
        {
            Controller();
            var paths = new[] { ControllerPath, MaskPath };
            var bytes = Array.ConvertAll(paths, File.ReadAllBytes);
            var guids = Array.ConvertAll(paths, p => AssetDatabase.AssetPathToGUID(p));
            var counts = Array.ConvertAll(paths, p => AssetDatabase.LoadAllAssetsAtPath(p).Length);
            var ids = Array.ConvertAll(paths, SubassetIds);
            for (int rebuild = 0; rebuild < 2; rebuild++)
            {
                if (rebuild > 0)
                    foreach (string path in paths) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Controller();
                for (int i = 0; i < paths.Length; i++)
                {
                    CollectionAssert.AreEqual(bytes[i], File.ReadAllBytes(paths[i]), paths[i]);
                    Assert.AreEqual(guids[i], AssetDatabase.AssetPathToGUID(paths[i]));
                    Assert.AreEqual(counts[i], AssetDatabase.LoadAllAssetsAtPath(paths[i]).Length);
                    CollectionAssert.AreEquivalent(ids[i], SubassetIds(paths[i]), paths[i]);
                }
            }
        }

        [Test]
        public void RebuildingRepairsChangedStatesSpeedsLayersAndMask()
        {
            var controller = Controller();
            var paths = new[] { ControllerPath, MaskPath };
            var ids = Array.ConvertAll(paths, SubassetIds);
            var guids = Array.ConvertAll(paths, p => AssetDatabase.AssetPathToGUID(p));
            int count = AssetDatabase.LoadAllAssetsAtPath(ControllerPath).Length;
            var tree = (BlendTree)State(controller, 0, "Walk").motion;
            State(controller, 0, TiffanyAnimation.WalkBack).speed = 1f;
            var children = tree.children;
            children[0].threshold = -1f;
            tree.children = children;
            var machine = controller.layers[1].stateMachine;
            machine.AddAnyStateTransition(machine.AddState("Obsolete"));
            State(controller, 1, "Empty").writeDefaultValues = false;
            var layers = controller.layers;
            layers[1].defaultWeight = 0.5f;
            controller.layers = layers;
            var mask = layers[1].avatarMask;
            for (int i = 0; i < mask.transformCount; i++) mask.SetTransformActive(i, true);
            mask.SetTransformPath(0, "Obsolete");
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, true);
            EditorUtility.SetDirty(mask);
            AssetDatabase.SaveAssets();
            controller = Controller();
            Assert.AreEqual(1f, controller.layers[1].defaultWeight);
            Assert.AreEqual(3, controller.layers[1].stateMachine.states.Length);
            Assert.IsEmpty(controller.layers[1].stateMachine.anyStateTransitions);
            Assert.AreEqual(PlayerMotion.SlowSpeed, ((BlendTree)State(controller, 0, "Walk").motion).children[0].threshold);
            Assert.AreEqual(-1f, State(controller, 0, TiffanyAnimation.WalkBack).speed);
            Assert.AreEqual(count, AssetDatabase.LoadAllAssetsAtPath(ControllerPath).Length);
            Assert.IsTrue(State(controller, 1, "Empty").writeDefaultValues);
            AssertMask(controller.layers[1].avatarMask);
            for (int i = 0; i < paths.Length; i++)
            {
                Assert.AreEqual(guids[i], AssetDatabase.AssetPathToGUID(paths[i]));
                CollectionAssert.AreEquivalent(ids[i], SubassetIds(paths[i]), paths[i]);
            }
        }

        [Test]
        public void SpecStatesUseTheNamesAndBlendTimesOfTiffanyAnimation()
        {
            var layers = spec.controller.layers;
            Assert.AreEqual(TiffanyAnimation.SpeedParameter, spec.controller.speedParameter);
            Assert.AreEqual("Arms", layers[TiffanyAnimation.ArmsLayer].name);
            for (int i = 0; i < StateNames.Length; i++)
            {
                CollectionAssert.AreEqual(StateNames[i], Array.ConvertAll(layers[i].states, s => s.name));
                foreach (var state in layers[i].states)
                    Assert.AreEqual(TiffanyAnimation.BlendFor(state.name), state.blend, state.name);
            }
        }

        [TestCase("Slow")]
        [TestCase("Walk")]
        [TestCase("Run")]
        [TestCase("TurnLeft")]
        [TestCase("TurnRight")]
        public void FeetAreLowestAtTheirDeclaredContacts(string name)
        {
            var entry = Array.Find(spec.clips, c => c.name == name);
            Assert.IsNotNull(entry.contacts, name);
            var root = Instance();
            var clip = Clips()[name];
            foreach (var (side, phase) in new[] { ("L", entry.contacts.left), ("R", entry.contacts.right) })
            {
                Assert.That(phase, Is.InRange(0f, 0.999f), name + " " + side);
                float lowest = float.MaxValue;
                for (int i = 0; i < 96; i++)
                {
                    clip.SampleAnimation(root, clip.length * i / 96f);
                    lowest = Mathf.Min(lowest, Local(root, "Foot." + side).y);
                }
                clip.SampleAnimation(root, clip.length * phase);
                float contactHeight = Local(root, "Foot." + side).y;
                Assert.AreEqual(lowest, contactHeight, 0.001f, name + " " + side);
                clip.SampleAnimation(root, clip.length * Mathf.Repeat(phase - 0.04f, 1f));
                Assert.Greater(Local(root, "Foot." + side).y, contactHeight + 0.001f, name + " " + side + " pisa sin haber levantado el pie");
                clip.SampleAnimation(root, clip.length * Mathf.Repeat(phase + 0.04f, 1f));
                Assert.AreEqual(contactHeight, Local(root, "Foot." + side).y, 0.001f, name + " " + side + " no empieza el apoyo en el contacto");
            }
        }

        [Test]
        public void CaughtEndsKneelingAndStill()
        {
            var root = Instance();
            var clips = Clips();
            clips["Idle"].SampleAnimation(root, 0f);
            float standing = Local(root, "Hips").y;
            var clip = clips["Caught"];
            var body = Array.FindAll(spec.skeleton, b => !b.name.StartsWith("Braid", StringComparison.Ordinal) &&
                                                         !b.name.StartsWith("Skirt", StringComparison.Ordinal));
            clip.SampleAnimation(root, clip.length - 0.1f);
            var positions = Array.ConvertAll(body, b => Local(root, b.name));
            var rotations = Array.ConvertAll(body, b => Bone(root, b.name).rotation);
            clip.SampleAnimation(root, clip.length);
            for (int i = 0; i < body.Length; i++)
            {
                Assert.Less(Vector3.Distance(positions[i], Local(root, body[i].name)), 0.001f, body[i].name);
                Assert.Less(Quaternion.Angle(rotations[i], Bone(root, body[i].name).rotation), 0.5f, body[i].name);
            }
            Assert.Less(Local(root, "Hips").y, standing - 0.25f);
            foreach (string side in new[] { "L", "R" })
                Assert.Less(Local(root, "LowerLeg." + side).y, 0.08f, side);
        }

        [TestCase("CarryLight")]
        [TestCase("Flash")]
        public void ArmLayerClipsOnlyAnimateArmBones(string name)
        {
            var bindings = AnimationUtility.GetCurveBindings(Clips()[name]);
            Assert.IsNotEmpty(bindings);
            var clip = Clips()[name];
            Assert.IsTrue(Array.Exists(bindings, b => Array.IndexOf(ArmBones, b.path.Substring(b.path.LastIndexOf('/') + 1)) >= 0));
            foreach (var binding in bindings)
            {
                if (Array.IndexOf(ArmBones, binding.path.Substring(binding.path.LastIndexOf('/') + 1)) >= 0) continue;
                var keys = AnimationUtility.GetEditorCurve(clip, binding).keys;
                foreach (var key in keys)
                    Assert.AreEqual(keys[0].value, key.value, 1e-4f, binding.path + " " + binding.propertyName);
            }
        }

        [Test]
        public void FlashPushesTheHandsOutAndReturnsToTheCarryPose()
        {
            var clips = Clips();
            var carry = Instance();
            var flash = Instance();
            clips["CarryLight"].SampleAnimation(carry, 0f);
            float reach = 0f, spread = 0f;
            for (int i = 0; i <= 28; i++)
            {
                clips["Flash"].SampleAnimation(flash, clips["Flash"].length * i / 28f);
                Vector3 left = Local(flash, "Hand.L"), right = Local(flash, "Hand.R");
                reach = Mathf.Max(reach, Mathf.Min(left.z, right.z) - Local(flash, "Chest").z);
                spread = Mathf.Max(spread, Vector3.Distance(left, right));
            }
            Assert.Greater(reach, 0.28f);
            Assert.Greater(spread, 0.12f);
            foreach (float time in new[] { 0f, clips["Flash"].length })
            {
                clips["Flash"].SampleAnimation(flash, time);
                foreach (string bone in ArmBones)
                    Assert.Less(Quaternion.Angle(Bone(carry, bone).localRotation, Bone(flash, bone).localRotation), 1f, time + " " + bone);
            }
        }

        static Dictionary<string, long> SubassetIds(string path)
        {
            var ids = new Dictionary<string, long>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _, out long id));
                ids.Add(asset.GetType().Name + ":" + asset.name, id);
            }
            return ids;
        }
    }
}
