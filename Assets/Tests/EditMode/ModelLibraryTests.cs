using System.IO;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame.Tests
{
    public class ModelLibraryTests
    {
        const string TempFolder = "Assets/ModelLibraryTestTemp";
        static readonly string[] ExpectedClips = { "Idle", "Stalking", "WindUp", "Sprinting", "Recovering", "Stunned", "Caught", "Landing", "TakingOff" };
        GameObject parent;

        [SetUp]
        public void SetUp()
        {
            ModelLibrary.materialFolder = TempFolder + "/Materials";
            parent = new GameObject("ModelLibraryTests");
            parent.transform.SetPositionAndRotation(new Vector3(4f, 3f, -2f), Quaternion.Euler(10f, 30f, 5f));
            parent.transform.localScale = new Vector3(2f, 3f, 4f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(parent);
            AssetDatabase.DeleteAsset(TempFolder);
            ModelLibrary.materialFolder = ModelLibrary.MaterialFolder;
        }

        static GameObject Model() => AssetDatabase.LoadAssetAtPath<GameObject>(WitchAnimator.ModelPath);

        [Test]
        public void WitchFbxIsOneSkinnedMeshWithinBudget()
        {
            var model = Model();
            Assert.IsNotNull(model);
            Assert.IsEmpty(model.GetComponentsInChildren<MeshFilter>(true));
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.AreEqual(1, skins.Length);
            var mesh = skins[0].sharedMesh;
            long indices = 0;
            for (int i = 0; i < mesh.subMeshCount; i++) indices += mesh.GetIndexCount(i);
            Assert.That(indices / 3, Is.InRange(3000, 6000));
            Assert.That(skins[0].bones.Length, Is.InRange(20, 40));
            foreach (var bone in skins[0].bones) Assert.IsNotNull(bone);
            foreach (string name in new[] { "Hips", "Head", "Hand.L", "Hand.R", "Foot.L", "Foot.R" })
                Assert.IsNotNull(Bone(model.transform, name), name);
        }

        [TestCase("Idle")]
        [TestCase("Stalking")]
        [TestCase("WindUp")]
        [TestCase("Sprinting")]
        [TestCase("Recovering")]
        [TestCase("Stunned")]
        [TestCase("Caught")]
        [TestCase("Landing")]
        [TestCase("TakingOff")]
        public void WitchClipsMatchTheirSpecAndStayInPlace(string name)
        {
            var clips = WitchAnimator.Clips();
            var spec = ModelLibrary.LoadSpec("witch");
            CollectionAssert.AreEquivalent(ExpectedClips, clips.Keys);
            CollectionAssert.AreEquivalent(ExpectedClips, System.Array.ConvertAll(spec.clips, c => c.name));
            var entry = System.Array.Find(spec.clips, c => c.name == name);
            var clip = clips[name];
            Assert.AreEqual(entry.loop, clip.isLooping, name);
            Assert.AreEqual(30f, clip.frameRate, 0.001f, name);
            Assert.AreEqual(entry.frames / 30f, clip.length, 0.001f, name);
            var importer = (ModelImporter)AssetImporter.GetAtPath(WitchAnimator.ModelPath);
            Assert.AreEqual(ModelImporterAnimationType.Generic, importer.animationType);
            Assert.AreEqual(ModelImporterAnimationCompression.KeyframeReduction, importer.animationCompression);
            var imported = System.Array.Find(importer.clipAnimations, c => c.name == name);
            Assert.IsNotNull(imported, name);
            Assert.AreEqual(0f, imported.firstFrame, name);
            Assert.AreEqual(entry.frames, imported.lastFrame, name);
            Assert.AreEqual(entry.loop ? WrapMode.Loop : WrapMode.ClampForever, imported.wrapMode, name);
            var root = Object.Instantiate(Model());
            try
            {
                var hips = Bone(root.transform, "Hips");
                for (int frame = 0; frame <= entry.frames; frame++)
                {
                    clip.SampleAnimation(root, frame / 30f);
                    AssertNeutral(root.transform);
                    var local = root.transform.InverseTransformPoint(hips.position);
                    Assert.Less(new Vector2(local.x, local.z).magnitude, 0.6f, $"{name} en cuadro {frame}");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase("Idle")]
        [TestCase("Stalking")]
        [TestCase("Sprinting")]
        [TestCase("Stunned")]
        public void WitchClipsLoopWithoutAJump(string name)
        {
            var root = Object.Instantiate(Model());
            var clip = Object.Instantiate(WitchAnimator.Clips()[name]);
            try
            {
                // El muestreo no debe envolver el último cuadro al primero.
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                clip.wrapMode = WrapMode.ClampForever;
                clip.SampleAnimation(root, 0f);
                var start = Snapshot(root.transform);
                clip.SampleAnimation(root, clip.length);
                var end = Snapshot(root.transform);
                for (int i = 0; i < start.Length; i++)
                {
                    Assert.Less(Quaternion.Angle(start[i].rotation, end[i].rotation), 1f, $"{name}: {start[i].name}");
                    Assert.Less((start[i].position - end[i].position).magnitude, 0.01f, $"{name}: {start[i].name}");
                }
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(root);
            }
        }

        [TestCase("L")]
        [TestCase("R")]
        public void SprintingForearmsClearTheHindLegsDuringRecovery(string side)
        {
            var root = Object.Instantiate(Model());
            try
            {
                var clip = WitchAnimator.Clips()["Sprinting"];
                var elbow = Bone(root.transform, "Forearm." + side);
                var wrist = Bone(root.transform, "Hand." + side);
                var knee = Bone(root.transform, "Shin." + side);
                var ankle = Bone(root.transform, "Foot." + side);
                for (int sample = 0; sample <= 16; sample++)
                {
                    float frame = 9f + sample / 8f;
                    clip.SampleAnimation(root, frame / clip.frameRate);
                    var a = root.transform.InverseTransformPoint(elbow.position);
                    var b = root.transform.InverseTransformPoint(wrist.position);
                    var c = root.transform.InverseTransformPoint(knee.position);
                    var d = root.transform.InverseTransformPoint(ankle.position) - c;
                    for (int point = 0; point <= 16; point++)
                    {
                        var forearm = Vector3.Lerp(a, b, point / 16f);
                        var shin = c + d * Mathf.Clamp01(Vector3.Dot(forearm - c, d) / d.sqrMagnitude);
                        Assert.Greater(Vector3.Distance(forearm, shin), 0.075f, $"{side}, cuadro {frame}");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WindUpLowersTheHipsAndHoldsItsLastPose()
        {
            var root = Object.Instantiate(Model());
            try
            {
                var clips = WitchAnimator.Clips();
                var hips = Bone(root.transform, "Hips");
                clips["Idle"].SampleAnimation(root, 0f);
                float standing = root.transform.InverseTransformPoint(hips.position).y;
                var windUp = clips["WindUp"];
                windUp.SampleAnimation(root, windUp.length);
                Assert.Greater(standing - root.transform.InverseTransformPoint(hips.position).y, 0.4f);
                var end = Snapshot(root.transform);
                windUp.SampleAnimation(root, windUp.length + 0.5f);
                var held = Snapshot(root.transform);
                for (int i = 0; i < end.Length; i++)
                {
                    Assert.Less(Quaternion.Angle(end[i].rotation, held[i].rotation), 0.1f, end[i].name);
                    Assert.Less((end[i].position - held[i].position).magnitude, 0.001f, end[i].name);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ControllerHasEveryStateAndTheSpeedParameter()
        {
            var controller = (AnimatorController)WitchAnimator.Controller();
            Assert.AreEqual(WitchAnimator.ControllerPath, AssetDatabase.GetAssetPath(controller));
            Assert.AreEqual(1, controller.parameters.Length);
            Assert.AreEqual("Speed", controller.parameters[0].name);
            Assert.AreEqual(AnimatorControllerParameterType.Float, controller.parameters[0].type);
            var machine = controller.layers[0].stateMachine;
            var profile = new WitchProfile();
            var clips = WitchAnimator.Clips();
            string[] names = ExpectedClips;
            Assert.AreEqual(names.Length, machine.states.Length);
            Assert.IsEmpty(machine.anyStateTransitions);
            Assert.AreEqual("Idle", machine.defaultState.name);
            foreach (string name in names)
            {
                var state = System.Array.Find(machine.states, s => s.state.name == name).state;
                Assert.IsNotNull(state, name);
                Assert.IsEmpty(state.transitions, name);
                Assert.IsNotNull(state.motion, name);
                if (name != "Stalking") Assert.AreSame(clips[name], state.motion, name);
            }
            var stalking = System.Array.Find(machine.states, s => s.state.name == "Stalking").state;
            var tree = (BlendTree)stalking.motion;
            Assert.AreEqual("Speed", tree.blendParameter);
            var children = tree.children;
            Assert.AreEqual(3, children.Length);
            Assert.AreSame(clips["Idle"], children[0].motion);
            Assert.AreEqual(0f, children[0].threshold);
            Assert.AreSame(clips["Stalking"], children[1].motion);
            Assert.AreEqual(profile.StalkSpeed, children[1].threshold);
            Assert.AreSame(clips["Stalking"], children[2].motion);
            Assert.AreEqual(profile.FollowSpeed, children[2].threshold);
            // Más rápido es más cadencia con la misma zancada.
            Assert.AreEqual(profile.FollowSpeed / profile.StalkSpeed, children[2].timeScale, 1e-4f);
            var sprint = System.Array.Find(machine.states, s => s.state.name == "Sprinting").state;
            Assert.AreSame(clips["Sprinting"], sprint.motion);
            Assert.IsTrue(sprint.speedParameterActive);
            Assert.AreEqual("Speed", sprint.speedParameter);
            Assert.AreEqual(1f, sprint.speed * profile.SprintSpeed, 1e-4f);

            string text = File.ReadAllText(WitchAnimator.ControllerPath);
            WitchAnimator.Controller();
            Assert.AreEqual(text, File.ReadAllText(WitchAnimator.ControllerPath), "el controller se rehízo sin cambios");
        }

        [Test]
        public void WitchStandsOnHerFeetFacingForward()
        {
            var root = ModelLibrary.Instantiate("witch", parent.transform);
            Assert.AreEqual("witch", root.name);
            Assert.AreSame(parent.transform, root.transform.parent);
            AssertNeutral(root.transform);

            var animator = root.GetComponent<Animator>();
            Assert.IsNotNull(animator);
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<AnimatorController>(WitchAnimator.ControllerPath), animator.runtimeAnimatorController);
            Assert.IsFalse(animator.applyRootMotion);
            Assert.AreEqual(AnimatorCullingMode.CullUpdateTransforms, animator.cullingMode);
            Assert.IsNull(root.transform.Find("WitchStand"));
            Assert.IsNull(root.transform.Find("WitchCrouch"));

            var skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
            var rest = RestBounds(skin, root.transform);
            Assert.That(rest.size.y, Is.InRange(2.25f, 2.4f));
            Assert.AreEqual(0f, rest.min.y, 0.01f);
            Assert.Less(new Vector2(rest.center.x, rest.center.z).magnitude, 0.3f);

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                Assert.Greater(Vector3.Dot(Vector3.Cross(t.right, t.up), t.forward), 0f, $"{t.name} está reflejado");
            var bones = BonePositions(root.transform);
            Assert.IsTrue(FacesForward(bones), "huesos de su izquierda en -X, rodillas y la mano alzada al frente");
            for (int i = 0; i < bones.Length; i++) bones[i].x = -bones[i].x;
            Assert.IsFalse(FacesForward(bones), "el esqueleto reflejado también pasaría");

            var hair = SubMeshBounds(skin, "WitchHair");
            Assert.IsTrue(HeadLeansToHerLeft(hair), $"centro del pelo en x = {hair.center.x}");
            hair.center = new Vector3(-hair.center.x, hair.center.y, hair.center.z);
            Assert.IsFalse(HeadLeansToHerLeft(hair), "el modelo reflejado también pasaría");
        }

        static readonly string[] Landmarks = { "Thigh.L", "Thigh.R", "Shin.L", "Shin.R", "Hand.L", "Hand.R", "Fingers.R" };

        static Vector3[] BonePositions(Transform root)
        {
            var positions = new Vector3[Landmarks.Length];
            for (int i = 0; i < Landmarks.Length; i++) positions[i] = root.InverseTransformPoint(Bone(root, Landmarks[i]).position);
            return positions;
        }

        // Ella mira a +Z: su izquierda es -X, las rodillas van delante de la cadera y la mano derecha alzada al frente.
        static bool FacesForward(Vector3[] b) =>
            b[0].x < 0f && b[1].x > 0f && b[4].x < 0f && b[5].x > 0f &&
            b[2].z > b[0].z + 0.03f && b[3].z > b[1].z + 0.03f && b[6].z > 0.3f;

        // Su izquierda es -X en Unity: witch.json pone la cabeza en x = -0.14.
        static bool HeadLeansToHerLeft(Bounds hair) => hair.center.x < -0.05f;

        static Bounds SubMeshBounds(SkinnedMeshRenderer skin, string material)
        {
            var materials = skin.sharedMaterials;
            int index = System.Array.FindIndex(materials, m => m.name == material);
            Assert.GreaterOrEqual(index, 0, material);
            return skin.sharedMesh.GetSubMesh(index).bounds;
        }

        static Bounds RestBounds(SkinnedMeshRenderer skin, Transform root)
        {
            var b = skin.sharedMesh.bounds;
            var m = root.worldToLocalMatrix * skin.transform.localToWorldMatrix;
            var bounds = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                bounds.Encapsulate(m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return bounds;
        }

        static Transform Bone(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            Assert.Fail($"Falta el hueso {name}");
            return null;
        }

        static (string name, Quaternion rotation, Vector3 position)[] Snapshot(Transform root)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            var snap = new (string, Quaternion, Vector3)[all.Length];
            for (int i = 0; i < all.Length; i++) snap[i] = (all[i].name, all[i].localRotation, all[i].localPosition);
            return snap;
        }

        [Test]
        public void WitchUsesSyncedOpaqueMaterialsAndNoColliders()
        {
            var root = ModelLibrary.Instantiate("witch", parent.transform);
            Assert.IsEmpty(root.GetComponentsInChildren<Collider>(true));
            var spec = ModelLibrary.LoadSpec("witch");
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Assert.AreEqual(1, renderers.Length);
            Assert.IsInstanceOf<SkinnedMeshRenderer>(renderers[0]);
            foreach (var renderer in renderers)
            {
                Assert.AreEqual(ShadowCastingMode.On, renderer.shadowCastingMode);
                Assert.AreEqual(spec.materials.Length, renderer.sharedMaterials.Length);
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.IsNotNull(material, renderer.name);
                    Assert.AreEqual($"{ModelLibrary.materialFolder}/{material.name}.mat", AssetDatabase.GetAssetPath(material));
                    Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
                    Assert.IsEmpty(material.shaderKeywords, material.name);
                    Assert.AreEqual(0f, material.GetFloat("_Surface"));
                    Assert.AreEqual(0f, material.GetFloat("_AlphaClip"));
                    Assert.IsNull(material.GetTexture("_BaseMap"));
                    Assert.IsNull(material.GetTexture("_EmissionMap"));
                    var entry = System.Array.Find(spec.materials, m => m.name == material.name);
                    Assert.IsNotNull(entry, material.name);
                    ColorUtility.TryParseHtmlString(entry.color, out var color);
                    var baseColor = material.GetColor("_BaseColor");
                    for (int c = 0; c < 4; c++) Assert.AreEqual(color[c], baseColor[c], 1e-4f, material.name);
                    Assert.AreEqual(entry.smoothness, material.GetFloat("_Smoothness"), 1e-4f);
                    Assert.AreEqual(entry.metallic, material.GetFloat("_Metallic"), 1e-4f, material.name);
                }
            }
        }

        [TestCase("witch", false)]
        [TestCase("witch", true)]
        [TestCase("oak", false)]
        [TestCase("oak", true)]
        [TestCase("bougainvillea", false)]
        [TestCase("bougainvillea", true)]
        public void SyncMaterialsIsStable(string id, bool useCommittedMaterials)
        {
            var entries = ModelLibrary.LoadSpec(id).materials;
            var paths = new string[entries.Length];
            var guids = new string[entries.Length];
            var contents = new byte[entries.Length][];
            GeneratedMaterials.EnsureFolder(ModelLibrary.materialFolder);
            if (!useCommittedMaterials) ModelLibrary.SyncMaterials(id);
            for (int i = 0; i < entries.Length; i++)
            {
                string source = $"{ModelLibrary.MaterialFolder}/{entries[i].name}.mat";
                paths[i] = $"{ModelLibrary.materialFolder}/{entries[i].name}.mat";
                contents[i] = File.ReadAllBytes(useCommittedMaterials ? source : paths[i]);
                if (useCommittedMaterials) Assert.IsTrue(AssetDatabase.CopyAsset(source, paths[i]), source);
                guids[i] = AssetDatabase.AssetPathToGUID(paths[i]);
                Assert.IsNotEmpty(guids[i], paths[i]);
            }
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (string path in paths)
                {
                    Resources.UnloadAsset(AssetDatabase.LoadAssetAtPath<Material>(path));
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
                ModelLibrary.SyncMaterials(id);
                for (int i = 0; i < paths.Length; i++)
                {
                    Assert.AreEqual(guids[i], AssetDatabase.AssetPathToGUID(paths[i]), paths[i]);
                    CollectionAssert.AreEqual(contents[i], File.ReadAllBytes(paths[i]), paths[i]);
                    var material = AssetDatabase.LoadAssetAtPath<Material>(paths[i]);
                    var baseColor = material.GetColor("_BaseColor");
                    var legacyColor = material.GetColor("_Color");
                    for (int c = 0; c < 4; c++) Assert.AreEqual(baseColor[c], legacyColor[c], 1e-5f, paths[i]);
                }
            }
        }

        [Test]
        public void MissingSourceFailsWithPrefix()
        {
            var error = Assert.Throws<FileNotFoundException>(() => ModelLibrary.SyncMaterials("no-model"));
            StringAssert.StartsWith(GameInfo.LogPrefix, error.Message);
        }

        [Test]
        public void MaterialNamesMustStartWithTheModelName()
        {
            var own = new ModelLibrary.MaterialSpec { name = "WitchSkin", color = "#000000" };
            var foreign = new ModelLibrary.MaterialSpec { name = "Skin", color = "#000000" };
            Assert.DoesNotThrow(() => ModelLibrary.CheckSpec("witch", new ModelLibrary.ModelSpec { materials = new[] { own } }));
            var error = Assert.Throws<InvalidDataException>(() =>
                ModelLibrary.CheckSpec("witch", new ModelLibrary.ModelSpec { materials = new[] { own, foreign } }));
            StringAssert.StartsWith(GameInfo.LogPrefix, error.Message);
            var stone = new ModelLibrary.MaterialSpec { name = "RockStone", color = "#000000" };
            Assert.DoesNotThrow(() => ModelLibrary.CheckSpec("rock-a", new ModelLibrary.ModelSpec { materials = new[] { stone } }));
        }

        static void AssertNeutral(Transform t)
        {
            Assert.AreEqual(Vector3.zero, t.localPosition, t.name);
            Assert.AreEqual(Quaternion.identity, t.localRotation, t.name);
            Assert.AreEqual(Vector3.one, t.localScale, t.name);
        }
    }
}
