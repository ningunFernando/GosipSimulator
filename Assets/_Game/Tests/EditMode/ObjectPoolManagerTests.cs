using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using GosipSimulator.Core.Pool;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// Covers C1 and M3. C1 is the most expensive bug in the audit: the auto-expand branch handed
    /// back an object that was still inactive, so a pool under pressure silently stopped producing
    /// visible objects.
    /// </summary>
    public class ObjectPoolManagerTests
    {
        private const string PoolId = "TestPool";

        private static readonly Regex Expanding    = new Regex(@"^\[ObjectPoolManager\].*Expanding");
        private static readonly Regex DoubleReturn = new Regex(@"^\[ObjectPoolManager\].*[Dd]ouble return");
        private static readonly Regex PoolEmpty    = new Regex(@"^\[ObjectPoolManager\].*autoExpand is off");
        private static readonly Regex NoSuchPool   = new Regex(@"^\[ObjectPoolManager\].*does not exist");

        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();

        private GameObject _prefab;

        // ────────────────────────────────
        // SETUP
        // ────────────────────────────────
        #region Setup

        [SetUp]
        public void SetUp()
        {
            _prefab = Track(new GameObject("PooledPrefab"));
        }

        [TearDown]
        public void TearDown()
        {
            // Pooled instances live under the manager's container, so destroying the manager
            // takes them with it.
            for (int i = _spawnedObjects.Count - 1; i >= 0; i--)
            {
                if (_spawnedObjects[i] != null) UnityEngine.Object.DestroyImmediate(_spawnedObjects[i]);
            }

            _spawnedObjects.Clear();
        }

        #endregion

        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [Test]
        public void Get_WhenPoolEmptyAndAutoExpand_ReturnsActiveObject()
        {
            ObjectPoolManager pool = CreateManager(NewConfig(initialSize: 0, autoExpand: true));

            LogAssert.Expect(LogType.Warning, Expanding);
            GameObject spawned = pool.Get(PoolId);

            Assert.IsNotNull(spawned, "Auto-expand produced nothing.");

            // C1: CreateInstance deactivates the object because its job is to feed the queue.
            // Get is the single place that activates, so both branches must agree here.
            Assert.IsTrue(spawned.activeSelf, "The auto-expand branch handed back an inactive object (C1).");
        }

        [Test]
        public void Get_FromPrefilledPool_ReturnsActiveObject()
        {
            // The other half of C1: the two branches of Get must be indistinguishable to a caller.
            ObjectPoolManager pool = CreateManager(NewConfig(initialSize: 1, autoExpand: false));

            GameObject spawned = pool.Get(PoolId);

            Assert.IsNotNull(spawned);
            Assert.IsTrue(spawned.activeSelf);
        }

        [Test]
        public void Return_SameObjectTwice_SecondIsIgnored()
        {
            ObjectPoolManager pool = CreateManager(NewConfig(initialSize: 1, autoExpand: false));

            GameObject spawned = pool.Get(PoolId);
            pool.Return(PoolId, spawned);

            LogAssert.Expect(LogType.Warning, DoubleReturn);
            pool.Return(PoolId, spawned);

            // The real damage of a double return is a queue holding the same object twice, which
            // hands one instance to two callers (M3). One Get must drain the pool completely.
            Assert.AreSame(spawned, pool.Get(PoolId));

            LogAssert.Expect(LogType.Warning, PoolEmpty);
            Assert.IsNull(pool.Get(PoolId), "The ignored return still enqueued the object a second time.");
        }

        [Test]
        public void Get_UnknownPool_LogsErrorAndReturnsNull()
        {
            ObjectPoolManager pool = CreateManager();

            LogAssert.Expect(LogType.Error, NoSuchPool);

            Assert.IsNull(pool.Get("PoolThatDoesNotExist"));
        }

        [Test]
        public void Get_WhenPoolEmptyAndNoAutoExpand_ReturnsNull()
        {
            ObjectPoolManager pool = CreateManager(NewConfig(initialSize: 0, autoExpand: false));

            LogAssert.Expect(LogType.Warning, PoolEmpty);

            Assert.IsNull(pool.Get(PoolId));
        }

        #endregion

        // ────────────────────────────────
        // HELPERS
        // ────────────────────────────────
        #region Helpers

        private PoolConfig NewConfig(int initialSize, bool autoExpand) => new PoolConfig
        {
            poolId      = PoolId,
            prefab      = _prefab,
            initialSize = initialSize,
            autoExpand  = autoExpand
        };

        private ObjectPoolManager CreateManager(params PoolConfig[] configs)
        {
            GameObject host = Track(new GameObject("ObjectPoolManager"));
            ObjectPoolManager pool = host.AddComponent<ObjectPoolManager>();

            InjectConfigs(pool, configs);
            pool.InitializePools();

            return pool;
        }

        /// <summary>
        /// The pool list is a private serialized field with no runtime setter, so the Inspector
        /// is its only real configuration path. Reflection is the test seam that avoids widening
        /// the production API for the tests alone; if the field is ever renamed, this fails with
        /// a message that says so rather than with a null reference.
        /// </summary>
        private static void InjectConfigs(ObjectPoolManager pool, PoolConfig[] configs)
        {
            FieldInfo field = typeof(ObjectPoolManager)
                .GetField("_poolConfigs", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field,
                "ObjectPoolManager._poolConfigs was renamed or removed. Update this test seam.");

            field.SetValue(pool, new List<PoolConfig>(configs));
        }

        private GameObject Track(GameObject value)
        {
            _spawnedObjects.Add(value);
            return value;
        }

        #endregion
    }
}
