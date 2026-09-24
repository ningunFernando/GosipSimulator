using NUnit.Framework;
using UnityEngine;
using GosipSimulator.Tools;
using Object = UnityEngine.Object;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// Milestone 3 of the node tool: the canvas remembering where you left things. The asset is only a
    /// list of positions, but it is the piece that can quietly corrupt a layout, so the odd keys are
    /// the interesting cases: none at all, one nobody placed, and the same one twice.
    /// </summary>
    public class VillageLayoutTests
    {
        private const string GUID  = "11112222333344445555666677778888";
        private const string OTHER = "88887777666655554444333322221111";

        private VillageLayoutSO _layout;

        // ────────────────────────────────
        // SETUP AND TEARDOWN
        // ────────────────────────────────
        #region Setup and Teardown

        [SetUp]
        public void SetUp()
        {
            // In memory and never saved, so a test run leaves nothing behind in the project.
            _layout = ScriptableObject.CreateInstance<VillageLayoutSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_layout);
        }

        #endregion

        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [Test]
        public void ANodeNobodyPlaced_HasNoPosition()
        {
            Assert.IsFalse(_layout.TryGet(GUID, out Vector2 position));
            Assert.AreEqual(Vector2.zero, position, "A refused lookup still has to leave the output at a sane value.");
        }

        [Test]
        public void APlacedNode_IsWhereItWasLeft()
        {
            Assert.IsTrue(_layout.Set(GUID, new Vector2(120f, -40f)));
            Assert.IsTrue(_layout.TryGet(GUID, out Vector2 position));

            Assert.AreEqual(new Vector2(120f, -40f), position);
        }

        [Test]
        public void MovingANode_ReplacesItsSpotInsteadOfAddingAnother()
        {
            _layout.Set(GUID, Vector2.zero);
            _layout.Set(GUID, new Vector2(10f, 10f));

            Assert.AreEqual(1, _layout.Count, "The same node was placed twice, not two nodes.");
            _layout.TryGet(GUID, out Vector2 position);
            Assert.AreEqual(new Vector2(10f, 10f), position, "The last drag wins.");
        }

        [Test]
        public void ADragThatEndedWhereItStarted_ReportsNothingToSave()
        {
            _layout.Set(GUID, new Vector2(5f, 5f));

            Assert.IsFalse(_layout.Set(GUID, new Vector2(5f, 5f)), "Nothing changed, so nothing needs writing to disk.");
        }

        [Test]
        public void ANodeWithNoGuid_IsRefused()
        {
            Assert.IsFalse(_layout.Set(null, Vector2.one));
            Assert.IsFalse(_layout.Set(string.Empty, Vector2.one));

            Assert.AreEqual(0, _layout.Count, "An empty key would be handed to the next asset that has none.");
            Assert.IsFalse(_layout.TryGet(string.Empty, out _));
        }

        [Test]
        public void TwoNodes_KeepTheirOwnSpots()
        {
            _layout.Set(GUID,  new Vector2(1f, 2f));
            _layout.Set(OTHER, new Vector2(3f, 4f));

            _layout.TryGet(GUID,  out Vector2 first);
            _layout.TryGet(OTHER, out Vector2 second);

            Assert.AreEqual(new Vector2(1f, 2f), first);
            Assert.AreEqual(new Vector2(3f, 4f), second);
            Assert.AreEqual(2, _layout.Count);
        }

        [Test]
        public void ForgettingANode_LeavesTheOthersAlone()
        {
            _layout.Set(GUID,  Vector2.one);
            _layout.Set(OTHER, Vector2.one);

            Assert.IsTrue(_layout.Forget(GUID));
            Assert.IsFalse(_layout.TryGet(GUID, out _));
            Assert.IsTrue(_layout.TryGet(OTHER, out _), "Forgetting one node is not forgetting the village.");
        }

        [Test]
        public void ForgettingANodeNobodyPlaced_ChangesNothing()
        {
            Assert.IsFalse(_layout.Forget(GUID));
            Assert.IsFalse(_layout.Forget(null));
        }

        #endregion
    }
}