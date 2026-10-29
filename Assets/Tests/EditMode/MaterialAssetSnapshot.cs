using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame.Tests
{
    sealed class MaterialAssetSnapshot : IDisposable
    {
        readonly Material saved;
        readonly string guid;
        readonly long fileId;
        readonly string renderType;

        public MaterialAssetSnapshot(Material material)
        {
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material, out guid, out fileId));
            renderType = material.GetTag("RenderType", false);
            saved = new Material(material);
        }

        public void AssertUnchanged(Material actual)
        {
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(actual, out string actualGuid, out long actualId));
            Assert.AreEqual(guid, actualGuid, actual.name);
            Assert.AreEqual(fileId, actualId, actual.name);
            Assert.AreSame(saved.shader, actual.shader, actual.name);
            Assert.AreEqual(saved.renderQueue, actual.renderQueue, actual.name);
            Assert.AreEqual(saved.enableInstancing, actual.enableInstancing, actual.name);
            Assert.AreEqual(saved.doubleSidedGI, actual.doubleSidedGI, actual.name);
            Assert.AreEqual(saved.globalIlluminationFlags, actual.globalIlluminationFlags, actual.name);
            Assert.AreEqual(renderType, actual.GetTag("RenderType", false), actual.name);
            CollectionAssert.AreEquivalent(saved.shaderKeywords, actual.shaderKeywords, actual.name);
            Assert.AreEqual(saved.passCount, actual.passCount, actual.name);
            for (int pass = 0; pass < saved.passCount; pass++)
            {
                string passName = saved.GetPassName(pass);
                Assert.AreEqual(saved.GetShaderPassEnabled(passName), actual.GetShaderPassEnabled(passName), passName);
            }
            for (int i = 0; i < saved.shader.GetPropertyCount(); i++)
            {
                string property = saved.shader.GetPropertyName(i);
                switch (saved.shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Color:
                        for (int c = 0; c < 4; c++)
                            Assert.AreEqual(saved.GetColor(property)[c], actual.GetColor(property)[c], 1e-5f, property);
                        break;
                    case ShaderPropertyType.Vector:
                        Assert.AreEqual(saved.GetVector(property), actual.GetVector(property), property);
                        break;
                    case ShaderPropertyType.Texture:
                        Assert.AreSame(saved.GetTexture(property), actual.GetTexture(property), property);
                        Assert.AreEqual(saved.GetTextureScale(property), actual.GetTextureScale(property), property);
                        Assert.AreEqual(saved.GetTextureOffset(property), actual.GetTextureOffset(property), property);
                        break;
                    case ShaderPropertyType.Int:
                        Assert.AreEqual(saved.GetInteger(property), actual.GetInteger(property), property);
                        break;
                    default:
                        Assert.AreEqual(saved.GetFloat(property), actual.GetFloat(property), 1e-5f, property);
                        break;
                }
            }
        }

        public void Dispose() => UnityEngine.Object.DestroyImmediate(saved);
    }
}
