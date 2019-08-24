﻿using com.csutil.logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace com.csutil.testing {

    public class XunitTestRunner {

        public class Test {

            public object classInstance;
            public MethodInfo methodToTest;
            public Task testTask;
            public Action StartTest;
            public object invokeResult;
            public ExceptionDispatchInfo reportedError;

            public Test(object classInstance, MethodInfo methodToTest) {
                this.classInstance = classInstance;
                this.methodToTest = methodToTest;
            }

            public override string ToString() {
                var res = methodToTest.ToStringV2() + ": ";
                res = (invokeResult != null) ? res + invokeResult : res;
                res = (reportedError != null) ? res + reportedError.SourceException.Message : res;
                return res;
            }

        }

        public static IEnumerable<Test> CreateExecutionIterator(Type classToTest, Action<Test> onTestStarted) {
            IEnumerable<MethodInfo> methodsToTest = GetMethodsToTest(classToTest);
            return methodsToTest.Map(methodToTest => {
                var test = new Test(CreateInstance(classToTest), methodToTest);
                ResetStaticInstances();
                onTestStarted(test);
                test.StartTest = () => { test.testTask = RunTestOnMethod(test); };
                return test;
            });
        }

        private static async Task RunTestOnMethod(Test res) {
            await Task.Delay(5);
            AssertIsUnityLog();
            try {
                res.invokeResult = res.methodToTest.Invoke(res.classInstance, null);
                if (res.invokeResult is Task t) {
                    while (!t.IsCompleted) { await Task.Delay(5); }
                    if (t.IsFaulted) { SetError(res, t.Exception); return; }
                }
            }
            catch (Exception e) { SetError(res, e.InnerException); }
        }

        private static void SetError(Test res, Exception e2) {
            res.reportedError = ExceptionDispatchInfo.Capture(e2);
        }

        private static void ResetStaticInstances() {
            EventBus.instance = new EventBus();
            IoC.inject = new injection.Injector();
            UnitySetup.SetupDefaultSingletonsIfNeeded();
        }

        private static void AssertIsUnityLog() {
            Assert.True(StaticFieldsSetCorrecty(), "Log.instance=" + Log.instance.GetType());
        }

        private static bool StaticFieldsSetCorrecty() { return Log.instance is LogToUnityDebugLog; }

        private static IEnumerable<MethodInfo> GetMethodsToTest(Type classToTest) {
            var f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var testMethods = classToTest.GetMethods(f).Filter(m => { return m.HasAttribute<Fact>(true); });
            return testMethods;
        }

        private static object CreateInstance(Type classToTest) {
            var c1 = classToTest.GetConstructors().FirstOrDefault(c => c.GetParameters().IsNullOrEmpty());
            if (c1 != null) { return c1.Invoke(null); }
            var c2 = classToTest.GetConstructors().FirstOrDefault(c => c.GetParameters().Single().ParameterType.IsCastableTo<ITestOutputHelper>());
            if (c2 != null) { return c2.Invoke(new object[1] { new ITestOutputHelper() }); }
            throw new ArgumentException("No construction found with no or a single ITestOutputHelper parameter");
        }

    }

}
