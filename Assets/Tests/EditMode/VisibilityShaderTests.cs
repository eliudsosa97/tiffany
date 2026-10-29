using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class VisibilityShaderTests
    {
        const string Occlusion = "Assets/Art/TiffanyOcclusionLit.shader";
        const string Silhouette = "Assets/Art/TiffanySilhouette.shader";
        const string Spirit = "Assets/Art/Shaders/Spirit.shader";
        const string WebLighting = "_ADDITIONAL_LIGHTS_VERTEX _MAIN_LIGHT_SHADOWS FOG_LINEAR";
        const string MacLighting = "_CLUSTER_LIGHT_LOOP _ADDITIONAL_LIGHTS _MAIN_LIGHT_SHADOWS_CASCADE _SHADOWS_SOFT_HIGH FOG_LINEAR";

        static IEnumerable<TestCaseData> Variants()
        {
            var shaders = new Dictionary<string, string[]>
            {
                [Occlusion] = new[]
                {
                    "", WebLighting, MacLighting,
                    "_NORMALMAP " + WebLighting,
                    "_NORMALMAP INSTANCING_ON " + WebLighting,
                    "_NORMALMAP INSTANCING_ON " + MacLighting,
                    "_TIFFANY_CHARACTER " + WebLighting,
                    "_TIFFANY_CHARACTER " + MacLighting,
                    "_TIFFANY_CHARACTER _TIFFANY_UNLIT " + WebLighting,
                    "_NORMALMAP _ALPHATEST_ON _GBUFFER_NORMALS_OCT INSTANCING_ON " + WebLighting,
                    "_ALPHATEST_ON _CASTING_PUNCTUAL_LIGHT_SHADOW INSTANCING_ON",
                    "_EMISSION _METALLICSPECGLOSSMAP _OCCLUSIONMAP " + MacLighting,
                    "EDITOR_VISUALIZATION _EMISSION _METALLICSPECGLOSSMAP"
                },
                [Silhouette] = new[] { "", "INSTANCING_ON" },
                [Spirit] = new[]
                {
                    "", WebLighting, MacLighting,
                    "_CLUSTER_LIGHT_LOOP _ADDITIONAL_LIGHTS _MAIN_LIGHT_SHADOWS_SCREEN _CASTING_PUNCTUAL_LIGHT_SHADOW"
                }
            };
            foreach (var shader in shaders)
                foreach (string keywords in shader.Value)
                    foreach (var platform in new[] { ShaderCompilerPlatform.Metal, ShaderCompilerPlatform.GLES3x })
                        yield return new TestCaseData(shader.Key, keywords, platform);
        }

        [TestCaseSource(nameof(Variants))]
        public void AllPassesCompileWithoutErrors(string path, string keywords, ShaderCompilerPlatform platform)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.IsNotNull(shader, path);
            var subshader = ShaderUtil.GetShaderData(shader).GetSubshader(0);
            Assert.IsNotNull(subshader, path);
            string[] expectedPasses = path == Occlusion
                ? new[] { "ForwardLit", "DepthOnly", "DepthNormals", "ShadowCaster", "Meta" }
                : path == Spirit ? new[] { "ForwardLit", "ShadowCaster", "DepthOnly", "DepthNormalsOnly" }
                : new[] { "Silhouette" };
            Assert.AreEqual(expectedPasses.Length, subshader.PassCount, path);
            var target = platform == ShaderCompilerPlatform.Metal ? BuildTarget.StandaloneOSX : BuildTarget.WebGL;
            // GLES3x compila las dos etapas al solicitar Vertex.
            var stages = platform == ShaderCompilerPlatform.GLES3x
                ? new[] { ShaderType.Vertex } : new[] { ShaderType.Vertex, ShaderType.Fragment };
            string[] enabledKeywords = keywords.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < subshader.PassCount; index++)
            {
                var pass = subshader.GetPass(index);
                Assert.That(pass.Name, Is.EqualTo(expectedPasses[index]).IgnoreCase, path);
                foreach (var stage in stages)
                {
                    string context = $"{path}: {pass.Name}, {stage}, {platform}, [{keywords}]";
                    var compiled = pass.CompileVariant(stage, enabledKeywords, platform, target);
                    var messages = compiled.Messages ?? Array.Empty<ShaderMessage>();
                    string diagnostics = context + "\n" + FormatMessages(messages);
                    Assert.IsTrue(compiled.Success, diagnostics);
                    Assert.IsNotEmpty(compiled.ShaderData, diagnostics);
                    foreach (var message in messages)
                        Assert.AreNotEqual(ShaderCompilerMessageSeverity.Error, message.severity, diagnostics);
                }
            }
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), path + "\n" + FormatMessages(ShaderUtil.GetShaderMessages(shader)));
        }

        static string FormatMessages(ShaderMessage[] messages) => string.Join("\n", Array.ConvertAll(messages,
            message => $"{message.platform} {message.severity} {message.file}:{message.line}: {message.message}\n{message.messageDetails}"));
    }
}
