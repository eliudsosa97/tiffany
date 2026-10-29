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
            Assert.IsNotNull(player.GetComponent<PlayerLight>().spot);
            Assert.IsNotNull(player.GetComponent<PlayerLight>().glow);
            Assert.IsNotNull(player.GetComponent<PlayerFootsteps>().source);
        }
    }
}
