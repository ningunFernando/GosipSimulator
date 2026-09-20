using UnityEngine;
using System;

namespace GosipSimulator.Data
{

    /// <summary>
    /// A social tie is a directed connection between two NPCs, where one NPC trusts the other to a certain degree.
    /// The trust value is an integer between 0 and 100, where 0 means no trust and 100 means complete trust.
    /// This class is used to define the relationships between NPCs in the game.
    /// </summary>
    [Serializable]
    public class SocialTie
    {
        [Tooltip("The NPC that this tie points to.")]
        [SerializeField] private string _otherNpcId;

        [Tooltip("How much this NPC trusts the story from the other NPC. 0 means they don't trust it at all, 100 means they trust it completely.")]
        [SerializeField] private int _trust = 50;

        public string OtherNpcId => _otherNpcId;
        public int Trust => _trust;
    }
}
