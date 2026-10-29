using System.Collections.Generic;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public class TylerModelTests
    {
        const string Id = "tyler";
        static string ModelPath => ModelLibrary.LoadSpec(Id).modelPath;
        const string TempFolder = "Assets/TylerModelTestTemp";
        GameObject parent;

        [SetUp]
        public void SetUp()
        {
            ModelLibrary.materialFolder = TempFolder + "/Materials";
            parent = new GameObject("TylerModelTests");
            parent.transform.SetPositionAndRotation(new Vector3(-3f, 1f, 6f), Quaternion.Euler(-20f, 75f, 10f));
            parent.transform.localScale = new Vector3(3f, 2f, 0.5f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(parent);
            AssetDatabase.DeleteAsset(TempFolder);
            ModelLibrary.materialFolder = ModelLibrary.MaterialFolder;
        }

        static SkinnedMeshRenderer Skin(GameObject root)
        {
            Assert.IsEmpty(root.GetComponentsInChildren<MeshFilter>(true));
            var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.AreEqual(1, skins.Length);
            return skins[0];
        }

        static Dictionary<string, AnimationClip> Clips()
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", System.StringComparison.Ordinal))
                    clips.Add(clip.name, clip);
            return clips;
        }

        static Transform Bone(GameObject root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            Assert.Fail($"falta el hueso {name}");
            return null;
        }

        static Vector3 Local(GameObject root, string bone) => root.transform.InverseTransformPoint(Bone(root, bone).position);

        [Test]
        public void SkinnedMeshFitsTheBudget()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var skin = Skin(root);
            Assert.That(ModelLibraryTests.TriangleCount(skin.sharedMesh), Is.InRange(1000, 3000));
            Assert.That(skin.bones.Length, Is.InRange(20, 30));
            Assert.LessOrEqual(Bone(root, ModelLibrary.LoadSpec(Id).rootMotionBone).GetComponentsInChildren<Transform>(true).Length, 30);
            Assert.AreEqual(skin.bones.Length, skin.sharedMesh.bindposes.Length);
            Assert.IsNotNull(skin.rootBone);
        }

        [Test]
        public void ClipsMatchTheSource()
        {
            var spec = ModelLibrary.LoadSpec(Id);
            var clips = Clips();
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            CollectionAssert.AreEquivalent(new[] { "Idle", "Walk", "Trot", "Run", "Sit", "Whine", "Growl", "Bark" }, clips.Keys);
            Assert.AreEqual(spec.clips.Length, clips.Count, string.Join(", ", clips.Keys));
            foreach (var entry in spec.clips)
            {
                Assert.IsTrue(clips.TryGetValue(entry.name, out var clip), entry.name);
                Assert.AreEqual(spec.fps, clip.frameRate, 0.001f, entry.name);
                Assert.AreEqual(entry.frames / (float)spec.fps, clip.length, 0.5f / spec.fps, entry.name);
                Assert.AreEqual(entry.loop, AnimationUtility.GetAnimationClipSettings(clip).loopTime, entry.name);
                Assert.IsFalse(clip.legacy, entry.name);
                var imported = System.Array.Find(importer.clipAnimations, c => c.name == entry.name);
                Assert.IsNotNull(imported, entry.name);
                Assert.AreEqual(0f, imported.firstFrame, entry.name);
                Assert.AreEqual(entry.frames, imported.lastFrame, entry.name);
                Assert.AreEqual(entry.loop ? WrapMode.Loop : WrapMode.ClampForever, imported.wrapMode, entry.name);
            }
        }

        [Test]
        public void GenericClipsKeepTheirRootInPlace()
        {
            var spec = ModelLibrary.LoadSpec(Id);
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            Assert.AreEqual(ModelImporterAnimationType.Generic, importer.animationType);
            Assert.IsTrue(importer.bakeAxisConversion);
            Assert.IsTrue(importer.preserveHierarchy);
            Assert.AreEqual(spec.rootMotionBone, importer.motionNodeName);
            foreach (var clip in importer.clipAnimations)
            {
                Assert.IsTrue(clip.lockRootPositionXZ, clip.name);
                Assert.IsTrue(clip.lockRootHeightY, clip.name);
                Assert.IsTrue(clip.lockRootRotation, clip.name);
            }
            var root = ModelLibrary.Instantiate(Id, parent.transform);
            foreach (var clip in Clips().Values)
            {
                clip.SampleAnimation(root, 0f);
                Vector3 start = Local(root, spec.rootMotionBone);
                Vector3 origin = root.transform.localPosition;
                for (int frame = 1; frame <= 16; frame++)
                {
                    clip.SampleAnimation(root, clip.length * frame / 16f);
                    Vector3 current = Local(root, spec.rootMotionBone);
                    Assert.AreEqual(start.x, current.x, 1e-4f, clip.name);
                    Assert.AreEqual(start.z, current.z, 1e-4f, clip.name);
                    Assert.AreEqual(origin.x, root.transform.localPosition.x, 1e-4f, clip.name);
                    Assert.AreEqual(origin.z, root.transform.localPosition.z, 1e-4f, clip.name);
                }
            }
        }

        [Test]
        public void LoopsCloseWithoutAJump()
        {
            var root = ModelLibrary.Instantiate(Id, parent.transform);
            var bones = Skin(root).bones;
            foreach (var entry in ModelLibrary.LoadSpec(Id).clips)
            {
                if (!entry.loop) continue;
                var clip = Clips()[entry.name];
                var start = new (Vector3, Quaternion)[bones.Length];
                clip.SampleAnimation(root, 0f);
                for (int i = 0; i < bones.Length; i++) start[i] = (bones[i].localPosition, bones[i].localRotation);
                clip.SampleAnimation(root, clip.length - 1e-4f);
                for (int i = 0; i < bones.Length; i++)
                {
                    Assert.Less(Vector3.Distance(start[i].Item1, bones[i].localPosition), 0.002f, $"{entry.name} {bones[i].name}");
                    Assert.Less(Quaternion.Angle(start[i].Item2, bones[i].localRotation), 1f, $"{entry.name} {bones[i].name}");
                }
            }
        }

        [TestCase("Walk", 0.02f, 0.14f)]
        [TestCase("Trot", 0.02f, 0.12f)]
        [TestCase("Run", 0.02f, 0.08f)]
        public void PlantedFrontPawMatchesTheNaturalSpeed(string name, float from, float to)
        {
            var root = ModelLibrary.Instantiate(Id, parent.transform);
            var clip = Clips()[name];
            float speed = System.Array.Find(ModelLibrary.LoadSpec(Id).clips, c => c.name == name).speed;
            clip.SampleAnimation(root, clip.length * from);
            Vector3 start = Local(root, "FrontPaw.L");
            clip.SampleAnimation(root, clip.length * to);
            Vector3 end = Local(root, "FrontPaw.L");
            Assert.AreEqual(-speed * clip.length * (to - from), end.z - start.z, 0.005f, name);
            Assert.AreEqual(start.y, end.y, 0.003f, name);
        }

        [Test]
        public void StandsOnThePivotFacingForward()
        {
            var root = ModelLibrary.Instantiate(Id, parent.transform);
            Assert.AreEqual(Id, root.name);
            Assert.AreSame(parent.transform, root.transform.parent);
            ModelLibraryTests.AssertNeutral(root.transform);
            var animator = root.GetComponent<Animator>();
            Assert.IsNotNull(animator);
            Assert.AreEqual(ModelLibrary.LoadSpec(Id).controllerPath, AssetDatabase.GetAssetPath(animator.runtimeAnimatorController));
            Assert.IsFalse(animator.applyRootMotion);
            Assert.AreEqual(AnimatorCullingMode.CullUpdateTransforms, animator.cullingMode);
            var skin = Skin(root);
            Assert.AreEqual(SkinQuality.Bone4, skin.quality);
            var bounds = Skin(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath)).localBounds;
            for (int axis = 0; axis < 3; axis++) Assert.Greater(skin.localBounds.size[axis], bounds.size[axis]);
            var bones = new HashSet<Transform>(skin.bones);
            // Los huesos guardan su orientación de reposo. Todo lo demás sale neutro.
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (!bones.Contains(t)) ModelLibraryTests.AssertNeutral(t);

            var rest = skin.sharedMesh.bounds;
            Assert.AreEqual(0f, rest.min.y, 0.01f);
            Assert.Less(new Vector2(rest.center.x, rest.center.z).magnitude, 0.12f);
            Assert.That(rest.size.y, Is.InRange(0.38f, 0.48f));
            Assert.That(rest.size.z, Is.InRange(0.5f, 0.75f));
            Assert.That(rest.size.x, Is.InRange(0.1f, 0.2f));

            // La nariz va por delante. Con el modelo al revés, quedaría en z negativo.
            var nose = ModelLibraryTests.SubMeshBounds(skin, "TylerNose");
            Assert.Greater(nose.center.z, 0.15f);
            Assert.Greater(nose.center.y, 0.3f);

            Clips()["Idle"].SampleAnimation(root, 0f);
            var hips = Local(root, "Hips");
            Assert.Greater(Local(root, "Head").z, hips.z + 0.25f);
            Assert.That(Local(root, "Head").y, Is.InRange(0.3f, 0.45f));
            Assert.Less(Local(root, "Tail3").z, hips.z);
        }

        [Test]
        public void SitDropsTheHipsAndRaisesTheHead()
        {
            var root = ModelLibrary.Instantiate(Id, parent.transform);
            var clips = Clips();
            clips["Idle"].SampleAnimation(root, 0f);
            Vector3 hips = Local(root, "Hips"), head = Local(root, "Head");
            clips["Sit"].SampleAnimation(root, 0f);
            Assert.Less(Local(root, "Hips").y, hips.y - 0.08f, $"cadera de pie en {hips}");
            Assert.Greater(Local(root, "Head").y, head.y);
            Assert.Greater(Local(root, "Head").z, Local(root, "Hips").z);
        }

        [Test]
        public void UsesSyncedOpaqueMaterialsAndNoColliders()
        {
            var root = ModelLibrary.Instantiate(Id, parent.transform);
            ModelLibraryTests.AssertSyncedMaterials(root, Id, 1, "TiffanyGame/Spirit");
            var used = new HashSet<string>();
            foreach (var material in Skin(root).sharedMaterials) used.Add(material.name);
            foreach (var entry in ModelLibrary.LoadSpec(Id).materials) Assert.IsTrue(used.Contains(entry.name), entry.name);
        }

        [Test]
        public void EveryMaterialUsesSpiritSettingsFromJson()
        {
            var root = ModelLibrary.Instantiate(Id, parent.transform);
            foreach (var entry in ModelLibrary.LoadSpec(Id).materials)
            {
                Assert.IsTrue(entry.spirit, entry.name);
                var material = System.Array.Find(Skin(root).sharedMaterials, m => m.name == entry.name);
                Assert.IsNotNull(material, entry.name);
                Assert.AreEqual("TiffanyGame/Spirit", material.shader.name, entry.name);
                Assert.IsTrue(ColorUtility.TryParseHtmlString(entry.rimColor, out var rimColor));
                for (int c = 0; c < 4; c++) Assert.AreEqual(rimColor[c], material.GetColor("_RimColor")[c], 1e-4f, entry.name);
                Assert.AreEqual(entry.rimPower, material.GetFloat("_RimPower"), 1e-4f, entry.name);
                Assert.AreEqual(entry.rimIntensity, material.GetFloat("_RimIntensity"), 1e-4f, entry.name);
            }
        }

        [TestCase("")]
        [TestCase("_ADDITIONAL_LIGHTS_VERTEX _MAIN_LIGHT_SHADOWS")]
        [TestCase("_ADDITIONAL_LIGHTS _MAIN_LIGHT_SHADOWS_CASCADE _SHADOWS_SOFT_LOW")]
        [TestCase("_CLUSTER_LIGHT_LOOP _ADDITIONAL_LIGHTS _MAIN_LIGHT_SHADOWS_SCREEN _CASTING_PUNCTUAL_LIGHT_SHADOW")]
        public void SpiritShaderCompilesWithShadowAndDepthPasses(string keywords)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Shaders/Spirit.shader");
            Assert.IsNotNull(shader);
            Assert.AreEqual("TiffanyGame/Spirit", shader.name);
            var material = new Material(shader);
            try
            {
                material.shaderKeywords = keywords.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                Assert.AreEqual("Opaque", material.GetTag("RenderType", false));
                Assert.GreaterOrEqual(material.FindPass("ShadowCaster"), 0);
                Assert.GreaterOrEqual(material.FindPass("DepthOnly"), 0);
                int depthNormals = material.FindPass("DepthNormalsOnly");
                Assert.GreaterOrEqual(depthNormals, 0);
                Assert.AreEqual("DepthNormalsOnly",
                    shader.FindPassTagValue(depthNormals, new UnityEngine.Rendering.ShaderTagId("LightMode")).name);
                Assert.GreaterOrEqual(material.FindPass("ForwardLit"), 0);
                for (int pass = 0; pass < material.passCount; pass++) ShaderUtil.CompilePass(material, pass, true);
                string errors = "";
                foreach (var message in ShaderUtil.GetShaderMessages(shader)) errors += $"{message.file}:{message.line} {message.message}\n";
                Assert.IsFalse(ShaderUtil.ShaderHasError(shader), errors);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }
    }
}
