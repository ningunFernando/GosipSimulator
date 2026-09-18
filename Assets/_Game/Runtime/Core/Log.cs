using System.Diagnostics;

namespace GosipSimulator.Core
{
    /// <summary>
    /// The only place in the project allowed to call UnityEngine.Debug (R13).
    /// Conditional strips the call site and the evaluation of its arguments at compile time,
    /// which a plain Debug.Log does not: the interpolated string is always built otherwise.
    /// </summary>
    public static class Log
    {
        public const string VERBOSE = "GOSIPSIMULATOR_VERBOSE";

        // Fully qualified on purpose. Once GosipSimulator.Debug exists as a namespace,
        // a bare Debug in this scope resolves to the namespace instead of the type (CS0118).
        // Stacked Conditional attributes are an OR: traces compile in the Editor and in development
        // builds (DEBUG), and a release build strips every call and its string unless VERBOSE is
        // added to its Scripting Define Symbols on purpose.
        [Conditional("DEBUG")]
        [Conditional(VERBOSE)]
        public static void Trace(string msg) => UnityEngine.Debug.Log(msg);

        // DEBUG, not the guide's "UNITY_EDITOR" plus "DEVELOPMENT_BUILD". Two reasons: a single
        // Conditional attribute takes one symbol, so the guide's version does not compile at all,
        // and DEVELOPMENT_BUILD is deprecated as a compilation directive in Unity 6, where it
        // raises warning UAC0009 on every compile. DEBUG is the variant-aware symbol Unity
        // defines in the Editor and in development builds, which is the intended behaviour.
        [Conditional("DEBUG")]
        public static void Info(string msg) => UnityEngine.Debug.Log(msg);

        public static void Warn(string msg) => UnityEngine.Debug.LogWarning(msg);

        public static void Error(string msg) => UnityEngine.Debug.LogError(msg);
    }
}
