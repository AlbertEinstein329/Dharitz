using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// Lanza los tests EditMode desde codigo y escribe el resumen en un fichero (util para comprobar un cambio sin abrir
/// el Test Runner). Menu: Tools &gt; Dharitz &gt; Tests &gt; Ejecutar EditMode.
/// </summary>
public static class DharitzTestRun
{
    public const string ResultPath = "Temp/DharitzTestResults.txt";

    [MenuItem("Tools/Dharitz/Tests/Ejecutar EditMode")]
    public static void RunEditMode()
    {
        File.WriteAllText(ResultPath, "EJECUTANDO...\n");
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Callbacks());
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
    }

    private class Callbacks : ICallbacks
    {
        private readonly StringBuilder log = new StringBuilder();
        private int passed, failed, skipped;

        public void RunStarted(ITestAdaptor testsToRun) { }

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.Test.IsSuite) return;
            switch (result.TestStatus)
            {
                case TestStatus.Passed: passed++; break;
                case TestStatus.Failed: failed++; log.AppendLine($"FALLA {result.Test.FullName}: {result.Message}"); break;
                default: skipped++; break;
            }
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            File.WriteAllText(ResultPath, $"TERMINADO: {passed} correctos, {failed} fallos, {skipped} omitidos\n{log}");
        }
    }
}
