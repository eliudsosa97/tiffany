using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public class TiffanyAnimatorTests
    {
        const string TempFolder = "Assets/TiffanyAnimatorTestTemp";
        GameObject spawn;
        PlayerController player;
        string materialFolder;
        string modelMaterialFolder;

        [SetUp]
        public void SetUp()
        {
            materialFolder = GeneratedMaterials.folder;
            modelMaterialFolder = ModelLibrary.materialFolder;
            GeneratedMaterials.folder = TempFolder + "/Materials";
            ModelLibrary.materialFolder = TempFolder + "/Models";
            spawn = new GameObject("Spawn");
            spawn.transform.SetPositionAndRotation(new Vector3(2f, 3f, -4f), Quaternion.Euler(0f, 35f, 0f));
            player = SceneBuilder.CreatePlayer(spawn.transform, out _);
        }

        [TearDown]
        public void TearDown()
        {
            if (player != null) Object.DestroyImmediate(player.gameObject);
            Object.DestroyImmediate(spawn);
            GeneratedMaterials.folder = materialFolder;
            ModelLibrary.materialFolder = modelMaterialFolder;
            AssetDatabase.DeleteAsset(TempFolder);
        }

        [Test]
        public void GeneratedPlayerPlacesAnimatedModelAtCharacterFeet()
        {
            var character = player.GetComponent<CharacterController>();
            var driver = player.GetComponent<TiffanyAnimator>();
            Assert.IsNotNull(driver.animator);
            Assert.AreEqual("tiffany-provisional", driver.animator.name);
            Assert.AreSame(player.transform, driver.animator.transform.parent);
            Assert.AreEqual(character.center - Vector3.up * character.height * 0.5f, driver.animator.transform.localPosition);
            Assert.AreEqual(Vector3.one, driver.animator.transform.localScale);
            Assert.AreEqual(spawn.transform.position, player.transform.position);
            Assert.Less(Quaternion.Angle(spawn.transform.rotation, player.transform.rotation), 0.001f);
            Assert.IsFalse(driver.animator.applyRootMotion);
            Assert.AreEqual(AnimatorCullingMode.AlwaysAnimate, driver.animator.cullingMode);
            Assert.IsNull(player.transform.Find("Body"));
            Assert.IsNull(player.transform.Find("Facing"));
            Assert.IsEmpty(driver.animator.GetComponentsInChildren<Collider>());
            var skin = driver.animator.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.AreEqual(0f, skin.sharedMesh.bounds.min.y, 0.005f);
            Assert.AreEqual(character.height, skin.sharedMesh.bounds.size.y, 0.05f);
            CollectionAssert.Contains(PlayerHiding.BodyRenderers(player.gameObject), skin);
        }

        [Test]
        public void ContactAndTurnTimingAreSerializedFromTheModelSpec()
        {
            var driver = player.GetComponent<TiffanyAnimator>();
            var spec = ModelLibrary.LoadSpec("tiffany-provisional");
            foreach (var clip in spec.clips)
            {
                if (clip.speed > 0f)
                    Assert.AreEqual(new Vector2(clip.contacts.left, clip.contacts.right), driver.locomotionContacts, clip.name);
                if (clip.name == TiffanyAnimation.TurnLeft || clip.name == TiffanyAnimation.TurnRight)
                {
                    bool left = clip.name == TiffanyAnimation.TurnLeft;
                    Assert.AreEqual(clip.turnSpeed, left ? driver.authoredTurnSpeeds.x : driver.authoredTurnSpeeds.y);
                    Assert.AreEqual(new Vector2(clip.contacts.left, clip.contacts.right), left ? driver.leftTurnContacts : driver.rightTurnContacts);
                }
                if (clip.name == TiffanyAnimation.Flash) Assert.AreEqual(clip.frames / (float)spec.fps, driver.flashDuration);
            }
            var light = player.GetComponent<PlayerLight>();
            Assert.AreEqual(new Vector3(0f, 0.85f, 0.12f), light.spot.transform.localPosition);
            Assert.AreEqual(28f, light.pitch);
            Assert.AreEqual(1.2f, light.bodyGlowBoost);
            CollectionAssert.AreEqual(driver.animator.GetComponentsInChildren<Renderer>(true), light.bodyRenderers);
            Assert.IsNull(player.transform.Find("Hand Glow"));
            Assert.IsEmpty(player.GetComponent<PlayerSoul>().startingPowers);
            Assert.AreEqual("Soul Glow", light.baseGlow.name);
            Assert.AreSame(player.transform, light.baseGlow.transform.parent);
            Assert.AreEqual(new Vector3(0f, 0.8f, 0f), light.baseGlow.transform.localPosition);
            Assert.AreEqual(LightType.Point, light.baseGlow.type);
            Assert.AreEqual(2.5f, light.baseGlow.range);
            Assert.AreEqual(new Color(0.75f, 0.85f, 1f), light.baseGlow.color);
            Assert.AreEqual(1.2f, light.baseGlow.intensity);
            Assert.AreEqual(LightShadows.None, light.baseGlow.shadows);
            Assert.AreEqual(LightRenderMode.Auto, light.baseGlow.renderMode);
            Assert.IsNotNull(player.GetComponent<PlayerFootsteps>().source);
        }
    }
}
