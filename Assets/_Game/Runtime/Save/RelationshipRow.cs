using System;

namespace GosipSimulator.Save
{
    /// <summary>
    /// One persisted opinion: what an NPC thinks of somebody, and why it moved last. Public fields
    /// and [Serializable] because JsonUtility only reads those, the same shape SaveData already has.
    ///
    /// This type lives in Save and not in Core on purpose. Core cannot see Save (R3), which is why
    /// OnRelationshipsRestored carries three parallel arrays instead of a list of these rows: the
    /// event is a Core type and could not name this one.
    /// </summary>
    [Serializable]
    public class RelationshipRow
    {
        /// <summary>Whose opinion this is.</summary>
        public string npcId;

        /// <summary>Who the opinion is about.</summary>
        public string aboutId;

        public int value;

        /// <summary>
        /// The action id that last moved this opinion, kept for the log and for a future HUD. It is
        /// the id of an ActionDefinitionSO, so renaming one leaves old saves pointing at an action
        /// that no longer exists; that is recorded as a known loose end in QWEN.md.
        /// </summary>
        public string reason;
    }
}
