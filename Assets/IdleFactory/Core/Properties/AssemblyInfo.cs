using System.Runtime.CompilerServices;

// The EditMode test assembly exercises the internal V1 import (T141, F6) without the import being public API.
[assembly: InternalsVisibleTo("IdleFactory.EditModeTests")]

// The PlayMode test assembly drives command-path state mutators (T304, AR-8) that are internal, not public API.
[assembly: InternalsVisibleTo("IdleFactory.PlayModeTests")]
