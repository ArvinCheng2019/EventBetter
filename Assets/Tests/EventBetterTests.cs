﻿using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;

using InvalidOperationException = System.InvalidOperationException;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using System.Runtime.CompilerServices;

public class EventBetterTests
{
    public class TestMessage { }
    public class TestMessage2 { }
    public class TestMessage3 { }

    public class TestBehaviour : MonoBehaviour
    {
        private int expectedInstanceId;

        private struct SomeStruct
        {
            public int Value { get; set; }
        }

        private class SomeClass
        {
            public int Value { get; set; }

            public override bool Equals(object obj)
            {
                var otherValue = (obj as SomeClass)?.Value;
                return otherValue.HasValue && otherValue.Value == Value;
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }

        public IEnumerator TestCoroutineCapture()
        {
            {
                // if nothing is captured - works
                yield return null;
                EventBetter.Listen(this, (TestMessage msg) => { });
                Assert.IsTrue(EventBetter.UnlistenAll(this));
            }

            {
                // if nothing is captured - works
                int count = 0;
                yield return null;
                using (var listener = EventBetter.ListenManual((TestMessage msg) => InstanceHandle(++count, 1)))
                {
                    Assert.IsTrue(EventBetter.Raise(new TestMessage()));
                    Assert.AreEqual(1, count);
                }
                Assert.IsFalse(EventBetter.Raise(new TestMessage()));
                Assert.AreEqual(1, count);
            }

            {
                // if local coroutine variables captured - won't
                int count = 0;
                yield return null;
                Assert.Throws<InvalidOperationException>(() => EventBetter.Listen(this, (TestMessage msg) => InstanceHandle(++count, 1)));
            }
        }

        public IEnumerator TestListenWait()
        {
            using (var listener = EventBetter.ListenWait<TestMessage>())
            {
                Assert.IsNull(listener.Messages);
                Assert.IsNull(listener.First);

                StartCoroutine(WaitAndThen(() =>
                {
                    Assert.IsTrue(EventBetter.Raise(new TestMessage()));
                    Assert.IsTrue(EventBetter.Raise(new TestMessage()));
                }));

                yield return listener;

                Assert.AreEqual(2, listener.Messages.Count);
                Assert.IsNotNull(listener.First);
                Assert.AreEqual(listener.First, listener.Messages[0]);

                Assert.IsFalse(EventBetter.Raise(new TestMessage()));
            }
        }

        public IEnumerator WaitAndThen(System.Action action)
        {
            yield return new WaitForSeconds(0.1f);
            action();
        }

        public void TestIfActiveAndEnabled()
        {
            int count = 0;

            EventBetter.Listen(this, (TestMessage o) => ++count);
            Assert.IsTrue(EventBetter.Raise(new TestMessage()));
            Assert.AreEqual(1, count);

            enabled = false;
            Assert.IsTrue(EventBetter.Raise(new TestMessage()));
            Assert.AreEqual(2, count);
            enabled = true;

            gameObject.SetActive(false);
            Assert.IsTrue(EventBetter.Raise(new TestMessage()));
            Assert.AreEqual(3, count);
            gameObject.SetActive(true);

            Assert.IsTrue(EventBetter.Unlisten<TestMessage>(this));
            Assert.IsFalse(EventBetter.Raise(new TestMessage()));
            Assert.AreEqual(3, count);

            EventBetter.Listen(this, (TestMessage o) => ++count, onlyIfActiveAndEnabled: true);
            Assert.IsTrue(EventBetter.Raise(new TestMessage()));
            Assert.AreEqual(4, count);

            enabled = false;
            Assert.IsFalse(EventBetter.Raise(new TestMessage()));
            Assert.AreEqual(4, count);
            enabled = true;

            Assert.IsTrue(EventBetter.Raise(new TestMessage()));
            Assert.AreEqual(5, count);

            gameObject.SetActive(false);
            Assert.IsFalse(EventBetter.Raise(new TestMessage()));
            Assert.AreEqual(5, count);
            gameObject.SetActive(true);

            Assert.IsTrue(EventBetter.Raise(new TestMessage()));
            Assert.AreEqual(6, count);
        }

        public void TestMutableLambda()
        {
            {
                int startValue = 100;
                EventBetter.Listen(this, (TestMessage o) => InstanceHandle(startValue++, 100));
                EventBetter.Listen(this, (TestMessage o) => InstanceHandle(startValue++, 101));
                EventBetter.Listen(this, (TestMessage o) => InstanceHandle(startValue++, 102));
                EventBetter.Listen(this, (TestMessage o) => InstanceHandle(startValue++, 103));
                EventBetter.Listen(this, (TestMessage o) => InstanceHandle(startValue++, 104));
            }
            {
                int startValue = 200;
                EventBetter.Listen(this, (TestMessage o) => InstanceHandle(startValue, 210));
                startValue += 10;
                EventBetter.Listen(this, (TestMessage o) => InstanceHandle(startValue, 210));
            }

            Assert.IsTrue(EventBetter.Raise(new TestMessage()));
            Assert.IsTrue(EventBetter.UnlistenAll(this));
        }

        public void TestSelf()
        {
            EventBetter.Listen(this, (TestMessage o) => InstanceHandle(null, null));
        }

        public void TestSelfAdv()
        {
            var date = System.DateTime.Now.Date;
            var str = "TestString";
            EventBetter.Listen(this, (TestMessage o) => InstanceHandle(null, null));
            EventBetter.Listen(this, (TestMessage o) => InstanceHandle(date, System.DateTime.Now.Date));
            EventBetter.Listen(this, (TestMessage o) => InstanceHandle(str, "TestString"));
        }

        public void TestSelfStatic()
        {
            EventBetter.Listen(this, (TestMessage o) => StaticHandle(null, null));
        }

        public void TestStruct()
        {
            var someStruct = new SomeStruct() { Value = 123 };
            EventBetter.Listen(this, (TestMessage o) => InstanceHandle(null, null));
            EventBetter.Listen(this, (TestMessage o) => InstanceHandle(someStruct, new SomeStruct() { Value = 123 }));
            EventBetter.Listen(this, (TestMessage o) => InstanceHandle(someStruct.Value, 123));
        }

        public void TestStructStatic()
        {
            var someStruct = new SomeStruct() { Value = 123 };
            EventBetter.Listen(this, (TestMessage o) => StaticHandle(null, null));
            EventBetter.Listen(this, (TestMessage o) => StaticHandle(someStruct, new SomeStruct() { Value = 123 }));
            EventBetter.Listen(this, (TestMessage o) => StaticHandle(someStruct.Value, 123));
        }

        public void TestClass()
        {
            var someClass = new SomeClass() { Value = 456 };

            // this captured implicitly
            Assert.Throws<InvalidOperationException>(() => EventBetter.Listen(this, (TestMessage o) => InstanceHandle(null, null)));
            Assert.Throws<InvalidOperationException>(() => EventBetter.Listen(this, (TestMessage o) => InstanceHandle(someClass, new SomeClass() { Value = 456 })));
            Assert.Throws<InvalidOperationException>(() => EventBetter.Listen(this, (TestMessage o) => InstanceHandle(someClass.Value, 456)));
        }

        public void TestClassStatic()
        {
            var someClass = new SomeClass() { Value = 456 };
            EventBetter.Listen(this, (TestMessage o) => StaticHandle(null, null));
            Assert.Throws<InvalidOperationException>(() => EventBetter.Listen(this, (TestMessage o) => StaticHandle(someClass, new SomeClass() { Value = 456 })));
            Assert.Throws<InvalidOperationException>(() => EventBetter.Listen(this, (TestMessage o) => StaticHandle(someClass.Value, 456)));
        }

        public void TestSomeOtherHost()
        {
            {
                var go = new GameObject("blah");
                go.transform.SetParent(transform);
                EventBetter.Listen(go, (TestMessage o) => StaticHandle(go.name, "blah"));
            }

            {
                var go = new GameObject("blah2");
                go.transform.SetParent(transform);
                Assert.Throws<InvalidOperationException>(() => EventBetter.Listen(go, (TestMessage o) => InstanceHandle(go.name, "blah2")));
            }
        }

        public void TestUnregister()
        {
            Assert.AreEqual(false, EventBetter.Unlisten<TestMessage>(this));
            Assert.AreEqual(false, EventBetter.Raise(new TestMessage()));

            EventBetter.Listen(this, (TestMessage o) => InstanceHandle(null, null));
            Assert.AreEqual(true, EventBetter.Raise(new TestMessage()));
            Assert.AreEqual(true, EventBetter.Unlisten<TestMessage>(this));
            Assert.AreEqual(false, EventBetter.Raise(new TestMessage()));

            EventBetter.Listen(this, (TestMessage o) => InstanceHandle(null, null));
            EventBetter.Listen(this, (TestMessage o) => InstanceHandle(null, null));
            EventBetter.Listen(this, (TestMessage o) => InstanceHandle(null, null));
            EventBetter.Listen(this, (TestMessage o) => InstanceHandle(null, null));
            Assert.AreEqual(true, EventBetter.Raise(new TestMessage()));
            Assert.AreEqual(true, EventBetter.Unlisten<TestMessage>(this));
            Assert.AreEqual(false, EventBetter.Unlisten<TestMessage>(this));
            Assert.AreEqual(false, EventBetter.Unlisten<TestMessage>(this));
            Assert.AreEqual(false, EventBetter.Unlisten<TestMessage>(this));
            Assert.AreEqual(false, EventBetter.Unlisten<TestMessage>(this));
            Assert.AreEqual(false, EventBetter.Raise(new TestMessage()));

            EventBetter.Listen(this, (TestMessage o) => InstanceHandle(null, null));
            Assert.AreEqual(true, EventBetter.Unlisten<TestMessage>(this));
            Assert.AreEqual(false, EventBetter.Unlisten<TestMessage>(this));
            Assert.AreEqual(false, EventBetter.Unlisten<TestMessage>(this));
            Assert.AreEqual(false, EventBetter.Raise(new TestMessage()));
        }

        public void TestNestedRegisterSimple()
        {
            int totalInvocations = 0;

            EventBetter.Listen(this, (TestMessage o) =>
            {
                InstanceHandle(null, null);
                ++totalInvocations;

                EventBetter.Listen(this, (TestMessage oo) =>
                {
                    InstanceHandle(null, null);
                    ++totalInvocations;
                });
            });

            EventBetter.Raise(new TestMessage());
            Assert.AreEqual(2, totalInvocations);

            EventBetter.Raise(new TestMessage());
            Assert.AreEqual(5, totalInvocations);

            EventBetter.Raise(new TestMessage());
            Assert.AreEqual(9, totalInvocations);
        }

        List<System.IDisposable> manualHandlers = new List<System.IDisposable>();
        public void TestNestedRegisterSimpleManual()
        {
            int totalInvocations = 0;

            manualHandlers.Add(EventBetter.ListenManual((TestMessage o) =>
            {
                InstanceHandle(null, null);
                ++totalInvocations;

                manualHandlers.Add(EventBetter.ListenManual((TestMessage oo) =>
                {
                    InstanceHandle(null, null);
                    ++totalInvocations;
                }));
            }));

            EventBetter.Raise(new TestMessage());
            Assert.AreEqual(2, totalInvocations);

            EventBetter.Raise(new TestMessage());
            Assert.AreEqual(5, totalInvocations);

            EventBetter.Raise(new TestMessage());
            Assert.AreEqual(9, totalInvocations);

            foreach (var handler in manualHandlers)
                handler.Dispose();
        }

        public void TestNestedRaiseSimple()
        {
            int nestedCalls = 0;
            int totalInvocations = 0;

            EventBetter.Listen(this, (TestMessage o) =>
            {
                ++totalInvocations;
                InstanceHandle(null, null);
                try
                {
                    ++nestedCalls;
                    if (nestedCalls < 10)
                    {
                        EventBetter.Raise(new TestMessage());
                    }
                }
                finally
                {
                    --nestedCalls;
                }
            });

            EventBetter.Raise(new TestMessage());
            Assert.AreEqual(10, totalInvocations);
        }

        public void TestNestedRaiseSimpleManual()
        {
            int nestedCalls = 0;
            int totalInvocations = 0;

            var handler = EventBetter.ListenManual((TestMessage o) =>
            {
                ++totalInvocations;
                InstanceHandle(null, null);
                try
                {
                    ++nestedCalls;
                    if (nestedCalls < 10)
                    {
                        EventBetter.Raise(new TestMessage());
                    }
                }
                finally
                {
                    --nestedCalls;
                }
            });

            try
            {
                EventBetter.Raise(new TestMessage());
                Assert.AreEqual(10, totalInvocations);
            }
            finally
            {
                handler.Dispose();
            }
        }

        public void TestNestedMessedUp()
        {
            int nestedCalls = 0;
            int totalInvocations = 0;

            int dummyMessage2Invocations = 0;
            EventBetter.Listen(this, (TestMessage2 o) =>
            {
                InstanceHandle(null, null);
                ++dummyMessage2Invocations;

                EventBetter.Unlisten<TestMessage>(this);
            });

            int dummyMessage3Invocations = 0;
            EventBetter.Listen(this, (TestMessage3 o) =>
            {
                InstanceHandle(null, null);
                ++dummyMessage3Invocations;

                EventBetter.Unlisten<TestMessage3>(this);
                EventBetter.Raise(new TestMessage());
            });

            EventBetter.Listen(this, (TestMessage o) =>
            {
                InstanceHandle(null, null);
                ++totalInvocations;
                ++nestedCalls;
                if (nestedCalls >= 10)
                {
                    EventBetter.Raise(new TestMessage2());
                    EventBetter.Raise(new TestMessage3());

                    EventBetter.Raise(new TestMessage2());
                    EventBetter.Raise(new TestMessage3());

                    EventBetter.Raise(new TestMessage2());
                    EventBetter.Raise(new TestMessage3());
                }

                EventBetter.Raise(new TestMessage());
            });

            EventBetter.Raise(new TestMessage());
            Assert.AreEqual(10, totalInvocations);
            Assert.AreEqual(3, dummyMessage2Invocations);
            Assert.AreEqual(1, dummyMessage3Invocations);
        }

        int nestedInvocations = 0;
        public void TestNestedRaiseContexts()
        {
            int totalInvocations = 0;
            nestedInvocations = 0;

            EventBetter.Listen(this, (TestMessage o) =>
            {
                InstanceHandle(null, null);
                ++totalInvocations;
                NestedDifferentContext(totalInvocations);
            });

            EventBetter.Raise(new TestMessage());
            Assert.AreEqual(2, totalInvocations + nestedInvocations);

            EventBetter.Raise(new TestMessage());
            Assert.AreEqual(5, totalInvocations + nestedInvocations);

            EventBetter.Raise(new TestMessage());
            Assert.AreEqual(9, totalInvocations + nestedInvocations);
        }

        public void NestedDifferentContext(int baseValue)
        {
            EventBetter.Listen(this, (TestMessage o) =>
            {
                InstanceHandle(null, null);
                ++nestedInvocations;
            });
        }
            

        private void Awake()
        {
            expectedInstanceId = GetInstanceID();
        }

        private void InstanceHandle(object param, object expectedValue)
        {
            Assert.AreEqual(expectedInstanceId, GetInstanceID());
            StaticHandle(param, expectedValue);
        }

        private static void StaticHandle(object param, object expectedValue)
        {
            Assert.AreEqual(expectedValue, param);
        }
    }

    private static void Collect()
    {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
    }

    private IEnumerator SimpleTest(System.Action<TestBehaviour> doStuff, bool expectedResult = true, bool collect = true)
    {
        return CoroTest(tb =>
        {
            doStuff(tb);
            return null;
        }, expectedResult, collect);
    }

    private IEnumerator CoroTest(System.Func<TestBehaviour, object> doStuff, bool expectedResult = true, bool collect = true)
    {
        var go = new GameObject("Test", typeof(TestBehaviour));
        var comp = go.GetComponent<TestBehaviour>();

        try
        {
            var result = doStuff(comp);
            if (result != null)
            {
                yield return result;
                result = null;
            }
            Assert.AreEqual(expectedResult, EventBetter.Raise(new TestMessage()));
            if (collect)
            {
                Collect();
                Assert.AreEqual(expectedResult, EventBetter.Raise(new TestMessage()));
            }
        }
        finally
        {
            Object.DestroyImmediate(comp, true);
            Object.DestroyImmediate(go, true);
        }

        // after the destroy there should be no receivers
        Assert.IsFalse(EventBetter.Raise(new TestMessage()));

        if (collect)
        {
            var weak = new System.WeakReference(comp);
            Assert.IsTrue(weak.IsAlive);
            go = null;
            comp = null;

            for (int i = 0; i < 10; ++i)
            {
                yield return null;
                Collect();
            }
            Assert.IsFalse(weak.IsAlive, "So we have a leak...");
        }
    }

    [SetUp]
    public void SetUp()
    {
        EventBetter.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        Assert.IsFalse(EventBetter.Test_IsLeaking);
    }

    [Test]
    public void RegisterManual()
    {
        int someValue = 666;

        var disp1 = EventBetter.ListenManual((TestMessage msg) => someValue++);
        var disp2 = EventBetter.ListenManual((TestMessage msg) => someValue++);

        Assert.AreEqual(true, EventBetter.Raise(new TestMessage()));
        Assert.AreEqual(668, someValue);

        disp1.Dispose();
        Assert.AreEqual(true, EventBetter.Raise(new TestMessage()));
        Assert.AreEqual(669, someValue);

        // whether double dispose breaks anything
        disp1.Dispose();
        Assert.AreEqual(true, EventBetter.Raise(new TestMessage()));
        Assert.AreEqual(670, someValue);

        disp2.Dispose();
        Assert.AreEqual(false, EventBetter.Raise(new TestMessage()));
        Assert.AreEqual(670, someValue);
    }

    [UnityTest] public IEnumerator IfActiveAndEnabled() => SimpleTest(t => t.TestIfActiveAndEnabled());

    [UnityTest] public IEnumerator Self() => SimpleTest(t => t.TestSelf());
    [UnityTest] public IEnumerator SelfAdv() => SimpleTest(t => t.TestSelfAdv());
    [UnityTest] public IEnumerator MutableLambda() => SimpleTest(t => t.TestMutableLambda(), expectedResult: false);
    [UnityTest] public IEnumerator SelfStatic() => SimpleTest(t => t.TestSelfStatic());
    [UnityTest] public IEnumerator CaptureStruct() => SimpleTest(t => t.TestStruct());
    [UnityTest] public IEnumerator CaptureStructStatic() => SimpleTest(t => t.TestStructStatic());
    [UnityTest] public IEnumerator SomeOtherHost() => SimpleTest(t => t.TestSomeOtherHost());
    [UnityTest] public IEnumerator Unregister() => SimpleTest(t => t.TestUnregister(), expectedResult: false);

    [UnityTest]
    public IEnumerator Destroy() => SimpleTest(t =>
    {
        t.TestSelf();
        Object.DestroyImmediate(t);
    }, expectedResult: false);

    [UnityTest] public IEnumerator NoCallbacks() => SimpleTest(t => { }, expectedResult: false);

    [UnityTest] public IEnumerator CaptureClass() => SimpleTest(t => t.TestClass(), expectedResult: false);
    [UnityTest] public IEnumerator CaptureClassStatic() => SimpleTest(t => t.TestClassStatic());


    [UnityTest] public IEnumerator NestedRegisterSimple() => SimpleTest(t => t.TestNestedRegisterSimple());
    [UnityTest] public IEnumerator NestedRegisterSimpleManual() => SimpleTest(t => t.TestNestedRegisterSimpleManual(), expectedResult: false);
    [UnityTest] public IEnumerator NestedRaiseSimple() => SimpleTest(t => t.TestNestedRaiseSimple());
    [UnityTest] public IEnumerator NestedRaiseContexts() => SimpleTest(t => t.TestNestedRaiseContexts());
    [UnityTest] public IEnumerator NestedRaiseSimpleManual() => SimpleTest(t => t.TestNestedRaiseSimpleManual(), expectedResult: false);
    [UnityTest] public IEnumerator NestedMessedUp() => SimpleTest(t => t.TestNestedMessedUp(), expectedResult: false);

    [UnityTest] public IEnumerator CoroCaptureTest() => CoroTest(b => b.TestCoroutineCapture(), expectedResult: false);
    [UnityTest] public IEnumerator CoroListenWait() => CoroTest(b => b.TestListenWait(), expectedResult: false);

    #region GC Tests

    [UnityTest]
    public IEnumerator GCCollectGameObjects()
    {
        System.WeakReference weak;
        {
            var other = new GameObject();
            weak = new System.WeakReference(other);
            GameObject.DestroyImmediate(other);
            Assert.IsTrue(weak.IsAlive);
        }

        Collect();

        // GC.Collect will only collect if run at least a frame after an object has been destroyed
        for (int i = 0; i < 10; ++i)
        {
            yield return null;
            Assert.IsTrue(weak.IsAlive);
        }

        Collect();
        Assert.IsFalse(weak.IsAlive);
    }

    [UnityTest]
    public IEnumerator GCConditinalWeakTableBroken()
    {
        object a = new object();
        object b = new object();

        ConditionalWeakTable<object, object> cwt = new ConditionalWeakTable<object, object>();
        cwt.Add(a, b);

        var wa = new System.WeakReference(a);
        var wb = new System.WeakReference(b);

        Assert.IsTrue(wa.IsAlive);
        Assert.IsTrue(wb.IsAlive);

        yield return null;
        Collect();

        Assert.IsTrue(wa.IsAlive);
        Assert.IsTrue(wb.IsAlive);

        b = null;

        for (int i = 0; i < 10; ++i)
        {
            yield return null;
            Collect();

            Assert.IsTrue(wa.IsAlive);
            Assert.IsTrue(wb.IsAlive);
        }

        a = null;

        for ( int i = 0; i < 10; ++i )
        {
            yield return null;
            Collect();

            Assert.IsTrue(wa.IsAlive, "If CWS worked, this would be false.");
            Assert.IsTrue(wb.IsAlive, "If CWS worked, this would be false.");
        }
    }

    [Test]
    public void GCCollectObjects()
    {
        object a = new object();
        var wa = new System.WeakReference(a);
        Assert.IsTrue(wa.IsAlive);

        Collect();
        Assert.IsTrue(wa.IsAlive);

        a = null;
        Collect();
        Assert.IsTrue(wa.IsAlive, "Why oh why does it not get collected?");
    }

    [UnityTest]
    public IEnumerator GCCollectObjectsCoro()
    {
        object a = new object();
        var wa = new System.WeakReference(a);
        Assert.IsTrue(wa.IsAlive);
  
        Collect();
        Assert.IsTrue(wa.IsAlive);

        a = null;
        Collect();

        // GC.Collect will only collect if run at least a frame after an object has been destroyed
        for (int i = 0; i < 10; ++i)
        {
            yield return null;
            Assert.IsTrue(wa.IsAlive, "Collect seems to need yielding to work");
        }

        yield return null;
        Collect();
        Assert.IsFalse(wa.IsAlive);
    }

    #endregion
}
