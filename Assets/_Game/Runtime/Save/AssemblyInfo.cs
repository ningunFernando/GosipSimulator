using System.Runtime.CompilerServices;

// SaveSystem.FolderOverride is internal so gameplay code cannot redirect the save. The PlayMode
// tests are the one caller that must, because they boot the real game and the Bootstrapper loads
// the save before a test can reach the SaveSystem it created.
[assembly: InternalsVisibleTo("GosipSimulator.Tests.PlayMode")]
