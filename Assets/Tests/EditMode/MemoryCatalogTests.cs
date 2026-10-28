using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ElVelador.Tests
{
    public sealed class MemoryCatalogTests
    {
        private readonly List<TextAsset> assets = new List<TextAsset>();

        [TearDown]
        public void TearDown()
        {
            foreach (TextAsset asset in assets)
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
            assets.Clear();
        }

        [Test]
        public void LoadsContractFieldsAndSpanishDrawings()
        {
            TextAsset asset = Asset("memory-1", @"{
                ""id"": 1, ""title"": ""Flores"", ""place"": ""Tumba de la abuela"", ""power"": ""Light"",
                ""drawings"": [
                    { ""image"": ""memory-1-1"", ""phrase"": ""La niña llevó flores."" },
                    { ""image"": ""memory-1-2"", ""phrase"": ""Mi abuela sonrió aquí."" }
                ]
            }");
            var catalog = new MemoryCatalog(new[] { asset });

            Assert.AreEqual(1, catalog.Count);
            Assert.IsTrue(catalog.TryGet(1, out MemoryData memory));
            Assert.AreEqual(1, memory.id);
            Assert.AreEqual("Flores", memory.title);
            Assert.AreEqual("Tumba de la abuela", memory.place);
            Assert.AreEqual(2, memory.drawings.Length);
            Assert.AreEqual("memory-1-1", memory.drawings[0].image);
            Assert.AreEqual("La niña llevó flores.", memory.drawings[0].phrase);
            Assert.AreEqual("memory-1-2", memory.drawings[1].image);
            Assert.AreEqual("Mi abuela sonrió aquí.", memory.drawings[1].phrase);
        }

        [Test]
        public void SortsAllAndFindsMemoriesById()
        {
            var catalog = new MemoryCatalog(new[]
            {
                Asset("memory-3", @"{""id"":3,""title"":""Muñeca""}"),
                Asset("memory-1", @"{""id"":1,""title"":""Flores""}"),
                Asset("memory-2", @"{""id"":2,""title"":""Listón""}")
            });

            Assert.AreEqual(3, catalog.Count);
            Assert.AreEqual(3, catalog.All.Count);
            for (int id = 1; id <= 3; id++)
            {
                Assert.AreEqual(id, catalog.All[id - 1].id);
                Assert.IsTrue(catalog.TryGet(id, out MemoryData memory));
                Assert.AreSame(catalog.All[id - 1], memory);
            }
            Assert.IsFalse(catalog.TryGet(9, out MemoryData missing));
            Assert.IsNull(missing);
        }

        [TestCase(@"{""id"":1,""title"":""Flores"",""power"":""UnknownPower""}")]
        [TestCase(@"{""id"":1,""title"":""Flores"",""power"":""UnknownPower"",""drawings"":null}")]
        public void NormalizesMissingDrawingsAndIgnoresUnknownPower(string json)
        {
            var catalog = new MemoryCatalog(new[] { Asset("memory-1", json) });

            Assert.IsTrue(catalog.TryGet(1, out MemoryData memory));
            Assert.IsNotNull(memory.drawings);
            Assert.IsEmpty(memory.drawings);
        }

        [Test]
        public void SkipsInvalidAssetsAndContinuesLoading()
        {
            LogAssert.Expect(LogType.Warning,
                new Regex("^" + Regex.Escape($"{GameInfo.LogPrefix} Recuerdo 'broken' omitido: JSON inválido:")));
            LogAssert.Expect(LogType.Warning,
                $"{GameInfo.LogPrefix} Recuerdo 'empty-json' omitido: el JSON está vacío");
            LogAssert.Expect(LogType.Warning,
                $"{GameInfo.LogPrefix} Recuerdo 'zero' omitido: el id debe ser 1 o mayor (tiene 0)");
            LogAssert.Expect(LogType.Warning,
                $"{GameInfo.LogPrefix} Recuerdo 'duplicate' omitido: el id 1 ya lo usa otro recuerdo");
            LogAssert.Expect(LogType.Warning,
                $"{GameInfo.LogPrefix} Recuerdo 'empty-title' omitido: falta el título");
            LogAssert.Expect(LogType.Warning,
                $"{GameInfo.LogPrefix} Recuerdo 'whitespace-title' omitido: falta el título");
            LogAssert.Expect(LogType.Warning,
                $"{GameInfo.LogPrefix} Recuerdo '<null>' omitido: el asset es null.");
            var catalog = new MemoryCatalog(new[]
            {
                Asset("memory-1", @"{""id"":1,""title"":""Flores""}"),
                Asset("broken", "not JSON"),
                Asset("empty-json", ""),
                Asset("zero", @"{""id"":0,""title"":""Cero""}"),
                Asset("duplicate", @"{""id"":1,""title"":""Duplicado""}"),
                Asset("empty-title", @"{""id"":2,""title"":""""}"),
                Asset("whitespace-title", @"{""id"":2,""title"":""   ""}"),
                null,
                Asset("memory-2", @"{""id"":2,""title"":""Listón""}")
            });

            Assert.AreEqual(2, catalog.Count);
            Assert.AreEqual(2, catalog.All.Count);
            Assert.IsFalse(catalog.TryGet(0, out _));
            Assert.IsTrue(catalog.TryGet(1, out MemoryData first));
            Assert.AreEqual("Flores", first.title);
            Assert.IsTrue(catalog.TryGet(2, out MemoryData second));
            Assert.AreEqual("Listón", second.title);
        }

        [Test]
        public void RejectsNullAssets()
        {
            Assert.Throws<ArgumentNullException>(() => new MemoryCatalog(null));
        }

        private TextAsset Asset(string name, string json)
        {
            var asset = new TextAsset(json)
            {
                name = name
            };
            assets.Add(asset);
            return asset;
        }
    }
}
