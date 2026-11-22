using NUnit.Framework;
using UnityEngine;

namespace Zfw.QualityTiers.Tests
{
    public class TierExtensionTests
    {
        [System.Serializable] class FakeExtA : TierExtension { public int value = 1; }
        [System.Serializable] class FakeExtB : TierExtension { }

        [Test]
        public void GetReturnsNullWhenMissing_AndGetOrAddCreatesOnce()
        {
            var t = ScriptableObject.CreateInstance<TierSettings>();
            Assert.IsNull(t.Get<FakeExtA>());
            var a = t.GetOrAdd<FakeExtA>();
            a.value = 7;
            Assert.AreSame(a, t.GetOrAdd<FakeExtA>());
            Assert.AreEqual(7, t.Get<FakeExtA>().value);
            Assert.IsNull(t.Get<FakeExtB>());
            Assert.AreEqual(1, t.extensions.Count);
            Object.DestroyImmediate(t);
        }
    }
}
