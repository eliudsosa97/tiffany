using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using LogAssert = TiffanyGame.Tests.TiffanyLogs;

namespace TiffanyGame.Tests
{
    public sealed class TiffanyLogsTests : TiffanyLogTests
    {
        [Test]
        public void InformationalLogsAreOptionalWhileExplicitExpectationsStillMatch()
        {
            Debug.Log($"{GameInfo.LogPrefix} Bruja suena: Crack");
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Escondida");
            LogAssert.Expect(LogType.Log, new Regex(@"\A\[Tiffany\] Bruja suena: Landed\z"));
            Debug.Log($"{GameInfo.LogPrefix} Latido: 55");
            Debug.Log($"{GameInfo.LogPrefix} Escondida");
            LogAssert.NoUnexpectedReceived();
            Debug.Log($"{GameInfo.LogPrefix} Bruja suena: Landed");
            Debug.Log($"{GameInfo.LogPrefix} Bruja suena: Landed");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void WarningReceivedBeforeExpectationDoesNotBlockInformationalLogs()
        {
            string warning = $"{GameInfo.LogPrefix} advertencia de prueba";
            Debug.LogWarning(warning);
            LogAssert.Expect(LogType.Warning, warning);
            LogAssert.NoUnexpectedReceived();

            string message = $"{GameInfo.LogPrefix} Escondida";
            LogAssert.Expect(LogType.Log, message);
            Debug.Log(message);
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(LogType.Warning)]
        [TestCase(LogType.Error)]
        public void WarningsAndErrorsRemainUnexpected(LogType type)
        {
            string message = $"{GameInfo.LogPrefix} fallo de prueba";
            if (type == LogType.Warning) Debug.LogWarning(message);
            else Debug.LogError(message);
            Assert.Catch<ResultStateException>(() => LogAssert.NoUnexpectedReceived());
            LogAssert.Expect(type, message);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ExceptionsRemainUnexpected()
        {
            Debug.LogException(new System.InvalidOperationException($"{GameInfo.LogPrefix} fallo de prueba"));
            Assert.Catch<ResultStateException>(() => LogAssert.NoUnexpectedReceived());
            LogAssert.Expect(LogType.Exception, new Regex(@"InvalidOperationException: \[Tiffany\] fallo de prueba"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OtherInformationalLogsRemainUnexpected()
        {
            const string message = "Otro sistema [Tiffany]";
            Debug.Log(message);
            Assert.Catch<ResultStateException>(() => LogAssert.NoUnexpectedReceived());
            LogAssert.Expect(LogType.Log, message);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
