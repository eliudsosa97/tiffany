using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;
using UnityLogAssert = UnityEngine.TestTools.LogAssert;

namespace TiffanyGame.Tests
{
    public abstract class TiffanyLogTests
    {
        [UnitySetUp]
        public IEnumerator SetUpTiffanyLogs()
        {
            TiffanyLogs.SetUp();
            yield break;
        }

        [UnityTearDown]
        public IEnumerator TearDownTiffanyLogs()
        {
            TiffanyLogs.TearDown();
            yield break;
        }
    }

    internal static class TiffanyLogs
    {
        static Filter filter;

        public static void SetUp()
        {
            filter = new Filter(Debug.unityLogger.logHandler);
            Debug.unityLogger.logHandler = filter;
        }

        public static void TearDown()
        {
            Debug.unityLogger.logHandler = filter.Original;
            filter = null;
        }

        public static void Expect(LogType type, string message)
        {
            if (type == LogType.Log) filter.Expected.Add((message, null));
            UnityLogAssert.Expect(type, message);
        }

        public static void Expect(LogType type, Regex pattern)
        {
            if (type == LogType.Log) filter.Expected.Add((null, pattern));
            UnityLogAssert.Expect(type, pattern);
        }

        public static void NoUnexpectedReceived() => UnityLogAssert.NoUnexpectedReceived();

        sealed class Filter : ILogHandler
        {
            static readonly Regex Informational = new Regex(@"\A" + Regex.Escape(GameInfo.LogPrefix));
            public readonly ILogHandler Original;
            public readonly List<(string Message, Regex Pattern)> Expected = new();

            public Filter(ILogHandler original) => Original = original;

            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
            {
                string message = string.Format(format, args);
                if (logType != LogType.Log || !Informational.IsMatch(message) || Consume(message))
                    Original.LogFormat(logType, context, format, args);
            }

            public void LogException(Exception exception, UnityEngine.Object context) =>
                Original.LogException(exception, context);

            bool Consume(string message)
            {
                for (int i = 0; i < Expected.Count; i++)
                {
                    var expected = Expected[i];
                    if (expected.Pattern != null ? !expected.Pattern.IsMatch(message) : expected.Message != message)
                        continue;
                    Expected.RemoveAt(i);
                    return true;
                }
                return false;
            }
        }
    }
}
